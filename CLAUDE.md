# Central

Enterprise infrastructure platform — network config, service desk, IT operations.
Desktop app builds as `Central.exe`. All namespaces are `Central.*`.
Switches run **PicOS 4.6** (FS brand). Config format is set-style CLI.

## DevExpress API Reference

When working with DevExpress WPF 25.2 components, **always use the `dxdocs` MCP server**
to verify class names, property names, and enum values before using them in code.
This prevents build errors from incorrect API names (e.g., wrong series type names,
non-existent properties, incorrect enum values). Query `dxdocs` for:
- Chart series types and their properties
- Grid column/view settings
- Ribbon control types
- Any DX API you're unsure about

## Architecture & Build Plan

**IMPORTANT**: All new development follows the phased architecture plan in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). Before starting work, check which
phase/step is current and build accordingly. Do not skip phases or change the
project structure without updating the architecture doc first.

| Doc | Purpose |
|-----|---------|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Master build plan — 8 phases, solution structure, module interfaces, RBAC, migration path |
| [docs/SERVER_ARCHITECTURE.md](docs/SERVER_ARCHITECTURE.md) | Multi-user server — API, gRPC, SignalR, K8s deployment, background services |
| [docs/TOTALLINK_PATTERNS.md](docs/TOTALLINK_PATTERNS.md) | TotalLink source patterns — reference implementations for module system, ribbon, CRUD, undo |
| [docs/FEATURE_TEST_CHECKLIST.md](docs/FEATURE_TEST_CHECKLIST.md) | 1,400+ testable items across 180 sections — every feature manually verifiable |
| [docs/TASKS_BUILDOUT.md](docs/TASKS_BUILDOUT.md) | Task module 11-phase buildout plan (Hansoft/P4 Plan clone) — all phases complete |
| [docs/MERGE_PLAN.md](docs/MERGE_PLAN.md) | Central + Secure merge — 10 phases, unified auth, API gateway, K8s elastic scaling |
| [docs/CREDENTIALS.md](docs/CREDENTIALS.md) | All login credentials, DSNs, SSH info, service URLs, K8s access |

### All 8 Phases + Task Module COMPLETE — Platform is production-ready

23 projects. .NET 10 / PG 18.3 / Npgsql 10.0.2 / DX 25.2 / Svg.NET 3.4.7 / Elsa 3.5.3. 0 build errors. 2,028 unit tests.

### Central + Secure Merge — ALL 10 PHASES COMPLETE

| Phase | What | Key Deliverables |
|-------|------|-----------------|
| 1 | Auth + RLS | auth-service (Rust), Argon2id re-hash, RLS, PgBouncer, user migration script |
| 2 | API Gateway | Reverse proxy, rate limiting, TLS, WebSocket/SignalR, health aggregation |
| 3 | Task Service | 26 Rust endpoints, SSE stream, batch ops, cursor pagination, Redis pub/sub |
| 4 | Storage + Sync | CAS/MinIO storage, vector clock sync, SQLite offline cache, auto-sync on reconnect |
| 5 | Flutter Mobile | 4 screens (Tasks+Kanban, Network, SD), drift DB, sync client, FCM push, build script |
| 6 | Angular Web | 7 modules (Tasks, Kanban, Network, SD, Audit, Admin, Dashboard), SSE, DxDataGrid/DxTreeList |
| 7 | M365 Audit | audit-service (9 endpoints), GDPR scoring (11 articles), WPF module (5 panels), Angular dashboard |
| 8 | K8s Scaling | Prometheus/Grafana/Jaeger/Loki, PDB, sealed-secrets, read replicas, PgBouncer |
| 9 | IaC + CI/CD | 4 GH Actions workflows, multi-arch builds, backup with retention, env promotion |
| 10 | Admin Console | Angular 5-tab admin (health, tenants CRUD, users, licenses, infra), WPF 5-panel Global Admin |

7 Rust services (all with source), 3 client surfaces (WPF + Flutter + Angular), 12 K8s manifests.

Phase 3 (Enterprise Desktop) — all complete:
- ✅ Backstage (User Profile + settings, Theme gallery, Connection, About, Switch User, Exit)
- ✅ Splash screen with progress bar
- ✅ Login flow (Windows auto-login → LoginWindow fallback → Offline)
- ✅ Theme gallery (9 installed themes, DX 25.2 RibbonGalleryBarItem + Gallery.ItemClick)
- ✅ Dynamic ribbon per-role (ApplyRibbonPermissions hides tabs/groups/buttons)
- ✅ User info in ribbon header (name + role)
- ✅ Context menus on ALL 10 grids (right-click: CRUD + navigation + export)
- ✅ Print Preview + Column Chooser on all ribbon tabs
- ✅ Column summaries/footers on Devices + Switches grids
- ✅ Cross-panel navigation (Go to Switch A/B, Go to Device from context menu)
- ✅ Set Password dialog (SHA256 + salt)
- ✅ Duplicate record (devices + all link types)
- ✅ DataModifiedMessage auto-refresh (device change → links refresh)
- ✅ DetailDialogService, UndoService, Plugin DLL discovery

Phase 5 (Server-Side SSH):
- ✅ SshOperationsService (ping, config download, BGP sync, batch ping)
- ✅ REST endpoints: /api/ssh/{id}/ping, download-config, sync-bgp, ping-all
- ✅ CredentialEncryptor (AES-256, server-side decrypt only)
- ✅ SignalR SyncProgress streaming during SSH operations

Phase 6 (Background Jobs):
- ✅ JobSchedulerService (checks every 30s, dispatches due jobs)
- ✅ 3 job types: ping_scan (10min), config_backup (24h), bgp_sync (6h)
- ✅ Admin API: /api/jobs (list, enable, disable, interval, run, history)
- ✅ DB migration 029 (job_schedules + job_history)

Phase 7 (Production Ready):
- ✅ Swagger/OpenAPI at /swagger
- ✅ Jobs panel in Admin tab (schedules + history grids)
- ✅ API URL + auto-connect settings in backstage
- ✅ Auto-connect to API server + SignalR on startup when configured

