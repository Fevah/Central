using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using Central.Engine.Net;
using Central.Engine.Net.Hierarchy;
using Central.Persistence.Net;
using DevExpress.Xpf.Core;
using Region = Central.Engine.Net.Hierarchy.Region;

namespace Central.Module.Networking.Hierarchy;

/// <summary>
/// Single DX dialog that edits any of the three "top-heavy CRUD" levels
/// of the geographic hierarchy — Region, Site, Building. Floor / Room /
/// Rack will get their own editors when Phase 2e lands.
///
/// The dialog is lightweight on purpose: only the fields a human
/// actually sets. Phase-3 pool assignments (subnet / ASN / loopback)
/// are owned by the allocation service and are read-only here.
/// </summary>
public partial class HierarchyDetailDialog : DXWindow
{
    public enum Mode { New, Edit }

    private readonly string _dsn;
    private readonly Guid _tenantId;
    private readonly int? _userId;
    private readonly Mode _mode;
    private readonly string _nodeType;    // Region / Site / Building

    private Region? _region;
    private Site? _site;
    private Building? _building;

    public Guid? SavedId { get; private set; }

    private HierarchyDetailDialog(string dsn, Guid tenantId, int? userId,
        Mode mode, string nodeType)
    {
        InitializeComponent();
        _dsn = dsn;
        _tenantId = tenantId;
        _userId = userId;
        _mode = mode;
        _nodeType = nodeType;

        PopulateEnumCombos();
    }

    // ── Factory entry points ─────────────────────────────────────────────

    public static HierarchyDetailDialog ForNewRegion(string dsn, Guid tenantId, int? userId)
    {
        var dlg = new HierarchyDetailDialog(dsn, tenantId, userId, Mode.New, "Region");
        dlg.TitleLabel.Text = "New region";
        dlg.SubtitleLabel.Text = "Top level of the geographic hierarchy.";
        dlg.RegionSection.Visibility = Visibility.Visible;
        dlg._region = new Region { OrganizationId = tenantId, B2bMeshPolicy = "None" };
        dlg.LoadFromRegion();
        return dlg;
    }

    public static async Task<HierarchyDetailDialog> ForNewSiteAsync(string dsn, Guid tenantId, int? userId,
        Guid? presetRegionId = null)
    {
        var dlg = new HierarchyDetailDialog(dsn, tenantId, userId, Mode.New, "Site");
        dlg.TitleLabel.Text = "New site";
        dlg.SubtitleLabel.Text = "A site sits inside a region and holds buildings.";
        dlg.SiteSection.Visibility = Visibility.Visible;
        dlg.ParentLabel.Text = "Region";
        dlg.ParentSection.Visibility = Visibility.Visible;
        await dlg.LoadParentRegionsAsync(presetRegionId);
        dlg._site = new Site { OrganizationId = tenantId, RegionId = presetRegionId ?? Guid.Empty };
        dlg.LoadFromSite();
        return dlg;
    }

    public static async Task<HierarchyDetailDialog> ForNewBuildingAsync(string dsn, Guid tenantId, int? userId,
        Guid? presetSiteId = null)
    {
        var dlg = new HierarchyDetailDialog(dsn, tenantId, userId, Mode.New, "Building");
        dlg.TitleLabel.Text = "New building";
        dlg.SubtitleLabel.Text = "A building sits inside a site.";
        dlg.BuildingSection.Visibility = Visibility.Visible;
        dlg.ParentLabel.Text = "Site";
        dlg.ParentSection.Visibility = Visibility.Visible;
        await dlg.LoadParentSitesAsync(presetSiteId);
        dlg._building = new Building { OrganizationId = tenantId, SiteId = presetSiteId ?? Guid.Empty };
        dlg.LoadFromBuilding();
        return dlg;
    }

