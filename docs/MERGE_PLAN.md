# Central + Secure Platform Merge Plan

Phased approach to merge the **Central** WPF desktop engine (C#/.NET 10/DX 25.2/PostgreSQL)
and the **Secure** Rust microservices backend (Axum/PostgreSQL/Redis/K8s) into a single
enterprise platform with multiple client surfaces.

---

## Current State Assessment

### Central (Desktop Engine)
| Aspect | Detail |
|--------|--------|
| **Stack** | C# .NET 10, WPF, DevExpress 25.2, Npgsql, Elsa 3.5.3 |
| **Strengths** | 16-panel task module, enterprise RBAC, ribbon customizer, icon system, 55+ panels total, 496 tests |
| **DB** | Single PostgreSQL 18.3, 105 tables, 67 migrations, direct Npgsql |
| **Auth** | Local user table + Windows auto-login, SHA256 passwords |
| **Multi-tenant** | Enterprise V2 started (Central.Tenancy) but not enforced at DB level |
| **API** | ASP.NET Core Minimal API, JWT, SignalR, 56+ endpoints |
| **Workflows** | Elsa 3.5.3 integrated (6 custom activities) |
| **Clients** | WPF desktop only |

### Secure (Rust Backend)
| Aspect | Detail |
|--------|--------|
| **Stack** | Rust, Axum 0.7, SQLx, Redis, K8s, Podman |
| **Strengths** | Enterprise auth (MFA, WebAuthn, SSO, SAML), RLS multi-tenancy, CAS storage, offline sync, M365 audit, GDPR |
| **DB** | Per-service databases (secure_admin, secure_auth, secure_audit, secure_sync, secure_storage) |
| **Auth** | Argon2id, AES-256-GCM PII, RS256 JWT, TOTP/WebAuthn/FIDO2/SMS/Email MFA, SAML 2.0, OIDC |
| **Multi-tenant** | PostgreSQL RLS enforced per `tenant_id`, PgBouncer pooling, tier-based limits |
| **API** | REST per service, no unified gateway yet |
| **Clients** | Flutter mobile (started), Web (basic HTML SPAs), Desktop (minimal Rust) |

### What Each Platform Brings

```
Central brings:                     Secure brings:
├── Rich WPF desktop engine         ├── Rust performance backend
├── DevExpress UI controls          ├── Enterprise auth (MFA, SSO, SAML)
├── Task management (Hansoft)       ├── Row-Level Security multi-tenancy
├── Service Desk (ME sync)          ├── Offline-first sync engine
├── Ribbon customizer               ├── Content-addressed storage
├── Icon system (11K icons)         ├── M365/Azure audit + GDPR
├── Elsa workflow engine            ├── K8s-native deployment
├── SignalR real-time               ├── Backup/restore system
├── 496 unit tests                  ├── System tray manager
└── PicOS network config            └── AWS Terraform infrastructure
```

---

## Target Architecture

```
                    ┌─────────────────────────────────────┐
                    │        CLIENTS (Multi-Surface)       │
                    │                                     │
                    │  WPF Desktop    Flutter    Angular   │
                    │  (Central.exe)  Mobile     Web       │
                    └────────┬──────────┬──────────┬──────┘
                             │          │          │
                    ┌────────▼──────────▼──────────▼──────┐
                    │         API GATEWAY (Rust)           │
                    │    Rate limiting, routing, TLS       │
                    │    JWT validation, tenant routing    │
                    └────────┬──────────────────────┬─────┘
                             │                      │
               ┌─────────────▼────────┐    ┌───────▼─────────┐
               │   CORE SERVICES      │    │  MODULE SERVICES │
               │   (Rust)             │    │  (Rust or .NET)  │
               │                      │    │                  │
               │  auth-service        │    │  task-service    │
               │  admin-service       │    │  network-service │
               │  sync-service        │    │  servicedesk-svc │
               │  storage-service     │    │  audit-service   │
               │  workflow-service    │    │  media-service   │
               └──────────┬───────────┘    └────────┬────────┘
                          │                         │
               ┌──────────▼─────────────────────────▼────────┐
               │              DATA LAYER                      │
               │                                              │
               │  PostgreSQL 18 (RLS)  │  Redis  │  S3/MinIO  │
               │  Per-service DBs      │  Cache  │  CAS Blobs │
               └──────────────────────────────────────────────┘
```

---

## Phase 1 — Unified Auth & Tenant Foundation (Weeks 1-3)

**Goal:** Replace Central's basic auth with Secure's enterprise auth.
The WPF desktop and API server authenticate against the Rust auth-service.

### 1.1 Deploy Secure Auth-Service Alongside Central

- Stand up `auth-service` (Rust) in the existing Central Podman pod
- Configure auth-service to use the Central PostgreSQL instance (new `secure_auth` DB)
- Run auth-service migrations (V001-V017)
- Seed default tenant + admin user matching current Central admin

### 1.2 Central.Api JWT Migration

- Replace Central.Api's homegrown JWT with validation against auth-service RS256 keys
- Central.Api.Client sends login to `http://localhost:8081/api/v1/auth/login`
- JWT claims include `tenant_id`, `user_id`, `roles[]`, `permissions[]`
- Keep backward compat: if auth-service unreachable, fall back to local auth

### 1.3 WPF Desktop Login Flow

- LoginWindow calls auth-service REST API (not local DB)
- Support MFA flow: login → MFA required → TOTP/WebAuthn prompt → token
- Store JWT + refresh token in `SecureString` memory
- Auto-refresh via background timer (14-minute cycle)
- Windows auto-login: match Windows SID to auth-service user via AD integration

### 1.4 Multi-Tenant at DB Level

- Add `tenant_id UUID` column to ALL Central tables (67 migrations + alter)
- Create RLS policies mirroring Secure's pattern (`SET LOCAL app.tenant_id`)
- Central.Data `DbRepository` sets tenant context before every query
- PgBouncer (transaction mode) between Central.Api and PostgreSQL
- Default tenant for existing data (single-tenant backward compat)

### 1.5 Deliverables

- [ ] auth-service running in Central pod (port 8081)
- [ ] Central.Api validates JWT from auth-service
- [ ] WPF LoginWindow authenticates against auth-service
- [ ] MFA support in desktop login flow
- [ ] RLS policies on all Central tables
- [ ] Tenant context set per-request in DbRepository
- [ ] Backward-compatible single-tenant mode

---

## Phase 2 — Rust API Gateway & Service Mesh (Weeks 4-6)

**Goal:** Unified API gateway in Rust that routes to both Central.Api (.NET)
and Secure services (Rust). All clients hit one endpoint.

### 2.1 API Gateway Service (Rust/Axum)

```
GET  /api/v1/auth/*         → auth-service (Rust, port 8081)
GET  /api/v1/admin/*        → admin-service (Rust, port 8080)
GET  /api/v1/audit/*        → audit-service (Rust, port 8082)
GET  /api/v1/sync/*         → sync-service (Rust, port 8083)
GET  /api/v1/storage/*      → storage-service (Rust, port 8084)
GET  /api/v1/tasks/*        → central-api (.NET, port 5000)
GET  /api/v1/devices/*      → central-api (.NET, port 5000)
GET  /api/v1/switches/*     → central-api (.NET, port 5000)
GET  /api/v1/links/*        → central-api (.NET, port 5000)
GET  /api/v1/servicedesk/*  → central-api (.NET, port 5000)
GET  /api/v1/workflows/*    → central-api (.NET, port 5000, Elsa)
```

- Rate limiting (token bucket per tenant tier)
- Request/response logging
- CORS per tenant
- JWT validation at gateway (shared RS256 public key)
- Health aggregation (`/health` checks all backends)
- WebSocket proxy for SignalR connections

### 2.2 Service Discovery

- Static config initially (service URLs in gateway config)
- Future: Consul or K8s service DNS

### 2.3 Mobile & Web Client Access

- Flutter mobile app points to gateway (port 443/TLS)
- Angular web app points to gateway
- WPF desktop continues direct to Central.Api for SignalR, gateway for REST

### 2.4 Deliverables

- [ ] Rust API gateway routing to all services
- [ ] JWT validation at gateway level
- [ ] Rate limiting per tenant tier
- [ ] Health endpoint aggregating all backends
- [ ] TLS termination for mobile/web
- [ ] WebSocket proxy for SignalR

---

## Phase 3 — Migrate Task Module to Rust Service (Weeks 7-10)

**Goal:** Move task management from Central.Api (.NET) to a dedicated Rust
`task-service` for performance at scale. Desktop still works via API client.

### 3.1 task-service (Rust/Axum)

- New Cargo workspace member: `services/task-service`
- Port all 25 task API endpoints to Axum handlers
- PostgreSQL with RLS (tasks, sprints, projects, etc.)
- Migrate task tables to `secure_tasks` database
- Import Central's 8 task migrations (060-067) as Rust SQLx migrations

### 3.2 Batch Operations for Scale

- Bulk task create/update (1000+ items per request)
- Streaming JSON for large result sets
- Cursor-based pagination (not offset)
- Full-text search via `tsvector` + GIN index

### 3.3 Workflow Integration

- Elsa workflow definitions stored in PostgreSQL (keep Central.Workflows for now)
- task-service publishes events to Redis pub/sub
- Elsa subscribes and triggers workflows
- Future: port workflow engine to Rust

### 3.4 Real-Time via SSE

- Replace SignalR for task updates with Server-Sent Events (SSE)
- `/api/v1/tasks/stream` — per-tenant event stream
- Events: task_created, task_updated, task_deleted, sprint_changed
- WPF desktop subscribes via `HttpClient` SSE reader
- Mobile/web subscribe directly

### 3.5 Deliverables

- [ ] task-service (Rust) with all 25 endpoints
- [ ] Bulk operations (batch create/update)
- [ ] Cursor-based pagination
- [ ] Full-text search
- [ ] SSE real-time stream
- [ ] WPF ApiDataService points to task-service
- [ ] Performance: <50ms p95 for list queries

---

## Phase 4 — Storage & Sync Integration (Weeks 11-13)

**Goal:** Connect Central's file management and task attachments to Secure's
CAS storage-service and offline sync-service.

### 4.1 File Storage Migration

- Central's `FileManagementService` routes to storage-service REST API
- Upload: `POST /api/v1/storage/upload` (multipart)
- Download: `GET /api/v1/storage/download/:id` (pre-signed URL)
- CAS deduplication for identical attachments across tenants
- Task attachments, SD request files, device configs → storage-service

### 4.2 Offline Sync for Mobile

- Flutter mobile syncs tasks, projects, sprints via sync-service
- Merkle tree differential sync (only changed items transferred)
- Vector clock conflict resolution (last-writer-wins + user prompt)
- Offline queue: create/edit tasks while disconnected, sync on reconnect

### 4.3 Desktop Offline Mode Enhancement

- Central's `ConnectivityManager` enhanced with sync-service awareness
- Offline: cache tasks locally in SQLite
- Reconnect: push local changes to sync-service
- Conflict UI: diff view for conflicting edits

### 4.4 Deliverables

- [ ] File uploads route through storage-service (CAS dedup)
- [ ] Task attachments stored in S3/MinIO via storage-service
- [ ] Flutter mobile offline sync for tasks
- [ ] Desktop offline mode with local SQLite cache
- [ ] Conflict resolution UI

---

## Phase 5 — Flutter Mobile Client (Weeks 14-17)

**Goal:** Full-featured Flutter mobile app consuming the unified API.

### 5.1 Core Mobile Features

- Biometric login (fingerprint/face → auth-service)
- My Tasks view (personal cross-project)
- Task detail (view/edit/comment/attach)
- Kanban board (swipe between columns)
- Sprint view (current sprint items)
- Push notifications (Firebase + backend events)

### 5.2 Offline-First

- SQLite local database (drift package)
- Background sync via sync-service
- Optimistic UI (update locally, sync later)
- Conflict resolution with visual diff

### 5.3 Network Config (PicOS)

- Switch status dashboard (ping results)
- Config preview (read-only)
- BGP neighbor status
- Alert on switch down

### 5.4 Service Desk

- View/create tickets
- Tech assignment
- Priority/status updates
- Photo attachment (camera → storage-service)

### 5.5 Deliverables

- [ ] Flutter app with biometric auth
- [ ] Task management (CRUD + Kanban + Sprint)
- [ ] Offline sync with conflict resolution
- [ ] Network monitoring dashboard
- [ ] Service desk ticket management
- [ ] Push notifications
- [ ] App Store / Play Store ready

---

## Phase 6 — Angular Web Client with DX Controls (Weeks 18-21)

**Goal:** Browser-based client using DevExpress Angular components for
users who can't install the desktop app.

### 6.1 Angular Project Setup

- Angular 18+ with DevExtreme Angular components
- Authentication via auth-service (OIDC/OAuth2 flow)
- Responsive layout (desktop + tablet)

### 6.2 Module Parity

- Task management (tree grid, Kanban, Gantt, sprint planning)
- Network/IPAM (device grid, switch detail)
- Service Desk (request grid, dashboards)
- Admin (users, roles, permissions)
- Reports (query builder, dashboards)

### 6.3 Real-Time

- SSE subscription for live updates
- Presence indicators (who's editing)
- Toast notifications

### 6.4 Deliverables

- [ ] Angular app with DevExtreme components
- [ ] Auth via OIDC (auth-service)
- [ ] Task, Network, SD, Admin modules
- [ ] Real-time updates via SSE
- [ ] Responsive design

---

## Phase 7 — M365 Audit & GDPR Integration (Weeks 22-24)

**Goal:** Bring Secure's audit-service forensic capabilities into
the Central desktop and web clients.

### 7.1 Audit Module in Desktop

- New `Central.Module.Audit` project
- Investigation panel (start, list, detail)
- Forensic briefing panel (8 auto-detected findings)
- Document tracker panel (DLP, sharing history)
- GDPR compliance dashboard (article scores)
- Evidence export (JSON/CSV with chain-of-custody)

### 7.2 Audit Module in Web

- Angular components for investigation UI
- M365 log search with filters
- GDPR audit report generation

### 7.3 Audit API

- Desktop/Web call audit-service (Rust) via gateway
- Microsoft API credentials stored in admin-service (encrypted)
- Mock data mode for dev/demo

### 7.4 Deliverables

- [ ] Audit module in WPF desktop (5+ panels)
- [ ] Audit module in Angular web
- [ ] M365 investigation workflow
- [ ] GDPR compliance scoring
- [ ] Evidence export with chain-of-custody

---

## Phase 8 — Kubernetes & Elastic Scale (Weeks 25-28)

**Goal:** Production deployment on K8s with auto-scaling for large tenant counts.

### 8.1 Service Containerization

- All services in Podman/containerd images
- Central.Api in .NET 10 container
- Rust services in Debian slim containers (~200MB)
- Health checks on all services

### 8.2 Kubernetes Deployment

- Namespace: `central` (merge Secure's `secure` namespace)
- HPA: auto-scale auth-service, task-service, gateway (CPU/memory targets)
- PDB: pod disruption budgets for zero-downtime deploys
- StatefulSets: PostgreSQL, Redis
- Secrets: sealed-secrets or external-secrets-operator

### 8.3 Database Scaling

- PgBouncer: transaction-mode pooling per service
- Read replicas for heavy query services (task-service, audit-service)
- Connection limits per tenant tier (free: 5, pro: 20, enterprise: 100)
- Monitoring: pg_stat_statements, slow query alerting

### 8.4 Observability

- Prometheus + Grafana (metrics)
- Jaeger (distributed tracing, OpenTelemetry)
- Loki (centralized logging)
- PagerDuty (alerting)
- Custom dashboards per service

### 8.5 Deliverables

- [ ] All services containerized
- [ ] K8s manifests for production
- [ ] HPA auto-scaling
- [ ] PgBouncer connection pooling
- [ ] Read replicas for heavy queries
- [ ] Prometheus/Grafana/Jaeger observability
- [ ] Terraform for AWS production

---

## Phase 9 — Infrastructure as Code & DevOps (Weeks 29-31)

**Goal:** Full IaC with Terraform, CI/CD pipeline, backup automation.

### 9.1 Terraform

- Merge Secure's existing Terraform (EKS, RDS, ElastiCache, S3)
- Add: .NET container registry, Elsa workflow DB
- Environments: dev, staging, prod with tfvars
- State management: S3 backend with DynamoDB locking

### 9.2 CI/CD Pipeline

- GitHub Actions (or GitLab CI)
- Rust: `cargo build --release`, `cargo test`, `cargo clippy`
- .NET: `dotnet build`, `dotnet test`
- Flutter: `flutter test`, `flutter build apk/ipa`
- Angular: `ng test`, `ng build --prod`
- Container builds: multi-arch (amd64 + arm64)
- Auto-deploy to staging on merge to main

### 9.3 Backup Automation

- Merge Secure's backup-manager into Central
- Scheduled PG dumps (all service DBs)
- Redis snapshots
- K8s manifest export
- Git-tracked with LFS for large dumps
- Retention policy (7 daily, 4 weekly, 12 monthly)

### 9.4 Deliverables

- [ ] Terraform for full infrastructure
- [ ] CI/CD pipeline (build, test, deploy)
- [ ] Multi-arch container builds
- [ ] Automated backup with retention
- [ ] Environment promotion (dev → staging → prod)

---

## Phase 10 — Admin Console & Global Management (Weeks 32-34)

**Goal:** Unified admin console for managing tenants, services, infrastructure.

### 10.1 Merge Admin Services

- Secure's admin-service (Rust): setup wizard, infrastructure deployment
- Central's Admin module: users, roles, lookups, ribbon config
- Unified admin panel in web + desktop

### 10.2 Global Admin Features

- Tenant management (create, suspend, delete)
- Service health dashboard (all services)
- User management across tenants
- License management (module enable/disable per tenant)
- Infrastructure status (K8s pods, DB connections, Redis memory)
- Backup management (trigger, restore, history)

### 10.3 Tray Manager Integration

- Secure's tray-manager (Rust) becomes the desktop operations tool
- Start/stop services, view logs, trigger deploys
- Certificate management for mobile gateway
- Metrics dashboard

### 10.4 Deliverables

- [ ] Unified admin console (web + desktop)
- [ ] Tenant lifecycle management
- [ ] Service health monitoring
- [ ] License management
- [ ] Tray manager with full ops controls

---

## Migration Strategy

### Database Consolidation

```
BEFORE (separate):                    AFTER (unified):
central DB (105 tables)      →       central DB (shared)
secure_admin (6 tables)      →       secure_admin DB (separate)
secure_auth (17 tables)      →       secure_auth DB (separate)
secure_audit (7 tables)      →       secure_audit DB (separate)
secure_sync (3 tables)       →       secure_sync DB (separate)
secure_storage (2 tables)    →       secure_storage DB (separate)
```

**Principle:** Per-service database ownership stays. Central tables get `tenant_id` + RLS.
No cross-service DB queries — services communicate via REST/events.

### Auth Migration Path

1. Deploy auth-service alongside Central
2. Migrate existing Central users to auth-service (one-time script)
3. Re-hash passwords with Argon2id (mark for re-hash on next login)
4. Switch Central.Api JWT validation to auth-service
5. Decommission Central's local auth tables

### Data Migration for Tasks → Rust

1. Export Central task tables as SQL dump
2. Import into `secure_tasks` DB with `tenant_id` added
3. Verify row counts match
4. Switch API gateway routing from Central.Api to task-service
5. Keep Central.Api endpoints as read-only fallback for 2 weeks
6. Decommission Central.Api task endpoints

---

## Technology Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Backend language** | Rust (primary), .NET (legacy) | Rust for new services; .NET stays for WPF-specific code |
| **API framework** | Axum (Rust), ASP.NET Core (legacy) | Axum for new; keep ASP.NET for Elsa/SignalR |
| **Desktop** | WPF + DevExpress 25.2 | Keep existing — too much to rewrite |
| **Mobile** | Flutter | Cross-platform, offline-first, existing work |
| **Web** | Angular + DevExtreme | Enterprise UI components, DX ecosystem |
| **Database** | PostgreSQL 18 + RLS | Proven, Secure's RLS pattern |
| **Cache** | Redis 7 | Session store, rate limiting, pub/sub |
| **Storage** | S3/MinIO via storage-service | CAS dedup, pre-signed URLs |
| **Auth** | Secure's auth-service (Rust) | Enterprise-grade, already built |
| **Workflow** | Elsa 3.5.3 (.NET) initially, port to Rust later | Keep working engine, migrate when stable |
| **Real-time** | SSE (Rust services) + SignalR (legacy) | SSE for new, SignalR for WPF compat |
| **Container** | Podman build, containerd runtime | Rootless, OCI-compliant |
| **Orchestration** | Kubernetes | EKS (prod), K3s/kubeadm (dev) |
| **IaC** | Terraform (AWS) + Vagrant (local) | Existing infra from Secure |
| **CI/CD** | GitHub Actions | Multi-language support |

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| Auth migration breaks existing users | Dual-auth period: local + auth-service, gradual cutover |
| Performance regression moving to microservices | Gateway adds <5ms latency; Rust services are faster than .NET |
| Data loss during DB migration | pg_dump before every migration, verify row counts, keep rollback dumps |
| Feature parity gap between clients | Desktop is primary; mobile/web are additive |
| Complexity explosion | Phase gates: each phase must be production-stable before starting next |
| Solo developer bandwidth | AI-assisted development; phases are self-contained |

---

## Timeline Summary

| Phase | Weeks | What |
|-------|-------|------|
| 1 | 1-3 | Unified auth + RLS multi-tenancy |
| 2 | 4-6 | Rust API gateway + service mesh |
| 3 | 7-10 | Task module → Rust service |
| 4 | 11-13 | Storage + sync integration |
| 5 | 14-17 | Flutter mobile client |
| 6 | 18-21 | Angular web client (DevExtreme) |
| 7 | 22-24 | M365 audit + GDPR integration |
| 8 | 25-28 | K8s + elastic scaling |
| 9 | 29-31 | IaC + CI/CD + backup automation |
| 10 | 32-34 | Unified admin console |

**Total: ~34 weeks** (8.5 months) for full convergence.
Each phase is independently deployable — partial completion still delivers value.