Phase 8 (Icon System + Ribbon Customizer):
- ✅ SVG icon system with Svg.NET rendering + in-memory PNG pre-cache
- ✅ 11,676 Axialist icons (OfficePro + Universal packs) — SVG + PNG 16px + PNG 32px
- ✅ ImagePickerWindow (pack checkboxes, category checkboxes, async batch loading, lazy render)
- ✅ IconService singleton (metadata cache, admin assignments, user overrides, search, bulk import)
- ✅ Local disk PNG cache (%LocalAppData%/Central/icon_cache/)
- ✅ 3-layer ribbon icon override: admin_ribbon_defaults → ribbon_items → user_ribbon_overrides
- ✅ PreloadIconOverridesAsync prevents flash on startup (loads overrides BEFORE ribbon build)
- ✅ Ribbon tree customizer — user tab (RibbonTreePanel) + admin tab (RibbonAdminTreePanel)
- ✅ Admin can push defaults for all users, set display style, link targets
- ✅ Display styles: large (icon+label below), small (icon+label right), smallNoText (icon only)
- ✅ Link targets: panel:PanelName, url:https://..., action:ActionKey, page:PageName
- ✅ Context tabs (Links blue, Switch green, Admin amber)
- ✅ Quick Access Toolbar (Save/Refresh/Undo pinned, user-customizable)
- ✅ DB migrations 030 (icon_library), 032 (ribbon_config) with pg_notify triggers

Engine platform (Core + Shell):
- ✅ ListViewModelBase<T> — Add/Delete/Duplicate/Refresh/Export commands, ContextMenu model, PanelMessageBus
- ✅ ImporterBase<T> — MapRow/ValidateRow/SaveItem with progress, cancel, error navigation
- ✅ ISettingsProvider + SettingsProvider (DB-backed per-user module settings)
- ✅ StartupWorkerManager + StartupWorkerBase (splash pipeline)
- ✅ PanelMessageBus (SelectionChanged, NavigateToPanel, DataModified, RefreshPanel)
- ✅ GridContextMenuBuilder (engine-rendered context menus from model)
- ✅ NotificationService (singleton toast system, Info/Success/Warning/Error, auto-hide)
- ✅ BulkEditWindow (reflection-based, any model, field picker + preview)
- ✅ Toast overlay (bottom-right, color-coded, 4s auto-hide)
- ✅ SignalR DataChanged → toast notifications for real-time multi-user awareness
- ✅ PasswordHasher (SHA256 + salt)
- ✅ CredentialEncryptor (AES-256)
- ✅ SvgHelper (SVG→WPF ImageSource via Svg.NET, currentColor→white, disk cache)
- ✅ IconService (singleton metadata cache, admin/user icon resolution, search, bulk SVG import)
- ✅ RibbonBuilder (DB-backed + fluent API, 3-layer override, separators, split buttons, context tabs)

Phase 4: ✅ COMPLETE (API Server):
- ✅ Central.Api (ASP.NET Core 10 Minimal API, tested with live data)
- ✅ 9 REST endpoint groups (Devices, Switches, Links, VLANs, BGP, Admin, SSH, Jobs, Ribbon)
- ✅ SignalR NotificationHub (DataChanged, PingResult, SyncProgress)
- ✅ JWT auth (login → bearer → 25 permission claims)
- ✅ pg_notify triggers (19+ tables) + ChangeNotifier background service
- ✅ Central.Api.Client (typed HTTP + SignalR client)
- ✅ IDataService abstraction (DirectDb / Api / Offline modes)
- ✅ DirectDbDataService + ApiDataService implementations
- ✅ ConnectivityManager tri-mode + SignalR DataChanged event
- ✅ WPF real-time grid refresh (SignalR → targeted Reload*Async per table)
- ✅ Multi-target Core+Data (net8.0 for API + net8.0-windows for WPF)
- ✅ Dockerfile + pod.yaml (API container ready to enable)

### Solution Structure

```
desktop/                                # .NET 10 / WPF / DevExpress 25.2 (15 C# projects)
├── Central.Core/              # Engine framework — auth, models, widgets, services, RibbonConfig, TaskFileParser
├── Central.Data/              # PostgreSQL repos + AppLogger + IconService — shared by API + Desktop
├── Central.Api/               # REST + SignalR + SSH + Jobs + Elsa Workflows
├── Central.Api.Client/        # Typed HTTP + SignalR client
├── Central.Desktop/           # WPF shell — MainWindow, services, ViewModels, SvgHelper, ImagePickerWindow
├── Central.Workflows/         # Elsa 3.5.3 workflow engine — custom activities, workflow definitions, PostgreSQL persistence
├── Central.Module.Devices/    # IPAM: 8 grid panels + ASN + Servers
├── Central.Module.Switches/   # Switches + detail + deploy
├── Central.Module.Links/      # P2P, B2B, FW + builder
├── Central.Module.Routing/    # BGP + diagram
├── Central.Module.VLANs/      # VLAN inventory
├── Central.Module.Admin/      # Users, roles, lookups, SSH/app logs, jobs, ribbon config, ribbon tree panels, AD browser, migrations, backups, purge, locations, references, podman, scheduler
├── Central.Module.Tasks/      # Task management — 16 panels: tree, backlog, sprint, burndown, kanban, gantt, QA, dashboards, reports, timesheet, activity, portfolio, import
├── Central.Module.ServiceDesk/ # ManageEngine SD — sync, dashboards, teams, group categories, write-back
└── Central.Tests/             # 2,028 unit + integration tests

SecureAPP/                              # Rust / Axum / Cargo workspace (7 services + 4 tools)
├── services/
│   ├── auth-service/          # Enterprise auth — MFA, WebAuthn, SAML, OIDC, JWT (COMPLETE)
│   ├── admin-service/         # Global admin — tenant management, setup wizard (COMPLETE)
│   ├── gateway/               # API gateway — reverse proxy, rate limiting, TLS, WebSocket/SignalR (COMPLETE)
│   ├── task-service/          # Task management — 26 endpoints, SSE, batch, search, Redis events (COMPLETE)
│   ├── storage-service/       # CAS storage — MinIO/S3, BLAKE3 dedup, multipart upload, pre-signed URLs (COMPLETE)
│   ├── sync-service/          # Offline sync — vector clocks, push/pull, conflict resolution (COMPLETE)
│   └── audit-service/         # M365 forensics, GDPR scoring, investigations, evidence export (COMPLETE)
├── tools/
│   ├── tray-manager/          # Desktop system tray operations tool
│   ├── backup-manager/        # Scheduled PG backup automation
│   ├── backup-service/        # Backup REST API
│   └── backup-app/            # Backup CLI
└── clients/
    ├── desktop/               # Minimal Rust desktop client
    └── mobile/                # Flutter mobile — 4 screens, drift offline DB, sync client, FCM push, build script (COMPLETE)

web-client/                             # Angular 21 + DevExtreme 25.2 — 6 modules, SSE, auth (COMPLETE)
```

