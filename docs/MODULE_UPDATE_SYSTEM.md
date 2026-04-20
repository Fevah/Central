# Module Update System — Multi-User Hot-Swap Design

Last updated: 2026-04-20
Status: **design only — not yet implemented**. Phase 1 is the next buildout.

## Goals

1. Customers keep using the app while we publish module updates.
2. Developers iterate on a single module without a full-client restart.
3. Most changes require **neither** a full client release nor a "restart, please" banner — they hot-swap silently.
4. When a change truly requires a restart (engine-contract break), classification is explicit + the user gets honest warning, not a surprise crash.
5. Remote clients pull updates from the current environment's API gateway; they don't need manual redeploy.

## Release classification (the contract that drives everything)

Every module version advertises a `changeKind` in its manifest. The server picks it; clients react accordingly.

| changeKind | User experience | What it means | Examples |
|------------|-----------------|---------------|----------|
| **`HotSwap`** | Silent. No banner. The next time a panel is opened, or SignalR pushes a reload, it re-renders with the new code. | Pure module-internal change. Engine contracts unchanged. No model schema migration required. | New ribbon button, new panel, new validation rule, UI polish, bug fix inside a module, new Rust endpoint the client already tolerates. |
| **`SoftReload`** | Banner: "Module X updated — reload when convenient." User clicks → module unloads + reloads in-place. In-flight dialogs preserved. | Module change that needs panel state reset but not a full restart. | New required field in a dialog, model shape change with a view-layer migration, permission code addition. |
| **`FullRestart`** | Banner: "Critical update scheduled — restarting in 5 minutes. Finish your work." One-time opt-out for urgent cases. Clicked "now" or timer expires → full client restart with full installer. | Engine contract break. Can't be hot-swapped safely. | `IModule` interface change, SignalR message schema break, `PanelMessageBus` message rename/remove, incompatible DB model requiring schema migration with downtime. |

**The unwritten rule:** if you catch yourself typing `FullRestart` in a manifest, stop and ask whether the change can be reshaped to `HotSwap` or `SoftReload` instead (add new method instead of changing signature, add new message type instead of renaming the old one, version the permission code instead of renaming).

## Coding discipline — how to stay HotSwap-eligible by default

1. **Engine interfaces are append-only.** `IModule`, `IModuleRibbon`, `IModulePanels`, `IPanelBuilder`, `IRibbonBuilder`: never rename, never remove, never change a method signature. Add new methods via a successor interface (`IModuleV2`) and check with `is` / `as`. Existing modules keep working.
2. **PanelMessageBus messages are additive.** Add new fields as nullable properties. Never rename fields, never drop fields, never change field types. New event type = new class, not a renamed old one.
3. **Permission codes are add-only.** Renaming `crm:read` to `crm:view` is a breaking change; adding `crm:deep-analytics` is not. If a rename is unavoidable, dual-ship both codes for one major release cycle.
4. **DB model shape changes ride migrations.** Any change that requires a schema migration is `SoftReload` at best, usually `FullRestart`. The fix: make the change backwards-compatible via views or nullable columns so the old module still reads OK during the rollover.
5. **Use `Central.Persistence.DbRepository` from modules, not raw SQL.** The repo layer absorbs schema drift; direct SQL in a module freezes the schema-shape it expects.
6. **Version every module artifact.** Each `Central.Module.*.dll` carries an `AssemblyInformationalVersion` + an `EngineContractVersion` attribute. CI refuses to publish if the contract version regresses or the module version doesn't bump.

## Phased buildout

### Phase 1 — Module identity + server-side catalog (no hot-swap yet)

**Goal:** every module declares a version; server knows what's published; client shows "N updates available" banner read-only.

- Add `Version` property to `IModule` (default implementation returns AssemblyInformationalVersion, existing modules inherit for free).
- Add `EngineContractVersion` property to `IModule` (default: 1). Bumped only when the shared engine interface breaks.
- New DB tables (migration 097 or next): `module_catalog` (code, displayName, owner, currentVersion) + `module_versions` (moduleCode, version, changeKind, minEngineContract, blobUrl, sha256, publishedAt, isYanked).
- New endpoint `GET /api/modules/catalog` — returns current module list with published versions per tenant (gated by license + version-policy).
- Desktop on boot: compares loaded module versions to catalog. Shows a read-only banner: `"3 module updates available — Networking 2.4.1, CRM 1.8.0, Audit 1.2.3"`. No install button yet.
- **What this does NOT do:** no download, no hot-swap. Phase 1 is visibility only — prove the plumbing is correct before cutting live traffic over to it.

### Phase 2 — Server-side module distribution

**Goal:** CI can publish module DLLs; clients can download individual modules.

- `POST /api/modules/publish` — CI uploads `{moduleCode, version, changeKind, minEngineContract, dll, pdb}`. Signed by publisher key. Writes to `module_versions` + uploads DLL to MinIO (`central-modules/{code}/{version}/module.dll`).
- `GET /api/modules/{code}/{version}/manifest` — lightweight metadata + sha256.
- `GET /api/modules/{code}/{version}/dll` — the DLL bytes (streamed).
- Client-side: `ModuleCatalogClient` in `libs/api-client` — thin wrapper over the catalog + download endpoints.
- GitHub Actions job: on tag `module/networking/2.4.1`, build + sign + publish. Tag triggers exactly one module publish; failures don't block other modules.

### Phase 3 — AssemblyLoadContext hot-swap kernel

**Goal:** a single module can be unloaded + reloaded in-process without a full app restart.

