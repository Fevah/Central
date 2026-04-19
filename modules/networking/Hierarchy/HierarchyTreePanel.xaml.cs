using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Central.Engine.Net.Hierarchy;
using Central.Engine.Shell;
using Central.Persistence.Net;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Grid;
using Region = Central.Engine.Net.Hierarchy.Region;
using UserControl = System.Windows.Controls.UserControl;

namespace Central.Module.Networking.Hierarchy;

/// <summary>
/// Read-only tree view of the geographic hierarchy — Region → Site →
/// Building → Floor → Room → Rack — for the current tenant. Writes
/// happen through the REST endpoints (Phase 2d) and detail dialogs
/// (Phase 2e); this panel is the landing view for operators who need
/// to eyeball the tenant's topology.
///
/// The hierarchy is loaded as a flat <see cref="HierarchyNode"/> list
/// and the DevExpress TreeListControl builds the parent/child shape
/// from <see cref="HierarchyNode.Id"/> / <see cref="HierarchyNode.ParentId"/>.
/// Synthetic composite keys ("Region:{guid}" etc.) guarantee uniqueness
/// across levels — two siblings at different levels could otherwise
/// share a Guid when the control deduplicates.
/// </summary>
public partial class HierarchyTreePanel : UserControl
{
    private string? _dsn;
    private Guid _tenantId;
    private int? _userId;
    private CancellationTokenSource? _cts;

