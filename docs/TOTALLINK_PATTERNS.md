# TotalLink Design Patterns for Central

Analysis of TIG.TotalLink.Client — a mature 20-project DevExpress WPF application with
dynamic ribbon, modular architecture, and enterprise RBAC. Extracting patterns to evolve
Central from a single-app into a modular platform.

---

## TotalLink Architecture Overview

| Layer | Projects | Pattern |
|-------|----------|---------|
| Host | 3 (Client, IisAdmin, ServerManager) | Executable shells |
| Core | 4 (Core, Editor, Undo, IisAdmin) | Framework + base classes |
| Modules | 13 (Admin, CRM, Inventory, Sale, etc.) | Feature plugins |
| Data | XPO ORM + Repository Facade | Abstracted persistence |

**Key:** 815 C# files, 126 XAML files, Autofac DI, DevExpress 15.1, MVVM

---

## Patterns to Extract (Priority Order)

### 1. Module System (Autofac Discovery)

**Source:** `Client/Core/TIG.TotalLink.Client.Editor/StartupWorker/InitModulesStartupWorker.cs`

**How it works:**
- Scans all loaded assemblies for types implementing `Autofac.Module`
- Each module registers its services, views, viewmodels, and widgets
- Zero configuration — drop a DLL, it auto-discovers

```csharp
foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    foreach (var type in assembly.GetTypes()
        .Where(t => typeof(IModule).IsAssignableFrom(t)))
    {
        var module = (IModule)Activator.CreateInstance(type);
        builder.RegisterModule(module);
    }
```

**Central Application:**
- Each feature becomes a module: `Central.Module.IPAM`, `Central.Module.Switches`,
  `Central.Module.Links`, `Central.Module.Admin`
- Modules register their own ribbon items, panels, and DB repositories
- New features (e.g. OSPF, MPLS, Monitoring) are added as new module DLLs

**Effort:** High — requires restructuring into multi-project solution
**ROI:** Enables plugin architecture, parallel development, clean boundaries

---

### 2. Dynamic Ribbon from Database

**Source:** `Client/Module/TIG.TotalLink.Client.Module.Admin/View/DocumentManagerView.xaml.cs`

**How it works:**
- Ribbon is an empty container in XAML
- `RibbonControl.CategoriesSourceProperty` bound to `MainViewModel.RibbonCategories`
- `DataTemplateSelector` renders categories → pages → groups → items
- Ribbon items stored in DB tables (`ribbon_category`, `ribbon_page`, `ribbon_group`, `ribbon_item`)
- Each item has `CommandType` + `CommandParameter` that generates an `ICommand` at runtime
- Admin can add/edit/remove ribbon items without code changes

**Key code:**
```csharp
RibbonControl.SetBinding(RibbonControl.CategoriesSourceProperty, "RibbonCategories");
newRibbonControl.CategoryTemplateSelector =
    newRibbonControl.TryFindResource("RibbonCategoryTemplateSelector") as DataTemplateSelector;
```

**Central Application:**
- Admin panel to manage ribbon tabs/buttons per role
- Per-site ribbon customization (different sites see different buttons)
- Dynamic commands: "Sync BGP", "Ping All", "Export" configurable per role

**Effort:** Medium — DB tables + template selectors + binding
**ROI:** Admin-configurable UI without recompilation

---

### 3. AppContextViewModel Singleton

**Source:** `Client/Core/TIG.TotalLink.Client.Core/AppContext/AppContextViewModel.cs`

**How it works:**
```csharp
public class AppContextViewModel : ViewModelBase
{
    private static AppContextViewModel _instance;
    public static AppContextViewModel Instance => _instance ??= new();

    public AuthState AuthState { get; set; }     // NotAuthenticated, Windows, TotalLink, Offline
    public UserInfo UserInfo { get; set; }        // Current user data
    public string ThemeName { get; set; }         // UI theme
    public WindowStateViewModel WindowState { get; set; }  // Window bounds
}
```

