# Global Admin Module — Phased Buildout Plan

Platform-level administration for the Central SaaS engine. Global Admin sits
**above** per-tenant Admin — it manages tenants, subscriptions, module licensing,
global users, and platform health. Only users with `is_global_admin = true` in
`central_platform.global_users` can access this module.

---

## Current State

**Data layer — COMPLETE:** `DbRepository.GlobalAdmin.cs` has all queries and mutations
(create/suspend/activate tenant, toggle admin, grant/revoke licenses, change plan,
platform metrics). `Central.Tenancy` has schema provisioning. `Central.Licensing` has
subscription enforcement and RSA-4096 license key generation.

**UI — SKELETON:** 5 panels exist with grids but use raw `Dictionary<string, object?>`
instead of typed models. Ribbon buttons have empty `() => {}` callbacks. No context-
sensitive ribbon — all buttons shown at once regardless of active panel. No detail
dialogs, no CRUD wiring, no validation.

**What needs building:** Typed models, ViewModels, context-sensitive ribbon, detail
dialogs, setup wizard, audit trail, and cross-panel integration.

---

## Architecture Decisions

### 1. Dynamic Ribbon — Context Tab per Active Panel

The Global Admin ribbon page keeps only the **Panels** toggle group (5 check buttons)
and a **Data** group (Refresh All). All action buttons move into a
`RibbonPageCategory` with sub-pages that appear/disappear based on the active panel:

| Active Panel | Context Page | Actions |
|---|---|---|
| Tenants | Tenant Actions (green) | New, Edit, Suspend, Activate, Provision Schema, Delete, Setup Wizard |
| Global Users | User Actions (blue) | Invite, Edit, Toggle Admin, Reset Password, Manage Memberships, Remove |
| Subscriptions | Subscription Actions (amber) | Assign Plan, Change Tier, Convert Trial, Cancel, Extend |
| Module Licenses | License Actions (purple) | Grant Module, Revoke, Bulk Grant, Bulk Revoke |
| Platform Dashboard | *(no context tab — read-only)* | |

This matches the existing pattern: Links (blue), Switch (green), Admin (amber).

### 2. Typed Models + ListViewModelBase<T>

Replace `Dictionary<string, object?>` with proper models so each grid gets:
- Add/Edit/Delete/Duplicate/Refresh/Export commands for free
- Context menus via `GetContextMenuItems()`
- `IActionTarget` integration (global Add/Delete/Refresh buttons route to active grid)
- Undo/Redo support

### 3. Module Catalog Awareness

The Global Admin must understand the **module licensing model**:
- 10 base modules (always included): devices, switches, links, routing, vlans,
  admin, tasks, servicedesk, audit, globaladmin
- 5 licensed add-ons per the catalog in `central_platform.module_catalog`
- Subscription tiers gate user/device limits but NOT module access
- Module licenses are granted/revoked per-tenant independently

---

## Phase 1 — Typed Models, Context Ribbon, and Tenant CRUD

**Goal:** Replace skeleton with working Tenant management and context-sensitive ribbon.

### Models to Create

```
Central.Module.GlobalAdmin/Models/
├── TenantRecord.cs          # Id(Guid), Slug, DisplayName, Domain, Tier, IsActive,
│                            # CreatedAt, UpdatedAt, UserCount, PlanName
├── GlobalUserRecord.cs      # Id(Guid), Email, DisplayName, EmailVerified,
│                            # IsGlobalAdmin, CreatedAt, TenantCount, TenantSlugs
├── SubscriptionRecord.cs    # Id, TenantSlug, TenantName, PlanName, Tier, MaxUsers,
│                            # MaxDevices, Status, StartedAt, ExpiresAt, StripeSubId
├── ModuleLicenseRecord.cs   # Id, TenantSlug, TenantName, ModuleCode, ModuleName,
│                            # IsBase, GrantedAt, ExpiresAt
└── PlatformMetrics.cs       # TotalTenants, ActiveTenants, TotalUsers, VerifiedUsers,
                             # ActiveSubs — for dashboard KPIs
```

### ViewModels to Create

```
Central.Module.GlobalAdmin/ViewModels/
├── TenantsListViewModel.cs          # ListViewModelBase<TenantRecord>
│   Custom commands: SuspendCommand, ActivateCommand, ProvisionSchemaCommand
│   Context menu: Suspend, Activate, Provision Schema, ───, Export, Refresh
│
├── GlobalUsersListViewModel.cs      # ListViewModelBase<GlobalUserRecord>
│   Custom commands: ToggleAdminCommand, ResetPasswordCommand
│
├── SubscriptionsListViewModel.cs    # ListViewModelBase<SubscriptionRecord>
│   Custom commands: ChangePlanCommand, ConvertTrialCommand
│
└── ModuleLicensesListViewModel.cs   # ListViewModelBase<ModuleLicenseRecord>
    Custom commands: GrantModuleCommand, RevokeModuleCommand
```