    public HierarchyTreePanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        TreeView.ShowGridMenu += OnShowGridMenu;
        // Cross-panel drill-down: entity grids publish
        // `focusBuilding:{building_code}` (or the other hierarchy
        // levels) to land the tree focus + expansion on a specific
        // node. Completes the find-from-anywhere trio: audit drill,
        // search drill, hierarchy drill.
        PanelMessageBus.Subscribe<NavigateToPanelMessage>(OnNavigate);
    }

    /// <summary>Pending focus request from a cross-panel drill-down.
    /// The tree may not have loaded when the message arrives — if so,
    /// we stash the request and apply it once ReloadAsync populates
    /// ItemsSource.</summary>
    private (string nodeType, string code)? _pendingFocus;

    private void OnNavigate(NavigateToPanelMessage msg)
    {
        if (msg.TargetPanel != "hierarchy") return;
        if (msg.SelectItem is not string payload) return;

        // Payload shape: `focus{NodeType}:{code}` — e.g.
        // focusBuilding:IT-B, focusSite:IT-R/IT-S. Accepts the five
        // levels the hierarchy tree exposes.
        var (nodeType, code) = ParseFocusPayload(payload);
        if (nodeType is null || code is null) return;

        _pendingFocus = (nodeType, code);
        Dispatcher.BeginInvoke(ApplyPendingFocus);
    }

    /// <summary>Parse `focusBuilding:IT-B` into (NodeType, code).
    /// Returns (null, null) on unknown payload so the caller just
    /// silently ignores it — matches the error-tolerance pattern
    /// the other panels' OnNavigate handlers use.</summary>
    internal static (string? nodeType, string? code) ParseFocusPayload(string payload)
    {
        const string prefix = "focus";
        if (!payload.StartsWith(prefix, StringComparison.Ordinal)) return (null, null);
        var colon = payload.IndexOf(':');
        if (colon < prefix.Length) return (null, null);

        var nodeType = payload.Substring(prefix.Length, colon - prefix.Length);
        var code = payload.Substring(colon + 1);
        if (string.IsNullOrEmpty(code)) return (null, null);
        return nodeType switch
        {
            "Region" or "Site" or "Building" or "Floor" or "Room" or "Rack"
                => (nodeType, code),
            _ => (null, null),
        };
    }

    /// <summary>Apply the pending focus to the tree: find the node,
    /// walk up its ParentId chain to expand every ancestor, set
    /// FocusedRow. No-op when the tree hasn't populated or the code
    /// doesn't exist (e.g. operator filtered it out of the parent
    /// query before drilling).</summary>
    private void ApplyPendingFocus()
    {
        if (_pendingFocus is not { nodeType: var nodeType, code: var code }) return;
        if (Tree.ItemsSource is not IEnumerable<HierarchyNode> all) return;

        var byId = all.ToDictionary(n => n.Id);
        var target = all.FirstOrDefault(n => n.NodeType == nodeType && n.Code == code);
        if (target is null)
        {
            StatusLabel.Text = $"No {nodeType.ToLower()} with code '{code}' visible in this tenant";
            return;
        }

        // Walk up the parent chain + expand each ancestor so the
        // target is actually in view. TreeListView's ExpandNode
        // takes the node's handle; easiest path is a recursive
        // expand-to-root via ParentId.
        var expandChain = new List<HierarchyNode>();
        var cursor = target;
        while (cursor.ParentId is string pid && byId.TryGetValue(pid, out var parent))
        {
            expandChain.Add(parent);
            cursor = parent;
        }
        // Expand from root down so each child has its parent already
        // materialised in the tree.
        expandChain.Reverse();
        foreach (var anc in expandChain)
        {
            var handle = TreeView.GetNodeByContent(anc)?.RowHandle;
            if (handle is int h && h >= 0) TreeView.ExpandNode(h);
        }

        var focusHandle = TreeView.GetNodeByContent(target)?.RowHandle;
        if (focusHandle is int fh && fh >= 0)
        {
            TreeView.FocusedRowHandle = fh;
            StatusLabel.Text = $"Focused {nodeType} '{code}' · {DateTime.Now:HH:mm:ss}";
        }

        _pendingFocus = null;
    }

    /// <summary>
    /// Called by the host (MainWindow) after construction so the panel
    /// knows which DSN + tenant to query. Safe to call multiple times —
    /// the most recent call wins and triggers a reload.
    /// </summary>
    public void SetContext(string dsn, Guid tenantId, int? userId = null)
    {
        _dsn = dsn;
        _tenantId = tenantId;
        _userId = userId;
        if (IsLoaded) _ = ReloadAsync();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_dsn) && _tenantId != Guid.Empty)
            _ = ReloadAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts = null;
    }

    public async Task ReloadAsync()
    {
        if (string.IsNullOrEmpty(_dsn) || _tenantId == Guid.Empty)
        {
            StatusLabel.Text = "No tenant context";
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        StatusLabel.Text = "Loading…";
        try
        {
            var repo = new HierarchyRepository(_dsn);

            var regions = await repo.ListRegionsAsync(_tenantId, ct);
            var sites = await repo.ListSitesAsync(_tenantId, null, ct);
            var buildings = await repo.ListBuildingsAsync(_tenantId, null, ct);
            var floors = await repo.ListFloorsAsync(_tenantId, null, ct);
            var rooms = await repo.ListRoomsAsync(_tenantId, null, ct);
            var racks = await repo.ListRacksAsync(_tenantId, null, ct);

            var nodes = BuildNodes(regions, sites, buildings, floors, rooms, racks);
            Tree.ItemsSource = nodes;
            StatusLabel.Text = $"{nodes.Count} nodes · loaded {DateTime.Now:HH:mm:ss}";
            // Apply any pending cross-panel drill-down focus that
            // arrived before the tree was populated.
            if (_pendingFocus is not null)
                Dispatcher.BeginInvoke(ApplyPendingFocus);
        }
        catch (OperationCanceledException)
        {
            // Panel unloaded or re-queried — ignore.
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Load failed: {ex.Message}";
        }
    }

    private static List<HierarchyNode> BuildNodes(
        List<Region> regions, List<Site> sites, List<Building> buildings,
        List<Floor> floors, List<Room> rooms, List<Rack> racks)
    {
        var nodes = new List<HierarchyNode>(
            regions.Count + sites.Count + buildings.Count +
            floors.Count + rooms.Count + racks.Count);

        foreach (var r in regions)
            nodes.Add(new HierarchyNode
            {
                Id = $"Region:{r.Id}",
                ParentId = null,
                EntityId = r.Id,
                NodeType = "Region",
                Code = r.RegionCode,
                Name = r.DisplayName,
                Status = r.Status.ToString(),
                Lock = r.LockState.ToString(),
                Version = r.Version
            });

        foreach (var s in sites)
            nodes.Add(new HierarchyNode
            {
                Id = $"Site:{s.Id}",
                ParentId = $"Region:{s.RegionId}",
                EntityId = s.Id,
                NodeType = "Site",
                Code = s.SiteCode,
                Name = s.DisplayName,
                Status = s.Status.ToString(),
                Lock = s.LockState.ToString(),
                Version = s.Version
            });

        foreach (var b in buildings)
            nodes.Add(new HierarchyNode
            {
                Id = $"Building:{b.Id}",
                ParentId = $"Site:{b.SiteId}",
                EntityId = b.Id,
                NodeType = "Building",
                Code = b.BuildingCode,
                Name = b.DisplayName,
                Status = b.Status.ToString(),
                Lock = b.LockState.ToString(),
                Version = b.Version
            });

        foreach (var f in floors)
            nodes.Add(new HierarchyNode
            {
                Id = $"Floor:{f.Id}",
                ParentId = $"Building:{f.BuildingId}",
                EntityId = f.Id,
                NodeType = "Floor",
                Code = f.FloorCode,
                Name = f.DisplayName ?? "",
                Status = f.Status.ToString(),
                Lock = f.LockState.ToString(),
                Version = f.Version
            });

        foreach (var rm in rooms)
            nodes.Add(new HierarchyNode
            {
                Id = $"Room:{rm.Id}",
                ParentId = $"Floor:{rm.FloorId}",
                EntityId = rm.Id,
                NodeType = "Room",
                Code = rm.RoomCode,
                Name = rm.RoomType,
                Status = rm.Status.ToString(),
                Lock = rm.LockState.ToString(),
                Version = rm.Version
            });

        foreach (var rk in racks)
            nodes.Add(new HierarchyNode
            {
                Id = $"Rack:{rk.Id}",
                ParentId = $"Room:{rk.RoomId}",
                EntityId = rk.Id,
                NodeType = "Rack",
                Code = rk.RackCode,
                Name = $"{rk.UHeight}U",
                Status = rk.Status.ToString(),
                Lock = rk.LockState.ToString(),
                Version = rk.Version
            });

        return nodes;
    }

    // ── Context menu ────────────────────────────────────────────────────

    private void OnShowGridMenu(object? sender, GridMenuEventArgs e)
    {
        if (e.MenuType != GridMenuType.RowCell) return;
        var selected = TreeView.FocusedRow as HierarchyNode;

        // Always available: create a new Region at the root.
        e.Customizations.Add(MakeButton("New region…", () => _ = OpenNewRegionAsync()));

        // New child — contextual, scopes under the selected node.
        if (selected is not null)
        {
            switch (selected.NodeType)
            {
                case "Region":
                    e.Customizations.Add(MakeButton("New site in this region…",
                        () => _ = OpenNewSiteAsync(selected.EntityId)));
                    break;
                case "Site":
                    e.Customizations.Add(MakeButton("New building in this site…",
                        () => _ = OpenNewBuildingAsync(selected.EntityId)));
                    break;
                case "Building":
                    e.Customizations.Add(MakeButton("New floor in this building…",
                        () => _ = OpenNewFloorAsync(selected.EntityId)));
                    break;
                case "Floor":
                    e.Customizations.Add(MakeButton("New room on this floor…",
                        () => _ = OpenNewRoomAsync(selected.EntityId)));
                    break;
                case "Room":
                    e.Customizations.Add(MakeButton("New rack in this room…",
                        () => _ = OpenNewRackAsync(selected.EntityId)));
                    break;
            }

            e.Customizations.Add(new BarItemLinkSeparator());

            // Cross-panel drill — mirrors the entity-grid "Show in
            // hierarchy" context item but in reverse. From a Building
            // node, drill to the Device grid filtered to rows in
            // this building. Only Building is wired today; Site /
            // Region would need a different filter grammar (OR-chain
            // over multiple buildings) that lives in a follow-up.
            if (selected.NodeType == "Building")
            {
                e.Customizations.Add(MakeButton("Show devices in this building",
                    () => ShowDevicesInBuilding(selected.Code)));
                e.Customizations.Add(MakeButton("Show servers in this building",
                    () => ShowServersInBuilding(selected.Code)));
            }

            e.Customizations.Add(new BarItemLinkSeparator());

            // All six levels are now editable + soft-deletable (Phase 2e).
            e.Customizations.Add(MakeButton($"Edit {selected.NodeType.ToLower()}…",
                () => _ = OpenEditAsync(selected)));
            e.Customizations.Add(MakeButton($"Delete {selected.NodeType.ToLower()}",
                () => _ = DeleteAsync(selected)));
        }
    }

    /// <summary>Publish the OpenPanelMessage + NavigateToPanelMessage
    /// pair so the Device grid opens filtered to rows in the given
    /// building code. DeviceGridPanel.OnNavigate handles
    /// `filterBy:Building:{code}` by setting the grid's FilterString.</summary>
    private void ShowDevicesInBuilding(string buildingCode)
    {
        if (string.IsNullOrWhiteSpace(buildingCode)) return;
        PanelMessageBus.Publish(new OpenPanelMessage("devices"));
        PanelMessageBus.Publish(new NavigateToPanelMessage(
            "devices", $"filterBy:Building:{buildingCode}"));
        StatusLabel.Text = $"Filtered devices to building '{buildingCode}'";
    }

    /// <summary>Same drill pattern for servers — ServerRow.BuildingCode
    /// is the filter field (vs DeviceRecord.Building). Column name
    /// shapes the payload so each grid maps its own DX FilterString
    /// without per-grid knowledge in the hierarchy panel.</summary>
    private void ShowServersInBuilding(string buildingCode)
    {
        if (string.IsNullOrWhiteSpace(buildingCode)) return;
        PanelMessageBus.Publish(new OpenPanelMessage("servers"));
        PanelMessageBus.Publish(new NavigateToPanelMessage(
            "servers", $"filterBy:BuildingCode:{buildingCode}"));
        StatusLabel.Text = $"Filtered servers to building '{buildingCode}'";
    }

    private static BarButtonItem MakeButton(string text, Action onClick)
    {
        var btn = new BarButtonItem { Content = text };
        btn.ItemClick += (_, _) => onClick();
        return btn;
    }

    private async Task OpenNewRegionAsync()
    {
        if (string.IsNullOrEmpty(_dsn) || _tenantId == Guid.Empty) return;
        var dlg = HierarchyDetailDialog.ForNewRegion(_dsn, _tenantId, _userId)
            .With(Window.GetWindow(this));
        if (dlg.ShowDialog() == true) await ReloadAsync();
    }

    private async Task OpenNewSiteAsync(Guid regionId)
    {
        if (string.IsNullOrEmpty(_dsn) || _tenantId == Guid.Empty) return;
        var dlg = (await HierarchyDetailDialog.ForNewSiteAsync(_dsn, _tenantId, _userId, regionId))
            .With(Window.GetWindow(this));
        if (dlg.ShowDialog() == true) await ReloadAsync();
    }

    private async Task OpenNewBuildingAsync(Guid siteId)
    {
        if (string.IsNullOrEmpty(_dsn) || _tenantId == Guid.Empty) return;
        var dlg = (await HierarchyDetailDialog.ForNewBuildingAsync(_dsn, _tenantId, _userId, siteId))
            .With(Window.GetWindow(this));
        if (dlg.ShowDialog() == true) await ReloadAsync();
    }

    private async Task OpenNewFloorAsync(Guid buildingId)
    {
        if (string.IsNullOrEmpty(_dsn) || _tenantId == Guid.Empty) return;
        var dlg = (await HierarchyDetailDialog.ForNewFloorAsync(_dsn, _tenantId, _userId, buildingId))
            .With(Window.GetWindow(this));
        if (dlg.ShowDialog() == true) await ReloadAsync();
    }

    private async Task OpenNewRoomAsync(Guid floorId)
    {
        if (string.IsNullOrEmpty(_dsn) || _tenantId == Guid.Empty) return;
        var dlg = (await HierarchyDetailDialog.ForNewRoomAsync(_dsn, _tenantId, _userId, floorId))
            .With(Window.GetWindow(this));
        if (dlg.ShowDialog() == true) await ReloadAsync();
    }

    private async Task OpenNewRackAsync(Guid roomId)
    {
        if (string.IsNullOrEmpty(_dsn) || _tenantId == Guid.Empty) return;
        var dlg = (await HierarchyDetailDialog.ForNewRackAsync(_dsn, _tenantId, _userId, roomId))
            .With(Window.GetWindow(this));
        if (dlg.ShowDialog() == true) await ReloadAsync();
    }

    private async Task OpenEditAsync(HierarchyNode node)
    {
        if (string.IsNullOrEmpty(_dsn) || _tenantId == Guid.Empty) return;
        try
        {
            var dlg = (await HierarchyDetailDialog.ForEditAsync(_dsn, _tenantId, _userId,
                    node.NodeType, node.EntityId))
                .With(Window.GetWindow(this));
            if (dlg.ShowDialog() == true) await ReloadAsync();
        }
        catch (Exception ex)
        {
            DevExpress.Xpf.Core.DXMessageBox.Show(Window.GetWindow(this),
                ex.Message, "Load failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DeleteAsync(HierarchyNode node)
    {
        if (string.IsNullOrEmpty(_dsn) || _tenantId == Guid.Empty) return;

        var confirm = DevExpress.Xpf.Core.DXMessageBox.Show(
            Window.GetWindow(this),
            $"Delete {node.NodeType.ToLower()} '{node.Code}'?\n\n" +
            "This is a soft delete — the entity is marked deleted but remains in the database.",
            $"Delete {node.NodeType.ToLower()}?",
            MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        try
        {
            var repo = new HierarchyRepository(_dsn);
            var deleted = node.NodeType switch
            {
                "Region"   => await repo.SoftDeleteRegionAsync(node.EntityId, _tenantId, _userId),
                "Site"     => await repo.SoftDeleteSiteAsync(node.EntityId, _tenantId, _userId),
                "Building" => await repo.SoftDeleteBuildingAsync(node.EntityId, _tenantId, _userId),
                "Floor"    => await repo.SoftDeleteFloorAsync(node.EntityId, _tenantId, _userId),
                "Room"     => await repo.SoftDeleteRoomAsync(node.EntityId, _tenantId, _userId),
                "Rack"     => await repo.SoftDeleteRackAsync(node.EntityId, _tenantId, _userId),
                _ => false
            };
            if (deleted) await ReloadAsync();
        }
        catch (Exception ex)
        {
            DevExpress.Xpf.Core.DXMessageBox.Show(Window.GetWindow(this),
                ex.Message, "Delete failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

internal static class DialogExtensions
{
    public static T With<T>(this T dlg, Window? owner) where T : Window
    {
        if (owner is not null) dlg.Owner = owner;
        return dlg;
    }
}
