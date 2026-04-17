using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Central.Engine.Net.Pools;
using Central.Persistence.Net;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Grid;
using UserControl = System.Windows.Controls.UserControl;

namespace Central.Module.Networking.Pools;

/// <summary>
/// Read-only tree view of the numbering-pool system for the current
/// tenant. Shows every pool tier (ASN / IP / VLAN / MLAG / MSTP), their
/// blocks / subnets / sub-rules, and a utilisation bar per row so the
/// operator can spot a pool that's about to exhaust at a glance.
///
/// Utilisation math is done here, not on the server — the row count is
/// a snapshot at refresh time. For ASN blocks: used = allocations.count,
/// capacity = last - first + 1. For subnets: used = ip_address.count,
/// capacity = host count per prefix (RFC 3021 aware). For VLAN blocks
/// and MLAG pools: same pattern.
///
/// Writes (pool / block CRUD, allocation, retire) happen through
/// dialogs + endpoints — this panel is the overview / monitoring
/// surface, not the editor.
/// </summary>
public partial class PoolsTreePanel : UserControl
{
    private string? _dsn;
    private Guid _tenantId;
    private int? _userId;
    private CancellationTokenSource? _cts;

    public PoolsTreePanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        TreeView.ShowGridMenu += OnShowGridMenu;
    }

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
            var repo = new PoolsRepository(_dsn);

            var asnPools = await repo.ListAsnPoolsAsync(_tenantId, ct);
            var asnBlocks = await repo.ListAsnBlocksAsync(_tenantId, null, ct);
            var asnAllocs = await repo.ListAsnAllocationsAsync(_tenantId, null, ct);

            var ipPools = await repo.ListIpPoolsAsync(_tenantId, ct);
            var subnets = await repo.ListSubnetsAsync(_tenantId, null, ct);
            var ipAddrs = await repo.ListIpAddressesAsync(_tenantId, null, ct);

            var vlanPools = await repo.ListVlanPoolsAsync(_tenantId, ct);
            var vlanBlocks = await repo.ListVlanBlocksAsync(_tenantId, null, ct);
            var vlans = await repo.ListVlansAsync(_tenantId, null, ct);

            var mlagPools = await repo.ListMlagPoolsAsync(_tenantId, ct);
            var mlagDomains = await repo.ListMlagDomainsAsync(_tenantId, null, ct);

            var mstpRules = await repo.ListMstpRulesAsync(_tenantId, ct);

            var nodes = BuildNodes(
                asnPools, asnBlocks, asnAllocs,
                ipPools, subnets, ipAddrs,
                vlanPools, vlanBlocks, vlans,
                mlagPools, mlagDomains,
                mstpRules);

            Tree.ItemsSource = nodes;
            StatusLabel.Text = $"{nodes.Count} nodes · loaded {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException) { /* ignore */ }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Load failed: {ex.Message}";
        }
    }

    private static List<PoolTreeNode> BuildNodes(
        List<AsnPool> asnPools, List<AsnBlock> asnBlocks, List<AsnAllocation> asnAllocs,
        List<IpPool> ipPools, List<Subnet> subnets, List<IpAddress> ipAddrs,
        List<VlanPool> vlanPools, List<VlanBlock> vlanBlocks, List<Vlan> vlans,
        List<MlagDomainPool> mlagPools, List<MlagDomain> mlagDomains,
        List<MstpPriorityRule> mstpRules)
    {
        // Count used values per container up front so we don't re-scan
        // for every block / subnet.
        var asnUsedByBlock = CountBy(asnAllocs, a => a.BlockId);
        var ipUsedBySubnet = CountBy(ipAddrs, a => a.SubnetId);
        var vlanUsedByBlock = CountBy(vlans, v => v.BlockId);
        var mlagUsedByPool = CountBy(mlagDomains, d => d.PoolId);
        var subnetsByPool = GroupBy(subnets, s => s.PoolId);
        var asnBlocksByPool = GroupBy(asnBlocks, b => b.PoolId);
        var vlanBlocksByPool = GroupBy(vlanBlocks, b => b.PoolId);

        var nodes = new List<PoolTreeNode>(
            asnPools.Count + asnBlocks.Count +
            ipPools.Count + subnets.Count +
            vlanPools.Count + vlanBlocks.Count +
            mlagPools.Count + mstpRules.Count);

        // ── ASN ──
        foreach (var p in asnPools)
        {
            long capacity = p.AsnLast - p.AsnFirst + 1;
            long used = 0;
            if (asnBlocksByPool.TryGetValue(p.Id, out var blocks))
                foreach (var b in blocks)
                    used += asnUsedByBlock.TryGetValue(b.Id, out var u) ? u : 0;

            nodes.Add(PoolRow("AsnPool", $"AsnPool:{p.Id}", null, p.Id,
                p.PoolCode, p.DisplayName, $"{p.AsnFirst}–{p.AsnLast}",
                used, capacity, p.Status, p.LockState, p.Version));
        }
        foreach (var b in asnBlocks)
        {
            long capacity = b.AsnLast - b.AsnFirst + 1;
            long used = asnUsedByBlock.TryGetValue(b.Id, out var u) ? u : 0;
            nodes.Add(PoolRow("AsnBlock", $"AsnBlock:{b.Id}", $"AsnPool:{b.PoolId}", b.Id,
                b.BlockCode, b.DisplayName, $"{b.AsnFirst}–{b.AsnLast}",
                used, capacity, b.Status, b.LockState, b.Version));
        }

        // ── IP ──
        foreach (var p in ipPools)
        {
            long used = 0;
            long capacity = 0;
            if (subnetsByPool.TryGetValue(p.Id, out var subs))
            {
                foreach (var s in subs)
                {
                    used += ipUsedBySubnet.TryGetValue(s.Id, out var u) ? u : 0;
                    capacity += SubnetHostCapacity(s.Network);
                }
            }
            nodes.Add(PoolRow("IpPool", $"IpPool:{p.Id}", null, p.Id,
                p.PoolCode, p.DisplayName, p.Network,
                used, capacity, p.Status, p.LockState, p.Version));
        }
        foreach (var s in subnets)
        {
            long used = ipUsedBySubnet.TryGetValue(s.Id, out var u) ? u : 0;
            long capacity = SubnetHostCapacity(s.Network);
            nodes.Add(PoolRow("Subnet", $"Subnet:{s.Id}", $"IpPool:{s.PoolId}", s.Id,
                s.SubnetCode, s.DisplayName, s.Network,
                used, capacity, s.Status, s.LockState, s.Version));
        }

        // ── VLAN ──
        foreach (var p in vlanPools)
        {
            long capacity = p.VlanLast - p.VlanFirst + 1;
            long used = 0;
            if (vlanBlocksByPool.TryGetValue(p.Id, out var blocks))
                foreach (var b in blocks)
                    used += vlanUsedByBlock.TryGetValue(b.Id, out var u) ? u : 0;

            nodes.Add(PoolRow("VlanPool", $"VlanPool:{p.Id}", null, p.Id,
                p.PoolCode, p.DisplayName, $"{p.VlanFirst}–{p.VlanLast}",
                used, capacity, p.Status, p.LockState, p.Version));
        }
        foreach (var b in vlanBlocks)
        {
            long capacity = b.VlanLast - b.VlanFirst + 1;
            long used = vlanUsedByBlock.TryGetValue(b.Id, out var u) ? u : 0;
            nodes.Add(PoolRow("VlanBlock", $"VlanBlock:{b.Id}", $"VlanPool:{b.PoolId}", b.Id,
                b.BlockCode, b.DisplayName, $"{b.VlanFirst}–{b.VlanLast}",
                used, capacity, b.Status, b.LockState, b.Version));
        }

        // ── MLAG ──
        foreach (var p in mlagPools)
        {
            long capacity = p.DomainLast - p.DomainFirst + 1;
            long used = mlagUsedByPool.TryGetValue(p.Id, out var u) ? u : 0;
            nodes.Add(PoolRow("MlagPool", $"MlagPool:{p.Id}", null, p.Id,
                p.PoolCode, p.DisplayName, $"{p.DomainFirst}–{p.DomainLast}",
                used, capacity, p.Status, p.LockState, p.Version));
        }

        // ── MSTP (no "capacity" — priorities aren't a finite pool) ──
        foreach (var r in mstpRules)
        {
            nodes.Add(PoolRow("MstpRule", $"MstpRule:{r.Id}", null, r.Id,
                r.RuleCode, r.DisplayName, "policy",
                0, 0, r.Status, r.LockState, r.Version));
        }

        return nodes;
    }

    private static PoolTreeNode PoolRow(string nodeType, string id, string? parentId,
        Guid entityId, string code, string name, string range,
        long used, long capacity,
        Central.Engine.Net.EntityStatus status,
        Central.Engine.Net.LockState lockState,
        int version)
        => new()
        {
            Id = id,
            ParentId = parentId,
            EntityId = entityId,
            NodeType = nodeType,
            Code = code,
            Name = name,
            Range = range,
            Used = used,
            Capacity = capacity,
            Status = status.ToString(),
            Lock = lockState.ToString(),
            Version = version,
        };

    /// <summary>
    /// Host capacity for a CIDR — /30 and larger subtract network +
    /// broadcast, /31 and /32 use every address (RFC 3021). Falls back
    /// to 0 if the CIDR can't be parsed — utilisation then shows as
    /// empty rather than blowing up the whole load.
    /// </summary>
    private static long SubnetHostCapacity(string cidr)
    {
        try
        {
            var slash = cidr.IndexOf('/');
            if (slash <= 0) return 0;
            if (!int.TryParse(cidr[(slash + 1)..], out var prefix)) return 0;
            var total = 1L << (32 - prefix);
            return prefix >= 31 ? total : total - 2;
        }
        catch { return 0; }
    }

    private static Dictionary<Guid, long> CountBy<T>(IEnumerable<T> src, Func<T, Guid> key)
    {
        var d = new Dictionary<Guid, long>();
        foreach (var item in src)
        {
            var k = key(item);
            d[k] = d.TryGetValue(k, out var v) ? v + 1 : 1;
        }
        return d;
    }

    private static Dictionary<Guid, List<T>> GroupBy<T>(IEnumerable<T> src, Func<T, Guid> key)
    {
        var d = new Dictionary<Guid, List<T>>();
        foreach (var item in src)
        {
            var k = key(item);
            if (!d.TryGetValue(k, out var list))
                d[k] = list = new List<T>();
            list.Add(item);
        }
        return d;
    }

    // ── Context menu ────────────────────────────────────────────────────

    private void OnShowGridMenu(object? sender, GridMenuEventArgs e)
    {
        if (e.MenuType != GridMenuType.RowCell) return;
        var selected = TreeView.FocusedRow as PoolTreeNode;

        // Root-level actions — always available.
        e.Customizations.Add(MakeButton("New ASN pool…",  () => _ = OpenNewPoolAsync("AsnPool")));
        e.Customizations.Add(MakeButton("New IP pool…",   () => _ = OpenNewPoolAsync("IpPool")));
        e.Customizations.Add(MakeButton("New VLAN pool…", () => _ = OpenNewPoolAsync("VlanPool")));
        e.Customizations.Add(MakeButton("New MLAG pool…", () => _ = OpenNewPoolAsync("MlagPool")));
        e.Customizations.Add(MakeButton("New VLAN template…", () => _ = OpenNewPoolAsync("VlanTemplate")));

        if (selected is not null)
        {
            e.Customizations.Add(new BarItemLinkSeparator());
            switch (selected.NodeType)
            {
                case "AsnPool":
                    e.Customizations.Add(MakeButton("New ASN block in this pool…",
                        () => _ = OpenNewBlockAsync("AsnBlock", selected.EntityId)));
                    e.Customizations.Add(MakeButton("Edit ASN pool…",
                        () => _ = OpenEditAsync("AsnPool", selected.EntityId)));
                    break;
                case "AsnBlock":
                    e.Customizations.Add(MakeButton("Allocate ASN from this block…",
                        () => _ = OpenAllocateAsync(AllocateDialog.AllocKind.Asn, selected.EntityId)));
                    e.Customizations.Add(MakeButton("Edit ASN block…",
                        () => _ = OpenEditAsync("AsnBlock", selected.EntityId)));
                    break;
                case "IpPool":
                    e.Customizations.Add(MakeButton("Carve subnet from this pool…",
                        () => _ = OpenAllocateAsync(AllocateDialog.AllocKind.SubnetCarve, selected.EntityId)));
                    e.Customizations.Add(MakeButton("Edit IP pool…",
                        () => _ = OpenEditAsync("IpPool", selected.EntityId)));
                    break;
                case "Subnet":
                    e.Customizations.Add(MakeButton("Allocate IP from this subnet…",
                        () => _ = OpenAllocateAsync(AllocateDialog.AllocKind.Ip, selected.EntityId)));
                    break;
                case "VlanPool":
                    e.Customizations.Add(MakeButton("New VLAN block in this pool…",
                        () => _ = OpenNewBlockAsync("VlanBlock", selected.EntityId)));
                    e.Customizations.Add(MakeButton("Edit VLAN pool…",
                        () => _ = OpenEditAsync("VlanPool", selected.EntityId)));
                    break;
                case "VlanBlock":
                    e.Customizations.Add(MakeButton("Allocate VLAN from this block…",
                        () => _ = OpenAllocateAsync(AllocateDialog.AllocKind.Vlan, selected.EntityId)));
                    break;
                case "MlagPool":
                    e.Customizations.Add(MakeButton("Allocate MLAG domain from this pool…",
                        () => _ = OpenAllocateAsync(AllocateDialog.AllocKind.Mlag, selected.EntityId)));
                    e.Customizations.Add(MakeButton("Edit MLAG pool…",
                        () => _ = OpenEditAsync("MlagPool", selected.EntityId)));
                    break;
            }

            // Delete for every CRUD-able tier.
            var deletable = selected.NodeType is "AsnPool" or "AsnBlock" or "IpPool" or "Subnet"
                or "VlanPool" or "VlanBlock" or "VlanTemplate" or "MlagPool";
            if (deletable)
            {
                e.Customizations.Add(new BarItemLinkSeparator());
                e.Customizations.Add(MakeButton($"Delete {selected.NodeType.ToLower()}",
                    () => _ = DeleteAsync(selected)));
            }
        }
    }

    private static BarButtonItem MakeButton(string text, Action onClick)
    {
        var btn = new BarButtonItem { Content = text };
        btn.ItemClick += (_, _) => onClick();
        return btn;
    }

    private async Task OpenNewPoolAsync(string nodeType)
    {
        if (!EnsureContext()) return;
        var dlg = nodeType switch
        {
            "AsnPool"      => PoolDetailDialog.ForNewAsnPool(_dsn!, _tenantId, _userId),
            "IpPool"       => PoolDetailDialog.ForNewIpPool(_dsn!, _tenantId, _userId),
            "VlanPool"     => PoolDetailDialog.ForNewVlanPool(_dsn!, _tenantId, _userId),
            "MlagPool"     => PoolDetailDialog.ForNewMlagPool(_dsn!, _tenantId, _userId),
            "VlanTemplate" => PoolDetailDialog.ForNewVlanTemplate(_dsn!, _tenantId, _userId),
            _              => throw new ArgumentException(nodeType)
        };
        dlg.Owner = Window.GetWindow(this);
        if (dlg.ShowDialog() == true) await ReloadAsync();
    }

    private async Task OpenNewBlockAsync(string nodeType, Guid parentPoolId)
    {
        if (!EnsureContext()) return;
        PoolDetailDialog dlg = nodeType switch
        {
            "AsnBlock"  => await PoolDetailDialog.ForNewAsnBlockAsync(_dsn!, _tenantId, _userId, parentPoolId),
            "VlanBlock" => await PoolDetailDialog.ForNewVlanBlockAsync(_dsn!, _tenantId, _userId, parentPoolId),
            _           => throw new ArgumentException(nodeType)
        };
        dlg.Owner = Window.GetWindow(this);
        if (dlg.ShowDialog() == true) await ReloadAsync();
    }

    private async Task OpenEditAsync(string nodeType, Guid entityId)
    {
        if (!EnsureContext()) return;
        try
        {
            var dlg = await PoolDetailDialog.ForEditAsync(_dsn!, _tenantId, _userId, nodeType, entityId);
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true) await ReloadAsync();
        }
        catch (Exception ex)
        {
            DevExpress.Xpf.Core.DXMessageBox.Show(Window.GetWindow(this),
                ex.Message, "Load failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task OpenAllocateAsync(AllocateDialog.AllocKind kind, Guid containerId)
    {
        if (!EnsureContext()) return;
        try
        {
            AllocateDialog dlg = kind switch
            {
                AllocateDialog.AllocKind.Asn         => await AllocateDialog.ForAsnAsync(_dsn!, _tenantId, _userId, containerId),
                AllocateDialog.AllocKind.Vlan        => await AllocateDialog.ForVlanAsync(_dsn!, _tenantId, _userId, containerId),
                AllocateDialog.AllocKind.Mlag        => await AllocateDialog.ForMlagAsync(_dsn!, _tenantId, _userId, containerId),
                AllocateDialog.AllocKind.Ip          => await AllocateDialog.ForIpAsync(_dsn!, _tenantId, _userId, containerId),
                AllocateDialog.AllocKind.SubnetCarve => await AllocateDialog.ForSubnetCarveAsync(_dsn!, _tenantId, _userId, containerId),
                _ => throw new ArgumentException(kind.ToString())
            };
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true)
            {
                await ReloadAsync();
                if (!string.IsNullOrEmpty(dlg.AllocatedDisplay))
                    StatusLabel.Text = $"Allocated {dlg.AllocatedDisplay}";
            }
        }
        catch (Exception ex)
        {
            DevExpress.Xpf.Core.DXMessageBox.Show(Window.GetWindow(this),
                ex.Message, "Allocate failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DeleteAsync(PoolTreeNode node)
    {
        if (!EnsureContext()) return;

        var confirm = DevExpress.Xpf.Core.DXMessageBox.Show(
            Window.GetWindow(this),
            $"Delete {node.NodeType.ToLower()} '{node.Code}'?\n\n" +
            "Soft delete — the row is kept in the DB with deleted_at set.",
            $"Delete {node.NodeType.ToLower()}?",
            MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        try
        {
            var repo = new PoolsRepository(_dsn!);
            var ok = node.NodeType switch
            {
                "AsnPool"  => await repo.SoftDeleteAsnPoolAsync(node.EntityId, _tenantId, _userId),
                "AsnBlock" => await repo.SoftDeleteAsnBlockAsync(node.EntityId, _tenantId, _userId),
                "IpPool"   => await repo.SoftDeleteIpPoolAsync(node.EntityId, _tenantId, _userId),
                "Subnet"   => await repo.SoftDeleteSubnetAsync(node.EntityId, _tenantId, _userId),
                _ => throw new NotSupportedException(
                    $"Delete for {node.NodeType} routes through REST — repo write not shipped yet.")
            };
            if (ok) await ReloadAsync();
        }
        catch (Exception ex)
        {
            DevExpress.Xpf.Core.DXMessageBox.Show(Window.GetWindow(this),
                ex.Message, "Delete failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool EnsureContext()
    {
        if (string.IsNullOrEmpty(_dsn) || _tenantId == Guid.Empty)
        {
            StatusLabel.Text = "No tenant context";
            return false;
        }
        return true;
    }
}
