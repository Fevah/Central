using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Central.Engine.Net.Pools;
using Central.Persistence.Net;
using DevExpress.Xpf.Core;

namespace Central.Module.Networking.Pools;

/// <summary>
/// One dialog, five allocation modes:
/// <list type="bullet">
///   <item>ASN   — pick block, set consumer type/id</item>
///   <item>VLAN  — pick block, set name + optional template + scope</item>
///   <item>MLAG  — pick pool, set name + scope</item>
///   <item>IP    — pick subnet, optional assigned-to</item>
///   <item>Subnet-carve — pick pool, prefix length, code + name, scope</item>
/// </list>
///
/// All five routes go through <see cref="AllocationService"/> or
/// <see cref="IpAllocationService"/> so the invariants hold
/// (advisory lock, shelf cool-down, range containment).
/// </summary>
public partial class AllocateDialog : DXWindow
{
    public enum AllocKind { Asn, Vlan, Mlag, Ip, SubnetCarve }

    private readonly string _dsn;
    private readonly Guid _tenantId;
    private readonly int? _userId;
    private readonly AllocKind _kind;

    public Guid? AllocatedId { get; private set; }
    public string? AllocatedDisplay { get; private set; }

    private AllocateDialog(string dsn, Guid tenantId, int? userId, AllocKind kind)
    {
        InitializeComponent();
        _dsn = dsn;
        _tenantId = tenantId;
        _userId = userId;
        _kind = kind;

        AsnConsumerTypeCombo.ItemsSource = new[] { "Device", "Server", "Building" };
        IpAssignedTypeCombo.ItemsSource = new[] { "Device", "ServerNic", "Vrrp", "Gateway", "Reserved" };
        ScopeLevelCombo.ItemsSource = Enum.GetNames<PoolScopeLevel>();
    }

    // ── Factories ────────────────────────────────────────────────────────

    public static async Task<AllocateDialog> ForAsnAsync(string dsn, Guid tenantId, int? userId,
        Guid? presetBlockId = null)
    {
        var dlg = new AllocateDialog(dsn, tenantId, userId, AllocKind.Asn);
        dlg.TitleLabel.Text = "Allocate ASN";
        dlg.SubtitleLabel.Text = "Next free ASN in the chosen block.";
        dlg.ContainerLabel.Text = "ASN block";
        dlg.AsnSection.Visibility = Visibility.Visible;
        dlg.AsnConsumerTypeCombo.EditValue = "Device";
        dlg.HintLabel.Text = "Shelved values are skipped. Allocation is serialised by per-block advisory lock.";
        await dlg.LoadAsnBlocksAsync(presetBlockId);
        return dlg;
    }

    public static async Task<AllocateDialog> ForVlanAsync(string dsn, Guid tenantId, int? userId,
        Guid? presetBlockId = null)
    {
        var dlg = new AllocateDialog(dsn, tenantId, userId, AllocKind.Vlan);
        dlg.TitleLabel.Text = "Allocate VLAN";
        dlg.SubtitleLabel.Text = "Next free VLAN ID in the chosen block.";
        dlg.ContainerLabel.Text = "VLAN block";
        dlg.NameSection.Visibility = Visibility.Visible;
        dlg.VlanSection.Visibility = Visibility.Visible;
        dlg.ScopeSection.Visibility = Visibility.Visible;
        dlg.ScopeLevelCombo.EditValue = PoolScopeLevel.Free.ToString();
        dlg.HintLabel.Text = "Pick a template to inherit role + description for config generation.";
        await dlg.LoadVlanBlocksAsync(presetBlockId);
        await dlg.LoadVlanTemplatesAsync();
        return dlg;
    }

    public static async Task<AllocateDialog> ForMlagAsync(string dsn, Guid tenantId, int? userId,
        Guid? presetPoolId = null)
    {
        var dlg = new AllocateDialog(dsn, tenantId, userId, AllocKind.Mlag);
        dlg.TitleLabel.Text = "Allocate MLAG domain";
        dlg.SubtitleLabel.Text = "Next free MLAG domain ID in the chosen pool.";
        dlg.ContainerLabel.Text = "MLAG pool";
        dlg.NameSection.Visibility = Visibility.Visible;
        dlg.ScopeSection.Visibility = Visibility.Visible;
        dlg.ScopeLevelCombo.EditValue = PoolScopeLevel.Building.ToString();
        dlg.HintLabel.Text = "MLAG domain IDs are tenant-wide unique — two buildings can't share one.";
        await dlg.LoadMlagPoolsAsync(presetPoolId);
        return dlg;
    }

