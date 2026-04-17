using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using Central.Engine.Net;
using Central.Engine.Net.Hierarchy;
using Central.Engine.Net.Pools;
using Central.Persistence.Net;
using DevExpress.Xpf.Core;

namespace Central.Module.Networking.Pools;

/// <summary>
/// Single DX dialog that edits every pool / block / template operators
/// routinely CRUD from the tree panel. Type is picked by factory
/// method; per-type sections are shown / hidden based on it.
///
/// Doesn't cover: subnet (use <see cref="AllocateDialog"/>.CarveSubnet),
/// MLAG domain / VLAN / ASN allocations (also AllocateDialog), MSTP
/// rules + steps (their own editor lands with the policy panel).
/// </summary>
public partial class PoolDetailDialog : DXWindow
{
    public enum Mode { New, Edit }

    private readonly string _dsn;
    private readonly Guid _tenantId;
    private readonly int? _userId;
    private readonly Mode _mode;
    private readonly string _nodeType;

    private AsnPool? _asnPool;
    private AsnBlock? _asnBlock;
    private IpPool? _ipPool;
    private VlanPool? _vlanPool;
    private VlanBlock? _vlanBlock;
    private VlanTemplate? _vlanTemplate;
    private MlagDomainPool? _mlagPool;

    public Guid? SavedId { get; private set; }

    private PoolDetailDialog(string dsn, Guid tenantId, int? userId, Mode mode, string nodeType)
    {
        InitializeComponent();
        _dsn = dsn;
        _tenantId = tenantId;
        _userId = userId;
        _mode = mode;
        _nodeType = nodeType;

        StatusCombo.ItemsSource = Enum.GetNames<EntityStatus>();
        LockCombo.ItemsSource = Enum.GetNames<LockState>();
        AsnKindCombo.ItemsSource = Enum.GetNames<AsnKind>();
        IpFamilyCombo.ItemsSource = new[] { "V4", "V6" };
        VlanRoleCombo.ItemsSource = new[]
            { "Management", "Servers", "Voice", "Video", "Guest", "DMZ", "Storage", "Backup" };
        ScopeLevelCombo.ItemsSource = Enum.GetNames<PoolScopeLevel>();
    }

    // ── Factories: New ───────────────────────────────────────────────────

    public static PoolDetailDialog ForNewAsnPool(string dsn, Guid tenantId, int? userId)
    {
        var dlg = new PoolDetailDialog(dsn, tenantId, userId, Mode.New, "AsnPool");
        dlg.TitleLabel.Text = "New ASN pool";
        dlg.SubtitleLabel.Text = "Defines an ASN range from which blocks are carved.";
        dlg.AsnSection.Visibility = Visibility.Visible;
        dlg.AsnKindSection.Visibility = Visibility.Visible;
        dlg._asnPool = new AsnPool { OrganizationId = tenantId, AsnFirst = 64512, AsnLast = 65534 };
        dlg.LoadFromAsnPool();
        return dlg;
    }

    public static async Task<PoolDetailDialog> ForNewAsnBlockAsync(string dsn, Guid tenantId,
        int? userId, Guid? presetPoolId = null)
    {
        var dlg = new PoolDetailDialog(dsn, tenantId, userId, Mode.New, "AsnBlock");
        dlg.TitleLabel.Text = "New ASN block";
        dlg.SubtitleLabel.Text = "Carves a sub-range out of a pool and scopes it.";
        dlg.AsnSection.Visibility = Visibility.Visible;
        dlg.ScopeSection.Visibility = Visibility.Visible;
        dlg.ParentLabel.Text = "ASN pool";
        dlg.ParentSection.Visibility = Visibility.Visible;
        await dlg.LoadAsnPoolsIntoParentAsync(presetPoolId);
        dlg._asnBlock = new AsnBlock { OrganizationId = tenantId, PoolId = presetPoolId ?? Guid.Empty };
        dlg.LoadFromAsnBlock();
        return dlg;
    }