**Central Application:**
Replace the static `UserSession` class with an observable `AppContext` singleton:
- `CurrentUser`, `Permissions`, `AllowedSites` (already exists)
- Add: `SelectedDevice`, `SelectedSwitch`, `ActiveModule`, `ConnectionState`
- Observable — UI binds directly, updates automatically
- Testable — can be mocked for unit tests

**Effort:** Low — rename + extend existing `UserSession`
**ROI:** Cleaner state management, testable, bindable

---

### 4. Entity-ViewModel Auto-Sync

**Source:** `Client/Core/TIG.TotalLink.Client.Core/ViewModel/EntityViewModelBaseOfT.cs`

**How it works:**
```csharp
public abstract class EntityViewModelBase<TDataObject> : EntityViewModelBase
{
    public TDataObject DataObject { get; protected set; }

    // When DataObject changes:
    // 1. Remove old event handlers
    // 2. Add new event handlers for Changed events
    // 3. For collections with [SyncFromDataObject], sync bidirectionally
}
```

**Central Application:**
- `DeviceViewModel<DeviceRecord>` auto-binds properties + collections
- Grid row changes automatically propagate to ViewModel
- ViewModel changes (from SSH sync) automatically update grid
- Eliminates manual `foreach (var d in devices) Devices.Add(d)` loops

**Effort:** Medium — create base class, refactor models to use it
**ROI:** Eliminates ~200 lines of collection sync boilerplate

---

### 5. Panel/Document/Widget Composition

**Source:** `Client/Module/TIG.TotalLink.Client.Module.Admin/ViewModel/Document/`

**How it works:**
- `DocumentViewModel` contains a collection of `PanelGroupViewModel`
- Each `PanelGroupViewModel` contains `PanelViewModel` instances
- Each `PanelViewModel` hosts a `WidgetViewModel` (the actual content)
- Widgets are registered by modules via `IWidgetProvider`
- Documents are opened via `ShowDocumentCommand` with a document ID

```
Document (e.g. "Switch Detail")
├── PanelGroup (horizontal split)
│   ├── Panel: "Interfaces" (Widget: InterfaceGridWidget)
│   └── Panel: "BGP Neighbors" (Widget: BgpNeighborWidget)
└── PanelGroup (vertical split)
    ├── Panel: "Running Config" (Widget: ConfigViewerWidget)
    └── Panel: "Audit Log" (Widget: AuditLogWidget)
```

**Central Application:**
- Switch detail view becomes a document with composable panels
- Each panel is a widget: Interfaces, BGP, Config, VLAN, Ping Status
- Users can rearrange panels within the document
- New switch types get different widget compositions

**Effort:** High — requires widget framework
**ROI:** Flexible, user-customizable detail views

---

### 6. Repository + Facade Service Layer

**Source:** `Client/Module/TIG.TotalLink.Client.Module.Repository/`

**How it works:**
- `IRepositoryFacade` interface abstracts all data operations
- Modules depend on `IRepositoryFacade`, not concrete DB implementation
- Registered as `SingleInstance` in Autofac (shared session)
- Can swap implementations (test mock, different DB, REST API)

**Central Application:**
- `IDeviceRepository`, `ILinkRepository`, `ISwitchRepository` interfaces
- Current `DbRepository` partial classes implement these interfaces
- Test projects can mock the interfaces
- Future: REST API backend instead of direct PostgreSQL

**Effort:** Low-Medium — extract interfaces from existing partial classes
**ROI:** Testable, swappable data layer

---

### 7. Undo/Redo (MonitoredUndo Framework)

**Source:** `Client/Core/TIG.TotalLink.Client.Undo/` + MUF NuGet package

**How it works:**
- `UndoService.Current[root].Undo()` / `.Redo()`
- Undo/Redo stacks exposed as `ICollectionView` on ribbon
- Property changes automatically tracked when wrapped in `UndoService.Current.BeginChangeSet()`

**Central Application:**
- Undo IPAM edits (device name, IP, status changes)
- Undo link changes (port assignment, VLAN changes)
- Ribbon shows undo/redo history dropdown

