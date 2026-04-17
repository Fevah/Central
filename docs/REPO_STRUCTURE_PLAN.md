# Repo Structure — Enterprise Redesign (Plan)

**Status:** Draft — no moves have been made yet. Review, redirect, approve before execution.
**Date:** 2026-04-17
**Scope:** the entire working tree at `c:\Code\New folder\Central\`.

---

## Why restructure

The current tree grew organically across many buildout phases. Day-to-day that works, but for a team:

- A new developer can't tell from `desktop/` that it contains the REST API, data access layer, workflow engine, and every shared library — they're all under one misleading name.
- Scripts live in four different places (`/scripts/`, `/desktop/scripts/`, `/infra/scripts/`, and loose at the root).
- `db/` exists twice (root + inside `desktop/`), with diverging content.
- Root is cluttered with one-off files (`Icons/`, `run.sh`, `start-gateway.cmd`, `generate_icon.py`, `find_window.ps1`, `cleanup-old-folders.ps1`).
- Platform surfaces are split inconsistently: frontend in `web-client/`, backend buried in `desktop/`, Rust services under `services/` but mostly missing.
- `web/` (legacy FastAPI) and several PowerShell cleanup scripts are dead code nobody has removed.

We want: a convention-based tree where a new developer lands and knows where everything is in 30 seconds, CI paths are predictable, and each top-level folder has exactly one job.

## Goals

1. **Human-first.** Names describe intent (apps / services / libs / modules), not history (`desktop/`, `SecureAPP/`).
2. **One job per folder.** `apps/` is end-user surfaces. `services/` is backend services. `libs/` is shared code. `modules/` is WPF feature modules. `infra/` is deployment. `tools/` is dev utilities. `docs/` is docs.
3. **No hidden platforms.** Frontend, backend, mobile, desktop, CLI — each visible at the top level.
4. **Predictable CI paths.** GitHub workflows key off folder globs; a clean tree means simpler workflows and faster builds.
5. **Root stays short.** Only: `README.md`, `CLAUDE.md`, `.gitignore`, `Central.sln`, the top-level folders, and CI config (`.github/`, `.vscode/`). No loose scripts, no loose assets.
6. **Tests live next to what they test,** not in a sprawling `Central.Tests` sibling. (Open question — see §9.)

## Target tree

```
/
├── apps/                          End-user surfaces
│   ├── desktop/                   WPF shell (was: desktop/Central.Desktop)
│   ├── web/                       Angular 21 + DevExtreme (was: web-client/)
│   └── mobile/                    Flutter client (was: SecureAPP/clients/mobile if restored)
│
├── services/                      Backend services (each independently deployable)
│   ├── api/                       Central.Api — REST + SignalR (was: desktop/Central.Api)
│   ├── auth/                      auth-service (Rust)
│   ├── admin/                     admin-service (Rust)
│   ├── gateway/                   gateway (Rust)
│   ├── task/                      task-service (Rust)
│   ├── storage/                   storage-service (Rust)
│   ├── sync/                      sync-service (Rust)
│   ├── audit/                     audit-service (Rust)
│   └── tenant-provisioner/        Rust — already in tree
│
├── libs/                          Shared .NET libraries (cross-app)
│   ├── core/                      Central.Core — engine, auth, models, widgets
│   ├── data/                      Central.Data — PostgreSQL repositories + logger
│   ├── api-client/                Central.Api.Client — typed HTTP + SignalR client
│   ├── workflows/                 Central.Workflows — Elsa integration
│   ├── security/                  Central.Security — ABAC engine
│   ├── tenancy/                   Central.Tenancy — tenant resolution
│   ├── licensing/                 Central.Licensing — license keys
│   ├── observability/             Central.Observability
│   ├── collaboration/             Central.Collaboration — presence
│   ├── protection/                Central.Protection
│   └── update-client/             Central.UpdateClient
│
├── modules/                       WPF feature modules (pluggable into apps/desktop)
│   ├── admin/                     Central.Module.Admin
│   ├── audit/                     Central.Module.Audit
│   ├── crm/                       Central.Module.CRM
│   ├── dashboard/                 Central.Module.Dashboard
│   ├── devices/                   Central.Module.Devices
│   ├── global-admin/              Central.Module.GlobalAdmin
│   ├── links/                     Central.Module.Links
│   ├── routing/                   Central.Module.Routing
│   ├── service-desk/              Central.Module.ServiceDesk
│   ├── switches/                  Central.Module.Switches
│   ├── tasks/                     Central.Module.Tasks
│   └── vlans/                     Central.Module.VLANs
│
├── db/                            Single source of truth for schema
│   ├── migrations/                001-082+.sql
│   ├── schema.sql                 Current consolidated schema
│   └── seed/                      Seed data (seed_auth.sql, etc.)
│
├── infra/                         IaC + deployment
│   ├── terraform/                 Modules (was: infra/modules)
│   ├── terragrunt/                Environments (was: infra/environments)
│   ├── k8s/                       Kustomize overlays
│   ├── ansible/                   Bootstrap roles
│   └── vagrant/                   Local VM definitions
│
├── tools/                         Dev utilities — NOT shipped
│   ├── icons/                     generate_icon.py, import_icons.py, import_svg_icons.py
│   ├── parser/                    picos_parser.py, db_loader.py, excel_importer.py
│   └── scripts/                   backup.sh, check-services.sh, migrate-users.sh, etc.
│
├── tests/                         Integration + E2E tests that span services
│   ├── dotnet/                    Central.Tests (consolidated — see §9)
│   ├── integration/               Cross-service flows
│   └── smoke/                     Post-deploy smoke tests
│
├── assets/                        Static assets (non-code)
│   └── icons/                     OfficePro/, Universal/ (was: Icons/)
│
├── config/                        Runtime configuration
│   ├── auth-service.toml
│   └── gateway.env
│
├── docs/                          Documentation (already exists)
│
├── backups/                       Gitignored — local dumps (was: backups/)
│
├── .github/                       CI/CD (unchanged)
├── .vscode/                       Editor config (unchanged)
├── .claude/                       Claude workspace (unchanged)
├── .gitignore
├── README.md
├── CLAUDE.md
└── Central.sln                    Promoted to root; references every .NET project
```

## Principles behind the shape

- **`apps/` vs `services/`** is the classic monorepo split: `apps/` are the things users launch, `services/` are things deployed to run continuously. An Angular SPA is an app; an auth microservice is a service. A mobile client is an app even though it talks to services.
- **`libs/` holds cross-cutting .NET code.** Anything two or more apps/services depend on goes here. The name matches Nx / Rush / Bazel / pnpm convention so it'll be familiar to JS, Go, Rust, and C# developers alike.
- **`modules/` is deliberately separate from `libs/`** because WPF feature modules have a narrower contract — they register a ribbon tab and a set of panels into the desktop shell. They're not general-purpose libraries.
- **Nothing "platform-shaped" at the root.** The old `desktop/` name lied: it held the whole .NET backend. In the target tree the backend lives in `services/api/` + `libs/*`, which is what it actually is. `apps/desktop/` is just the WPF shell.
- **`db/` wins the naming battle.** One database, one folder. The copy under `desktop/db/` is stale and deleted.
- **`tools/` separates dev utilities from anything shipped.** Nothing in `tools/` is on the critical path. CI doesn't need to build it. New developers can skip it.
- **Tests co-located or consolidated — pick one.** Current state is "all .NET tests in one sibling project". That works fine; the move is just `desktop/Central.Tests → tests/dotnet/`. If we ever want per-lib tests (xUnit projects next to each lib), that's a later decision.

## Phased execution

Each phase is a single PR. Each PR builds, tests green, and is reviewable in isolation. Don't skip phases — ordering matters (Phase 2 depends on Phase 1 cleanup, Phase 3 depends on the .NET solution having moved in Phase 2).

### Phase 0 — Land the plan (this doc)

No file moves. Just:

- Land `docs/REPO_STRUCTURE_PLAN.md` (this file).
- Update `.gitignore`: `__pycache__/`, `packages-offline/`, any editor temp files that shouldn't be tracked.
- Create the **empty** top-level directories (`apps/`, `libs/`, `modules/`, `tools/`, `assets/`, `tests/`) with a single `.gitkeep` each so the target layout is visible before anything moves.

**Risk:** zero. Pure additions.

### Phase 1 — Discard dead code and loose clutter

Before moving anything, remove what we're not carrying forward.

- **Delete** `web/` (legacy FastAPI — superseded by `Central.Api` + Angular). Already flagged as legacy in the checklist rebuild.
- **Delete** `desktop/db/` (duplicate of root `db/`, drifted content).
- **Delete** `desktop/packages-offline/` (offline NuGet cache, not referenced by builds).
- **Delete** `desktop/cleanup-old-folders.ps1`, `desktop/find_window.ps1`, `desktop/generate_icon.py` — ad-hoc utilities never referenced.
- **Delete** `run.sh`, `start-gateway.cmd`, `start-task-service.cmd` at the root (replaced later by proper dev scripts in `tools/scripts/`).

**Risk:** low. Each deletion is independently verifiable by searching for references to its path. Do the search per-file before deleting.

**Verification gate:** `dotnet build`, `dotnet test`, Angular build, all green.

### Phase 2 — .NET reshuffle

The big one. Move 25 .NET projects into their proper homes.

**Moves:**

| From | To |
|------|----|
| `desktop/Central.Core` | `libs/core` |
| `desktop/Central.Data` | `libs/data` |
| `desktop/Central.Api.Client` | `libs/api-client` |
| `desktop/Central.Workflows` | `libs/workflows` |
| `desktop/Central.Security` | `libs/security` |
| `desktop/Central.Tenancy` | `libs/tenancy` |
| `desktop/Central.Licensing` | `libs/licensing` |
| `desktop/Central.Observability` | `libs/observability` |
| `desktop/Central.Collaboration` | `libs/collaboration` |
| `desktop/Central.Protection` | `libs/protection` |
| `desktop/Central.UpdateClient` | `libs/update-client` |
| `desktop/Central.Module.*` (12 projects) | `modules/<name>` |
| `desktop/Central.Api` | `services/api` |
| `desktop/Central.Desktop` | `apps/desktop` |
| `desktop/Central.Tests` | `tests/dotnet` |
| `desktop/Central.sln` | `Central.sln` (root) |

After moves:

- Rewrite every `<ProjectReference>` path in every `.csproj` to the new location. There are ~30 project references; scripted find/replace.
- Rewrite `Central.sln` paths.
- Rewrite any CI workflow paths in `.github/workflows/*.yml`.
- Rewrite any script that hardcodes `desktop/...` (search for the string; fix each).
- Remove the now-empty `desktop/` folder.

**Rename scope:** three projects get new assembly + namespace names in this phase — `Central.Core → Central.Engine`, `Central.Data → Central.Persistence`, `Central.Api.Client → Central.ApiClient`. See §"Assembly rename scope" for impact. All other 22 projects keep their current names. Split this phase into two commits if the diff is unmanageable — (2a) folder moves with names unchanged, verify green build; (2b) scripted namespace rename of the three projects, re-verify green build.

**Risk:** medium. Path breakage is the failure mode. Catch it with a full build + test run before merge.

**Verification gate:** `dotnet build Central.sln --configuration Release -p:Platform=x64` = 0 errors. `dotnet test` = same pass count as before the move. Desktop app launches. API starts.

### Phase 3 — Frontend move + Rust services doc-fix

- **Move** `web-client/` → `apps/web/`.
- `services/tenant-provisioner/` stays put.
- **Update** `docs/SERVER_ARCHITECTURE.md`, `docs/MERGE_PLAN.md`, and `CLAUDE.md` to clearly mark the seven unbuilt Rust services (auth, admin, gateway, task, storage, sync, audit) as **planned** rather than shipped. Anyone adding one lands it straight into `services/<name>/`.

**Risk:** low — Angular is just a path change, the rest is documentation.

**Verification gate:** Angular build succeeds from `apps/web/`, dev server runs.

### Phase 4 — Tools + assets + root cleanup

- **Move** `scripts/` → `tools/scripts/`.
- **Move** `desktop/scripts/` (remaining contents) → `tools/icons/` (SVG import) / `tools/scripts/` as appropriate.
- **Move** `infra/scripts/` — stays (it's deployment-adjacent, keep it near IaC). Decision flag: merge into `tools/scripts/` only if the CI/deployment split would be cleaner; otherwise leave.
- **Move** `parser/` → `tools/parser/`.
- **Move** `Icons/` → `assets/icons/` (OfficePro + Universal subfolders intact).
- **Replace** the old loose root scripts (`run.sh`, etc. — already deleted in Phase 1) with proper dev CLI scripts in `tools/scripts/`: `dev-up.sh`, `dev-down.sh`, `build-all.sh`, `test-all.sh`.
- **Move** the root `Central.sln` references — already done in Phase 2; just sanity-check.

**Risk:** low. Mostly renames.

**Verification gate:** all dev commands in the README still work from their new paths. CI workflows still find their inputs.

### Phase 5 — Documentation sweep

Update every doc that references the old paths.

- `CLAUDE.md` — replace every `desktop/Central.X` reference with the new `libs/x` / `apps/desktop` / `services/api` location. This is the highest-traffic doc.
- `docs/ARCHITECTURE.md` — rewrite the Solution Structure section.
- `docs/MERGE_PLAN.md`, `docs/TOTALLINK_PATTERNS.md`, `docs/FEATURE_TEST_CHECKLIST.md` — search and replace paths.
- `docs/SERVER_ARCHITECTURE.md` — similar.
- `README.md` (root) — promote from stub to a proper entry point: what the project is, how to build, where things live (link to this doc).

**Risk:** zero. Documentation only.

**Verification gate:** `grep -r "desktop/Central\." docs/ CLAUDE.md` returns no hits (except historical mentions explicitly flagged as "pre-restructure").

### Phase 6 — Optional polish

Only after Phases 0-5 are merged and stable. Treat each as its own PR.

- **Top-level README.md** — proper project landing page with build matrix, quickstart, and a diagram of the tree.
- **Per-folder README.md** in each of `apps/`, `services/`, `libs/`, `modules/`, `tools/` — one paragraph on what belongs there and the convention for adding new entries.
- **CI workflow optimisation** — now that paths are predictable, use `paths:` filters in `.github/workflows/` so unrelated changes don't trigger the full build.
- **Renaming** — consider renaming `Central.Core` → `Central.Engine`, `Central.Data` → `Central.Persistence`, etc. to match the new folder names. Deferred — separate risk surface.
- **Code-owner file** — `.github/CODEOWNERS` mapping `libs/core/` → a specific reviewer, etc., once a team actually exists.

## Risks and rollback

- **Build breakage mid-move.** Mitigation: do moves in single focused PRs, run `dotnet build` + `dotnet test` locally before push, require a green CI run to merge.
- **IDE state confusion.** Rider/VS will cache old paths in `.suo`, `.user`, `.idea/`. Add these to `.gitignore` if not already, and tell devs to close-and-reopen after a restructure PR lands.
- **Git history readability.** Large rename commits look like "9,000 files changed". Use `git mv` (not delete+add) so `git log --follow` keeps history per file. Git's rename detection catches it anyway; this just makes it explicit.
- **Rust services state unknown.** Flagged in Phase 3. We can't plan a move until we know where they actually are.
- **Rollback.** Each phase is its own commit; `git revert <sha>` puts the tree back. The restructure is mechanical, so a revert is safe.

## Decisions — resolved 2026-04-17

1. **Tests:** consolidated `tests/dotnet/` ✅
2. **Rename assembly names as well as folders:** ✅ — see §"Assembly rename scope" below for the proposed new names (sign-off needed on the list before execution).
3. **`CLAUDE.md` at root:** ✅
4. **Legacy `desktop/` deleted cleanly, no symlink:** ✅
5. **`tests/` top-level:** ✅
6. **Rust services:** they don't exist in the current repo. `services/tenant-provisioner/` is the only Rust code present. Git history shows no `.rs` files outside it. Session docs reference 7 other Rust services as aspirational architecture — see §"Rust services status" below.

## Assembly rename scope (needs sign-off)

Current state: every .NET project is `Central.<Something>.csproj` with assembly name + root namespace to match.

Proposed renames — mostly light-touch to match the new folder layout. None are mandatory; they're improvements where the current name is awkward or misleading. Reject any you don't want.

| Current | Proposed | New folder | Reason |
|---------|----------|------------|--------|
| `Central.Core` | `Central.Engine` | `libs/engine/` | "Core" is overused in .NET ecosystems (Microsoft.AspNetCore, EF Core). "Engine" matches the framing in CLAUDE.md ("engine platform" / "engine services") and won't collide in NuGet search. |
| `Central.Data` | `Central.Persistence` | `libs/persistence/` | "Data" is ambiguous (data layer? data model? data service?). "Persistence" makes it explicit it's the repo/DB layer. |
| `Central.Api.Client` | `Central.ApiClient` | `libs/api-client/` | Drop the second dot. Current name reads like a sub-namespace of `Central.Api`, which it isn't. |
| `Central.Module.Devices` → `Central.Module.VLANs` (12 projects) | **unchanged** | `modules/<name>/` | Module names are fine. Only folders move. |
| `Central.Api` | **unchanged** | `services/api/` | Name already matches purpose. |
| `Central.Desktop` | **unchanged** | `apps/desktop/` | Same. |
| `Central.Tests` | **unchanged** | `tests/dotnet/` | Same. |
| `Central.Workflows`, `Central.Security`, `Central.Tenancy`, `Central.Licensing`, `Central.Observability`, `Central.Collaboration`, `Central.Protection`, `Central.UpdateClient` | **unchanged** | `libs/<name>/` | All fine as-is. |

So only 3 actual renames: `Core → Engine`, `Data → Persistence`, `Api.Client → ApiClient`. Everything else keeps its name.

**Rename impact (for the 3 renamed projects):**

- ~900 `using Central.Core;` statements across the codebase become `using Central.Engine;` (scripted).
- ~120 `using Central.Data.*;` → `using Central.Persistence.*;`.
- ~30 XAML `xmlns:` references updated.
- `[assembly: InternalsVisibleTo]` attributes updated.
- Any DB-stored assembly-qualified type names (audit log, serialised settings) — check and update if found.
- NuGet package IDs (if any are published) would need new names or transitional metapackages.

**Reject any of the 3 renames above if the churn isn't worth it.** "Central.Core" is fine if you don't want the disruption. Most concrete case: renaming `Central.Core` touches 900+ files.

## Rust services status

The current repo has exactly one Rust service: `services/tenant-provisioner/`. The other seven referenced in `CLAUDE.md` and session memory don't exist in this git history. They were planned architecture, not shipped code. Here's what each was meant to do:

| Service | Purpose |
|---------|---------|
| `auth-service` | Enterprise auth: MFA (TOTP + WebAuthn), SAML2, OIDC, JWT issuing. Currently: covered by `Central.Api/Auth/*` in .NET. |
| `admin-service` | Global-admin platform operations: tenant CRUD, setup wizard, license key issuing. Currently: `/api/global-admin/*` endpoints in `Central.Api`. |
| `gateway` | Reverse proxy + TLS termination + rate limiting + WebSocket/SignalR passthrough. Currently: client talks directly to `Central.Api` (no gateway). |
| `task-service` | High-throughput task management: 26 endpoints, SSE streams, batch ops, Redis pub/sub for change events. Currently: covered by `/api/tasks` + `/api/projects` in `Central.Api` + SignalR. |
| `storage-service` | Content-addressable storage: MinIO/S3 backend, BLAKE3 dedup, multipart upload, pre-signed URLs. Currently: basic `/api/files` endpoint; no CAS, no dedup. |
| `sync-service` | Offline-first sync: vector clocks, push/pull reconciliation, conflict resolution. Currently: `sync_configs` table + `Central.Api.Services.SyncEngine` handles integration sync (different scope — this is outbound ETL, not client-offline sync). |
| `audit-service` | M365 forensics, GDPR scoring, investigation workflows, evidence export. Currently: `audit_log` table + `Central.Module.Audit` panel for read access; no forensics workflows. |

**Recommendation:** don't try to "find" them — they were never committed — and don't build all 7 upfront. That's a multi-month programme. For the restructure plan, scope around reality:

- `services/` has exactly one service today (`tenant-provisioner`).
- Document the seven others as planned/aspirational in `docs/SERVER_ARCHITECTURE.md` (they're already mentioned there — we just need to mark them "not yet built" instead of implying they exist).
- If/when any of them gets built, it lands straight into `services/<name>/` — the folder shape is already right.

Phase 3 becomes simpler as a result: just the Angular move (`web-client/` → `apps/web/`) and a docs pass that clarifies which Rust services exist vs are planned.

## What success looks like

A new contributor clones the repo, opens it in their IDE, and:

- Sees `apps/`, `services/`, `libs/`, `modules/`, `infra/`, `docs/`, `tools/`.
- Knows `apps/desktop` is the WPF shell without looking inside.
- Knows where to put a new feature module (`modules/foo/`) or a new Rust service (`services/bar/`).
- Opens `Central.sln` at the root and sees a project tree that mirrors the folder tree.
- Reads `README.md` and has a working dev loop in under 10 minutes.

Without any of that requiring them to read this doc.