    public static PoolDetailDialog ForNewIpPool(string dsn, Guid tenantId, int? userId)
    {
        var dlg = new PoolDetailDialog(dsn, tenantId, userId, Mode.New, "IpPool");
        dlg.TitleLabel.Text = "New IP pool";
        dlg.SubtitleLabel.Text = "A supernet from which subnets are carved.";
        dlg.IpSection.Visibility = Visibility.Visible;
        dlg._ipPool = new IpPool { OrganizationId = tenantId };
        dlg.LoadFromIpPool();
        return dlg;
    }

    public static PoolDetailDialog ForNewVlanPool(string dsn, Guid tenantId, int? userId)
    {
        var dlg = new PoolDetailDialog(dsn, tenantId, userId, Mode.New, "VlanPool");
        dlg.TitleLabel.Text = "New VLAN pool";
        dlg.SubtitleLabel.Text = "A range inside the 802.1Q space (1-4094).";
        dlg.VlanSection.Visibility = Visibility.Visible;
        dlg._vlanPool = new VlanPool { OrganizationId = tenantId, VlanFirst = 1, VlanLast = 4094 };
        dlg.LoadFromVlanPool();
        return dlg;
    }

    public static async Task<PoolDetailDialog> ForNewVlanBlockAsync(string dsn, Guid tenantId,
        int? userId, Guid? presetPoolId = null)
    {
        var dlg = new PoolDetailDialog(dsn, tenantId, userId, Mode.New, "VlanBlock");
        dlg.TitleLabel.Text = "New VLAN block";
        dlg.SubtitleLabel.Text = "A sub-range carved from a VLAN pool.";
        dlg.VlanSection.Visibility = Visibility.Visible;
        dlg.ScopeSection.Visibility = Visibility.Visible;
        dlg.ParentLabel.Text = "VLAN pool";
        dlg.ParentSection.Visibility = Visibility.Visible;
        await dlg.LoadVlanPoolsIntoParentAsync(presetPoolId);
        dlg._vlanBlock = new VlanBlock { OrganizationId = tenantId, PoolId = presetPoolId ?? Guid.Empty };
        dlg.LoadFromVlanBlock();
        return dlg;
    }

    public static PoolDetailDialog ForNewVlanTemplate(string dsn, Guid tenantId, int? userId)
    {
        var dlg = new PoolDetailDialog(dsn, tenantId, userId, Mode.New, "VlanTemplate");
        dlg.TitleLabel.Text = "New VLAN template";
        dlg.SubtitleLabel.Text = "Reusable VLAN pattern — attach at allocation time.";
        dlg.VlanTemplateSection.Visibility = Visibility.Visible;
        dlg._vlanTemplate = new VlanTemplate { OrganizationId = tenantId };
        dlg.LoadFromVlanTemplate();
        return dlg;
    }

    public static PoolDetailDialog ForNewMlagPool(string dsn, Guid tenantId, int? userId)
    {
        var dlg = new PoolDetailDialog(dsn, tenantId, userId, Mode.New, "MlagPool");
        dlg.TitleLabel.Text = "New MLAG pool";
        dlg.SubtitleLabel.Text = "Range of MLAG domain IDs. Tenant-wide uniqueness.";
        dlg.MlagSection.Visibility = Visibility.Visible;
        dlg._mlagPool = new MlagDomainPool { OrganizationId = tenantId, DomainFirst = 1, DomainLast = 4094 };
        dlg.LoadFromMlagPool();
        return dlg;
    }

    // ── Factory: Edit ────────────────────────────────────────────────────