- Rewrite `libs/engine/Modules/ModuleLoader.cs` to give each module its own `AssemblyLoadContext` (collectible). Today all plugins share the default context — no unload possible.
- New `ModuleHost` service: owns the lifecycle of one module (load / register ribbon+panels+permissions / close its panels / deregister / unload / garbage-collect). Keeps a weak reference to verify unload completed.
- WPF caveats — document honestly in the service:
  - **ResourceDictionary cache:** XAML resources from an unloaded context linger until their last consumer is gone. Mitigation: modules must define resources *inside* their UserControls, not merged into `App.Resources`. Already enforced by `feedback_wpf_resources.md`.
  - **Event subscription GC roots:** `EventManager.RegisterClassHandler` rooted in an unloaded module prevents context collection. Mitigation: lint rule + audit test (`AllModulesDoNotRegisterClassHandlers`).
  - **XAML pack URIs:** `pack://application:,,,/Central.Module.CRM;component/...` bind to the assembly at load time. After unload, stale URIs crash. Mitigation: host closes all panels + re-resolves panel types from the new assembly on reload.
- Hot-swap flow: `ModuleHost.Reload()` → publish `ModuleReloadingMessage(code)` → panels for this module unsubscribe + close themselves → unload context → download new DLL → load into fresh context → re-register → publish `ModuleReloadedMessage(code)` → MainWindow reopens the panels that were previously open.

### Phase 4 — SignalR notification + auto-reload policy

**Goal:** publishing a new module version fans out to live clients; client applies the right policy per `changeKind`.

- New SignalR message on `NotificationHub`: `ModuleUpdated { code, fromVersion, toVersion, changeKind }`.
- Server emits on any successful `POST /api/modules/publish` — scoped to the tenant group(s) that have the module licensed + are on a channel that includes this version (prod, stable, beta).
- Client-side handlers:
  - `HotSwap` → pull + `ModuleHost.Reload()` silently. Toast: "Networking updated to 2.4.1."
  - `SoftReload` → `NotificationCenter` banner with "Reload now" button; on click, `ModuleHost.Reload()`.
  - `FullRestart` → scheduled-restart banner with 5-min countdown + opt-out. On fire, call existing `UpdateManager.RestartApplication()`.
- Tenant-level opt-out: `tenant_version_policy.auto_hotswap` flag. Off = banner for every change regardless of `changeKind`.

### Phase 5 — Developer hot-reload in dev mode

**Goal:** save a .cs file in `modules/crm/`, the running Central.exe picks up the new DLL within seconds.

- `FileSystemWatcher` on each `modules/*/bin/Debug/net10.0-windows/Central.Module.*.dll`.
- Debounce 500ms (.NET emits multiple events on a single rebuild).
- Dev-only code path — `#if DEBUG` — so prod clients don't watch filesystem.
- Same `ModuleHost.Reload()` path as Phase 3; no server round-trip, just local file.
- Requires a build setup where `dotnet build modules/crm/Central.Module.CRM.csproj` re-emits the DLL to the path the watcher monitors. Usually the default.

### Phase 6 — Version-gate CI discipline

**Goal:** CI refuses to publish a module that breaks the engine contract without bumping `EngineContractVersion`.

- CI job `central-engine-contract-check`: builds + reflects every public type in `Central.Engine`. Compares to the previous tag's surface. Diff checker: new method = OK; removed method = fail; signature change = fail.
- Same check on `libs/collaboration` (PanelMessageBus messages), `libs/api-client` (SignalR messages), `libs/engine/Auth` (permission codes — tracked in a generated `.txt` shipped with the build; missing code is a fail).
- On contract change: CI bumps `EngineContractVersion`, requires a matching PR to bump every module's `MinEngineContract` + version. Merge-queue enforced.
- The effect: accidental `FullRestart`-class changes never ship — CI catches them. Intentional ones go through a single "contract v2" PR batch.

## How this reshapes future work

From here on, every feature slice asks "can this stay `HotSwap`?" before it asks "what file do I edit?":

- **Validation rule** — Rust side change, no client touch. `HotSwap`.
- **New ribbon button** — module-internal. `HotSwap`. Just publish a new module version.
- **New panel** — module-internal. `HotSwap`.
- **New API endpoint** — server side only. Client tolerates via typed client's optional parameters. `HotSwap`.
- **New permission code** — add-only, `HotSwap`. Rename = `FullRestart`, don't rename.
- **Changing `IModule.cs`** — stop. Add an `IModuleV2` instead. Or dual-ship.
- **Schema migration** — `SoftReload` if backwards-compat views cover the old shape; `FullRestart` if not. Default to the former.

## Failure modes + rollback

- **Hot-swap crashes on load:** `ModuleHost` catches; logs; leaves old module running; emits `ModuleLoadFailed` SignalR event. User sees "Update failed — running previous version. Details in logs." Support can roll back server-side with `PUT /api/modules/{code}/{version}/yank`.
- **AssemblyLoadContext fails to collect:** logged with the rooting chain. Module swap falls back to `SoftReload` next time. If it repeats, reclassify the change as `FullRestart`.
- **Tenant policy disables auto-update:** client shows banner instead of silent swap even for `HotSwap`. Respected at all phases.

## Out of scope (intentionally)

- Signed package verification beyond SHA-256 + publisher key. Cryptographic signing chain is a Phase 7 concern.
- Canary rollouts / percentage rollout. Phase 7.
- Offline-first module caching. Phase 7.
- Telemetry that correlates crash rates to module versions. Phase 7.
