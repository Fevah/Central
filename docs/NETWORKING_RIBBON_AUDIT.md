# Networking ribbon audit — Chunk B

**Date:** 2026-04-18
**Source of truth:** [libs/engine/Net/Ribbons/NetworkingRibbonRegistrar.cs](../libs/engine/Net/Ribbons/NetworkingRibbonRegistrar.cs)
**Audit tests:** [tests/dotnet/Net/NetworkingRibbonAuditTests.cs](../tests/dotnet/Net/NetworkingRibbonAuditTests.cs)

## Purpose

Before Chunk B, 13 of 17 action buttons on the Networking ribbon dispatched `() => { }` — "clicked a button, nothing happened". This was a quiet drift: every phase since 2c had been adding panels, not wiring the ribbon that reaches them.

Chunk B:
1. Wired every placeholder through `PanelMessageBus` with a `NavigateToPanelMessage` (most actions) or `RefreshPanelMessage` (refresh buttons).
2. Extracted the registration into a net10.0 engine helper so the test project can exercise it without WPF references.
3. Added an audit test that re-invokes every button's `OnClick` and asserts a message was published. A future placeholder lambda will fail the audit.

## Button inventory

Buttons whose `OnClick` produces an `IPanelMessage`. The `Panels` group below is CheckButtons (different dispatch path) and listed separately.

| Group | Button | Permission | Message | Target | Action payload |
|---|---|---|---|---|---|
| Devices | New Device | `devices:write` | Navigate | `devices` | `action:new` |
| Devices | Delete Device | `devices:delete` | Navigate | `devices` | `action:delete` |
| Devices | Refresh | `devices:read` | Refresh | `devices` | — |
| Devices | Export | `devices:export` | Navigate | `devices` | `action:export` |
| Switches | New Switch | `switches:write` | Navigate | `switches` | `action:new` |
| Switches | Edit Switch | `switches:write` | Navigate | `switches` | `action:edit` |
| Switches | Delete Switch | `switches:delete` | Navigate | `switches` | `action:delete` |
| Switches | Ping All | `switches:ping` | Navigate | `switches` | `action:pingAll` |
| Switches | Ping Selected | `switches:ping` | Navigate | `switches` | `action:pingSelected` |
| Switches | Sync Config | `switches:sync` | Navigate | `switches` | `action:syncConfig` |
| Links | New Link | `links:write` | Navigate | `links` | `action:new` |
| Links | Delete Link | `links:delete` | Navigate | `links` | `action:delete` |
| Links | Build Config | `links:read` | Navigate | `links` | `action:build` |
| Routing | Sync BGP | `bgp:sync` | Navigate | `bgp` | `action:syncSelected` |
| Routing | Sync All BGP | `bgp:sync` | Navigate | `bgp` | `action:syncAll` |
| VLANs | Refresh VLANs | `vlans:read` | Refresh | `vlans` | — |
| VLANs | Show Default VLAN (toggle) | `vlans:read` | Navigate | `vlans` | `action:showDefault:{true\|false}` |
| Servers | New Server | `net:servers:write` | Navigate | `servers` | `action:new` |
| Servers | Edit Server | `net:servers:write` | Navigate | `servers` | `action:edit` |
| Servers | Delete Server | `net:servers:delete` | Navigate | `servers` | `action:delete` |
| Servers | Ping NICs | `net:servers:write` | Navigate | `servers` | `action:pingNics` |
| Servers | Refresh | `net:servers:read` | Refresh | `servers` | — |

## Panels group (CheckButtons)

`CheckButton`s bind to the DockLayoutManager via `IsChecked="{Binding IsXPanelOpen}"` in [apps/desktop/MainWindow.xaml](../apps/desktop/MainWindow.xaml). They don't participate in the audit above — their "click handler" is the WPF binding itself.

