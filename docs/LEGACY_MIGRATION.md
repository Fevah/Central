# Legacy `source/` Migration — Audit & Removal Plan

**Date:** 2026-04-17
**Status:** Audit complete. Ready to delete `source/` after executing the migration punch list below.
**Tree size:** 97 .csproj files, 1,650 .cs files across 3 top-level trees.

---

## Why this document exists

`source/` holds the legacy TotalLink/IntegrationServer codebase we treated as read-only reference while building Central. Many patterns have already been extracted (see [TOTALLINK_PATTERNS.md](TOTALLINK_PATTERNS.md)), but before we delete the folder we need a clear record of:

1. What's been migrated already.
2. What's worth migrating that we haven't done yet.
3. What can safely be discarded and why.

The goal is zero loss of useful IP — with the folder and its `.sln` reference gone from the working tree afterwards.

---

## Executive summary

| Tree | Size | Verdict |
|------|------|---------|
| `TIG.TotalLink.Client` | 20 projects / ~815 .cs / 13 WPF modules | 99 % superseded or domain-specific. **One valuable piece** (MonitoredUndo framework) worth porting. |
| `TIG.TotalLink.Server` | 49 projects (WCF / XPO / SQL Server) | 95 % superseded. A couple of patterns (sales order release, stock adjustment, sequence generation) are reference-only unless we add commerce/inventory modules later. |
| `TIG.IntegrationServer` | 28 projects (Topshelf / Autofac / Quartz) | Fully replaced by Central's sync engine. **Three small items** worth porting: SQL Server CDC ChangeTracker, CalculateConverter, AutoGenerateConverter. |

**Total migration work to avoid IP loss: ~1.5 weeks of engineering.** After that the whole `source/` tree can be deleted.

---

## Consolidated migration punch list (prioritised)

Work to do **before** deletion, ordered by ROI.

### Tier 1 — do these (~1 week combined)

| # | Item | Source | Effort | Value |
|---|------|--------|--------|-------|
| 1 | **Port MonitoredUndo framework** | `TIG.TotalLink.Client/Client/Core/TIG.TotalLink.Client.Undo/` | Medium (~800 LOC, 4-6 hrs after XPO→EF refactor) | High — Central has no undo today; enterprise-grade change safety across all detail dialogs. |
| 2 | **Add SQL Server CDC `ChangeTracker` + `IChangeDetectionStrategy` plug point to Central's SyncEngine** | `TIG.IntegrationServer/Plugin/ChangeTracker/SqlServerChangeTracker/` | 3-5 days | Medium-high — Central's hash-based detection is O(n); CDC is O(delta). Matters for >1M-row sync sources. Also provides a pattern for the PostgreSQL equivalent (logical replication / pglogical). |
| 3 | **Port `CalculateConverter`** (replace JScript.Eval with Roslyn scripting or Flee) | `TIG.IntegrationServer/Plugin/Converter/CalculateConverter/` | 1-2 days | Medium — closes a real gap in Central's `IFieldConverter` set (math expressions for computed fields at sync time). |
| 4 | **Port `AutoGenerateConverter`** (sequential + UUID modes) | `TIG.IntegrationServer/Plugin/Converter/AutoGenerateConverter/` | 1 day | Medium — needed when syncing to systems without shared identity; straight translation. |

### Tier 2 — reference only, no code migration

| # | Item | Source | Action |
|---|------|--------|--------|
| 5 | **Sales Order Release logic** (atomic release, partial delivery, bin allocation, `MaxReleaseAttempts=10` with `LockingException` retry) | `TIG.TotalLink.Server.MethodService.Sale/SaleMethodService.svc.cs` L68-200 | Don't port. Copy the method body + comments into `docs/REFERENCE_SALES_ORDER_RELEASE.md` for whoever eventually builds a Commerce/Fulfilment module in Central. |
| 6 | **Stock Adjustment with multi-entity impact** | `TIG.TotalLink.Shared.Facade.Inventory/InventoryFacade.cs` | Don't port. Only matters if Central adds an Inventory module. Save the pattern alongside item 5. |
| 7 | **Application-level Sequence Generation** (lock-safe counter with retry loop) | `TIG.TotalLink.Server.MethodService.Admin/AdminMethodService.svc.cs` L41-99 | Don't port — PostgreSQL native sequences (`NEXTVAL`) are better. Note the pattern as reference for any scenario needing app-level sequences (e.g. external-system codes). |