### MainViewModel Changes

Add computed properties for context tab visibility:
```csharp
public bool IsGlobalAdminPanelActive => ActivePanel is
    ActivePanel.GlobalTenants or ActivePanel.GlobalUsers or
    ActivePanel.GlobalSubscriptions or ActivePanel.GlobalLicenses;

public bool IsGlobalTenantsPanelActive  => ActivePanel == ActivePanel.GlobalTenants;
public bool IsGlobalUsersPanelActive    => ActivePanel == ActivePanel.GlobalUsers;
public bool IsGlobalSubsPanelActive     => ActivePanel == ActivePanel.GlobalSubscriptions;
public bool IsGlobalLicensesPanelActive => ActivePanel == ActivePanel.GlobalLicenses;
```

### XAML — Context Tab Category

Add to MainWindow.xaml after AdminContextCategory:
```xml
<dxr:RibbonPageCategory x:Name="GlobalAdminContextCategory"
                         Caption="Global Admin"
                         Color="#4CAF50"
                         IsVisible="{Binding IsGlobalAdminPanelActive}">
    <dxr:RibbonPage Caption="Tenant Actions"
                     IsVisible="{Binding IsGlobalTenantsPanelActive}">
        <!-- Groups: CRUD + Tenant Operations -->
    </dxr:RibbonPage>
    <dxr:RibbonPage Caption="User Actions"
                     IsVisible="{Binding IsGlobalUsersPanelActive}">
        <!-- Groups: CRUD + User Operations -->
    </dxr:RibbonPage>
    <!-- ... Subscriptions, Licenses pages -->
</dxr:RibbonPageCategory>
```

### GlobalAdminModule.RegisterRibbon — Trim to Panels Only

Strip all action buttons. Keep only:
```
Global Admin (static page)
├── Panels group: Tenants, Global Users, Subscriptions, Licenses, Dashboard (check buttons)
└── Data group: Refresh All
```

### DbRepository.GlobalAdmin.cs — Add Typed Queries

Add `GetTenantsTypedAsync() -> List<TenantRecord>` etc. alongside existing
Dictionary-based queries (keep old ones for backward compat until Phase 5 cleanup).

### Wire in MainWindow.xaml.cs

- Replace `BindGlobalAdminGrids()` Dictionary binding with ViewModel-driven binding
- Move inline DB handlers (lines 7553-7692) into ViewModel commands
- Wire context tab ItemClick events to ViewModel command dispatch

### Deliverables

- [x] 4 typed models with INotifyPropertyChanged
- [x] 4 ViewModels extending ListViewModelBase<T>
- [x] Context-sensitive ribbon (tabs appear per active panel)
- [x] Tenant CRUD: Add, Edit (detail dialog), Suspend, Activate, Delete
- [x] Context menus on all 4 grids
- [x] Global Add/Edit/Delete/Refresh buttons route to active grid

---

## Phase 2 — Subscription and Module License Management

**Goal:** Full subscription lifecycle and module license grant/revoke with bulk operations.

### Dialogs to Create

```
Central.Module.GlobalAdmin/Views/Dialogs/
├── TenantDetailDialog.xaml          # Slug, DisplayName, Domain, Tier dropdown
│                                    # Tabs: Subscriptions, Modules, Members (sub-grids)
├── AssignPlanDialog.xaml            # Tenant label, Plan dropdown (Free/Pro/Enterprise),
│                                    # Status radio (active/trial), Expiry date picker
└── GrantModuleDialog.xaml           # Tenant label, Module checklist from catalog,
                                     # Multi-select for bulk grant, Expiry date picker
```

### New DbRepository Methods

```csharp
// Subscriptions
CreateSubscriptionAsync(Guid tenantId, int planId, string status, DateTime? expiresAt)
CancelSubscriptionAsync(int subscriptionId)
ConvertTrialToPaidAsync(int subscriptionId)
ExtendSubscriptionAsync(int subscriptionId, DateTime newExpiry)

// Bulk license operations
BulkGrantModulesAsync(Guid tenantId, List<int> moduleIds)
BulkRevokeNonBaseModulesAsync(Guid tenantId)

// Per-tenant detail queries
GetTenantSubscriptionsAsync(Guid tenantId)
GetTenantModulesAsync(Guid tenantId)
GetTenantMembershipsAsync(Guid tenantId)
```