| Button | Panel id | Docked target |
|---|---|---|
| Hierarchy | `HierarchyPanel` | net.* hierarchy tree (Phase 2c) |
| Pools | `PoolsPanel` | net.* pool tree with utilisation (Phase 3f) |
| Servers | `ServersPanel` | net.server grid (Phase 6f — panel not yet built) |
| IPAM | `DevicesPanel` | Legacy device grid |
| Device Details | `DeviceDetailPanel` | Legacy device detail view |
| Switches | `SwitchesPanel` | Legacy switch grid |
| Switch Details | `SwitchDetailPanel` | Legacy switch detail |
| P2P | `P2PPanel` | Legacy P2P links grid (Phase 5d imports data under new model) |
| B2B | `B2BPanel` | Legacy B2B links grid |
| FW | `FWPanel` | Legacy FW links grid |
| BGP | `BgpPanel` | Legacy BGP grid |
| VLANs | `VlanPanel` | Legacy VLANs grid |

## Handler receivers

Subscribers to these messages live on the WPF side. The receiver convention:

- `NavigateToPanelMessage` with `TargetPanel = "devices"` is handled by `DeviceGridPanel` (see [modules/networking/Devices/DeviceGridPanel.xaml.cs](../modules/networking/Devices/DeviceGridPanel.xaml.cs)). New / delete / export actions trigger the corresponding grid operation.
- `NavigateToPanelMessage` with `TargetPanel = "switches"` is handled by `SwitchGridPanel`. Ping + sync actions go through the engine's `SshOperationsService`.
- `NavigateToPanelMessage` with `TargetPanel = "links"` — **no subscriber yet**. The unified Links panel lands alongside Phase 5's final UI swap. Until then, Links-group buttons fire a message into the void; panels binding to the P2P/B2B/FW CheckButtons remain the primary path.
- `NavigateToPanelMessage` with `TargetPanel = "bgp"` — **partial**. BGP-sync handlers exist in `MainWindow.xaml.cs` via direct event handlers (`SyncBgpButton_ItemClick` / `SyncAllBgpButton_ItemClick`). The engine-ribbon path duplicates the XAML-ribbon path; one will retire when the XAML ribbon does.
- `RefreshPanelMessage` with `TargetPanel = "vlans"` — handled by `VlanGridPanel` reload hook.

## Known gaps

| Surface | Gap | Impact | Fix |
|---|---|---|---|
| `links` target | No subscriber wired | Clicking New/Delete/Build Config on a freshly-launched desktop does nothing until the unified Links panel lands. | Phase 5 UI swap (lands with the Links panel tree refactor). |
| XAML vs engine-ribbon | Two ribbons coexist (engine-registered buttons from `NetworkingRibbonRegistrar` and the static `MainWindow.xaml` ribbon). Most panels still bind to the XAML ribbon. | Dual maintenance burden; edits might miss one side. | Phase 11 retires the XAML ribbon once every panel subscribes to the bus. |
| Permission leak audit | Done. Every action button declares a `Permission`; the test `EveryActionButton_HasAPermissionCode` guards against future regressions. | — | — |
| Placeholder lambdas | Done. `EveryActionButton_PublishesAMessage` fails the suite if anyone adds a button with `() => { }` that doesn't publish a message. | — | — |

## How to add a button

1. Add the registration to [NetworkingRibbonRegistrar.cs](../libs/engine/Net/Ribbons/NetworkingRibbonRegistrar.cs), next to its group peers. Use an existing permission code from `P.*` (or add a new one in [PermissionCode.cs](../libs/engine/Auth/PermissionCode.cs) first).
2. Wire the `OnClick` to `PanelMessageBus.Publish(...)` — never `() => { }`.
3. Add a `[InlineData]` row to `NavigateButton_PublishesCorrectTargetAndAction` in the audit test so the exact (target, action) pair is asserted.
4. Add an entry to the Button Inventory table above.
5. If the target panel has no subscriber yet, note it in the Known Gaps table and file a follow-up.
