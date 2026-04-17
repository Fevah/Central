# CRM Expansion — 5 Major Stages Toward Salesforce Parity

**Date:** 2026-04-17
**Status:** **ALL 5 STAGES COMPLETE** (2026-04-17) — migrations 060-080 delivered, 95+ new tables, 13 new endpoint files
**Dependencies:** All 29 base CRM phases complete (migrations 036-059)

## Completion Summary (2026-04-17)

| Stage | Migrations | Status |
|-------|-----------|--------|
| Stage 1 — Marketing Automation | 060-062 | ✅ Complete |
| Stage 2 — Sales Operations | 063-066 | ✅ Complete |
| Stage 3 — CPQ + Contracts + Revenue | 067-071 | ✅ Complete |
| Stage 4 — AI & Intelligence (dual-tier providers + BYOK) | 076-080 | ✅ Complete |
| Stage 5 — Portals + Platform + Commerce | 072-075 | ✅ Complete |

---

## Current Central CRM vs Salesforce — Gap Analysis

### What we have (29 base phases)

| Area | Built |
|---|---|
| **Sales Core** | Accounts, Contacts (M:N with roles), Deals + 5-stage pipeline + history, Leads + scoring + conversion, Activities timeline, Quotes versioned, Products, Price books |
| **Analytics** | 4 materialized views (revenue/activity/lead ROI/account health), forecast snapshots (committed/best/worst/weighted), saved reports, KPI summary, live pipeline forecast |
| **Integration** | 6 sync agents (Salesforce/HubSpot/Dynamics/Exchange/Gmail/Pipedrive), bidirectional sync configs, `crm_external_ids` + conflict table |
| **Email** | Accounts (SMTP/IMAP/OAuth), 4 templates, tracking pixel + click redirect, auto-log to CRM |
| **Documents** | Documents + templates + approvals + e-signature scaffold |
| **Webhooks** | 28 event types, subscriptions, deliveries with retry |
| **Cross-module** | Contact↔SD, Account↔Infra, Deal↔Tasks, deal-won triggers |

### Salesforce superset — what they have that we don't

| Cloud / Feature | We have? | Priority |
|---|---|---|
| **Campaigns + marketing automation** | ❌ | **P0 — revenue attribution** |
| **Email drip sequences / nurture** | Manual sends only | **P0** |
| **Landing pages + web forms** | ❌ | **P0** |
| **Multi-touch attribution** | ❌ | **P0** |
| **Segmentation / dynamic lists** | ❌ | **P0** |
| **Territory + quota management** | ❌ | **P1** |
| **Commission calculations** | ❌ | P1 |
| **Opportunity splits + account teams** | ❌ | P1 |
| **Account plans + relationship mapping** | ❌ | P1 |
| **Forecast hierarchies (manager rollup)** | Flat only | P1 |
| **CPQ (Configure-Price-Quote)** | Simple quotes only | **P1** |
| **Product bundles / kits** | ❌ | P1 |
| **Discount approval workflows** | ❌ | P1 |
| **Contract management + clause library** | Docs only | **P1** |
| **Subscription/renewal management** | ❌ | P1 |
| **Revenue recognition (ASC 606)** | ❌ | P2 |
| **ML lead scoring / opp scoring** | Rule-based only | **P1** |
| **AI assistant (GPT/LLM)** | ❌ | P1 |
| **Activity auto-capture (email/cal parse)** | Manual log only | P1 |
| **Call transcription + sentiment** | ❌ | P2 |
| **Duplicate detection (fuzzy)** | ❌ | **P0 — data quality** |
| **Data enrichment (Clearbit-like)** | ❌ | P2 |
| **Customer self-service portal** | ❌ | P1 |
| **Partner portal + deal registration** | ❌ | P2 |
| **Commerce (cart, checkout, orders)** | ❌ | P2 |
| **Approval processes engine** | Doc approvals only | **P0** |
| **Validation rules framework** | ❌ | **P0** |
| **Workflow rules (field-change triggers)** | ❌ | **P0** |
| **Custom objects framework** | ❌ | **P1** |
| **Field-level security** | Role-level only | **P0** |
| **Cohort / funnel analytics** | Basic only | P2 |
| **Einstein Next Best Action** | ❌ | P2 |