    public static async Task<PoolDetailDialog> ForEditAsync(string dsn, Guid tenantId, int? userId,
        string nodeType, Guid entityId)
    {
        var dlg = new PoolDetailDialog(dsn, tenantId, userId, Mode.Edit, nodeType);
        dlg.TitleLabel.Text = $"Edit {PrettyType(nodeType)}";
        var repo = new PoolsRepository(dsn);

        switch (nodeType)
        {
            case "AsnPool":
                dlg._asnPool = await repo.GetAsnPoolAsync(entityId, tenantId)
                    ?? throw new InvalidOperationException($"ASN pool {entityId} not found");
                dlg.AsnSection.Visibility = Visibility.Visible;
                dlg.AsnKindSection.Visibility = Visibility.Visible;
                dlg.SubtitleLabel.Text = $"ASN pool · v{dlg._asnPool.Version}";
                dlg.LoadFromAsnPool();
                break;
            case "AsnBlock":
                dlg._asnBlock = await repo.GetAsnBlockAsync(entityId, tenantId)
                    ?? throw new InvalidOperationException($"ASN block {entityId} not found");
                dlg.AsnSection.Visibility = Visibility.Visible;
                dlg.ScopeSection.Visibility = Visibility.Visible;
                dlg.ParentLabel.Text = "ASN pool";
                dlg.ParentSection.Visibility = Visibility.Visible;
                await dlg.LoadAsnPoolsIntoParentAsync(dlg._asnBlock.PoolId);
                dlg.ParentCombo.IsEnabled = false;
                dlg.SubtitleLabel.Text = $"ASN block · v{dlg._asnBlock.Version}";
                dlg.LoadFromAsnBlock();
                break;
            case "IpPool":
                dlg._ipPool = await repo.GetIpPoolAsync(entityId, tenantId)
                    ?? throw new InvalidOperationException($"IP pool {entityId} not found");
                dlg.IpSection.Visibility = Visibility.Visible;
                dlg.SubtitleLabel.Text = $"IP pool · v{dlg._ipPool.Version}";
                dlg.LoadFromIpPool();
                break;
            case "VlanPool":
                dlg._vlanPool = await repo.GetVlanPoolAsync(entityId, tenantId)
                    ?? throw new InvalidOperationException($"VLAN pool {entityId} not found");
                dlg.VlanSection.Visibility = Visibility.Visible;
                dlg.SubtitleLabel.Text = $"VLAN pool · v{dlg._vlanPool.Version}";
                dlg.LoadFromVlanPool();
                break;
            case "VlanTemplate":
                dlg._vlanTemplate = await repo.GetVlanTemplateAsync(entityId, tenantId)
                    ?? throw new InvalidOperationException($"VLAN template {entityId} not found");
                dlg.VlanTemplateSection.Visibility = Visibility.Visible;
                dlg.SubtitleLabel.Text = $"VLAN template · v{dlg._vlanTemplate.Version}";
                dlg.LoadFromVlanTemplate();
                break;
            case "MlagPool":
                dlg._mlagPool = await repo.GetMlagPoolAsync(entityId, tenantId)
                    ?? throw new InvalidOperationException($"MLAG pool {entityId} not found");
                dlg.MlagSection.Visibility = Visibility.Visible;
                dlg.SubtitleLabel.Text = $"MLAG pool · v{dlg._mlagPool.Version}";
                dlg.LoadFromMlagPool();
                break;
            default:
                throw new NotSupportedException($"Editing {nodeType} is not supported here.");
        }

        dlg.MetaSection.Visibility = Visibility.Visible;
        dlg.MetaLabel.Text = dlg.BuildMeta();
        return dlg;
    }

    // ── Parent loaders ───────────────────────────────────────────────────

    private async Task LoadAsnPoolsIntoParentAsync(Guid? preset)
    {
        var repo = new PoolsRepository(_dsn);
        var pools = await repo.ListAsnPoolsAsync(_tenantId);
        ParentCombo.ItemsSource = pools
            .Select(p => new ParentOption(p.Id, $"{p.PoolCode} ({p.AsnFirst}-{p.AsnLast})"))
            .ToList();
        if (preset is Guid g && g != Guid.Empty) ParentCombo.EditValue = g;
    }

