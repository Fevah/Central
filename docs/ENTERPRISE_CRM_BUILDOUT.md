# Enterprise SaaS Engine + CRM Module — 29-Phase Buildout

**Date:** 2026-04-16
**Status:** Planning
**Dependencies:** All 8 core phases + 10 merge phases complete. 24 projects, 2,089 tests.

## Scope

Transform Central from an infrastructure management platform into a full enterprise
SaaS engine with CRM capabilities. Builds on the existing multi-tenancy (schema-per-tenant),
RBAC/ABAC, sync engine, and module system.

**Key deliverables:**
- Company/Organization entity with hierarchy
- Full contact management (beyond SD requesters)
- Team/department mapping with org chart
- Enhanced address system (billing, shipping, site)
- User profile system (avatar, preferences, social)
- CRM module (accounts, contacts, deals, leads, pipeline, activity)
- Tenant onboarding + billing integration
- Cross-module linking (CRM ↔ Tasks ↔ ServiceDesk ↔ Infrastructure)

---

## Current State (What Exists)

| Area | Status | Tables/Code |
|------|--------|-------------|
| Multi-tenancy | Schema-per-tenant + RLS | `central_platform.tenants`, TenantSchemaManager |
| Global Admin | 6 panels + 7 API groups | TenantsPanel, GlobalUsersPanel, SubscriptionsPanel |
| Licensing | Tier-based + module grants | RegistrationService, SubscriptionService, ModuleLicenseService |
| Users | AD/OIDC/SAML + local auth | `app_users` (username, email, dept, title, phone, company) |
| Addresses | Tenant addresses only | `tenant_addresses`, `countries`, `regions`, `postcodes` |
| Contacts | SD requesters only | `sd_requesters` (name, email, phone, dept, site) |
| Teams | SD teams only | `sd_teams`, `sd_team_members` |
| Companies | Not implemented | Only `app_users.company` text field |
| CRM | Not implemented | No accounts, deals, leads, pipeline |
| Sync Engine | Pluggable agents | SyncEngine, ManageEngine/EntraID/CSV/REST agents |
| ABAC | Row + field level policies | SecurityPolicyEngine |
| Activity | Task-scoped only | `activity_feed` (task changes) |

---

## Phase Map

### Foundation — Core SaaS Entities (Phases 1-5)

#### Phase 1: Company/Organization Entity
**Goal:** Central registry of companies that tenants, contacts, and users belong to.

**Migration:** `070_companies.sql`
```
companies
  id              serial PK
  tenant_id       uuid (RLS)
  name            text NOT NULL
  legal_name      text
  registration_no text          -- company registration / tax ID
  industry        text
  size_band       text          -- 1-10, 11-50, 51-200, 201-1000, 1000+
  website         text
  logo_url        text
  parent_id       int FK(companies) -- for subsidiaries/divisions
  is_active       boolean DEFAULT true
  metadata        jsonb DEFAULT '{}'
  created_by      int FK(app_users)
  created_at      timestamptz DEFAULT now()
  updated_at      timestamptz DEFAULT now()
```

**API:** `/api/companies` — full CRUD with pagination/filter
**WPF:** CompaniesPanel in Admin module (grid + detail + logo upload)
**Permission:** `companies:read`, `companies:write`, `companies:delete`
**Tests:** CompanyModelTests, CompanyEndpointTests

---

#### Phase 2: Contact Entity (Full CRM Contact)
**Goal:** Replace ad-hoc SD requesters with a unified contact model.

**Migration:** `071_contacts.sql`
```
contacts
  id              serial PK
  tenant_id       uuid (RLS)
  company_id      int FK(companies)
  prefix          text          -- Mr, Mrs, Dr, etc.
  first_name      text NOT NULL
  last_name       text NOT NULL
  email           text
  phone           text
  mobile          text
  job_title       text
  department      text
  linkedin_url    text
  is_primary      boolean DEFAULT false   -- primary contact for company
  contact_type    text DEFAULT 'customer' -- customer, vendor, partner, internal
  status          text DEFAULT 'active'   -- active, inactive, archived
  source          text          -- web, referral, import, manual
  tags            text[]
  notes           text
  avatar_url      text
  metadata        jsonb DEFAULT '{}'
  created_by      int FK(app_users)
  created_at      timestamptz DEFAULT now()
  updated_at      timestamptz DEFAULT now()

contact_addresses
  id              serial PK
  contact_id      int FK(contacts) ON DELETE CASCADE
  address_type    text NOT NULL   -- work, home, mailing
  line1           text
  line2           text
  city            text
  state_region    text
  postal_code     text
  country_code    char(2)
  is_primary      boolean DEFAULT false

contact_communications
  id              serial PK
  contact_id      int FK(contacts) ON DELETE CASCADE
  channel         text NOT NULL   -- email, phone, meeting, note
  direction       text            -- inbound, outbound
  subject         text
  body            text
  occurred_at     timestamptz DEFAULT now()
  logged_by       int FK(app_users)
```

