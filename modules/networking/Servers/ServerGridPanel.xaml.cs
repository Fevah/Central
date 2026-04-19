using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Central.Engine.Net.Pools;
using Central.Engine.Net.Servers;
using Central.Engine.Shell;
using Central.Persistence.Net;
using UserControl = System.Windows.Controls.UserControl;

namespace Central.Module.Networking.Servers;

/// <summary>
/// Read-only grid view of <c>net.server</c> for the current tenant.
/// Loads server rows plus the building code, profile code, ASN, and
/// loopback IP via joined lookups, then counts NICs per server.
///
/// <para>Mutations go through the REST endpoints from Phase 6c or
/// through a dedicated server detail dialog (future — the plan's
/// creation flow uses <see cref="ServerCreationService"/> which is
/// already live and invoked by the "New Server" ribbon button).</para>
/// </summary>
public partial class ServerGridPanel : UserControl
{
    private string? _dsn;
    private Guid _tenantId;
    private CancellationTokenSource? _cts;

    public ServerGridPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        // Cross-panel drill-down: SearchPanel publishes
        // `selectId:{guid}:{label}` for this grid's target id
        // ("servers"). ServerRow.Id is the net.server uuid — the
        // native key matches directly, no hostname fallback needed.
        PanelMessageBus.Subscribe<NavigateToPanelMessage>(OnNavigate);
    }

    private void OnNavigate(NavigateToPanelMessage msg)
    {
        if (msg.TargetPanel != "servers") return;
        if (msg.SelectItem is not string payload) return;
        if (!payload.StartsWith("selectId:", StringComparison.Ordinal)) return;

        // Payload: `selectId:{guid}:{label}`. For net.server, the
        // guid is the row's primary key — match by Id first, fall
        // back to hostname if the row hasn't loaded yet / the id
        // doesn't resolve.
        var parts = payload.Split(':', 3);
        if (parts.Length < 2) return;
        Guid.TryParse(parts[1], out var id);
        var label = parts.Length >= 3 ? parts[2] : "";
        Dispatcher.BeginInvoke(() => FocusBy(id, label));
    }

    internal bool FocusBy(Guid id, string label)
    {
        if (Grid.ItemsSource is not IEnumerable source) return false;
        var idx = 0;
        foreach (var item in source)
        {
            if (item is ServerRow r &&
                (r.Id == id ||
                 (!string.IsNullOrEmpty(label) &&
                  string.Equals(r.Hostname, label, StringComparison.OrdinalIgnoreCase))))
            {
                Grid.CurrentItem = r;
                // Grid.View is the TableView — focus the handle to
                // bring the row into view and set keyboard focus.
                if (Grid.View is DevExpress.Xpf.Grid.TableView tv)
                    tv.FocusedRowHandle = Grid.GetRowHandleByListIndex(idx);
                return true;
            }
            idx++;
        }
        return false;
    }

    public void SetContext(string dsn, Guid tenantId)
    {
        _dsn = dsn;
        _tenantId = tenantId;
        if (IsLoaded) _ = ReloadAsync();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_dsn) && _tenantId != Guid.Empty) _ = ReloadAsync();
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
            var serversRepo = new ServersRepository(_dsn);
            var poolsRepo = new PoolsRepository(_dsn);
            var hierRepo = new HierarchyRepository(_dsn);

            // Parallelise the four lookup fetches — they're independent.
            var serversTask   = serversRepo.ListServersAsync(_tenantId, null, ct);
            var profilesTask  = serversRepo.ListProfilesAsync(_tenantId, ct);
            var nicsTask      = serversRepo.ListNicsAsync(_tenantId, null, ct);
            var allocTask     = poolsRepo.ListAsnAllocationsAsync(_tenantId, null, ct);
            var ipsTask       = poolsRepo.ListIpAddressesAsync(_tenantId, null, ct);
            var buildingsTask = hierRepo.ListBuildingsAsync(_tenantId, null, ct);

            await Task.WhenAll(serversTask, profilesTask, nicsTask,
                               allocTask, ipsTask, buildingsTask);

            var rows = BuildRows(
                await serversTask, await profilesTask, await nicsTask,
                await allocTask, await ipsTask, await buildingsTask);

            Grid.ItemsSource = rows;
            StatusLabel.Text = $"{rows.Count} servers · loaded {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException) { /* ignore */ }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Load failed: {ex.Message}";
        }
    }

    private static List<ServerRow> BuildRows(
        List<Server> servers,
        List<ServerProfile> profiles,
        List<ServerNic> nics,
        List<AsnAllocation> allocs,
        List<IpAddress> ips,
        List<Central.Engine.Net.Hierarchy.Building> buildings)
    {
        // Build lookups once per reload so the row loop is O(n).
        var buildingByCode = new Dictionary<Guid, string>(buildings.Count);
        foreach (var b in buildings) buildingByCode[b.Id] = b.BuildingCode;

        var profileByCode = new Dictionary<Guid, string>(profiles.Count);
        foreach (var p in profiles) profileByCode[p.Id] = p.ProfileCode;

        var asnByAlloc = new Dictionary<Guid, long>(allocs.Count);
        foreach (var a in allocs) asnByAlloc[a.Id] = a.Asn;

        var ipByAddr = new Dictionary<Guid, string>(ips.Count);
        foreach (var ip in ips) ipByAddr[ip.Id] = ip.Address;

        var nicCountByServer = new Dictionary<Guid, int>();
        foreach (var n in nics)
        {
            nicCountByServer[n.ServerId] =
                nicCountByServer.TryGetValue(n.ServerId, out var c) ? c + 1 : 1;
        }

        var rows = new List<ServerRow>(servers.Count);
        foreach (var s in servers)
        {
            rows.Add(new ServerRow
            {
                Id            = s.Id,
                Hostname      = s.Hostname,
                BuildingCode  = s.BuildingId is Guid bId && buildingByCode.TryGetValue(bId, out var bc) ? bc : "",
                ProfileCode   = s.ServerProfileId is Guid pId && profileByCode.TryGetValue(pId, out var pc) ? pc : "",
                Asn           = s.AsnAllocationId is Guid aId && asnByAlloc.TryGetValue(aId, out var asn) ? asn : null,
                LoopbackIp    = s.LoopbackIpAddressId is Guid iId && ipByAddr.TryGetValue(iId, out var ip) ? ip : "",
                NicCount      = nicCountByServer.TryGetValue(s.Id, out var nc) ? nc : 0,
                ManagementIp  = s.ManagementIp,
                LastPingOk    = s.LastPingOk,
                LastPingMs    = s.LastPingMs,
                Status        = s.Status.ToString(),
                Lock          = s.LockState.ToString(),
                Version       = s.Version,
            });
        }
        return rows;
    }
}