### Tier 3 — explicit "do not migrate"

Everything else. See the per-tree breakdown below for the full list and rationale.

---

## Tree 1: `TIG.TotalLink.Client` (WPF client)

### Already migrated / pattern borrowed

Documented in [TOTALLINK_PATTERNS.md](TOTALLINK_PATTERNS.md). Covers:
- Module registration (Autofac + `IModule` scan) → `Central.Desktop/Bootstrapper.cs`
- Dynamic ribbon with `CategoriesSource` binding → Central's ribbon builder (DB-backed, 3-layer override)
- Grid CRUD via `ListViewModelBase<T>` with `[WidgetCommand]` → `Central.Core/Widgets/ListViewModelBase.cs`
- Detail dialogs via `IDetailDialogService.ShowDialog(EditMode, entity)` → `Central.Desktop/Services/DetailDialogService.cs`
- Entity-ViewModel sync via `EntityViewModelBase<T>` + `[SyncFromDataObject]` → pattern adopted (manual mapping in Central)
- `AppContextViewModel` singleton → Central uses `UserSession` static (less observable but sufficient)
- `FacadeBase` dual Data+Method services → Central's REST + SignalR split
- `StartupWorkerManager` → Central's `StartupWorkerBase` + splash pipeline

### Worth migrating — Tier 1 item #1