**API:** `/api/contacts` — CRUD + search + filter by company
**WPF:** ContactsPanel (grid + detail + communication log)
**Permission:** `contacts:read`, `contacts:write`, `contacts:delete`
**Sync:** Map `sd_requesters` → contacts via migration script
**Tests:** ContactModelTests, ContactSearchTests

---

#### Phase 3: Team/Department Entity + Hierarchy
**Goal:** Organizational structure — departments, teams, reporting lines.

**Migration:** `072_teams.sql`
```
departments
  id              serial PK
  tenant_id       uuid (RLS)
  name            text NOT NULL
  parent_id       int FK(departments)  -- sub-departments
  head_user_id    int FK(app_users)    -- department head
  cost_center     text
  is_active       boolean DEFAULT true

teams
  id              serial PK
  tenant_id       uuid (RLS)
  department_id   int FK(departments)
  name            text NOT NULL
  description     text
  team_lead_id    int FK(app_users)
  is_active       boolean DEFAULT true

team_members
  id              serial PK
  team_id         int FK(teams) ON DELETE CASCADE
  user_id         int FK(app_users) ON DELETE CASCADE
  role_in_team    text DEFAULT 'member'  -- member, lead, observer
  joined_at       timestamptz DEFAULT now()
  UNIQUE(team_id, user_id)
```

**API:** `/api/departments`, `/api/teams`
**WPF:** OrgStructurePanel (tree view + team members grid)
**Permission:** `admin:teams`
**Tests:** TeamHierarchyTests

---

#### Phase 4: Address System Enhancement
**Goal:** Unified address model for companies, contacts, tenants, and locations.

**Migration:** `073_addresses.sql`
```
addresses
  id              serial PK
  tenant_id       uuid (RLS)
  entity_type     text NOT NULL   -- company, contact, tenant, location
  entity_id       int NOT NULL
  address_type    text NOT NULL   -- billing, shipping, hq, branch, site
  label           text            -- "London Office", "Warehouse 2"
  line1           text NOT NULL
  line2           text
  line3           text
  city            text NOT NULL
  state_region    text
  postal_code     text
  country_code    char(2) NOT NULL
  latitude        numeric(9,6)
  longitude       numeric(9,6)
  is_primary      boolean DEFAULT false
  is_verified     boolean DEFAULT false
  created_at      timestamptz DEFAULT now()
  updated_at      timestamptz DEFAULT now()

-- Polymorphic index for fast lookups
CREATE INDEX idx_addresses_entity ON addresses(entity_type, entity_id);
```

**API:** `/api/addresses` — CRUD scoped to entity
**WPF:** AddressEditorControl (reusable, embedded in Company/Contact/Tenant details)
**Note:** Migrate existing `tenant_addresses` data into unified `addresses` table
**Tests:** AddressValidationTests, GeocodingTests

---

#### Phase 5: User Profile System
**Goal:** Rich user profiles with avatars, preferences, social links, notification settings.

**Migration:** `074_user_profiles.sql`
```
user_profiles
  id              serial PK
  user_id         int FK(app_users) UNIQUE ON DELETE CASCADE
  avatar_url      text
  bio             text
  timezone        text DEFAULT 'UTC'
  locale          text DEFAULT 'en-GB'
  date_format     text DEFAULT 'dd/MM/yyyy'
  time_format     text DEFAULT 'HH:mm'
  linkedin_url    text
  github_url      text
  phone_ext       text
  office_location text
  start_date      date           -- employment start
  manager_id      int FK(app_users)
  department_id   int FK(departments)
  team_ids        int[]          -- multiple team memberships
  skills          text[]
  certifications  text[]
  metadata        jsonb DEFAULT '{}'
```

**API:** `/api/profile` (current user), `/api/users/{id}/profile` (admin)
**WPF:** ProfilePanel in Backstage (avatar upload, timezone, preferences)
**Tests:** ProfileModelTests, TimezoneTests

---

### Global Admin Enhancement (Phases 6-10)

#### Phase 6: Tenant Onboarding Wizard Enhancement
- Multi-step setup: Company → Admin user → Branding → Modules → Billing
- Logo upload + primary color + subdomain selection
- Template selection (IT operations, MSP, enterprise)
- Auto-provision demo data option
- Onboarding progress tracking (`tenant_onboarding_steps` table)

