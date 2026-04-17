# services/

Backend services. Independently deployable units — each runs as its own process / container / pod in production.

## What's here

| Folder | Stack | What it is |
|--------|-------|------------|
| [api/](api/) | .NET 10 / ASP.NET Core Minimal API | `Central.Api` — REST + SignalR hub. The canonical entry point for every client. Authenticates via JWT (Argon2id + MFA + identity providers), enforces RBAC, owns persistence via `libs/persistence/`. |
| [tenant-provisioner/](tenant-provisioner/) | Rust (Axum + sqlx + kube) | K8s-aware tenant provisioning: listens to `tenant_provisioning_jobs`, creates dedicated PostgreSQL databases + K8s namespaces + NetworkPolicies + ResourceQuotas for enterprise-tier tenants. |

## Planned but not yet built

Documented in [docs/SERVER_ARCHITECTURE.md](../docs/SERVER_ARCHITECTURE.md). When any of these are built they land here:

| Future service | Purpose |
|----------------|---------|
| `auth/` | Dedicated MFA / WebAuthn / SAML / OIDC issuing (currently inside `Central.Api`). |
| `admin/` | Global-admin platform operations. |
| `gateway/` | Reverse proxy, TLS, rate limiting, WebSocket/SignalR passthrough. |
| `task/` | High-throughput task backend with SSE + Redis pub/sub. |
| `storage/` | CAS with MinIO/S3, BLAKE3 dedup, multipart upload. |
| `sync/` | Offline-first vector-clock sync for mobile/desktop clients. |
| `audit/` | M365 forensics, GDPR scoring, investigation workflows. |

## Conventions

- **One service per folder.** Each owns its own `Cargo.toml` / `.csproj`, its own Containerfile, its own migrations-in-flight (if any), its own health endpoint.
- **No shared state between services.** Communication is HTTP (REST or gRPC) — no sharing a process, no sharing an in-process cache, no direct DB writes to another service's tables.
- **Every service exposes `/health`.** Returns 200 for liveness, 503 if a dependency it owns is down.
- **Every service runs in a container.** Even the .NET API — `services/api/Containerfile` is the canonical build. No "just run on the host" assumptions.
- **Migrations live in `db/migrations/`**, not per-service. PostgreSQL is shared; migrations are the shared contract.
- **Secrets via environment variables**, never committed. See [docs/CREDENTIALS.md](../docs/CREDENTIALS.md).

## Adding a new service

1. `services/<name>/` — new folder.
2. Add a Containerfile — builds reproducibly from the repo root as context.
3. Add a `/health` endpoint.
4. Update [docs/SERVER_ARCHITECTURE.md](../docs/SERVER_ARCHITECTURE.md) to describe what it does.
5. If .NET: `dotnet new` with assembly `Central.<Name>`, add to `Central.sln`. If Rust: `cargo new`.
6. Wire into CI — add a job in `.github/workflows/ci.yml`.
7. Add K8s manifests to `infra/k8s/base/`.
