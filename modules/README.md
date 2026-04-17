# modules/

WPF feature modules. Each registers a ribbon tab plus a set of docked panels into `apps/desktop`. Deliberately separate from `libs/` because a module has a narrower contract than a general-purpose library.

## What's here

| Folder | Assembly | Ribbon tab / purpose |
|--------|----------|----------------------|
### Always-on core

| Folder | Assembly | Contents |
|--------|----------|----------|
| [global/](global/) | `Central.Module.Global` | Platform-required functionality that every tenant has. Three ribbon tabs registered by one assembly: **Home** (landing dashboard + notifications), **Admin** (users, roles, lookups, AD, jobs, backups, migrations, API keys, identity providers, purge), **Global Admin** (tenant CRUD, licensing, platform audit — gated on `global_admin:read`). Merged 2026-04-17 from former separate modules; internal subfolders `Admin/`, `Dashboard/`, `Platform/` keep the code organised. |

### Tenant-togglable feature modules

| Folder | Assembly | Contents |
|--------|----------|----------|
| [crm/](crm/) | `Central.Module.CRM` | Accounts, deals, pipeline Kanban. Module-specific dashboard in [crm/Dashboards/](crm/Dashboards/). |
| [devices/](devices/) | `Central.Module.Devices` | IPAM — 8 grid panels, ASN, servers. |
| [networking/](networking/) | `Central.Module.Networking` | Switches, routing (BGP), VLANs, links (P2P/B2B/FW). Internal subfolders `Switches/`, `Routing/`, `Vlans/`, `Links/`. |
| [projects/](projects/) | `Central.Module.Projects` | 16-panel project + task management (Hansoft/P4 Plan clone): tree, backlog, sprint, burndown, Kanban, Gantt, QA, reports, timesheet, activity, portfolio, programmes, import. Module-specific dashboards in [projects/Dashboards/](projects/Dashboards/). |
| [service-desk/](service-desk/) | `Central.Module.ServiceDesk` | ManageEngine sync, teams, group categories, write-back. |
| [audit/](audit/) | `Central.Module.Audit` | Audit log viewer with before/after JSONB diffs, real-time via SignalR. Module-specific GDPR dashboard in [audit/Dashboards/](audit/Dashboards/). |

### Convention: per-module dashboards live in `Dashboards/`

Feature modules that want a module-specific dashboard panel (KPI cards for just their own domain) put it in a `Dashboards/` subfolder: [crm/Dashboards/CrmDashboardPanel.xaml](crm/Dashboards/), [projects/Dashboards/TaskDashboardPanel.xaml](projects/Dashboards/), [audit/Dashboards/GdprDashboardPanel.xaml](audit/Dashboards/). The **platform-wide** landing dashboard (aggregating across modules) lives in [global/Dashboard/](global/Dashboard/). A tenant disabling a feature module loses its module-specific dashboard with it; the platform dashboard stays (because `global/` is always on).

## What makes something a module (vs a lib)

A module:

- Implements `IModule` and registers via Autofac at startup.
- Publishes a **ribbon tab** (or extends an existing one) declaratively.
- Contributes **DockLayoutManager panels** that the shell can host.
- Depends on `Central.Engine` (for `ListViewModelBase<T>`, widget framework, messaging) and typically `Central.Persistence` (for repos).
- Is loaded by `apps/desktop` via the `Bootstrapper` scan; nothing else consumes it.

If the code is just utility functions or a cross-cutting service, it belongs in `libs/`, not here.

## Conventions

- **Folder name is kebab-case**, short: `admin`, `service-desk`, `global-admin`.
- **Assembly name is `Central.Module.<PascalCase>`**, matching the legacy convention.
- **One ribbon tab per module** (or a group on an existing tab). The tab name shows in the shell.
- **Panels follow the shell's docking model.** Register with `DocumentPanel` for tab-docked, `LayoutAnchorable` for sidebar.
- **Panels are closeable.** Use `DockController.Close()`/`Restore()` — not `Visibility.Collapsed` (which breaks DevExpress tab close buttons).
- **Context menu on every grid.** Use `GridContextMenuBuilder` from `libs/engine/`.
- **Cross-panel navigation** via `PanelMessageBus`; no direct coupling between panels.

## Adding a new module

1. `modules/<name>/` — folder.
2. `dotnet new classlib -n Central.Module.<Name>`.
3. Implement `IModule` in a `<Name>Module.cs` bootstrapper — registers ribbon tab + panels + view-models via Autofac.
4. Add `<ProjectReference>` to `libs/engine`, `libs/persistence` as needed.
5. Add to `Central.sln` and reference from `apps/desktop/Central.Desktop.csproj`.
6. Add `Bootstrapper` registration so `apps/desktop` picks it up at startup.
7. Add panel-level tests to `tests/dotnet/Widgets/` (or per-module subfolder if many).