### ViewModel Wiring

- `SubscriptionsListViewModel.ChangePlanCommand` opens `AssignPlanDialog`
- `ModuleLicensesListViewModel.GrantModuleCommand` opens `GrantModuleDialog`
- `ModuleLicensesListViewModel.BulkGrantCommand` selects modules → grants all at once
- Tenant detail dialog shows sub-grids for the selected tenant's subscriptions,
  modules, and members

### Deliverables

- [x] Tenant detail dialog with sub-grids (subscriptions, modules, members)
- [x] Plan assignment and tier changes via dialog
- [x] Module grant/revoke with bulk operations
- [x] Trial→paid conversion and subscription cancellation

---

## Phase 3 — User Management and Tenant Memberships

**Goal:** Full global user lifecycle — invite, manage memberships, password reset.

### Dialogs to Create

```
Central.Module.GlobalAdmin/Views/Dialogs/
├── InviteUserDialog.xaml            # Email, DisplayName, Initial Password,
│                                    # IsGlobalAdmin checkbox, Assign to Tenants multi-select
├── ManageMembershipsDialog.xaml     # Grid: user's tenant memberships (tenant, role, joined)
│                                    # Add/Remove membership, Change role dropdown
└── ResetPasswordDialog.xaml         # User email (read-only), New password, Confirm,
                                     # Force email re-verification checkbox
```

### New Model

```
Central.Module.GlobalAdmin/Models/
└── TenantMembershipRecord.cs    # Id, UserId, TenantId, TenantSlug, TenantName,
                                 # Role, JoinedAt
```

### New DbRepository Methods

```csharp
CreateGlobalUserAsync(email, displayName, passwordHash, salt, isGlobalAdmin)
ResetPasswordAsync(Guid userId, string newHash, string newSalt)
AddTenantMembershipAsync(Guid userId, Guid tenantId, string role)
RemoveTenantMembershipAsync(int membershipId)
UpdateMembershipRoleAsync(int membershipId, string newRole)
GetUserMembershipsAsync(Guid userId)
```

### Password Hashing

Use existing `PasswordHasher` in `Central.Core` (SHA256 + salt, matching migration
027). For new user creation, generate salt + hash server-side.

### Deliverables

- [x] Invite global user with tenant assignment
- [x] Manage tenant memberships (add/remove/change role)
- [x] Password reset
- [x] User detail with membership list

---

## Phase 4 — Setup Wizard, Audit Trail, and Dashboard Charts

**Goal:** Guided tenant onboarding, audit trail for all operations, and rich dashboard.

### Setup Wizard

`TenantSetupWizard.xaml` — DXDialogWindow with 6 steps:

1. **Tenant Details** — Slug (validated unique), Display Name, Domain, Tier
2. **Choose Plan** — Radio buttons for subscription plans (Free/Pro/Enterprise)
3. **Select Modules** — Checklist from catalog, base modules pre-checked and locked
4. **Provision Schema** — Progress bar, calls `TenantSchemaManager.ProvisionTenantAsync()`
5. **Invite First Admin** — Email, Display Name, Password, Role=Admin
6. **Summary** — Review all selections, Confirm button

Launched from "Setup Wizard" large button on the Tenant Actions context tab.

### Audit Trail

New migration: `central_platform.global_admin_audit_log`
```sql
CREATE TABLE central_platform.global_admin_audit_log (
    id              serial PRIMARY KEY,
    actor_user_id   uuid,
    actor_email     varchar(255),
    action          varchar(64) NOT NULL,
    entity_type     varchar(64),
    entity_id       varchar(128),
    details         jsonb,
    created_at      timestamptz DEFAULT now()
);
```

Actions: `tenant_created`, `tenant_suspended`, `tenant_activated`, `tenant_deleted`,
`user_invited`, `user_removed`, `admin_toggled`, `password_reset`,
`plan_changed`, `module_granted`, `module_revoked`, `schema_provisioned`

`GlobalAdminAuditService.LogAsync(action, entityType, entityId, details)` — called
from every ViewModel command handler. Auto-captures current user from `AuthContext`.

New panel: `GlobalAdminAuditPanel` — read-only grid, filterable by action/date/actor.

### Dashboard Enhancement

Enhance `PlatformDashboardPanel` with DX ChartControl:
- **Pie chart:** Tenants by tier (Free/Pro/Enterprise)
- **Bar chart:** Users per tenant (top 10)
- **Line chart:** Tenant growth over time (monthly from `created_at`)
- **Donut chart:** Subscription status distribution (active/trial/cancelled)
- **Bar chart:** Module adoption rates (% of tenants with each module)

