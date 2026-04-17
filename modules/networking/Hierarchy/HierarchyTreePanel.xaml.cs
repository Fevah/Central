using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Central.Engine.Net.Hierarchy;
using Central.Persistence.Net;
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
    private CancellationTokenSource? _cts;

    public HierarchyTreePanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Called by the host (MainWindow) after construction so the panel
    /// knows which DSN + tenant to query. Safe to call multiple times —
    /// the most recent call wins and triggers a reload.
    /// </summary>
    public void SetContext(string dsn, Guid tenantId)
    {
        _dsn = dsn;
        _tenantId = tenantId;
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
}