### TotalLink Source Reference

The `source/` folder contains the TotalLink WPF client — a mature 20-project DevExpress
app with Autofac DI, dynamic ribbon, modular architecture. **Read-only reference — do
not modify.**

When building new modules, ribbon features, CRUD grids, or dialog patterns, read
[docs/TOTALLINK_PATTERNS.md](docs/TOTALLINK_PATTERNS.md) first — it documents the
proven implementations to follow:
- **Module registration**: `Autofac.Module` + `IModule` scan (AdminModule.cs)
- **Dynamic ribbon**: `CategoriesSource` binding + `RibbonCategoryTemplateSelector`
- **Grid CRUD**: `ListViewModelBase<T>` with `[WidgetCommand]` auto-ribbon
- **Detail dialogs**: `IDetailDialogService.ShowDialog(EditMode, entity)`
- **Entity-VM sync**: `EntityViewModelBase<T>` + `[SyncFromDataObject]`
- **App context**: `AppContextViewModel` singleton (auth state, theme, window bounds)
- **Server facades**: `FacadeBase` dual Data+Method service connection
- **Change tracking**: `ChangeFactoryEx` + MonitoredUndo
- **Startup pipeline**: `StartupWorkerManager` → `InitModulesStartupWorker`

## Project Purpose

Build switch configurations from scratch using an Excel guide (`switch_guide.xlsx`)
as the source of truth, generate PicOS-compatible `set` command output, and store
everything in PostgreSQL for the WPF desktop application to query.

## Infrastructure

- **Runtime**: Kubernetes 1.31 on VMware Workstation (7 nodes, Terraform + Terragrunt IaC)
- **Database**: PostgreSQL 18.3 HA StatefulSet in K8s (primary + streaming replica)
- **Cache**: Redis 7 StatefulSet in K8s
- **Containers**: Podman for builds, K8s for running
- **Load Balancing**: MetalLB L2 (192.168.56.200-220)
- **No Docker** — Podman builds only, K8s for orchestration

### K8s Cluster (primary environment)

```
k8s-master      192.168.56.10   control-plane (Calico + MetalLB)
k8s-worker-01   192.168.56.21   role=database  (4 CPU / 8GB — PG primary)
k8s-worker-02   192.168.56.22   role=database  (4 CPU / 8GB — PG replica)
k8s-worker-03   192.168.56.23   role=general
k8s-worker-04   192.168.56.24   role=general
k8s-worker-05   192.168.56.25   role=general
k8s-worker-06   192.168.56.26   role=general
```

### Service endpoints (via MetalLB)

| Service | External IP | Port |
|---------|------------|------|
| Central API | `http://192.168.56.200` | 5000 |
| PostgreSQL (write) | `192.168.56.201` | 5432 |
| PostgreSQL (read) | `192.168.56.202` | 5432 |
| Container Registry | `192.168.56.10` | 30500 |

### Post-reboot startup

When the machine is rebooted, start **all** services in this order:

```bash
# 1. K8s VMs (Vagrant checks if already running)
cd infra/vagrant && vagrant status
# if any VM is "poweroff": vagrant up

# 2. Refresh kubeconfig if cert errors after reboot
vagrant ssh k8s-master -c "sudo cat /etc/kubernetes/admin.conf" > ~/.kube/central-local.conf

# 3. Verify K8s cluster + services
export KUBECONFIG=~/.kube/central-local.conf
kubectl get nodes
kubectl -n central get pods

# 4. Start Angular dev server (background)
cd web-client && npx ng serve --port 4200 --host 0.0.0.0 &
# Access at http://localhost:4200

# 5. Start FastAPI web app (background)
./run.sh &
# Access at http://localhost:8080

# 6. WPF desktop (GUI — launch manually from bin/x64/Release)
```

**Rule for Claude:** When user says "rebooted" or "start services", run ALL of steps 1-5 above. Verify each reaches a "ready" state before reporting done.

**Rule for versions:** Always check for the **latest stable** before writing a version number. Don't guess image tags or package versions — query the registry:
- Docker: `curl -s "https://hub.docker.com/v2/repositories/<owner>/<image>/tags/?page_size=20"`
- GitHub: `gh release list --repo owner/name --limit 5`
- npm: `npm view <pkg> version`
- Cargo: `cargo search <crate>`
- NuGet: `dotnet package search <pkg>`

A version matrix is maintained in `docs/CREDENTIALS.md` — keep it current when bumping anything.

### Daily commands

```bash
# Set K8s context
export KUBECONFIG=~/.kube/central-local.conf

# Check status
./infra/setup.sh k8s-status

# Connect to DB
./infra/setup.sh k8s-psql central
./infra/setup.sh k8s-psql secure_auth

# View logs
./infra/setup.sh k8s-logs central-api
./infra/setup.sh k8s-logs auth-service

# Build + push container images
podman build -f Central.Api/Containerfile -t central-api:latest .
podman tag central-api:latest 192.168.56.10:30500/central/central-api:latest
podman push 192.168.56.10:30500/central/central-api:latest --tls-verify=false

# Deploy to K8s
./infra/setup.sh k8s-deploy

# Start/stop VMs
cd infra/vagrant && vagrant halt    # stop all
cd infra/vagrant && vagrant up      # start all
```

### IaC Structure (Terraform + Terragrunt)