#### Phase 7: Tenant Billing & Subscription Management
- Stripe integration (payment intents, subscriptions, invoices)
- `billing_accounts` table (stripe_customer_id, payment_method, billing_email)
- `invoices` table (amount, currency, status, stripe_invoice_id, pdf_url)
- Usage metering (user count, device count, API calls, storage)
- Upgrade/downgrade flow with prorated billing
- Invoice history panel in Global Admin + tenant self-service

#### Phase 8: Tenant Usage Analytics Dashboard
- Per-tenant metrics: active users, API calls, storage, module usage
- `tenant_usage_metrics` table (tenant_id, metric_type, value, recorded_at)
- Usage trend charts (DevExpress ChartControl)
- Tenant health score (activity, adoption, growth)
- Alerting on usage thresholds (approaching plan limits)

#### Phase 9: Global Search Across Tenants
- Cross-tenant full-text search for Global Admin only
- PostgreSQL `tsvector` indexes on key tables
- Search endpoint: `GET /api/global-admin/search?q=term`
- Results grouped by entity type (tenants, users, companies)
- Respects tenant isolation (regular users see only their tenant)

#### Phase 10: Tenant Data Export/Import
- Full tenant data export (JSON/CSV archive)
- Tenant migration between environments
- Data anonymization for staging/demo
- Bulk tenant provisioning from CSV
- Tenant snapshot/restore for disaster recovery

---

### Admin Enhancement (Phases 11-14)

#### Phase 11: User Provisioning Workflows
- Invite flow: Admin sends email → user registers → auto-joins tenant
- `user_invitations` table (email, role, invited_by, token, expires_at, accepted_at)
- Approval workflow (Elsa): new user → manager approval → provisioned
- Self-service registration with domain auto-routing to tenant
- Bulk invite from CSV/AD import