    public static async Task<HierarchyDetailDialog> ForEditAsync(string dsn, Guid tenantId, int? userId,
        string nodeType, Guid entityId)
    {
        var dlg = new HierarchyDetailDialog(dsn, tenantId, userId, Mode.Edit, nodeType);
        dlg.TitleLabel.Text = $"Edit {nodeType.ToLower()}";
        var repo = new HierarchyRepository(dsn);

        switch (nodeType)
        {
            case "Region":
                dlg._region = await repo.GetRegionAsync(entityId, tenantId);
                if (dlg._region is null) throw new InvalidOperationException($"Region {entityId} not found");
                dlg.RegionSection.Visibility = Visibility.Visible;
                dlg.SubtitleLabel.Text = $"Region · v{dlg._region.Version}";
                dlg.LoadFromRegion();
                break;
            case "Site":
                dlg._site = await repo.GetSiteAsync(entityId, tenantId);
                if (dlg._site is null) throw new InvalidOperationException($"Site {entityId} not found");
                dlg.SiteSection.Visibility = Visibility.Visible;
                dlg.SubtitleLabel.Text = $"Site · v{dlg._site.Version}";
                dlg.LoadFromSite();
                break;
            case "Building":
                dlg._building = await repo.GetBuildingAsync(entityId, tenantId);
                if (dlg._building is null) throw new InvalidOperationException($"Building {entityId} not found");
                dlg.BuildingSection.Visibility = Visibility.Visible;
                dlg.SubtitleLabel.Text = $"Building · v{dlg._building.Version}";
                dlg.LoadFromBuilding();
                break;
            default:
                throw new NotSupportedException($"Editing {nodeType} is not yet supported.");
        }

        dlg.MetaSection.Visibility = Visibility.Visible;
        dlg.MetaLabel.Text = dlg.BuildMetaString();
        return dlg;
    }

    // ── Combo / parent loading ───────────────────────────────────────────

    private void PopulateEnumCombos()
    {
        StatusCombo.ItemsSource = Enum.GetNames<EntityStatus>();
        LockCombo.ItemsSource = Enum.GetNames<LockState>();
        B2bPolicyCombo.ItemsSource = new[] { "None", "FullMesh", "HubAndSpoke" };
    }

    private async Task LoadParentRegionsAsync(Guid? preset)
    {
        var repo = new HierarchyRepository(_dsn);
        var regions = await repo.ListRegionsAsync(_tenantId);
        ParentCombo.ItemsSource = regions
            .Select(r => new ParentOption(r.Id, $"{r.RegionCode} — {r.DisplayName}"))
            .ToList();
        if (preset.HasValue) ParentCombo.EditValue = preset.Value;
    }

    private async Task LoadParentSitesAsync(Guid? preset)
    {
        var repo = new HierarchyRepository(_dsn);
        var sites = await repo.ListSitesAsync(_tenantId, null);
        ParentCombo.ItemsSource = sites
            .Select(s => new ParentOption(s.Id, $"{s.SiteCode} — {s.DisplayName}"))
            .ToList();
        if (preset.HasValue) ParentCombo.EditValue = preset.Value;
    }

    // ── Load form from entity ────────────────────────────────────────────

    private void LoadFromRegion()
    {
        var r = _region!;
        CodeBox.Text = r.RegionCode;
        NameBox.Text = r.DisplayName;
        StatusCombo.EditValue = r.Status.ToString();
        LockCombo.EditValue = r.LockState.ToString();
        B2bPolicyCombo.EditValue = r.B2bMeshPolicy;
        NotesBox.Text = r.Notes ?? "";
    }

    private void LoadFromSite()
    {
        var s = _site!;
        CodeBox.Text = s.SiteCode;
        NameBox.Text = s.DisplayName;
        StatusCombo.EditValue = s.Status.ToString();
        LockCombo.EditValue = s.LockState.ToString();
        CityBox.Text = s.City ?? "";
        CountryBox.Text = s.Country ?? "";
        TimezoneBox.Text = s.Timezone ?? "";
        SiteMaxBuildings.EditValue = s.MaxBuildings ?? 0;
        NotesBox.Text = s.Notes ?? "";
    }

    private void LoadFromBuilding()
    {
        var b = _building!;
        CodeBox.Text = b.BuildingCode;
        NameBox.Text = b.DisplayName;
        StatusCombo.EditValue = b.Status.ToString();
        LockCombo.EditValue = b.LockState.ToString();
        BuildingNumberBox.EditValue = b.BuildingNumber ?? 0;
        BuildingMaxFloors.EditValue = b.MaxFloors ?? 0;
        BuildingMaxDevices.EditValue = b.MaxDevicesTotal ?? 0;
        ReservedCheck.IsChecked = b.IsReserved;
        NotesBox.Text = b.Notes ?? "";
    }

    // ── Save ─────────────────────────────────────────────────────────────

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveButton.IsEnabled = false;
            var repo = new HierarchyRepository(_dsn);