```
infra/
├── terragrunt.hcl                    # Root config (S3 backend, provider)
├── modules/                          # 11 Terraform modules (33 .tf files)
│   ├── vpc, eks, rds, elasticache    # AWS infrastructure
│   ├── ecr, s3, kms, secrets         # Storage + security
│   ├── monitoring, k8s-service       # Observability + K8s deployments
│   └── local-cluster/                # VMware VMs via Vagrant
├── environments/                     # Terragrunt per-environment configs
│   ├── _envcommon/                   # DRY module templates
│   ├── local/                        # VMware Workstation (current)
│   ├── dev/, staging/, prod/         # AWS environments
├── k8s/base/                         # Kustomize manifests (8 YAML)
├── vagrant/                          # Generated Vagrantfile
├── ansible/                          # K8s bootstrap roles
└── scripts/                          # Migration + utility scripts
```

## Database

**Write DSN**: `postgresql://central:central@192.168.56.201:5432/central`
**Read DSN**: `postgresql://central:central@192.168.56.202:5432/central`
**Auth DSN**: `postgresql://central:central@192.168.56.201:5432/secure_auth`
**Desktop DSN**: `Host=192.168.56.201;Port=5432;Database=central;Username=central;Password=central`

Schema file: [db/schema.sql](db/schema.sql)

Key tables:

| Table | Purpose |
|---|---|
| `switches` | One row per switch (hostname, site, role, loopback) |
| `vlans` | VLANs per switch with description and L3 interface |
| `interfaces` | Physical/breakout interfaces (speed, VLAN, mode) |
| `l3_interfaces` | SVIs and loopbacks (IP/prefix) |
| `bgp_config` | BGP AS, router-id, multipath per switch |
| `bgp_neighbors` | eBGP peers with BFD |
| `bgp_networks` | Advertised prefixes |
| `vrrp_config` | VRRP VIPs per interface |
| `static_routes` | Static routes per switch |
| `dhcp_relay` | DHCP relay helpers |
| `firewall_filters` | DSCP/QoS firewall filters (JSONB from/then params) |
| `cos_forwarding_classes` | QoS forwarding classes |
| `switch_guide` | Inventory from Excel import |
| `switch_connections` | Port-level connections from Excel |
| `config_templates` | Generated config snippets for the desktop app |

Useful views: `v_switch_summary`, `v_bgp_peers`, `v_vlan_ip_map`

Apply schema:
```bash
psql $DSN -f db/schema.sql
```

## Python Scripts

Install dependencies:
```bash
pip install psycopg2-binary openpyxl
```

### Parse PicOS config files

```bash
# Parse all .txt files in current directory, dump JSON
python parser/picos_parser.py .

# Parse a single file
python -c "
from parser.picos_parser import PicOSParser
cfg = PicOSParser('MEP-91-CORE02.txt').parse()
print(cfg.hostname, cfg.site, cfg.role)
print('VLANs:', len(cfg.vlans))
print('BGP AS:', cfg.bgp.local_as if cfg.bgp else None)
"
```

### Inspect the Excel guide

```bash
python parser/excel_importer.py switch_guide.xlsx
```

### Load everything into PostgreSQL

```bash
# Load configs + guide
python parser/db_loader.py \
  --dsn "postgresql://central:central@localhost:5432/central" \
  --config-dir . \
  --guide switch_guide.xlsx

# Configs only
python parser/db_loader.py --configs-only

# Guide only
python parser/db_loader.py --guide-only
```

Environment variable shortcut:
```bash
export CENTRAL_DSN="postgresql://central:central@localhost:5432/central"
python parser/db_loader.py
```

## Config File Format (PicOS)

Files are named `{SITE}-{ROLE}.txt`, e.g. `MEP-91-CORE02.txt`.

Config uses `set` CLI syntax:
```
set system hostname "MEP-91-CORE02"
set vlans vlan-id 101 description "IT"
set interface gigabit-ethernet xe-1/1/20 description "Prox01-Trunk"
set interface gigabit-ethernet xe-1/1/20 family ethernet-switching native-vlan-id 120
set interface gigabit-ethernet xe-1/1/20 family ethernet-switching port-mode "trunk"
set interface gigabit-ethernet xe-1/1/20 family ethernet-switching vlan members 1-500
set l3-interface vlan-interface vlan-101 address 10.11.101.2 prefix-length 24
set protocols bgp local-as "65112"
set protocols bgp neighbor 10.5.17.2 remote-as "65121"
set protocols vrrp interface vlan-101 vrid 1 ip 10.11.101.254
```

### Switch naming convention

```
MEP-{site_num}-{ROLE}{instance}
  MEP-91-CORE02   → site=MEP-91, role=core   (FS N-series 25/100G)
  MEP-93-L1-CORE02 → site=MEP-93, role=l1   (distribution)
  MEP-96-L2-CORE02 → site=MEP-96, role=l2   (access, ge- ports)
```

### Interface naming

| Prefix | Type | Speed |
|---|---|---|
| `xe-1/1/N` | 10G SFP+ | up to 100G with speed override |
| `ge-1/1/N` | 1G copper/SFP | 1G |
| `xe-1/1/N.M` | Breakout sub-interface | 10/25G |
| `ae-N` | LACP aggregate | varies |

## Sites in This Project

| Site | Role | Switches |
|---|---|---|
| MEP-91 | Building 91 | CORE02 (eBGP AS 65112) |
| MEP-92 | Building 92 | CORE01 (eBGP AS 65121) |
| MEP-93 | Building 93 | L1-CORE02 (distribution) |
| MEP-94 | Building 94 | CORE01 |
| MEP-96 | Building 96 | L2-CORE02 (access) |

eBGP peerings: 91↔92 over vlan-1017/vlan-262, 91→93 over vlan-1022

## Web App

FastAPI + HTMX + Jinja2. No build step — edit templates and reload.

### Authentication

HTTP Basic Auth is enabled. Default credentials: **admin / admin**

Note: SSH password for the switches (admin / admin123) is separate — enter it in the connectivity panel or save it via Edit Switch.

Override via environment variables:
```bash
export CENTRAL_USER=admin
export CENTRAL_PASS=admin123
```

### Start

```bash
./run.sh              # starts Podman pod + web server on :7472 (default)
./run.sh 9000         # custom port
```

Or manually:
```bash
pip install fastapi uvicorn[standard] psycopg2-binary jinja2 python-multipart paramiko openpyxl
export CENTRAL_DSN="postgresql://central:central@localhost:5432/central"
python -m uvicorn web.app:app --host 127.0.0.1 --port 8080 --reload
```