**P0 = critical for enterprise parity. Drives the stage ordering.**

---

## Stage 1 — Marketing Automation & Attribution

**Goal:** Close the loop from marketing spend to revenue. Campaigns, segmentation, nurture sequences, UTM tracking, multi-touch attribution.

**Business case:** Marketing leaders need to answer "which campaigns drove which deals" and "which leads should get which sequence." Salesforce Pardot/Marketing Cloud is a $1500/user/month add-on — replacing just the core is worth £100s/seat.

### Scope

| # | Entity / Feature | Notes |
|---|---|---|
| 1.1 | `crm_campaigns` + `crm_campaign_members` | Source, budget, expected ROI, start/end, status (planning/active/completed), linked deals via `campaign_id` on deals |
| 1.2 | `crm_segments` + `crm_segment_members` | Static + dynamic (SQL/JSONLogic rules), shared with email sends |
| 1.3 | `crm_email_sequences` + `crm_sequence_steps` + `crm_sequence_enrollments` | Drip campaigns, trigger on event (lead created, deal stalled), pause/resume |
| 1.4 | `crm_landing_pages` + `crm_form_submissions` | Public forms capturing leads, UTM tags, hidden fields |
| 1.5 | `crm_utm_events` | Track utm_source/medium/campaign on page views + form submits |
| 1.6 | `crm_attribution_models` + `attribution_touches` | First-touch, last-touch, linear, position-based, time-decay — materialize revenue per touch per campaign |
| 1.7 | `/api/crm/campaigns` + `/segments` + `/sequences` + `/attribution/*` | Full CRUD + execution endpoints |
| 1.8 | Email sequence executor (background worker) | Walks steps, pauses on reply/unsubscribe, tracks conversion |
| 1.9 | Campaign influence report materialized view | Revenue influenced by each campaign (across all attribution models) |
| 1.10 | WPF: Campaign detail panel + funnel visualization | Pipeline by campaign source |

**Migrations:** `060_crm_campaigns.sql`, `061_crm_segments_sequences.sql`, `062_crm_attribution.sql`
**Effort:** 4-5 weeks
**Unlocks:** Marketing ROI dashboards, closed-loop attribution, automated nurture

---

## Stage 2 — Sales Operations & Revenue Intelligence

**Goal:** Territory, quota, commission, team selling, forecast rollup — the operational layer sales leaders need.

**Business case:** Sales leaders need to assign territories, set quotas, track rep attainment, calculate commissions, and roll up forecasts through a management hierarchy. Salesforce calls this "Sales Cloud operational layer" — $75-$300/user/month.

### Scope

| # | Entity / Feature | Notes |
|---|---|---|
| 2.1 | `crm_territories` + `crm_territory_rules` | Geographic, industry, account-size, named-account territories. Auto-assignment rules. Hierarchy (parent_id). |
| 2.2 | `crm_quotas` | Per-user per-period (monthly/quarterly/annual), product-category splits, ramping quotas for new hires |
| 2.3 | `crm_commission_plans` + `crm_commission_tiers` + `crm_commission_payouts` | Flat %, tiered (accelerators for overachievement), SPIFFs, clawbacks on churn |
| 2.4 | `crm_opportunity_splits` | Multi-rep credit on single deal — revenue % per user, overlay roles (sales engineer, SDR) |
| 2.5 | `crm_account_teams` + `crm_account_team_roles` | Named account teams with role assignments, access permissions per role |
| 2.6 | `crm_account_plans` | Strategic account planning — goals, stakeholders, relationship map, whitespace analysis |
| 2.7 | `crm_org_charts` | Person-level hierarchy within an account (reports-to, influence-level) |
| 2.8 | Forecast hierarchies — `app_users.manager_id` rollup | Manager-level forecasts aggregating direct reports, commit/adjustments per level |
| 2.9 | Pipeline health scores — materialized view | Stalled deals, coverage ratio (pipeline/quota), momentum, at-risk |
| 2.10 | Deal insights — "Einstein Lite" | Rules-based nudges: "No activity for 21 days", "Stalled in stage X for 2× average", "Probability mismatch with stage default" |
| 2.11 | `/api/crm/territories` + `/quotas` + `/commissions` + `/forecasts/rollup` | Full set |
| 2.12 | WPF: Territory map, Quota dashboard, Commission calculator preview | - |