    private async Task LoadVlanPoolsIntoParentAsync(Guid? preset)
    {
        var repo = new PoolsRepository(_dsn);
        var pools = await repo.ListVlanPoolsAsync(_tenantId);
        ParentCombo.ItemsSource = pools
            .Select(p => new ParentOption(p.Id, $"{p.PoolCode} ({p.VlanFirst}-{p.VlanLast})"))
            .ToList();
        if (preset is Guid g && g != Guid.Empty) ParentCombo.EditValue = g;
    }

    // ── Load from entity ─────────────────────────────────────────────────

    private void LoadFromAsnPool()
    {
        var p = _asnPool!;
        CodeBox.Text = p.PoolCode;
        NameBox.Text = p.DisplayName;
        StatusCombo.EditValue = p.Status.ToString();
        LockCombo.EditValue = p.LockState.ToString();
        AsnFirstBox.EditValue = p.AsnFirst;
        AsnLastBox.EditValue = p.AsnLast;
        AsnKindCombo.EditValue = p.AsnKind.ToString();
        NotesBox.Text = p.Notes ?? "";
    }

    private void LoadFromAsnBlock()
    {
        var b = _asnBlock!;
        CodeBox.Text = b.BlockCode;
        NameBox.Text = b.DisplayName;
        StatusCombo.EditValue = b.Status.ToString();
        LockCombo.EditValue = b.LockState.ToString();
        AsnFirstBox.EditValue = b.AsnFirst;
        AsnLastBox.EditValue = b.AsnLast;
        ScopeLevelCombo.EditValue = b.ScopeLevel.ToString();
        NotesBox.Text = b.Notes ?? "";
    }

    private void LoadFromIpPool()
    {
        var p = _ipPool!;
        CodeBox.Text = p.PoolCode;
        NameBox.Text = p.DisplayName;
        StatusCombo.EditValue = p.Status.ToString();
        LockCombo.EditValue = p.LockState.ToString();
        IpNetworkBox.Text = p.Network;
        IpFamilyCombo.EditValue = p.AddressFamily.ToString();
        NotesBox.Text = p.Notes ?? "";
    }

    private void LoadFromVlanPool()
    {
        var p = _vlanPool!;
        CodeBox.Text = p.PoolCode;
        NameBox.Text = p.DisplayName;
        StatusCombo.EditValue = p.Status.ToString();
        LockCombo.EditValue = p.LockState.ToString();
        VlanFirstBox.EditValue = p.VlanFirst;
        VlanLastBox.EditValue = p.VlanLast;
        NotesBox.Text = p.Notes ?? "";
    }

    private void LoadFromVlanBlock()
    {
        var b = _vlanBlock!;
        CodeBox.Text = b.BlockCode;
        NameBox.Text = b.DisplayName;
        StatusCombo.EditValue = b.Status.ToString();
        LockCombo.EditValue = b.LockState.ToString();
        VlanFirstBox.EditValue = b.VlanFirst;
        VlanLastBox.EditValue = b.VlanLast;
        ScopeLevelCombo.EditValue = b.ScopeLevel.ToString();
        NotesBox.Text = b.Notes ?? "";
    }

    private void LoadFromVlanTemplate()
    {
        var t = _vlanTemplate!;
        CodeBox.Text = t.TemplateCode;
        NameBox.Text = t.DisplayName;
        StatusCombo.EditValue = t.Status.ToString();
        LockCombo.EditValue = t.LockState.ToString();
        VlanRoleCombo.EditValue = t.VlanRole;
        VlanTemplateDescBox.Text = t.Description ?? "";
        VlanTemplateDefaultCheck.IsChecked = t.IsDefault;
        NotesBox.Text = t.Notes ?? "";
    }