To restart after code changes (uvicorn --reload may not auto-pick-up on Windows):
```powershell
# Kill all Python processes bound to 8080, then restart run.sh
Get-Process python | Stop-Process -Force
```

### Pages

| URL | Description |
|---|---|
| `/` | Dashboard — all switches with stats |
| `/ipam` | IPAM grid — all 987 devices, sortable/filterable/groupable, links to config |
| `/switches/{hostname}` | Switch detail — VLANs, interfaces, BGP, routing, QoS, connectivity |
| `/switches/{hostname}/running-configs` | Downloaded config history |
| `/guide` | Switch guide from Excel — filterable |
| `/guide/{id}` | Per-switch connections — toggle enabled/disabled with one click |
| `/switches/{hostname}/preview` | Generated PicOS config — copy or download |
| `/import` | Upload .txt config files or .xlsx guide |

### Connectivity panel (switch detail page)

Each switch detail page has a connectivity bar at the top:
- **Ping** — pings `management_ip` (VLAN-152), shows latency, updates DB
- **Test SSH** — opens SSH connection to verify credentials
- **Download Running Config** — SSHs and pulls `show configuration`, stores in `running_configs` table, diffs vs previous

SSH uses `management_ip` (VLAN-152 address), `ssh_username` (default: root), `ssh_port` (default: 22).
Password can be entered inline or saved via Edit Switch form.

### Management IPs (VLAN-152)

| Switch | Management IP |
|---|---|
| MEP-91-CORE02 | 10.11.152.2 |
| MEP-92-CORE01 | 10.12.152.1 |
| MEP-93-L1-CORE02 | 10.13.152.2 |
| MEP-94-CORE01 | 10.14.152.1 |
| MEP-96-L2-CORE02 | 10.16.152.2 |

### How config generation works

1. Load parsed configs into DB via `parser/db_loader.py`
2. Import `switch_guide.xlsx` to get port connections
3. In `/guide/{id}`, toggle connections on/off (HTMX, no page reload)
4. Preview/Export builds PicOS `set` commands:
   - Infrastructure/trunk ports always included
   - Access ports only included if `enabled = true` in `switch_connections`
5. Download or copy the raw config text

### Web files

```
web/
├── app.py                      FastAPI routes + config generator + Basic Auth middleware
├── ssh_utils.py                ping_host(), ssh_download_config(), diff_configs()
├── requirements.txt
├── templates/
│   ├── base.html               Nav + layout
│   ├── dashboard.html          Switch card grid
│   ├── switch_detail.html      Tabbed detail + connectivity panel (HTMX)
│   ├── switch_form.html        Create/Edit switch (includes management IP + SSH fields)
│   ├── guide.html              Guide table with filters
│   ├── guide_detail.html       Per-switch connections + toggles
│   ├── preview.html            Config output + copy/download
│   ├── import.html             File upload forms
│   ├── running_configs.html    Downloaded config history list
│   ├── running_config_detail.html  Full config + coloured diff
│   ├── running_config_compare.html Side-by-side version compare
│   ├── generate.html           Config generation page
│   ├── ipam.html               IPAM device grid
│   ├── _connection_row.html    HTMX partial for connection toggle
│   ├── _ping_status.html       HTMX partial — ping result badge
│   ├── _ssh_status.html        HTMX partial — SSH test result badge
│   └── _ssh_download_result.html  HTMX partial — download result
└── static/
    ├── css/app.css             Dark theme styles
    └── js/app.js               Tabs, copy, drag-drop
```

### Database tables added by migrations

| Migration | Adds |
|---|---|
| 002_builder.sql | `vlan_templates`, extra columns on `switch_connections` and `bgp_config` |
| 003_connectivity.sql | `management_ip`, `ssh_*`, `last_ping_*`, `last_ssh_*` on `switches`; `running_configs` table |
| 004_ipam_fields.sql | Additional IPAM device fields |
| 009_config_ranges.sql | Config range definitions |
| 010_excel_sheets.sql | Excel sheet import tracking |
| 011_view_reserved.sql | Reserved device view |
| 012_config_versions.sql | Config version history for compare |
| 023_bgp_sync.sql | `fast_external_failover`, `bestpath_multipath_relax`, `last_synced` on `bgp_config` |

## VLAN Standard (across sites)

| VLAN | Description |
|---|---|
| 82 | Switch Management |
| 101 | IT |
| 105 | Conference Rooms |
| 106 | Printers/MFD |
| 112 | Data Staff |
| 120 | Servers |
| 128 | Data Staff WiFi |
| 136 | VoIP Staff |
| 152 | Devices (management trunk native) |
| 176 | Backup Replication |
| 235–239 | DMZ zones |
| 248 | DMZ Staff Patching |
| 254 | DMZ Cyber Isolation |
| 1017+ | eBGP interconnect VLANs |

## QoS / CoS (consistent across all switches)

| Class | Priority | Traffic |
|---|---|---|
| Teams-Voice | 6 | UDP 50000-50019 DSCP 46 |
| Teams-Video | 6 | UDP 50020-50039, 3478-3481 DSCP 34 |
| Teams-Screen-Share | 6 | UDP/TCP 50040-50059 DSCP 18 |
| Call-Sig-NetCtrl | 4 | SIP 5060/5061, H.323 1720 DSCP 24 |
| Telepresence | 5 | — |
| O365-FileShare-AV | — | SMB, SMTP, IMAP DSCP 16 |
| Low-priority | 1 | Default |

## Desktop App (WPF)

Location: `desktop/Central.sln`

**Stack:** C# / .NET 10 / WPF · DevExpress 25.2 (WPF subscription) · Npgsql 10.0.2 · Svg.NET 3.4.7

**Build:**
```powershell
cd desktop
dotnet build Central.sln --configuration Release -p:Platform=x64
# Output: desktop/Central.Desktop/bin/x64/Release/net10.0-windows/Central.exe
```

**Run:**
```powershell
cd desktop/Central.Desktop/bin/x64/Release/net10.0-windows
./Central.exe
```

**DevExpress license:** Place `DevExpress_License.txt` in `%AppData%\DevExpress` or run the DX Unified Installer. Without license = evaluation mode (watermark).