#### Phase 12: Team Management Panel
- Full CRUD for departments and teams in Admin module
- Drag-drop team member assignment
- Team capacity view (allocated hours vs available)
- Team inbox (shared view of team's tasks/tickets)
- Team performance metrics

#### Phase 13: Role Templates + Custom Role Builder
- Pre-built role templates: IT Admin, Network Engineer, Help Desk, Viewer, Manager
- Custom role builder UI (permission tree with checkboxes)
- Role comparison view (side-by-side permissions)
- Role cloning (copy existing + modify)
- Role audit log (who changed what permissions when)

#### Phase 14: Org Chart + Reporting Structure
- Interactive org chart panel (DevExpress Diagram or TreeListView)
- Manager → direct reports hierarchy from `user_profiles.manager_id`
- Department → team → member drill-down
- Export org chart to PDF/image
- Dotted-line reporting (matrix orgs)

---

### CRM Module (Phases 15-25)

#### Phase 15: CRM Module Skeleton + Account Entity
- New project: `Central.Module.CRM`
- CrmModule class (IModule, IModuleRibbon, IModulePanels)
- Account entity (company + CRM metadata):
  ```
  crm_accounts
    id, tenant_id, company_id FK, account_type (customer/prospect/partner/vendor),
    account_owner int FK(app_users), annual_revenue numeric, employee_count int,
    industry text, rating text (hot/warm/cold), source text,
    last_activity_at timestamptz, next_follow_up date, stage text,
    website text, description text, tags text[], metadata jsonb
  ```
- Account list panel + detail panel
- Permission: `crm:read`, `crm:write`, `crm:delete`, `crm:admin`

#### Phase 16: CRM Contact Management
- Link contacts to CRM accounts (many-to-many via `crm_account_contacts`)
- Contact roles per account (decision maker, influencer, user, billing)
- Contact timeline (all interactions across modules)
- Contact merge/dedup tool
- vCard import/export

#### Phase 17: Deal/Opportunity Pipeline
```
crm_deals
  id, tenant_id, account_id FK, contact_id FK,
  title text, value numeric, currency char(3) DEFAULT 'GBP',
  stage text (qualification/proposal/negotiation/closed_won/closed_lost),
  probability int, expected_close date,
  owner_id int FK(app_users), source text,
  competitor text, loss_reason text,
  closed_at timestamptz, created_at, updated_at

crm_deal_stages
  id, tenant_id, name text, sort_order int, probability int,
  is_won boolean, is_lost boolean, color text
```
- Deal list with stage filtering
- Deal detail panel (value, probability, timeline)
- Stage transition validation (Elsa workflow)

#### Phase 18: Lead Management + Scoring
```
crm_leads
  id, tenant_id, first_name, last_name, email, phone, company_name,
  title text, source text (web/referral/event/cold_call),
  status text (new/contacted/qualified/converted/lost),
  score int DEFAULT 0, owner_id FK, converted_account_id FK,
  converted_contact_id FK, converted_deal_id FK,
  converted_at timestamptz, created_at, updated_at

crm_lead_scoring_rules
  id, tenant_id, field text, operator text, value text,
  points int, is_enabled boolean
```
- Lead capture form (embeddable widget URL)
- Lead scoring engine (rule-based + manual)
- Lead → Account+Contact+Deal conversion flow
- Lead assignment rules (round-robin, territory, capacity)

#### Phase 19: Activity Timeline
```
crm_activities
  id, tenant_id, entity_type text (account/contact/deal/lead),
  entity_id int, activity_type text (call/email/meeting/note/task),
  subject text, body text, direction text (inbound/outbound),
  duration_minutes int, occurred_at timestamptz,
  logged_by int FK(app_users), attachments jsonb,
  related_task_id int FK(tasks), related_sd_request_id int FK(sd_requests)
```
- Unified activity timeline (chronological per entity)
- Quick log buttons (Call, Email, Meeting, Note)
- Activity linking to Tasks and Service Desk tickets
- Activity reminders and follow-ups

#### Phase 20: Email Integration
- SMTP send + IMAP/EWS receive
- `email_accounts` table (provider, credentials, auto_log boolean)
- Email templates with merge fields (contact.first_name, deal.value)
- Email tracking (open/click via pixel + redirect)
- Email-to-CRM logging (manual or auto by sender domain)
- Sync agent: ExchangeOnlineAgent, GmailAgent

#### Phase 21: Sales Pipeline Visualization
- Kanban board (deals by stage, drag to advance)
- Funnel chart (conversion rates per stage)
- Pipeline value chart (weighted by probability)
- Win/loss analysis dashboard
- Sales velocity metrics (avg deal size, cycle time, win rate)

#### Phase 22: Quotes & Proposals
```
crm_quotes
  id, tenant_id, deal_id FK, quote_number text (auto from reference_config),
  contact_id FK, billing_address_id FK, shipping_address_id FK,
  status text (draft/sent/accepted/rejected/expired),
  subtotal numeric, discount_pct numeric, tax_pct numeric, total numeric,
  currency char(3), valid_until date, notes text,
  accepted_at timestamptz, sent_at timestamptz

crm_quote_lines
  id, quote_id FK, product_id FK,
  description text, quantity numeric, unit_price numeric,
  discount_pct numeric, line_total numeric, sort_order int
```
- Quote builder UI (line items, totals, tax)
- PDF generation (DevExpress XtraReports)
- Email quote to contact
- Quote → Deal conversion
- Quote versioning (v1, v2, v3)

#### Phase 23: Products & Services Catalog
```
crm_products
  id, tenant_id, sku text, name text, description text,
  category text, unit_price numeric, currency char(3),
  is_recurring boolean, billing_period text (monthly/annual),
  is_active boolean DEFAULT true, metadata jsonb

crm_price_books
  id, tenant_id, name text, is_default boolean,
  currency char(3), valid_from date, valid_to date

crm_price_book_entries
  id, price_book_id FK, product_id FK,
  unit_price numeric, min_quantity int
```
- Product catalog panel (grid + detail)
- Price book management (multi-currency, volume pricing)
- Product → Quote line item linking

#### Phase 24: CRM Dashboards + KPIs
- Revenue dashboard (pipeline value, closed won, forecast)
- Activity dashboard (calls/emails/meetings per rep per week)
- Lead dashboard (new leads, conversion rate, source breakdown)
- Account health dashboard (at-risk accounts, renewal dates)
- KPI cards using existing KpiCardBuilder
- Configurable dashboard layouts per user

#### Phase 25: CRM Reports + Forecasting
- Pipeline forecast report (weighted + committed)
- Sales rep performance report (quota vs actual)
- Lead source ROI report
- Account revenue history report
- Custom report builder (saved_reports integration)
- Scheduled report delivery (email PDF)
- Export to Excel/PDF via existing export system

---

### Integration (Phases 26-28)

#### Phase 26: CRM Sync Agents
- **SalesforceAgent** — bidirectional account/contact/opportunity sync
- **HubSpotAgent** — bidirectional company/contact/deal sync
- **DynamicsAgent** — bidirectional account/contact/opportunity sync
- All use existing SyncEngine framework (entity maps, field maps, hash detection)
- Conflict resolution: last-write-wins with audit trail

#### Phase 27: Email Provider Integration
- **ExchangeOnlineAgent** — Microsoft Graph API for email/calendar
- **GmailAgent** — Google Workspace API
- **SMTPAgent** — generic SMTP send
- Auto-log emails to CRM based on sender/recipient matching
- Calendar sync (meetings → CRM activities)

#### Phase 28: Document Management + Templates
```
crm_documents
  id, tenant_id, entity_type text, entity_id int,
  file_id int FK(files), document_type text (proposal/contract/nda/invoice),
  version int, status text (draft/final/signed), signed_at timestamptz

crm_templates
  id, tenant_id, name text, template_type text (email/quote/proposal),
  subject text, body text (HTML with merge fields),
  is_default boolean, category text
```
- Template editor (rich text + merge field picker)
- Document generation from templates
- Document versioning + approval workflow (Elsa)
- E-signature integration placeholder (DocuSign/Adobe Sign)

---

### Polish (Phase 29)

#### Phase 29: Cross-Module Linking + Mobile + API
- **Cross-module linking:**
  - CRM Contact → Service Desk Requester (auto-link by email)
  - CRM Account → Infrastructure (devices owned by company)
  - CRM Deal → Task Project (delivery project per won deal)
  - CRM Activity → Task (create task from CRM note)
- **CRM API completion:** All CRM entities get full REST API with pagination/filter/search
- **Mobile views:** Flutter CRM screens (accounts list, contact detail, deal pipeline)
- **Angular CRM module:** Web client CRM views
- **Webhook events:** `crm.account.created`, `crm.deal.stage_changed`, `crm.lead.converted`
- **Import/migration tools:** CSV import for accounts, contacts, deals
- **Data quality:** Duplicate detection, field completeness scoring

---

## Dependency Graph

```
Phase 1 (Companies) ──┬── Phase 2 (Contacts) ──┬── Phase 16 (CRM Contacts)
                      │                         │
                      ├── Phase 4 (Addresses) ──┤
                      │                         │
Phase 3 (Teams) ──────┼── Phase 12 (Team Mgmt) ─┼── Phase 14 (Org Chart)
                      │                         │
Phase 5 (Profiles) ───┤                         ├── Phase 15 (CRM Skeleton)
                      │                         │         │
Phase 6 (Onboarding) ─┤                         │   Phase 17 (Deals) ──── Phase 21 (Pipeline)
                      │                         │         │
Phase 7 (Billing) ────┤                         │   Phase 18 (Leads) ──── Phase 24 (Dashboards)
                      │                         │         │
Phase 8 (Analytics) ──┘                         │   Phase 22 (Quotes) ─── Phase 23 (Products)
                                                │         │
Phase 9 (Search) ───────────────────────────────┤   Phase 25 (Reports)
                                                │
Phase 10 (Export) ──────────────────────────────┤
                                                │
Phase 11 (Invitations) ─── Phase 13 (Roles) ───┘

Phase 19 (Activities) ─── Phase 20 (Email) ─── Phase 27 (Providers)
Phase 26 (CRM Sync) ─── Phase 28 (Docs) ─── Phase 29 (Polish)
```

## Estimation

| Block | Phases | Effort | Priority |
|-------|--------|--------|----------|
| Foundation | 1-5 | 4-5 weeks | CRITICAL |
| Global Admin | 6-10 | 3-4 weeks | HIGH |
| Admin | 11-14 | 2-3 weeks | HIGH |
| CRM Core | 15-19 | 5-6 weeks | HIGH |
| CRM Extended | 20-25 | 4-5 weeks | MEDIUM |
| Integration | 26-28 | 3-4 weeks | MEDIUM |
| Polish | 29 | 2-3 weeks | MEDIUM |
| **Total** | **29** | **23-30 weeks** | |

## Convention Rules

- Every phase gets a numbered migration file (`070_companies.sql`, `071_contacts.sql`, etc.)
- Every new entity gets: DB table + API CRUD + WPF panel + permission codes + tests
- All tables include `tenant_id uuid` + RLS policy (via `get_current_tenant_id()`)
- All list APIs support pagination (offset/limit), filtering (field:value), search, sorting
- All error responses use RFC 7807 `application/problem+json`
- Follow existing module pattern: `IModule` + `IModuleRibbon` + `IModulePanels`
- New permissions registered in `PermissionCode.cs` as `module:action` format
- Feature test checklist updated per phase
- Sync agents extend existing `SyncEngine` with `IIntegrationAgent`