    private void LoadFromMlagPool()
    {
        var p = _mlagPool!;
        CodeBox.Text = p.PoolCode;
        NameBox.Text = p.DisplayName;
        StatusCombo.EditValue = p.Status.ToString();
        LockCombo.EditValue = p.LockState.ToString();
        MlagFirstBox.EditValue = p.DomainFirst;
        MlagLastBox.EditValue = p.DomainLast;
        NotesBox.Text = p.Notes ?? "";
    }

    // ── Save ─────────────────────────────────────────────────────────────

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveButton.IsEnabled = false;
            var repo = new PoolsRepository(_dsn);
            switch (_nodeType)
            {
                case "AsnPool":      await SaveAsnPoolAsync(repo); break;
                case "AsnBlock":     await SaveAsnBlockAsync(repo); break;
                case "IpPool":       await SaveIpPoolAsync(repo); break;
                case "VlanPool":     await SaveVlanPoolAsync(repo); break;
                case "VlanBlock":    await SaveVlanBlockAsync(repo); break;
                case "VlanTemplate": throw new NotImplementedException(
                    "VLAN template CRUD routes through REST in Phase 3e; repo write lands later.");
                case "MlagPool":     throw new NotImplementedException(
                    "MLAG pool write routes through REST in Phase 3e; repo write lands later.");
            }
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            DXMessageBox.Show(this, ex.Message, "Save failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { SaveButton.IsEnabled = true; }
    }

    private async Task SaveAsnPoolAsync(PoolsRepository repo)
    {
        var p = _asnPool!;
        p.PoolCode = CodeBox.Text.Trim();
        p.DisplayName = NameBox.Text.Trim();
        p.Status = ParseStatus();
        p.LockState = ParseLock();
        p.AsnFirst = ToInt64(AsnFirstBox.EditValue);
        p.AsnLast = ToInt64(AsnLastBox.EditValue);
        p.AsnKind = Enum.TryParse<AsnKind>(AsnKindCombo.EditValue as string, out var k) ? k : AsnKind.Private2;
        p.Notes = Nullable(NotesBox.Text);
        if (string.IsNullOrEmpty(p.PoolCode) || string.IsNullOrEmpty(p.DisplayName))
            throw new InvalidOperationException("Code and display name are required.");
        if (p.AsnFirst > p.AsnLast)
            throw new InvalidOperationException("First ASN must be <= last ASN.");

        SeedBase(p);
        SavedId = _mode == Mode.New
            ? await repo.CreateAsnPoolAsync(p, _userId)
            : (await repo.UpdateAsnPoolAsync(p, _userId) > 0 ? (Guid?)p.Id : null);
        if (SavedId is null) throw new InvalidOperationException("Update affected no rows (version conflict).");
        SavedId ??= p.Id;
    }

    private async Task SaveAsnBlockAsync(PoolsRepository repo)
    {
        var b = _asnBlock!;
        b.BlockCode = CodeBox.Text.Trim();
        b.DisplayName = NameBox.Text.Trim();
        b.Status = ParseStatus();
        b.LockState = ParseLock();
        b.AsnFirst = ToInt64(AsnFirstBox.EditValue);
        b.AsnLast = ToInt64(AsnLastBox.EditValue);
        b.ScopeLevel = Enum.TryParse<PoolScopeLevel>(ScopeLevelCombo.EditValue as string, out var s)
            ? s : PoolScopeLevel.Free;
        b.Notes = Nullable(NotesBox.Text);
        if (_mode == Mode.New)
        {
            if (ParentCombo.EditValue is not Guid poolId || poolId == Guid.Empty)
                throw new InvalidOperationException("ASN pool is required.");
            b.PoolId = poolId;
        }
        if (string.IsNullOrEmpty(b.BlockCode))
            throw new InvalidOperationException("Code is required.");
        if (b.AsnFirst > b.AsnLast)
            throw new InvalidOperationException("First ASN must be <= last ASN.");

        SeedBase(b);
        SavedId = _mode == Mode.New
            ? await repo.CreateAsnBlockAsync(b, _userId)
            : await repo.UpdateAsnBlockAsync(b, _userId) > 0 ? b.Id : null;
        if (SavedId is null) throw new InvalidOperationException("Update affected no rows.");
    }

