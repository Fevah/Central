# Central

Enterprise platform for network operations, IT service management, and CRM. Windows desktop (WPF + DevExpress) + Angular web client + ASP.NET Core API + PostgreSQL.

**Status:** in active development. 80 DB migrations, 2,382 passing tests, 0 build errors.

## Quick start

```bash
# Start local PostgreSQL + Redis (Podman)
podman play kube infra/pod.yaml

# Apply schema
psql -h 127.0.0.1 -U central -d central -f db/schema.sql

# Build + test
dotnet build Central.sln --configuration Release -p:Platform=x64
dotnet test tests/dotnet/Central.Tests.csproj -c Release -p:Platform=x64

# Run the desktop app
cd apps/desktop/bin/x64/Release/net10.0-windows && ./Central.exe

# Run the API (listens on :5000)
dotnet run --project services/api/Central.Api.csproj -c Release

# Run the web client (listens on :4200)
cd apps/web && npm ci && npx ng serve
```

DSN / credentials / API URLs: [docs/CREDENTIALS.md](docs/CREDENTIALS.md).

## Repository layout

```
/
├── apps/                     User-facing surfaces
│   ├── desktop/              Central.Desktop — WPF shell
│   └── web/                  Angular 21 + DevExtreme
│
├── services/                 Backend services
│   ├── api/                  Central.Api — ASP.NET Core 10 REST + SignalR
│   └── tenant-provisioner/   Rust — K8s-aware tenant DB provisioning
│
├── libs/                     Shared .NET libraries
│   ├── engine/               Central.Engine — auth, models, widgets, services
│   ├── persistence/          Central.Persistence — Npgsql repos + logger
│   ├── api-client/           Central.ApiClient — typed HTTP + SignalR client
│   ├── workflows/            Central.Workflows — Elsa 3.5.3 integration
│   ├── security/             Central.Security — ABAC policy engine
│   ├── tenancy/              Central.Tenancy — tenant resolution
│   ├── licensing/            Central.Licensing — keys, subscriptions, modules
│   ├── observability/        Central.Observability
│   ├── collaboration/        Central.Collaboration — presence
│   ├── protection/           Central.Protection
│   └── update-client/        Central.UpdateClient
│
├── modules/                  WPF feature modules (plug into apps/desktop)
│   ├── admin/                Users, roles, lookups, jobs, ribbon config
│   ├── audit/                Audit log viewer
│   ├── crm/                  Accounts, deals, pipeline, dashboard
│   ├── dashboard/            KPI cards, notification center
│   ├── devices/              IPAM
│   ├── global-admin/         Tenant / licensing / audit
│   ├── links/                P2P / B2B / FW link builder
│   ├── routing/              BGP
│   ├── service-desk/         ManageEngine integration
│   ├── switches/             Switch config + deploy
│   ├── tasks/                Task management (16 panels)
│   └── vlans/                VLAN inventory
│
├── tests/
│   └── dotnet/               Central.Tests — 2,382 tests
│
├── db/                       Migrations (001-082+), schema.sql, seed
├── infra/                    Terraform, Terragrunt, K8s, Ansible, Vagrant
├── tools/                    Dev utilities (icons, parser, scripts) — not shipped
├── assets/                   Static assets (icon packs)
├── config/                   Runtime config
├── docs/                     Architecture + buildout plans + reference docs
├── backups/                  Local DB dumps (gitignored by pattern)
├── Central.sln               References every .NET project
├── NuGet.config
└── CLAUDE.md                 AI-agent context + project-specific instructions
```

Every top-level folder has one job. Convention based — an engineer familiar with monorepo patterns (`apps/`, `services/`, `libs/`) should know where to put anything in 30 seconds.

See [docs/REPO_STRUCTURE_PLAN.md](docs/REPO_STRUCTURE_PLAN.md) for the rationale behind each folder and the original migration plan from the legacy `desktop/`-rooted layout.

## Key docs

| What you need | Where to look |
|---------------|---------------|
| Architecture + all buildout plans | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| Feature-by-feature test checklist | [docs/FEATURE_TEST_CHECKLIST.md](docs/FEATURE_TEST_CHECKLIST.md) |
| Platform credentials + local DSNs | [docs/CREDENTIALS.md](docs/CREDENTIALS.md) |
| CRM buildout (29-phase) | [docs/ENTERPRISE_CRM_BUILDOUT.md](docs/ENTERPRISE_CRM_BUILDOUT.md) |
| Server-side architecture | [docs/SERVER_ARCHITECTURE.md](docs/SERVER_ARCHITECTURE.md) |
| Task module buildout | [docs/TASKS_BUILDOUT.md](docs/TASKS_BUILDOUT.md) |
| Global Admin buildout | [docs/GLOBALADMIN_BUILDOUT.md](docs/GLOBALADMIN_BUILDOUT.md) |
| Legacy TotalLink reference docs | [docs/LEGACY_MIGRATION.md](docs/LEGACY_MIGRATION.md) |
| Future-module reference snippets | docs/REFERENCE_*.md (sales-order release, inventory, warehousing, sequences) |

## Stack

- **.NET 10** / C# — `apps/desktop`, `services/api`, `libs/*`, `modules/*`, `tests/dotnet`
- **Angular 21** + DevExtreme 25.2 — `apps/web`
- **Rust** (Axum + sqlx) — `services/tenant-provisioner`
- **PostgreSQL 18.3** + Npgsql 10.0.2
- **DevExpress WPF 25.2.6** — `apps/desktop`
- **Elsa 3.5.3** workflow engine — `libs/workflows`
- **Podman** (not Docker) for local containers; Kubernetes 1.31 for deploy

## Contributing

1. Read [CLAUDE.md](CLAUDE.md) — project-wide conventions and current state.
2. Pick a folder that matches what you're adding (new WPF module → `modules/`, new library → `libs/`, new service → `services/`).
3. Name your .NET project `Central.<Something>`; all csprojs live inside their folder (`libs/foo/Central.Foo.csproj`).
4. Add the project to `Central.sln` (`dotnet sln Central.sln add <path>`).
5. Build + test before pushing.

Test conventions: `tests/dotnet/` is the consolidated test project for every .NET target. Services that need their own in-process tests can add `<name>.Tests.csproj` alongside — judgment call.

## License

Proprietary. Not for redistribution.