**DB connection:** Reads `CENTRAL_DSN` env var or falls back to `Host=localhost;Port=5432;Database=central;Username=central;Password=central`. ConnectivityManager handles offline mode with 5s connection timeout and 10s background retry — app starts in offline mode if DB unreachable, auto-loads data when connection restores.

### Ribbon Tabs

| Tab | Groups |
|---|---|
| Home | Connection, Export, Web App, Layout (Save/Restore Default via cog) |
| Devices | Actions (New/Edit/Delete), Group By, Filter, Panels (IPAM/Switches/Details), Connectivity (Sync BGP/Sync All BGP) |
| Switches | Actions, Connectivity (Ping All/Ping Selected), Panels, Data |
| Links | (context tab, blue) — Actions, Panels |
| Routing | (context tab) — BGP actions |
| VLANs | (context tab) — VLAN actions |
| Tasks | (context tab) — Task actions |
| Admin | Actions (New/Edit/Delete — routes by active panel), Panels (Roles/Users/Lookups/Details/Ribbon Config/Ribbon Admin), Data |

Context tabs (Links blue, Switch green, Admin amber) appear/disappear based on the active panel.

Quick Access Toolbar: Save/Refresh/Undo pinned, user-customizable.

### Ribbon Customization (3-layer override)

| Layer | Table | Priority | Who |
|---|---|---|---|
| 1 | `admin_ribbon_defaults` | Lowest | Admin pushes defaults for all users |
| 2 | `ribbon_items` (DB seed) | Middle | Module registration / system defaults |
| 3 | `user_ribbon_overrides` | Highest | Per-user icon, text, visibility |

`PreloadIconOverridesAsync` runs BEFORE ribbon build to prevent icon flash on startup.

### Document Panels

| Panel | Content | Closeable |
|---|---|---|
| IPAM | DeviceRecord grid — inline editing, dropdown columns, RESERVED row highlighting | Yes |
| Configured Switches | SwitchRecord grid — ping/SSH status icons (green/red/grey circles), latency | Yes |
| Roles & Permissions | Split: roles grid left, permissions tree + site access checkboxes right | Yes |
| Users | User grid with role dropdown bound to DB roles | Yes |
| Lookup Values | Category/Value grid for dropdown options | Yes |
| BGP | Master-detail: top grid (BGP config per switch), bottom tabs (Neighbors, Advertised Networks). SSH sync from live switches | Yes |
| Config Compare | Side-by-side diff with line numbers, pink highlighting, synced scroll — toggled via Compare button in Details > Config tab | Yes |
| Asset Details | Right-docked detail panel, updates on grid row selection | Yes |
| Ribbon Config | User ribbon customizer tree — icon picker, custom text, hide/show, reorder, apply/reset | Yes |
| Ribbon Admin | Admin ribbon customizer tree — set defaults for all users, display style, link targets, push all | Yes |

### Authentication & RBAC

- **Auto-login** by Windows username match to `app_users` table
- **Roles** (Admin/Operator/Viewer + custom): per-module permissions (View/Edit/Delete) for ipam, switches, admin
- **Site-level access**: `role_sites` table controls which buildings each role can see — data is filtered at SQL level (`WHERE building = ANY(@sites)`) so unauthorized data never leaves the DB
- Hidden UI: ribbon buttons use `IsVisible`, panels use `DockController.Close()`, grid editing disabled per role

### Key Architecture Patterns

- **ItemClick events** instead of Command binding for DevExpress BarButtonItem (Command binding is unreliable in DX ribbon)
- **ComboBoxEditSettings** wired in code-behind via `BindComboSources()` (XAML binding fails — EditSettings are not in the visual tree)
- **DockController.Close()/Restore()** for panel toggle (not Visibility.Collapsed — that breaks DX tab close buttons)
- **INotifyPropertyChanged** on all models for live grid updates (e.g., ping status icons updating in real-time)
- **ValidateRow** event on TableView for auto-save on row commit
- **_deletePending flag** + capture CurrentItem before CancelRowEdit() for delete-during-edit
- **LayoutService** saves/restores grid layouts, dock layout, window bounds, panel states, preferences to `user_settings` table per user
- **Settings cog** in ribbon page header (BarSubItem via code-behind) for Save Layout / Restore Default
- **NavigationStyle="Cell"** required for inline editing (Row navigation prevents cell editing)
- **ShownEditor** event must be wired in code-behind constructor, not XAML attributes
- **Saved layouts override XAML** — skip layout restore for newly-editable grids until user re-saves
- **ConnectivityManager** handles DB connection with offline mode, background retry, ConnectionChanged event
- **Multi-column port dropdowns** via ComboBoxEdit ItemTemplate (Interface, Admin, Link, Speed, Description, LLDP)
- **Global ComboBox clear** via EventManager.RegisterClassHandler for Delete/Backspace on ComboBoxEdit
- **Natural numeric sort** for interface names (xe-1/1/2 before xe-1/1/10)
- **3-layer ribbon icon override** — PreloadIconOverridesAsync loads admin_ribbon_defaults + user_ribbon_overrides BEFORE ribbon build (no flash)
- **SVG icon rendering** via Svg.NET — SvgHelper replaces "currentColor" with white, caches in-memory + disk (%LocalAppData%/Central/icon_cache/)
- **IconService singleton** — metadata cache on startup, on-demand PNG byte loading, admin/user resolution chain
- **ImagePickerWindow** — pack + category checkboxes, async batch load from DB (png_32 preferred), generation counter cancels stale loads
- **RibbonTreeItem** — flat tree node model with Id/ParentId for TreeListControl, NodeType discriminator (page/group/item/separator)
- **Context tabs** — Links (blue), Switch (green), Admin (amber) — appear/disappear by active panel
- **Quick Access Toolbar** — Save/Refresh/Undo pinned, user-customizable

### Desktop Files