    public static async Task<AllocateDialog> ForIpAsync(string dsn, Guid tenantId, int? userId,
        Guid? presetSubnetId = null)
    {
        var dlg = new AllocateDialog(dsn, tenantId, userId, AllocKind.Ip);
        dlg.TitleLabel.Text = "Allocate IP address";
        dlg.SubtitleLabel.Text = "Next free host address in the chosen subnet.";
        dlg.ContainerLabel.Text = "Subnet";
        dlg.IpSection.Visibility = Visibility.Visible;
        dlg.HintLabel.Text = "/30+ skip network and broadcast. /31 and /32 use every bit (RFC 3021).";
        await dlg.LoadSubnetsAsync(presetSubnetId);
        return dlg;
    }

    public static async Task<AllocateDialog> ForSubnetCarveAsync(string dsn, Guid tenantId, int? userId,
        Guid? presetPoolId = null)
    {
        var dlg = new AllocateDialog(dsn, tenantId, userId, AllocKind.SubnetCarve);
        dlg.TitleLabel.Text = "Carve subnet";
        dlg.SubtitleLabel.Text = "Next free aligned /N inside the chosen pool.";
        dlg.ContainerLabel.Text = "IP pool";
        dlg.SubnetCarveSection.Visibility = Visibility.Visible;
        dlg.ScopeSection.Visibility = Visibility.Visible;
        dlg.ScopeLevelCombo.EditValue = PoolScopeLevel.Free.ToString();
        dlg.PrefixBox.EditValue = 30;
        dlg.HintLabel.Text = "Candidate is aligned to its prefix boundary. DB GIST EXCLUDE is the final overlap guard.";
        await dlg.LoadIpPoolsAsync(presetPoolId);
        return dlg;
    }

    // ── Container loaders ────────────────────────────────────────────────

    private async Task LoadAsnBlocksAsync(Guid? preset)
    {
        var repo = new PoolsRepository(_dsn);
        var blocks = await repo.ListAsnBlocksAsync(_tenantId, null);
        ContainerCombo.ItemsSource = blocks
            .Select(b => new Option(b.Id, $"{b.BlockCode} ({b.AsnFirst}-{b.AsnLast})"))
            .ToList();
        if (preset is Guid g && g != Guid.Empty) ContainerCombo.EditValue = g;
    }

    private async Task LoadVlanBlocksAsync(Guid? preset)
    {
        var repo = new PoolsRepository(_dsn);
        var blocks = await repo.ListVlanBlocksAsync(_tenantId, null);
        ContainerCombo.ItemsSource = blocks
            .Select(b => new Option(b.Id, $"{b.BlockCode} ({b.VlanFirst}-{b.VlanLast})"))
            .ToList();
        if (preset is Guid g && g != Guid.Empty) ContainerCombo.EditValue = g;
    }

    private async Task LoadVlanTemplatesAsync()
    {
        var repo = new PoolsRepository(_dsn);
        var templates = await repo.ListVlanTemplatesAsync(_tenantId);
        var opts = templates
            .Select(t => new Option(t.Id, $"{t.TemplateCode} — {t.VlanRole}"))
            .ToList();
        opts.Insert(0, new Option(Guid.Empty, "(none)"));
        VlanTemplateCombo.ItemsSource = opts;
        VlanTemplateCombo.EditValue = Guid.Empty;
    }

    private async Task LoadMlagPoolsAsync(Guid? preset)
    {
        var repo = new PoolsRepository(_dsn);
        var pools = await repo.ListMlagPoolsAsync(_tenantId);
        ContainerCombo.ItemsSource = pools
            .Select(p => new Option(p.Id, $"{p.PoolCode} ({p.DomainFirst}-{p.DomainLast})"))
            .ToList();
        if (preset is Guid g && g != Guid.Empty) ContainerCombo.EditValue = g;
    }

    private async Task LoadSubnetsAsync(Guid? preset)
    {
        var repo = new PoolsRepository(_dsn);
        var subnets = await repo.ListSubnetsAsync(_tenantId, null);
        ContainerCombo.ItemsSource = subnets
            .Select(s => new Option(s.Id, $"{s.SubnetCode} — {s.Network}"))
            .ToList();
        if (preset is Guid g && g != Guid.Empty) ContainerCombo.EditValue = g;
    }

    private async Task LoadIpPoolsAsync(Guid? preset)
    {
        var repo = new PoolsRepository(_dsn);
        var pools = await repo.ListIpPoolsAsync(_tenantId);
        ContainerCombo.ItemsSource = pools
            .Select(p => new Option(p.Id, $"{p.PoolCode} — {p.Network}"))
            .ToList();
        if (preset is Guid g && g != Guid.Empty) ContainerCombo.EditValue = g;
    }

    // ── Save ─────────────────────────────────────────────────────────────