**Effort:** Medium — integrate MUF NuGet, wrap editable properties
**ROI:** Enterprise undo/redo for accidental changes

---

## Patterns to AVOID Extracting

| Pattern | Reason |
|---------|--------|
| XPO ORM | Too heavy for Central's PostgreSQL stack — keep Npgsql |
| Full dynamic ribbon from DB | Overkill now — Central's static ribbon + visibility bindings is appropriate |
| Every-module-registers-ribbon | Too flexible for current scope — use when module count > 5 |
| AutoMapper profiles | Central maps are simple enough for manual mapping |

---

## Recommended Migration Path

### Phase 1: Quick Wins (Current Sprint)
- [x] Extract services from MainWindow.xaml.cs (LinkEditorHelper, SwitchSyncService)
- [x] DbRepository partial class split
- [x] PreferenceKeys constants
- [x] INetworkLink interface
- [ ] Replace `UserSession` with observable `AppContext` singleton
- [ ] Extract `IDeviceRepository` / `ILinkRepository` interfaces

### Phase 2: Module Preparation (Next Sprint)
- [ ] Move IPAM logic into `Central.Module.IPAM` project
- [ ] Move Link logic into `Central.Module.Links` project
- [ ] Move Admin logic into `Central.Module.Admin` project
- [ ] Add Autofac DI container
- [ ] Each module registers its services, panels, and ribbon items

### Phase 3: Dynamic UI (Future)
- [ ] Ribbon items from database
- [ ] Widget/panel composition framework
- [ ] Document-based navigation (switch detail as document)
- [ ] Per-role ribbon customization
- [ ] Undo/Redo via MonitoredUndo

### Phase 4: Platform (Future)
- [ ] Plugin DLL discovery (drop new module DLL → auto-load)
- [ ] REST API backend option
- [ ] Multi-user collaboration (change notifications)
- [ ] Offline mode with local SQLite cache + sync

---

---

## SecureAPP RBAC Model (Rust Backend)

The SecureAPP at `c:\Development\Secure\SecureAPP` is a production Rust IAM platform.
Its permission model is the gold standard for Central's RBAC evolution.

### Permission Architecture

```
Users → user_roles → Roles → role_permissions → Permissions
                                                    ↓
                                              code: "module:action"
                                              e.g. "users:read", "roles:write"
```

**Permission code format:** `{module}:{action}` — maps directly to Central modules:

| Central Module | Permissions |
|---------------------|-------------|
| `devices` | `devices:read`, `devices:write`, `devices:delete`, `devices:export` |
| `switches` | `switches:read`, `switches:write`, `switches:sync`, `switches:deploy` |
| `links` | `links:read`, `links:write`, `links:delete` |
| `bgp` | `bgp:read`, `bgp:sync` |
| `vlans` | `vlans:read`, `vlans:write` |
| `admin` | `admin:users`, `admin:roles`, `admin:settings`, `admin:audit` |

### Role Hierarchy (Priority System)

| Role | Priority | Description |
|------|----------|-------------|
| `super_admin` | 1000 | Full access, all sites |
| `site_admin` | 100 | Full access within assigned sites |
| `operator` | 50 | Read + write + sync, no delete |
| `viewer` | 10 | Read-only (default for new users) |
| `guest` | 1 | API read only |

Higher priority = more privileged. Admins bypass permission checks.

### Key Patterns to Adopt

1. **`module:action` permission codes** — replace Central's boolean `CanView/CanEdit/CanDelete`
   with string-based permission codes for fine-grained control

2. **AuthContext singleton** — JWT-style session with `has_permission("devices:write")`
   check. Maps to Central's `UserSession.CanEdit("devices")`

3. **Auto-role on group membership** — Groups have `auto_role_ids[]`. When user joins
   a building group, they get that building's role automatically

4. **Row-Level Security** — PostgreSQL RLS policies enforce site isolation at DB level.
   Currently Central does this in SQL WHERE clauses — RLS is more secure

5. **Soft delete** — `is_deleted` + `deleted_at` instead of hard DELETE.
   Enables audit trail and undo