            switch (_nodeType)
            {
                case "Region": await SaveRegionAsync(repo); break;
                case "Site":   await SaveSiteAsync(repo); break;
                case "Building": await SaveBuildingAsync(repo); break;
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            DXMessageBox.Show(this, ex.Message, "Save failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private async Task SaveRegionAsync(HierarchyRepository repo)
    {
        var r = _region!;
        r.RegionCode = CodeBox.Text.Trim();
        r.DisplayName = NameBox.Text.Trim();
        r.Status = ParseStatus();
        r.LockState = ParseLock();
        r.B2bMeshPolicy = B2bPolicyCombo.EditValue as string ?? "None";
        r.Notes = NullableText(NotesBox.Text);
        if (string.IsNullOrEmpty(r.RegionCode) || string.IsNullOrEmpty(r.DisplayName))
            throw new InvalidOperationException("Code and display name are required.");

        if (_mode == Mode.New)
        {
            r.Tags ??= new JsonObject();
            r.ExternalRefs ??= new JsonArray();
            SavedId = await repo.CreateRegionAsync(r, _userId);
        }
        else
        {
            await repo.UpdateRegionAsync(r, _userId);
            SavedId = r.Id;
        }
    }

    private async Task SaveSiteAsync(HierarchyRepository repo)
    {
        var s = _site!;
        s.SiteCode = CodeBox.Text.Trim();
        s.DisplayName = NameBox.Text.Trim();
        s.Status = ParseStatus();
        s.LockState = ParseLock();
        s.City = NullableText(CityBox.Text);
        s.Country = NullableText(CountryBox.Text);
        s.Timezone = NullableText(TimezoneBox.Text);
        s.MaxBuildings = IntOrNull(SiteMaxBuildings.EditValue);
        s.Notes = NullableText(NotesBox.Text);

        if (_mode == Mode.New)
        {
            if (ParentCombo.EditValue is Guid regionId && regionId != Guid.Empty)
                s.RegionId = regionId;
            else
                throw new InvalidOperationException("Region is required.");
        }

        if (string.IsNullOrEmpty(s.SiteCode) || string.IsNullOrEmpty(s.DisplayName))
            throw new InvalidOperationException("Code and display name are required.");

        if (_mode == Mode.New)
        {
            s.Tags ??= new JsonObject();
            s.ExternalRefs ??= new JsonArray();
            SavedId = await repo.CreateSiteAsync(s, _userId);
        }
        else
        {
            await repo.UpdateSiteAsync(s, _userId);
            SavedId = s.Id;
        }
    }

    private async Task SaveBuildingAsync(HierarchyRepository repo)
    {
        var b = _building!;
        b.BuildingCode = CodeBox.Text.Trim();
        b.DisplayName = NameBox.Text.Trim();
        b.Status = ParseStatus();
        b.LockState = ParseLock();
        b.BuildingNumber = IntOrNull(BuildingNumberBox.EditValue);
        b.MaxFloors = IntOrNull(BuildingMaxFloors.EditValue);
        b.MaxDevicesTotal = IntOrNull(BuildingMaxDevices.EditValue);
        b.IsReserved = ReservedCheck.IsChecked == true;
        b.Notes = NullableText(NotesBox.Text);

        if (_mode == Mode.New)
        {
            if (ParentCombo.EditValue is Guid siteId && siteId != Guid.Empty)
                b.SiteId = siteId;
            else
                throw new InvalidOperationException("Site is required.");
        }

        if (string.IsNullOrEmpty(b.BuildingCode) || string.IsNullOrEmpty(b.DisplayName))
            throw new InvalidOperationException("Code and display name are required.");

        if (_mode == Mode.New)
        {
            b.Tags ??= new JsonObject();
            b.ExternalRefs ??= new JsonArray();
            SavedId = await repo.CreateBuildingAsync(b, _userId);
        }
        else
        {
            await repo.UpdateBuildingAsync(b, _userId);
            SavedId = b.Id;
        }
    }

    private EntityStatus ParseStatus()
        => Enum.TryParse<EntityStatus>(StatusCombo.EditValue as string, out var s) ? s : EntityStatus.Planned;

    private LockState ParseLock()
        => Enum.TryParse<LockState>(LockCombo.EditValue as string, out var l) ? l : LockState.Open;

    private static string? NullableText(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static int? IntOrNull(object? v)
    {
        if (v is null) return null;
        if (v is int i) return i == 0 ? null : i;
        if (v is decimal d) return d == 0 ? null : (int)d;
        return int.TryParse(v.ToString(), out var n) ? (n == 0 ? null : n) : null;
    }

    private string BuildMetaString()
    {
        EntityBase? e = _region as EntityBase ?? _site as EntityBase ?? _building;
        if (e is null) return "";
        var parts = new List<string> { $"v{e.Version}" };
        parts.Add($"created {e.CreatedAt:yyyy-MM-dd HH:mm}");
        parts.Add($"updated {e.UpdatedAt:yyyy-MM-dd HH:mm}");
        return string.Join(" · ", parts);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // ── Parent combo item ────────────────────────────────────────────────

    private sealed class ParentOption
    {
        public Guid Id { get; }
        public string DisplayText { get; }
        public ParentOption(Guid id, string text) { Id = id; DisplayText = text; }
        public override string ToString() => DisplayText;
    }
}