**Migrations:** `063_crm_territories.sql`, `064_crm_quotas_commissions.sql`, `065_crm_account_teams.sql`, `066_crm_forecast_hierarchies.sql`
**Effort:** 5-6 weeks
**Unlocks:** Enterprise sales leaders actually using the tool

---

## Stage 3 — CPQ + Contract Lifecycle + Revenue Recognition

**Goal:** Configure-Price-Quote with approval workflows; full contract lifecycle from negotiation through renewal; subscription management with expansion tracking; optional ASC 606 revenue recognition.

**Business case:** Salesforce CPQ is $75-$150/user/month as a separate SKU. DocuSign CLM is another $30-$50. We roll this in natively. For any SaaS business this is table stakes — you cannot grow past ~£5M ARR without CPQ/contract automation.

### Scope

| # | Entity / Feature | Notes |
|---|---|---|
| 3.1 | `crm_product_bundles` + `crm_bundle_components` | Parent product with child SKUs, fixed or optional components |
| 3.2 | `crm_pricing_rules` | Volume breaks, customer-specific, promo codes, bundle discounts, MAP (min advertised price) floor |
| 3.3 | `crm_discount_approval_matrix` | Multi-level approval routing based on discount %, deal size, product category |
| 3.4 | `approval_requests` + `approval_steps` + `approval_actions` | Generic approval engine (not CRM-specific — reusable across modules) |
| 3.5 | `crm_contracts` + `crm_contract_versions` + `crm_contract_clauses` | Contract lifecycle (draft→review→signed→active→renewing→expired), version history, standard clause library |
| 3.6 | `crm_contract_milestones` | Delivery milestones, payment schedules, renewal notification triggers |
| 3.7 | `crm_subscriptions` + `crm_subscription_events` | Active subscriptions tied to contracts, MRR/ARR calc, upgrade/downgrade/cancel events |
| 3.8 | `crm_renewal_forecast` materialized view | Upcoming renewals next 30/60/90/180 days, at-risk flagging (inactive accounts, unresolved issues) |
| 3.9 | `crm_revenue_schedules` (ASC 606) | Performance-obligation-level revenue recognition, ratable vs point-in-time |
| 3.10 | `crm_orders` + `crm_order_lines` | Firm orders (post-quote-acceptance), fulfillment status, linked to billing invoices |
| 3.11 | `/api/crm/cpq/*`, `/contracts/*`, `/subscriptions/*`, `/approvals/*`, `/revenue/*` | Full REST |
| 3.12 | WPF: CPQ quote builder with bundles, Contract lifecycle panel, Renewal dashboard | - |

**Migrations:** `067_crm_cpq.sql`, `068_approval_engine.sql`, `069_crm_contracts.sql`, `070_crm_subscriptions_revenue.sql`, `071_crm_orders.sql`
**Effort:** 6-8 weeks (largest stage)
**Unlocks:** Replacement of Salesforce CPQ + DocuSign CLM; compliance-ready revenue reporting

---

## Stage 4 — AI & Intelligence Layer

**Goal:** ML scoring, AI assistant, activity auto-capture, duplicate detection, data enrichment, predictive churn/LTV.

**Business case:** Einstein AI is Salesforce's fastest-growing feature, $75+/user/month add-on. LLM-backed assistants are becoming table stakes. Duplicate/enrichment is pure data quality and saves ~1hr/day per rep.

### Scope

