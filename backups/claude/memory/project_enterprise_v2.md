---
name: Enterprise V2 — multi-tenancy, licensing, global admin, all wired in
description: Enterprise upgrade fully wired 2026-03-28. 7 enterprise projects integrated into API+Desktop. Global Admin module with 17 API endpoints. 21 total projects, 0 errors.
type: project
---

Enterprise V2 fully wired on 2026-03-28.

**Why:** Transform from single-tenant desktop app to multi-tenant enterprise SaaS platform.

**How to apply:** All enterprise libraries are now active — DI registered, middleware wired, endpoints mapped, desktop panels created.

## What Was Done

### Enterprise Projects (all wired into API + Desktop)
1. **Central.Tenancy** — ITenantConnectionFactory in DI, TenantContext scoped, TenantResolutionMiddleware active
2. **Central.Licensing** — RegistrationService/SubscriptionService/ModuleLicenseService/LicenseKeyService in DI
3. **Central.Security** — SecurityPolicyEngine singleton in DI, CRUD endpoints at /api/security/policies
4. **Central.Collaboration** — PresenceService singleton, SignalR JoinEditing/LeaveEditing/GetEditors in NotificationHub
5. **Central.Observability** — CorrelationIdMiddleware (first in pipeline), CorrelationIdHandler on desktop HTTP client
6. **Central.Protection** — IntegrityChecker on desktop startup, CertificatePinningHandler on HttpClient

### Global Admin Module (NEW — Central.Module.GlobalAdmin)
- 21st project in solution
- 17 API endpoints at /api/global-admin/* (protected by GlobalAdmin auth policy)
- Manages: tenants, global users, subscriptions, module licenses, platform dashboard
- 5 desktop panels: TenantsPanel, GlobalUsersPanel, SubscriptionsPanel, ModuleLicensesPanel, PlatformDashboardPanel
- Authorization: `is_global_admin` boolean on global_users, `global_admin` JWT claim

### Auth Enhancements
- JWT now includes: tenant_id, tenant_slug, tenant_tier, global_admin claims
- Multi-tenant login: resolves memberships, returns available_tenants if multiple
- Backward compat: single-tenant users default to "default" tenant with enterprise tier
