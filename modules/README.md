# modules/

WPF feature modules. Each registers a ribbon tab plus a set of docked panels into `apps/desktop`. Deliberately separate from `libs/` because a module has a narrower contract than a general-purpose library.

## What's here

| Folder | Assembly | Ribbon tab / purpose |
|--------|----------|----------------------|
| [admin/](admin/) | `Central.Module.Admin` | Users, roles, lookups, AD browser, jobs, ribbon config, migrations, backups, purge, locations, references, Podman, scheduler. |
| [audit/](audit/) | `Central.Module.Audit` | Audit log viewer (before/after JSONB diffs, real-time via SignalR). |
| [crm/](crm/) | `Central.Module.CRM` | Accounts, deals, pipeline Kanban, CRM dashboard. |
| [dashboard/](dashboard/) | `Central.Module.Dashboard` | Landing-page KPI cards + notification center. |
| [devices/](devices/) | `Central.Module.Devices` | IPAM — 8 grid panels, ASN, servers. |
| [global-admin/](global-admin/) | `Central.Module.GlobalAdmin` | Platform-level tenant / licensing / audit (GlobalAdmin-only). |
| [links/](links/) | `Central.Module.Links` | P2P / B2B / FW link inventory + builder. |
| [routing/](routing/) | `Central.Module.Routing` | BGP + topology diagram. |
| [service-desk/](service-desk/) | `Central.Module.ServiceDesk` | ManageEngine sync, dashboards, teams, group categories, write-back. |
| [switches/](switches/) | `Central.Module.Switches` | Switches, detail view, deploy. |
| [tasks/](tasks/) | `Central.Module.Tasks` | 16-panel task management — tree, backlog, sprint, burndown, Kanban, Gantt, QA, reports, timesheet, activity, portfolio, import. |
| [vlans/](vlans/) | `Central.Module.VLANs` | VLAN inventory. |

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