```
desktop/Central.Desktop/
├── App.xaml.cs                     Startup, auto-login, session init
├── MainWindow.xaml                 Ribbon + DockLayoutManager + all grids
├── MainWindow.xaml.cs              Event handlers, panel routing, ping, layout, config compare, ribbon overrides
├── ImagePickerWindow.xaml/.cs      Icon picker dialog — pack/category checkboxes, async batch PNG load, search
├── BulkEditWindow.xaml/.cs         Reflection-based bulk edit (any model, field picker + preview)
├── LoginWindow.xaml/.cs            Login dialog (fallback from Windows auto-login)
├── SetPasswordWindow.xaml/.cs      Change password dialog (SHA256 + salt)
├── SplashWindow.xaml/.cs           Splash screen with startup worker progress
├── Converters/
│   ├── NullToBoolConverter.cs
│   └── StringToBrushConverter.cs
├── Data/
│   └── DbRepository.cs            All DB CRUD (devices, switches, users, roles, sites, lookups, settings, config versions)
├── Models/
│   ├── (models now in Central.Core/Models/ — see below)
├── Services/
│   ├── UserSession.cs              Static singleton — CurrentUser, Permissions, AllowedSites
│   ├── PingService.cs              Parallel ping via System.Net.NetworkInformation
│   ├── SshService.cs               SSH connectivity, config download, BGP config parsing
│   ├── LayoutService.cs            Save/restore grids, dock, window bounds, preferences
│   ├── ConnectivityManager.cs      DB connection management — 5s timeout, 10s retry, offline mode
│   ├── SvgHelper.cs                SVG→WPF ImageSource via Svg.NET, currentColor→white, disk + memory cache
│   ├── GridContextMenuBuilder.cs   One-line right-click menus for any grid
│   ├── ConfigCompareHelper.cs      Side-by-side diff panel helper
│   ├── BackstageHelper.cs          Backstage tab wiring
│   ├── DetailDialogService.cs      Nested entity detail dialogs
│   ├── SettingsProvider.cs         DB-backed per-user module settings
│   ├── ConfigBuilderService.cs     PicOS config generation
│   ├── LinkEditorHelper.cs         Link editing helpers
│   ├── DirectDbDataService.cs      IDataService → direct PostgreSQL
│   ├── SshProxy.cs                 SSH proxy service
│   ├── SwitchSyncService.cs        Switch config sync
│   └── InputPrompt.cs              Simple text input dialog
├── ViewModels/
│   ├── MainViewModel.cs            All collections, CRUD methods, permission properties
│   └── RelayCommand.cs             ICommand implementation
└── NuGet.config                    Licensed DevExpress feed

desktop/Central.Core/Models/
├── AppUser.cs, DeviceRecord.cs, SwitchRecord.cs  — INotifyPropertyChanged
├── P2PLink.cs, B2BLink.cs, FWLink.cs             — Network link models
├── BgpRecord.cs, BgpNeighborRecord.cs, BgpNetworkRecord.cs
├── RibbonConfig.cs                — RibbonPageConfig, RibbonGroupConfig, RibbonItemConfig, UserRibbonOverride
├── RibbonTreeItem.cs              — Flat tree node (Id/ParentId), display style, link target, icon preview
├── SavedFilter.cs                 — DB-backed saved filters per panel per user
├── TaskItem.cs                    — Task model with tree hierarchy
├── (+ 20 more: VlanEntry, Server, RoleRecord, LookupItem, MlagConfig, MstpConfig, etc.)

desktop/Central.Data/
├── DbRepository.cs                — Main DB repo (partial class)
├── DbRepository.Ribbon.cs         — Ribbon CRUD: pages, groups, items, user overrides, admin defaults, saved filters
├── IconService.cs                 — Singleton: metadata cache, admin/user icon resolution, search, bulk SVG import
├── AppLogger.cs                   — Application logging to DB

desktop/Central.Module.Admin/Views/
├── RibbonConfigPanel.xaml/.cs     — Ribbon config grid panel (flat list)
├── RibbonTreePanel.xaml/.cs       — User ribbon customizer tree (icon picker, hide/show, reorder, apply/reset)
├── RibbonAdminTreePanel.xaml/.cs  — Admin ribbon customizer tree (push defaults, display style, link targets)
```

### Database Migrations