    private async void Allocate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AllocateButton.IsEnabled = false;
            if (ContainerCombo.EditValue is not Guid containerId || containerId == Guid.Empty)
                throw new InvalidOperationException("Select a container first.");

            var alloc = new AllocationService(_dsn);
            var ipAlloc = new IpAllocationService(_dsn);

            switch (_kind)
            {
                case AllocKind.Asn:
                {
                    var type = (AsnConsumerTypeCombo.EditValue as string)?.Trim();
                    if (string.IsNullOrEmpty(type)) throw new InvalidOperationException("Consumer type is required.");
                    if (!Guid.TryParse(AsnConsumerIdBox.Text.Trim(), out var cid))
                        throw new InvalidOperationException("Consumer ID must be a valid GUID.");
                    var r = await alloc.AllocateAsnAsync(containerId, _tenantId, type, cid, _userId);
                    AllocatedId = r.Id;
                    AllocatedDisplay = $"ASN {r.Asn}";
                    break;
                }
                case AllocKind.Vlan:
                {
                    var name = NameBox.Text.Trim();
                    if (string.IsNullOrEmpty(name)) throw new InvalidOperationException("Display name is required.");
                    if (!Enum.TryParse<PoolScopeLevel>(ScopeLevelCombo.EditValue as string, out var scope))
                        scope = PoolScopeLevel.Free;
                    Guid? templateId = null;
                    if (VlanTemplateCombo.EditValue is Guid tid && tid != Guid.Empty) templateId = tid;
                    var desc = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim();

                    var v = await alloc.AllocateVlanAsync(containerId, _tenantId, name, desc,
                        scope, null, templateId, _userId);
                    AllocatedId = v.Id;
                    AllocatedDisplay = $"VLAN {v.VlanId}";
                    break;
                }
                case AllocKind.Mlag:
                {
                    var name = NameBox.Text.Trim();
                    if (string.IsNullOrEmpty(name)) throw new InvalidOperationException("Display name is required.");
                    if (!Enum.TryParse<PoolScopeLevel>(ScopeLevelCombo.EditValue as string, out var scope))
                        scope = PoolScopeLevel.Building;
                    var d = await alloc.AllocateMlagDomainAsync(containerId, _tenantId, name,
                        scope, null, _userId);
                    AllocatedId = d.Id;
                    AllocatedDisplay = $"MLAG domain {d.DomainId}";
                    break;
                }
                case AllocKind.Ip:
                {
                    var type = string.IsNullOrWhiteSpace(IpAssignedTypeCombo.EditValue as string)
                        ? null
                        : (IpAssignedTypeCombo.EditValue as string)?.Trim();
                    Guid? id = null;
                    if (!string.IsNullOrWhiteSpace(IpAssignedIdBox.Text))
                    {
                        if (!Guid.TryParse(IpAssignedIdBox.Text.Trim(), out var parsed))
                            throw new InvalidOperationException("Assigned-to ID must be a valid GUID (or leave blank).");
                        id = parsed;
                    }
                    var a = await ipAlloc.AllocateNextIpAsync(containerId, _tenantId, type, id, _userId);
                    AllocatedId = a.Id;
                    AllocatedDisplay = $"IP {a.Address}";
                    break;
                }
                case AllocKind.SubnetCarve:
                {
                    var code = SubnetCodeBox.Text.Trim();
                    var name = SubnetNameBox.Text.Trim();
                    if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
                        throw new InvalidOperationException("Subnet code and display name are required.");
                    var prefix = (int)(PrefixBox.EditValue is int pi ? pi
                        : PrefixBox.EditValue is decimal pd ? (int)pd : 30);
                    if (!Enum.TryParse<PoolScopeLevel>(ScopeLevelCombo.EditValue as string, out var scope))
                        scope = PoolScopeLevel.Free;
                    var s = await ipAlloc.AllocateSubnetAsync(containerId, _tenantId, prefix,
                        code, name, scope, null, parentSubnetId: null, _userId);
                    AllocatedId = s.Id;
                    AllocatedDisplay = $"Subnet {s.Network}";
                    break;
                }
            }

            DialogResult = true;
            Close();
        }
        catch (PoolExhaustedException ex)
        {
            DXMessageBox.Show(this, ex.Message, "Pool exhausted",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (AllocationRangeException ex)
        {
            DXMessageBox.Show(this, ex.Message, "Invalid range",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (AllocationContainerNotFoundException ex)
        {
            DXMessageBox.Show(this, ex.Message, "Container not found",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            DXMessageBox.Show(this, ex.Message, "Allocate failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { AllocateButton.IsEnabled = true; }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private sealed class Option
    {
        public Guid Id { get; }
        public string DisplayText { get; }
        public Option(Guid id, string text) { Id = id; DisplayText = text; }
        public override string ToString() => DisplayText;
    }
}