- **`Client/Core/TIG.TotalLink.Client.Undo/`** — MonitoredUndo Framework.
  Production undo/redo: change stacks, property-change tracking, collection add/remove tracking, data-object create/delete tracking, ribbon-bound undo/redo stacks with previews.
  **Why Central needs it:** the engine docs list undo as "Phase 3 future" and nothing has shipped. All edits are direct DB saves — one fat-fingered change and it's committed.
  **Porting work:** 800 LOC core + wiring. Requires replacing XPO references with our Npgsql/EF model and pulling the MonitoredUndo NuGet package. Wire into `DetailDialogService` (`BeginChangeSet`) and ribbon (Undo/Redo `BarSubItem`s backed by the undo service's stacks).

### Safe to discard

| Item | Reason |
|------|--------|
| All 13 WPF modules (`Admin`, `Sale`, `Inventory`, `Purchasing`, `Crm`, `Repository`, `ActiveDirectory`, `Authentication`, `Global`, `Integration`, `Workflow`, `Task`, `Test`) | Domain-specific to TotalLink's Navision e-commerce business, or superseded by Central's own richer modules (CRM 29-phase, Tasks 16-panel, Admin, Elsa Workflows). |
| `TypeDescriptor` / `AliasTypeDescriptor` | XPO-specific reflection helpers. Central uses Npgsql directly. |
| `GridEditStrategyBehavior`, `ConditionMetadataBuilder`, `EditorConditions` | Over-engineered dynamic editor framework. Central's ribbon + detail dialogs are simpler and sufficient. |
| `MainWindow` / `Backstage` / `WidgetCustomizer` / `MessageMonitor` / `MessageLog` dialogs | Superseded by `Central.Desktop/MainWindow.xaml` + backstage + admin panels. |
| `AutofacViewLocator`, `BootstrapperBase`, `StartupWorkerManager`, `WindowStateViewModel` | Patterns already borrowed; code itself tied to XPO. |
| `AsyncCommandEx` / `AsyncCommandExOfT` | Equivalent `RelayCommand`s exist in Central. |
| `TIG.TotalLink.Client.IisAdmin` | Named-pipe IIS ops. Irrelevant — Central doesn't run on IIS. |

---

## Tree 2: `TIG.TotalLink.Server` (WCF / XPO / SQL Server)

### Already superseded by Central's modern stack

| Legacy | Replaced by |
|--------|-------------|
| WCF hosting (`DummyConsoleHost`, service behaviors, message inspectors) | `Central.Api` (ASP.NET Core 10 Minimal API in a container) + Rust services in `SecureAPP/services/` |
| `DataServiceBase` / `MethodServiceBase` / `FacadeBase<TData, TMethod>` | REST endpoints in `Central.Api/Endpoints/` + Rust business services; pattern of Data/Method separation kept as REST + service layer |
| XPO ORM + `UnitOfWork` + `CachedDataStore` | Direct Npgsql in `Central.Data/` |
| SQL Server DDL (`CreateBlobedDatabase.sql`, `CreateStreamedDatabase.sql`, `CreateFileTable.sql`) | 80 PostgreSQL migrations in `db/migrations/` |
| FormsAuth token flow (`AuthenticationFacade`) | Argon2id + JWT + MFA (TOTP) in `Central.Api.Auth` + Rust `auth-service` |
| `AdminFacade.NextSequenceNumber()` (lock-and-retry counter) | PostgreSQL native `NEXTVAL()` on sequences |
| `GlobalFacade` (schema updates, data init) | Migration runner + `StartupHealthCheck` |
| `TaskFacade` | `Central.Module.Tasks` (16-panel Hansoft/P4 Plan clone) |
| `WorkflowActivity` proprietary engine | Elsa 3.5.3 in `Central.Workflows` |
| `ActiveDirectoryUser` sync table | Identity providers + claim mappings (`identity_providers`, `auth_events`) |
| 13 `Shared.DataModel.*` XPO model assemblies | PostgreSQL schema across 80 migrations |

### Worth migrating — reference-only (Tier 2)

| Item | Why preserve as reference |
|------|---------------------------|
| `SaleMethodService.ReleaseSalesOrder()` / `.ReleaseDelivery()` | Interesting atomic-release pattern with partial delivery, bin allocation, and `MaxReleaseAttempts=10` with `LockingException` retry. Useful if/when Central gains a Fulfilment module on top of `/api/crm/orders`. |
| `InventoryFacade.AddStockAdjustment()` | Multi-entity stock-adjustment propagation (stock ↔ bin ↔ warehouse). Only relevant if we add an Inventory module. |
| Application-level Sequence Generation pattern | Fallback pattern when PostgreSQL sequences aren't available (e.g. generating codes to push into Navision-style external systems). |

Capture these as `docs/REFERENCE_*.md` snippets before deletion.

### Safe to discard

All other facades, data services, XPO models, WCF infrastructure, EnterpriseLibrary integration, SQL Server DDL, Topshelf host, Navision/ECommerce domain models.

---

## Tree 3: `TIG.IntegrationServer` (ETL / sync engine monolith)

### Already replaced

| Legacy | Replaced by |
|--------|-------------|
| `SyncEngine.Core` + `.Custom` (lifecycle + task dispatcher) | `Central.Core/Integration/SyncEngine.cs` (`ExecuteSyncAsync` with CancellationToken) |
| `Facade.Integration` (WCF/OData) | `Central.Api/Endpoints/SyncEndpoints.cs` (REST) + `sync_configs`, `sync_entity_maps`, `sync_field_maps` tables |
| Reflection-based plugin discovery (`PluginAttribute`, `IAgentPlugin`, `IConverterPlugin`, `IFieldMapperPlugin`, `IChangeTrackerPlugin`) | Explicit registration: `SyncEngine.RegisterAgent(agent)` / `RegisterConverter(converter)` with a `ConcurrentDictionary` by code. Simpler and safer. |
| `DictionaryEntityMapper` | Field mapping lives in `sync_field_maps` + inline in `ExecuteSyncAsync` |
| Autofac DI | `Microsoft.Extensions.DependencyInjection` |
| Topshelf Windows service host | In-process (`JobSchedulerService` inside the API host) |
| Enterprise Library logging | Serilog structured JSON |
| `Security.Cryptography.IHashMaster` | `System.Security.Cryptography` + hash tracked in `sync_entity_maps.last_hash` |
| Quartz.NET (`TimeoutManager.Quartz`) | `job_schedules` table + `JobSchedulerService` polling loop |
| All three bundled agents — `ECommerceAgent`, `NavisionAgent`, `TotalLinkAgent` | Not relevant. Central uses its own agents (`CsvImportAgent`, `RestApiAgent`) and anyone adding a new integration implements `IIntegrationAgent`. |

### Converter gap analysis (the Tier 1 items)

| Legacy | Central equivalent | Gap? |
|--------|--------------------|------|
| `AutoGenerateConverter` | none | **Yes** — port. |
| `CalculateConverter` (uses deprecated `Microsoft.JScript.Eval`) | partial — Central has basic `ExpressionConverter` with upper/lower/trim/bool, no math | **Yes** — port with safe expression evaluator (Roslyn scripting or Flee). |
| `CombineConverter` | `CombineConverter` — identical | No. |
| `ConstantConverter` | `ConstantConverter` — identical | No. |
| `SplitConverter` | `SplitConverter` — identical | No. |
| `EntityConverter` (related-entity lookup) | `LookupConverter` — covers the main use | Partial. Acceptable. |
| `PropertyConverter` (reflection property access) | covered by `DirectConverter` + field mapping | No. |

Central also has a converter the legacy engine lacks: `DateFormatConverter`.

### Worth migrating — Tier 1 items #2, #3, #4

1. **`SqlServerChangeTracker` + `IChangeDetectionStrategy` plug point** — see punch list item #2. Keep Central's hash-based strategy as the default; add SQL Server CDC as an opt-in strategy for large-table scenarios. This also establishes the extension point for a future pg_logical-replication strategy.
2. **`CalculateConverter`** — punch list item #3.
3. **`AutoGenerateConverter`** — punch list item #4.

### Safe to discard

Everything not listed under "worth migrating" above. Specifically: all agents, `DictionaryEntityMapper`, WCF facades, Topshelf host, EnterpriseLib logging, Quartz scheduler, Autofac DI bootstrap, `Security.Cryptography`, most of `Plugin/Core` (replaced by explicit registration), `SyncEngine/Custom` (merged into `ExecuteSyncAsync`).

---

## Execution plan — how to delete `source/`

1. **Capture reference snippets (Tier 2 items).** Create `docs/REFERENCE_SALES_ORDER_RELEASE.md`, `docs/REFERENCE_STOCK_ADJUSTMENT.md`, and `docs/REFERENCE_SEQUENCE_GENERATION.md` with the relevant method bodies + comments. Commit.
2. **Port Tier 1 items.** Four PRs, each buildable and testable in isolation:
   1. MonitoredUndo port (new project `Central.Core/Undo/` + DetailDialogService + ribbon wiring + unit tests).
   2. `IChangeDetectionStrategy` interface + `SqlServerCdcChangeDetector` impl + tests. Default stays hash-based.
   3. `CalculateConverter` (Roslyn-scripting-based) + tests.
   4. `AutoGenerateConverter` (sequential + UUID modes) + tests.
3. **Search the wider repo for references** to the `source/` tree (solution files, build scripts, CI workflows, docs) and remove them. Known reference: [CLAUDE.md](../CLAUDE.md) "TotalLink Source Reference" section — replace with a short pointer to this document.
4. **Delete `source/`.**
5. **Commit** with message `Remove legacy source/ tree (see docs/LEGACY_MIGRATION.md for audit + migrated items)`.

---

## Quick-reference: "I want to build X — does the legacy engine have it?"

- **Undo/redo** — yes, port it (Tier 1 #1).
- **Better change detection for large sync sources** — yes, port CDC (Tier 1 #2).
- **Math expressions in sync field mapping** — yes, port CalculateConverter (Tier 1 #3).
- **ID generation in sync without shared identity** — yes, port AutoGenerateConverter (Tier 1 #4).
- **Inventory / warehouse / bin-location management** — no, not built. Reference logic captured if you need to build it.
- **Sales-order fulfilment / partial delivery** — no, not built. Reference logic captured.
- **Navision / ecommerce agents** — intentionally discarded. Build a new `IIntegrationAgent` if needed.
- **Workflow designer UI** — no, and we don't want to build one. Use Elsa (`Central.Workflows`) or add activities to the existing engine.
- **Cron-syntax scheduling (Quartz)** — not today. `job_schedules.interval_minutes` is simpler. Add `schedule_cron` column + cron parser if we ever need it (small job).