| Migration | Adds |
|---|---|
| 002_builder.sql | `vlan_templates`, extra columns on `switch_connections` and `bgp_config` |
| 003_connectivity.sql | `management_ip`, `ssh_*`, `last_ping_*`, `last_ssh_*` on `switches`; `running_configs` table |
| 004_ipam_fields.sql | Additional IPAM device fields |
| 005_lookup_values.sql | `lookup_values` table for dropdown options |
| 006_users_roles.sql | `app_users`, `role_permissions`, `user_settings` tables |
| 007_roles_table.sql | `roles` table for role management UI |
| 008_role_sites.sql | `role_sites` table for per-role site/building access control |
| 009_config_ranges.sql | Config range definitions |
| 010_excel_sheets.sql | Excel sheet import tracking |
| 011_view_reserved.sql | Reserved device view |
| 012_config_versions.sql | Config version history for compare |
| 023_bgp_sync.sql | `fast_external_failover`, `bestpath_multipath_relax`, `last_synced` on `bgp_config` |
| 024_permissions_v2.sql | `permissions` table (25 module:action codes), `role_permission_grants`, role priorities |
| 025_audit_log_v2.sql | `audit_log` (append-only, JSONB), soft delete columns on link + device tables |
| 026_pg_notify.sql | pg_notify triggers on 19 tables for real-time SignalR |
| 027_user_auth.sql | `password_hash`, `salt`, `user_type`, `last_login` on `app_users` |
| 028_default_settings.sql | `default_user_settings` table + auto-seed trigger |
| 029_job_schedules.sql | `job_schedules` + `job_history` tables for background jobs |
| 030_icon_library.sql | `icon_library` (PNG/SVG), `ribbon_icon_assignments`, `user_icon_overrides` |
| 031_tasks.sql | `tasks` table for task management module |
| 032_ribbon_config.sql | `ribbon_pages`, `ribbon_groups`, `ribbon_items` + pg_notify triggers |
| 033_service_desk_incremental.sql | SD tables (`sd_requests`, `sd_requesters`, `sd_technicians`), `integrations`, `integration_credentials`, permissions, ME seed |
| 034_sd_teams.sql | `sd_teams` + `sd_team_members` for tech team grouping |
| 035_sd_resolved_at.sql | `resolved_at` + `me_completed_time` columns on `sd_requests` |
| 036_sd_groups.sql | `sd_groups` lookup + `sd_group_categories` + `sd_group_category_members` for nested group filtering |
| 039_extended_user_fields.sql | department, title, phone, mobile, company, ad_guid, last_ad_sync on `app_users` |
| 040_ad_config.sql | AD integration config in `integrations`, `admin:ad` permission |
| 041_migration_history.sql | `migration_history` table for schema migration tracking |
| 042_backup_history.sql | `backup_history` table, `db_backup` job schedule |
| 043_location_tables.sql | `countries`, `regions`, `postcodes` with seed data |
| 044_reference_numbers.sql | `reference_config` table, `next_reference()` PG function |
| 045_panel_customizations.sql | `panel_customizations` (per-user JSONB settings) |
| 046_appointments.sql | `appointments`, `appointment_resources` for scheduler |
| 047_identity_providers.sql | `identity_providers`, `idp_domain_mappings`, `user_external_identities`, `claim_mappings`, `auth_events`, `social_providers`, `magic_link_tokens`, `mfa_recovery_codes`, `revoked_tokens` + app_users auth extensions |
| 048_pg_notify_new_tables.sql | pg_notify triggers for all tables added in migrations 029-047 |
| 049_sync_engine.sql | `sync_configs`, `sync_entity_maps`, `sync_field_maps`, `sync_status`, `sync_log` — pluggable integration sync engine |
| 050_webhook_log.sql | `webhook_log` — inbound webhook payload storage with pg_notify |
| 051_job_cron.sql | `schedule_cron` column on `job_schedules` for cron expression scheduling |
| 052_audit_log.sql | `audit_log` (before/after JSONB snapshots) + `password_history` for reuse prevention |
| 053_api_keys.sql | `api_keys` — service-to-service API key auth with SHA256 hash, role, usage tracking |
| 054_notification_prefs.sql | `notification_preferences` + `active_sessions` — per-user alert channels + session management |
| 055_saved_filters.sql | `saved_filters` — per-user per-panel filter presets (was referenced but table missing) |
| 060_tasks_v2.sql | `portfolios`, `programmes`, `task_projects`, `sprints`, `releases`, `task_links`, `task_dependencies` + 23 new columns on `tasks` |
| 061_sprint_planning.sql | `sprint_allocations`, `sprint_burndown` + `snapshot_sprint_burndown()` function |
| 062_kanban_board.sql | `board_columns`, `board_lanes` with default 5-column seed |
| 063_workflows.sql | `workflow_assignments`, `workflow_approvals`, `workflow_execution_log` (Elsa tracking) |
| 064_baselines.sql | `task_baselines` + `save_project_baseline()` function |
| 065_custom_columns.sql | `custom_columns`, `custom_column_permissions`, `task_custom_values` |
| 066_reports_dashboards.sql | `saved_reports`, `dashboards`, `dashboard_snapshots` |
| 067_time_activity.sql | `time_entries`, `activity_feed`, `task_views` + `log_task_activity()` auto-trigger |

## Development Notes

- PicOS uses `set` format (similar to Junos flat format)
- Interfaces on core switches: `xe-1/1/N` (10G/25G/40G/100G)
- Breakout ports (`xe-1/1/31`, `xe-1/1/32`) split into .1/.2/.3/.4 sub-interfaces
- VRRP VRID 1 = site MEP-91, VRID 12 = site MEP-92
- BGP ECMP max-paths 4 on all core switches
- MSTP bridge-priority: CORE02=6000 (master), CORE01=12288 (secondary)
- DHCP relay servers: 10.11.120.10 and 10.11.120.11 (both sites)
- Config generation: P2P, B2B, and FW links do NOT emit `port-mode "trunk"` — not needed for point-to-point links
- BGP neighbor commands in B2B BuildConfig: `set protocols bgp neighbor ... remote-as ... bfd`

### Icon System

Two icon sources available:

**Axialist Icons** (custom, stored in DB):
- **11,676 icons** in two packs: OfficePro + Universal
- Stored in `icon_library` table as SVG source + pre-rendered PNG 16px + PNG 32px
- `SvgHelper.RenderSvgToImageSource()` — Svg.NET renders SVG to WPF BitmapImage, replaces `currentColor` with `#FFFFFF`
- In-memory cache (hash-keyed) + local disk cache (`%LocalAppData%/Central/icon_cache/`)
- `IconService` singleton loads metadata on startup, loads PNG bytes on demand
- `ImagePickerWindow` — DXDialogWindow with pack checkboxes (OfficePro/Universal), category checkboxes, search, async batch PNG load

**DevExpress SVG Gallery** (built-in, theme-aware):
- **34 categories**, thousands of vector icons in color + grayscale
- Ships with `DevExpress.Images.v25.2.dll` — no DB storage needed
- Auto-adapts to app theme (Win11Dark, Office2019Colorful, etc.)
- XAML: `Glyph="{dx:DXImage 'SvgImages/Actions/Open2.svg'}"`
- Code: `DxSvgGallery.GetSvgImage("Actions", "Open2")` returns `SvgImage`
- `DxSvgGallery.cs` has category list + common icon reference
- Categories: Actions, Business Objects, Chart, Content, Data, Edit, Filter, Grid, Navigation, Reports, Setup, Zoom, etc.
- Use the DX Image Picker (Visual Studio designer) for visual browsing

### Ribbon Customization

- **3-layer override**: `admin_ribbon_defaults` (lowest) → `ribbon_items` (middle) → `user_ribbon_overrides` (highest)
- `PreloadIconOverridesAsync` runs at startup BEFORE ribbon build — loads all 3 layers into `_preloadedOverrides` dictionary
- `RibbonTreePanel` (user) — TreeListControl with Id/ParentId hierarchy, icon picker button, hide/show toggle, custom text, reorder (Move Up/Down), Apply/Reset All
- `RibbonAdminTreePanel` (admin) — same tree + display style dropdown (large/small/smallNoText), item type dropdown, link target picker, Push All Defaults button
- Link targets: `panel:PanelName`, `url:https://...`, `action:ActionKey`, `page:PageName`
- Auto-save on icon pick / hide toggle / rename (SaveSingleOverride delegate)
- DB tables: `ribbon_pages`, `ribbon_groups`, `ribbon_items`, `admin_ribbon_defaults`, `user_ribbon_overrides`