6. **Audit logging** — Append-only table, never UPDATE/DELETE.
   Central's `switch_audit_log` is a start; extend to all entities

### Migration Path for Central RBAC

**Current state:** Boolean permissions per module (CanView/CanEdit/CanDelete)

**Target state:**
```sql
-- Replace role_permissions table
CREATE TABLE permissions (
    id SERIAL PRIMARY KEY,
    code VARCHAR(100) UNIQUE NOT NULL,  -- 'devices:read', 'switches:sync'
    name VARCHAR(255),
    category VARCHAR(100),              -- 'devices', 'switches', 'admin'
    is_system BOOLEAN DEFAULT TRUE
);

CREATE TABLE role_permission_grants (
    role_id INTEGER REFERENCES roles(id),
    permission_id INTEGER REFERENCES permissions(id),
    PRIMARY KEY (role_id, permission_id)
);
```

**Check pattern:**
```csharp
// Before (current):
if (UserSession.CanEdit("devices")) { ... }

// After (module:action):
if (UserSession.HasPermission("devices:write")) { ... }
if (UserSession.HasPermission("switches:sync")) { ... }
if (UserSession.HasPermission("bgp:sync")) { ... }
```

---

## Key Source Files for Reference

### TotalLink WPF Client
| File | Pattern |
|------|---------|
| `Client/Core/.../AppContext/AppContextViewModel.cs` | App state singleton |
| `Client/Core/.../ViewModel/EntityViewModelBaseOfT.cs` | Entity-ViewModel auto-sync |
| `Client/Core/.../StartupWorker/InitModulesStartupWorker.cs` | Module discovery |
| `Client/Host/.../ViewModel/MainViewModel.cs` | Ribbon + document orchestration |
| `Client/Module/.../Admin/AdminModule.cs` | Module registration pattern |
| `Client/Module/.../Admin/View/DocumentManagerView.xaml.cs` | Dynamic ribbon binding |
| `Client/Module/.../Admin/ViewModel/Document/PanelViewModel.cs` | Dockable panel model |
| `Client/Module/.../Admin/ViewModel/Ribbon/Core/RibbonItemViewModelBase.cs` | Command generation |
| `Client/Module/.../Repository/RepositoryModule.cs` | Data layer registration |

### TotalLink Server (WCF Services)
| File | Pattern |
|------|---------|
| `Server/Core/.../DataServiceBase.cs` | XPO data service base with cache |
| `Shared/Core/.../FacadeBase.cs` | Client-side facade — dual Data+Method connection |
| `Shared/Core/.../DataFacade.cs` | Cache invalidation via NotifyDirtyTables |
| `Shared/Core/.../MethodFacade.cs` | Method service proxy with auth token |
| `Server/Core/.../AuthenticationServiceMessageInspector.cs` | Token validation + user lookup |
| `Server/MethodService/.../AdminMethodService.cs` | Sequence generation with locking + retry |
| `Server/MethodService/.../RepositoryMethodService.cs` | Chunked file upload/download |
| `Server/MethodService/.../GlobalMethodService.cs` | DB schema management, import/export |
| `Client/Host/.../ServerManager/Bootstrapper.cs` | Server management WPF app |
| `Client/Core/.../IisAdmin/IisAdminProvider.cs` | Elevated IIS management via named pipes |

### SecureAPP Rust Backend (RBAC Reference)
| File | Pattern |
|------|---------|
| `services/auth-service/src/db/models/role.rs` | Role + permission models |
| `services/auth-service/src/db/models/user.rs` | User model with MFA + risk |
| `services/auth-service/src/api/middleware/auth.rs` | AuthContext + has_permission() |
| `services/auth-service/src/services/auth_service.rs` | Login flow + token generation |
| `migrations/auth-service/V004__create_roles_permissions.sql` | Permission seeding |
| `migrations/auth-service/V006__create_rls_policies.sql` | Row-Level Security |
| `migrations/auth-service/V015__create_teams.sql` | Teams + auto-role assignment |
| `migrations/auth-service/V016__create_groups.sql` | Groups + permission grouping |