    private async Task SaveIpPoolAsync(PoolsRepository repo)
    {
        var p = _ipPool!;
        p.PoolCode = CodeBox.Text.Trim();
        p.DisplayName = NameBox.Text.Trim();
        p.Status = ParseStatus();
        p.LockState = ParseLock();
        p.Network = IpNetworkBox.Text.Trim();
        p.AddressFamily = (IpFamilyCombo.EditValue as string) == "V6"
            ? IpAddressFamily.V6 : IpAddressFamily.V4;
        p.Notes = Nullable(NotesBox.Text);
        if (string.IsNullOrEmpty(p.PoolCode) || string.IsNullOrEmpty(p.DisplayName))
            throw new InvalidOperationException("Code and display name are required.");
        if (string.IsNullOrEmpty(p.Network))
            throw new InvalidOperationException("Network (CIDR) is required.");

        SeedBase(p);
        SavedId = _mode == Mode.New
            ? await repo.CreateIpPoolAsync(p, _userId)
            : await repo.UpdateIpPoolAsync(p, _userId) > 0 ? p.Id : null;
        if (SavedId is null) throw new InvalidOperationException("Update affected no rows.");
    }

    private Task SaveVlanPoolAsync(PoolsRepository repo)
    {
        // VLAN pool repo writes aren't on PoolsRepository yet — Phase 3b
        // only shipped reads for the VLAN pool tier. Surface this cleanly
        // instead of pretending to succeed.
        throw new NotImplementedException(
            "VLAN pool CRUD routes through REST (Phase 3e). Repository writes land with the VLAN editor.");
    }

    private Task SaveVlanBlockAsync(PoolsRepository repo)
    {
        throw new NotImplementedException(
            "VLAN block CRUD routes through REST (Phase 3e). Repository writes land with the VLAN editor.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static void SeedBase(EntityBase e)
    {
        e.Tags ??= new JsonObject();
        e.ExternalRefs ??= new JsonArray();
    }

    private EntityStatus ParseStatus()
        => Enum.TryParse<EntityStatus>(StatusCombo.EditValue as string, out var s) ? s : EntityStatus.Planned;
    private LockState ParseLock()
        => Enum.TryParse<LockState>(LockCombo.EditValue as string, out var l) ? l : LockState.Open;
    private static string? Nullable(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static long ToInt64(object? v) => v switch
    {
        null => 0L,
        long l => l,
        int i => i,
        decimal d => (long)d,
        _ => long.TryParse(v.ToString(), out var n) ? n : 0L,
    };

    private string BuildMeta()
    {
        EntityBase? e = _asnPool as EntityBase ?? _asnBlock as EntityBase ?? _ipPool as EntityBase
                        ?? _vlanPool as EntityBase ?? _vlanBlock as EntityBase
                        ?? _vlanTemplate as EntityBase ?? _mlagPool;
        if (e is null) return "";
        return $"v{e.Version} · created {e.CreatedAt:yyyy-MM-dd HH:mm} · updated {e.UpdatedAt:yyyy-MM-dd HH:mm}";
    }

    private static string PrettyType(string t) => t switch
    {
        "AsnPool"      => "ASN pool",
        "AsnBlock"     => "ASN block",
        "IpPool"       => "IP pool",
        "VlanPool"     => "VLAN pool",
        "VlanBlock"    => "VLAN block",
        "VlanTemplate" => "VLAN template",
        "MlagPool"     => "MLAG pool",
        _              => t
    };

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private sealed class ParentOption
    {
        public Guid Id { get; }
        public string DisplayText { get; }
        public ParentOption(Guid id, string text) { Id = id; DisplayText = text; }
        public override string ToString() => DisplayText;
    }
}