| # | Entity / Feature | Notes |
|---|---|---|
| 4.1 | `crm_ml_models` + `model_versions` + `model_scores` | Hosted model registry (embeddings cached in PG), versioning, A/B test harness |
| 4.2 | Lead scoring v2 — ML-backed | Train on historical conversions, augment rule-based score with probability. Background worker retrains weekly. |
| 4.3 | Opportunity scoring | Win probability prediction — features: stage velocity, contact count, email engagement, account health |
| 4.4 | `crm_next_best_actions` + recommendation engine | "Send follow-up email", "Call the CFO", "Add security addon" — per-entity, ranked by expected value |
| 4.5 | `crm_ai_assistant` conversations table + `/api/crm/ai/chat` endpoint | LLM-backed chat (pluggable provider: OpenAI, Anthropic, local Ollama). Context-aware on current account/deal/contact. Supports "Summarize this account", "Draft a follow-up", "What's the next step on this deal?" |
| 4.6 | Activity auto-capture — Exchange/Gmail integration worker | Parse inbox, match sender to contact by email domain, auto-create crm_activity. Detect meeting invites from calendar. |
| 4.7 | `crm_duplicates` + `merge_operations` | Fuzzy match (trgm + Levenshtein) across accounts/contacts/leads. Match rules editable. Merge with field-level precedence. |
| 4.8 | `crm_enrichment_providers` + `enrichment_jobs` | Pluggable enrichment (Clearbit, Apollo, ZoomInfo style). Company firmographics + contact info from email. |
| 4.9 | Predictive churn model + `crm_churn_risks` materialized view | Feature: activity drop-off, support ticket volume, contract renewal proximity, NPS score (if tracked) |
| 4.10 | Customer LTV calculation | Per-account historical revenue, projected based on subscription events |
| 4.11 | Call recording integration — `crm_call_recordings` + transcription webhook | Pluggable (Gong, Chorus, Zoom). Transcript stored, sentiment analysis optional. |
| 4.12 | `/api/crm/ai/*`, `/duplicates/*`, `/enrichment/*`, `/churn/*`, `/ltv/*` | - |

**Migrations:** `072_ml_models.sql`, `073_crm_scoring_v2.sql`, `074_crm_assistant.sql`, `075_crm_dedup_enrichment.sql`, `076_crm_churn_ltv.sql`
**Effort:** 6-8 weeks (requires external ML service or local Ollama deployment)
**Unlocks:** Einstein parity, rep productivity lift, better data quality

---

## Stage 5 — Portals, Platform & Commerce

**Goal:** Customer/partner self-service portals; approval/workflow/validation rule engines; custom objects; field-level security; import wizard; commerce checkout.

**Business case:** Portals unlock B2B self-service (reduced support load, customer retention). The platform features (workflow rules, validation rules, custom objects) are what Salesforce Admins use daily — not having them forces data into the wrong tables. Commerce turns CRM into an actual order-taking system.

### Scope

| # | Entity / Feature | Notes |
|---|---|---|
| 5.1 | Customer portal — `portal_users` + `portal_sessions` | Password-less login via magic-link, scoped view of their account (contracts, invoices, tickets, orders, quotes) |
| 5.2 | Partner portal | Deal registration, co-selling workflow, commission visibility |
| 5.3 | Community features — `community_posts` + `community_threads` + `kb_articles` | Forum + knowledge base with search + voting |
| 5.4 | **Approval processes engine** (generic) — `approval_processes`, `approval_steps`, `approval_conditions` | Multi-step sequential/parallel, conditional branching, escalation on timeout, ownership-based routing. Used by CPQ, time-off, expense, any record-level flow. |
| 5.5 | **Validation rules engine** — `validation_rules` | Per-entity field-level constraints: "Deal value > £10k requires discount_reason". Rules defined in JSONLogic, evaluated pre-save via before-trigger. |
| 5.6 | **Workflow rules engine** — `workflow_rules` + `workflow_actions` | Field-change triggers (on UPDATE OF column X) — send email, create task, update field, webhook. Central engine, re-used by all modules. |
| 5.7 | **Custom objects** — `custom_entities` + `custom_fields` + `custom_relationships` | User-defined entities with dynamic columns (using PG jsonb or schema-per-tenant dynamic tables). CRUD UI generated from metadata. |
| 5.8 | **Field-level security** — `field_permissions` | Per role + per entity + per field: read/write/hidden. Applied in API response shaping. |
| 5.9 | Import wizard — `import_jobs` | CSV → field mapping UI → preview → commit. Handles accounts/contacts/leads/custom objects. De-dup on import. |
| 5.10 | Commerce — `shopping_carts` + `cart_items` + `orders` (extending CRM orders) + `payments` | Simple B2B checkout: product catalog + cart + Stripe-backed payment. Quote → order → invoice → payment flow. |
| 5.11 | `/api/portal/*` (separate namespace), `/api/crm/workflows`, `/validation-rules`, `/custom-objects`, `/import`, `/commerce/*` | Separate tenant-scoped authorization for portal |
| 5.12 | WPF Admin panels for all rule engines — Workflow Builder, Validation Rule editor, Custom Object Designer, Field Permission Matrix | Power-user tooling |
| 5.13 | Angular customer portal web app — standalone deployable | Mirror of the portal features, white-labeled per tenant |