New DB queries:
```csharp
GetTenantGrowthAsync()              // monthly creation histogram
GetSubscriptionDistributionAsync()  // count by status/tier
GetModuleAdoptionAsync()            // count of tenants per module
```

### Deliverables

- [x] Setup Wizard: 6-step guided tenant onboarding
- [x] Audit trail panel with filterable log
- [x] Every Global Admin mutation writes audit record
- [x] Platform dashboard with 5 charts
- [x] Module adoption visibility

---

## Phase 5 — Permissions, Cross-Panel Navigation, and Polish

**Goal:** Production hardening — permissions, validation, cross-panel links, shortcuts.

### Permissions

New migration adds permissions:
- `global_admin:read` — View Global Admin panels
- `global_admin:write` — Create/edit tenants, users, subscriptions
- `global_admin:delete` — Delete/suspend tenants, revoke licenses
- `global_admin:provision` — Provision tenant schemas (destructive, separate gate)

`GlobalAdminModule.RegisterRibbon` sets `RequiredPermission = "global_admin:read"` on
the page. Individual buttons gate on `:write`, `:delete`, `:provision`.

### Cross-Panel Navigation

- Click tenant slug in Subscriptions grid → navigate to Tenants panel, select that tenant
- Click tenant slug in Licenses grid → navigate to Tenants panel
- Click user email in Memberships dialog → navigate to Global Users panel
- Uses `PanelMessageBus.Publish(new NavigateToPanelMessage(...))` pattern

### Validation

- Slug: `^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$` — lowercase, no spaces, 3-50 chars
- Email: standard RFC 5322 validation
- Required fields: Display Name on tenants, Email on users
- Unique slug/email checked before save (async DB query)

### Confirmation Dialogs

All destructive operations show `DXMessageBox.Show()` confirmation:
- Suspend tenant: "Suspend {slug}? All users will lose access."
- Delete tenant: "Permanently delete {slug}? This cannot be undone."
- Revoke license: "Remove {module} from {tenant}?"

### Keyboard Shortcuts

When a Global Admin panel is active:
- `Ctrl+N` — Add new entity
- `F2` — Edit selected
- `Del` — Delete selected
- `F5` — Refresh
- `Ctrl+E` — Export to clipboard

### Deliverables

- [x] Fine-grained permissions (read/write/delete/provision)
- [x] Cross-panel navigation via PanelMessageBus
- [x] Input validation with clear error messages
- [x] Confirmation dialogs on all destructive operations
- [x] Keyboard shortcuts
- [x] Loading indicators during async operations
- [x] Error handling with status bar feedback

---

## Phase Summary

| Phase | What | Priority | Depends On |
|-------|------|----------|------------|
| 1 | Typed models, ViewModels, context ribbon, tenant CRUD | Must have | — |
| 2 | Subscription + module license management | Must have | Phase 1 |
| 3 | User management + memberships | Must have | Phase 1 |
| 4 | Setup wizard + audit trail + dashboard charts | Should have | Phase 1-3 |
| 5 | Permissions + cross-panel + polish | Should have | Phase 1-4 |

Build order: **1 → 2 → 3 → 4 → 5** (sequential, each builds on previous).

---

## Module Catalog Reference

| Code | Name | Base? | Description |
|------|------|-------|-------------|
| devices | IPAM / Devices | Yes | IP address management, device inventory |
| switches | Switches | Yes | Switch configuration, PicOS deployment |
| links | Links | Yes | P2P, B2B, FW link management |
| routing | Routing / BGP | Yes | BGP configuration, peering |
| vlans | VLANs | Yes | VLAN inventory and management |
| admin | Admin | Yes | Users, roles, permissions, lookups |
| tasks | Tasks | Yes | Task/project management (Hansoft clone) |
| servicedesk | Service Desk | Yes | ManageEngine sync, dashboards |
| audit | Audit | Yes | M365 audit, GDPR scoring |
| globaladmin | Global Admin | Yes | Platform-level tenant management |
| workflows | Workflows | Licensed | Elsa workflow engine |
| reporting | Advanced Reporting | Licensed | Custom report builder |
| mobile | Mobile Access | Licensed | Flutter mobile client access |
| sso | SSO / SAML | Licensed | Enterprise SSO integration |
| api | API Access | Licensed | REST API for integrations |

---

## Subscription Tiers Reference

| Tier | Max Users | Max Devices | Price/mo | Notes |
|------|-----------|-------------|----------|-------|
| Free | 3 | 50 | $0 | Community/evaluation |
| Professional | 25 | 500 | $49 | Small-medium teams |
| Enterprise | Unlimited | Unlimited | Custom | Large orgs, SLA, support |