**Migrations:** `077_portals.sql`, `078_approval_engine.sql` (full — Stage 3 added the base), `079_validation_workflow_rules.sql`, `080_custom_objects.sql`, `081_field_permissions.sql`, `082_import_jobs.sql`, `083_commerce.sql`
**Effort:** 8-10 weeks (largest stage — includes new client: Angular portal)
**Unlocks:** Replacement of Salesforce Experience Cloud + Process Builder + Commerce Cloud; platform extensibility

---

## Cumulative Plan

| Stage | Weeks | Migrations added | New tables (approx) | Migrations range |
|---|---|---|---|---|
| 1: Marketing Automation | 4-5 | 3 | 12 | 060-062 |
| 2: Sales Operations | 5-6 | 4 | 15 | 063-066 |
| 3: CPQ + Contracts + Revenue | 6-8 | 5 | 20 | 067-071 |
| 4: AI & Intelligence | 6-8 | 5 | 15 | 072-076 |
| 5: Portals, Platform, Commerce | 8-10 | 7 | 25 | 077-083 |
| **Total** | **29-37** | **24** | **~87** | **060-083** |

## Priority Sequencing

**Recommended order** (highest marginal value first):

1. **Stage 5 first** — approval/validation/workflow engines unlock everything downstream. Custom objects mean future features don't need schema changes. Field-level security is an enterprise-deal blocker *today*. **Week 1-10.**
2. **Stage 1 — Marketing Automation** — highest external visibility, "CRM with marketing" is a hot positioning. **Week 11-15.**
3. **Stage 2 — Sales Operations** — enables large sales org deployments, unlocks commission-driven customer segments. **Week 16-21.**
4. **Stage 3 — CPQ + Contracts + Revenue** — biggest deal-size lifter, replaces 2-3 external tools. **Week 22-29.**
5. **Stage 4 — AI & Intelligence** — force multiplier on all prior stages, but relies on rich data from them. **Week 30-37.**

**Alternative — customer-pull order:**
- If specific customers have concrete Stage 2 or Stage 3 requirements, pull those forward.
- Stage 5 items are incrementally landable — can ship validation rules before custom objects, for example.

## Convention Rules (apply throughout)

- Every new table gets `tenant_id uuid` + RLS policy (extending migration 053's per-op policies)
- All list APIs: pagination, filter, search, sort via existing `PaginationHelpers`
- All errors: RFC 7807 `application/problem+json` via `ApiProblem`
- All writes: column whitelist via `EndpointHelpers.ValidateColumns`
- All model files: `INotifyPropertyChanged` + computed properties for UI binding
- All entities get pg_notify triggers for SignalR DataChanged
- Every stage: migration + model + API + permission codes + tests + docs update
- Reuse existing engines (SyncEngine for external sync, webhook_subscriptions for events, audit_log for change tracking)
- For AI/ML: pluggable providers, never hardcode a vendor. Local Ollama should be a valid option alongside OpenAI.

## Decision Points Before Build

1. **ML infrastructure** — local Ollama (free, private) vs hosted (OpenAI, Anthropic) vs managed (Vertex, Bedrock)?
2. **Portal authentication** — share app auth stack or separate (simpler)?
3. **Custom objects storage** — jsonb in one table (flexible, slower) vs dynamic tables per entity (fast, complex migrations)?
4. **Commerce** — full storefront or just B2B cart/checkout? Full storefront overlaps with Shopify/Stripe.
5. **Portal branding** — per-tenant theming (requires theme engine) or central branding?

## Not in scope (explicitly)

- Industry-specific clouds (Financial Services, Health, Education) — sell the platform + extensibility instead
- Offline mobile — already have Flutter with drift; not a CRM-specific problem
- Call center telephony (CTI / Softphone) — niche, specialist providers do it better
- Full enterprise CMS / WCM — community posts + KB is enough
- Video conferencing — integrations only (Zoom/Teams/Meet webhooks)
