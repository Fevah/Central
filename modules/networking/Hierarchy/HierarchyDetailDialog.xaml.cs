using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using Central.Engine.Net;
using Central.Engine.Net.Dialogs;
using Central.Engine.Net.Hierarchy;
using Central.Persistence.Net;
using DevExpress.Xpf.Core;
using Region = Central.Engine.Net.Hierarchy.Region;
using ValidationMode = Central.Engine.Net.Dialogs.HierarchyValidation.Mode;

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
    private Floor? _floor;
    private Room? _room;
    private Rack? _rack;

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

    public static async Task<HierarchyDetailDialog> ForNewFloorAsync(string dsn, Guid tenantId, int? userId,
        Guid? presetBuildingId = null)
    {
        var dlg = new HierarchyDetailDialog(dsn, tenantId, userId, Mode.New, "Floor");
        dlg.TitleLabel.Text = "New floor";
        dlg.SubtitleLabel.Text = "A floor sits inside a building. Use negative numbers for basements.";
        dlg.FloorSection.Visibility = Visibility.Visible;
        dlg.ParentLabel.Text = "Building";
        dlg.ParentSection.Visibility = Visibility.Visible;
        await dlg.LoadParentBuildingsAsync(presetBuildingId);
        dlg._floor = new Floor { OrganizationId = tenantId, BuildingId = presetBuildingId ?? Guid.Empty };
        dlg.LoadFromFloor();
        return dlg;
    }

    public static async Task<HierarchyDetailDialog> ForNewRoomAsync(string dsn, Guid tenantId, int? userId,
        Guid? presetFloorId = null)
    {
        var dlg = new HierarchyDetailDialog(dsn, tenantId, userId, Mode.New, "Room");
        dlg.TitleLabel.Text = "New room";
        dlg.SubtitleLabel.Text = "A room sits on a floor and holds racks.";
        dlg.RoomSection.Visibility = Visibility.Visible;
        dlg.ParentLabel.Text = "Floor";
        dlg.ParentSection.Visibility = Visibility.Visible;
        await dlg.LoadParentFloorsAsync(presetFloorId);
        dlg._room = new Room { OrganizationId = tenantId, FloorId = presetFloorId ?? Guid.Empty };
        dlg.LoadFromRoom();
        return dlg;
    }

    public static async Task<HierarchyDetailDialog> ForNewRackAsync(string dsn, Guid tenantId, int? userId,
        Guid? presetRoomId = null)
    {
        var dlg = new HierarchyDetailDialog(dsn, tenantId, userId, Mode.New, "Rack");
        dlg.TitleLabel.Text = "New rack";
        dlg.SubtitleLabel.Text = "A rack sits in a room and holds devices.";
        dlg.RackSection.Visibility = Visibility.Visible;
        dlg.ParentLabel.Text = "Room";
        dlg.ParentSection.Visibility = Visibility.Visible;
        await dlg.LoadParentRoomsAsync(presetRoomId);
        dlg._rack = new Rack { OrganizationId = tenantId, RoomId = presetRoomId ?? Guid.Empty };
        dlg.LoadFromRack();
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
            case "Floor":
                dlg._floor = await repo.GetFloorAsync(entityId, tenantId);
                if (dlg._floor is null) throw new InvalidOperationException($"Floor {entityId} not found");
                dlg.FloorSection.Visibility = Visibility.Visible;
                dlg.SubtitleLabel.Text = $"Floor · v{dlg._floor.Version}";
                dlg.LoadFromFloor();
                break;
            case "Room":
                dlg._room = await repo.GetRoomAsync(entityId, tenantId);
                if (dlg._room is null) throw new InvalidOperationException($"Room {entityId} not found");
                dlg.RoomSection.Visibility = Visibility.Visible;
                dlg.SubtitleLabel.Text = $"Room · v{dlg._room.Version}";
                dlg.LoadFromRoom();
                break;
            case "Rack":
                dlg._rack = await repo.GetRackAsync(entityId, tenantId);
                if (dlg._rack is null) throw new InvalidOperationException($"Rack {entityId} not found");
                dlg.RackSection.Visibility = Visibility.Visible;
                dlg.SubtitleLabel.Text = $"Rack · v{dlg._rack.Version}";
                dlg.LoadFromRack();
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
        // Room types are a soft list — operators can type anything, but we
        // seed the common values so IDF / MDF / DataHall are one click away.
        RoomTypeCombo.ItemsSource = new[]
        {
            "DataHall", "MDF", "IDF", "Office", "Comms", "Plant", "Custom"
        };
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

    private async Task LoadParentBuildingsAsync(Guid? preset)
    {
        var repo = new HierarchyRepository(_dsn);
        var buildings = await repo.ListBuildingsAsync(_tenantId, null);
        ParentCombo.ItemsSource = buildings
            .Select(b => new ParentOption(b.Id, $"{b.BuildingCode} — {b.DisplayName}"))
            .ToList();
        if (preset.HasValue) ParentCombo.EditValue = preset.Value;
    }

    private async Task LoadParentFloorsAsync(Guid? preset)
    {
        var repo = new HierarchyRepository(_dsn);
        var floors = await repo.ListFloorsAsync(_tenantId, null);
        ParentCombo.ItemsSource = floors
            .Select(f => new ParentOption(f.Id, $"{f.FloorCode}{(string.IsNullOrEmpty(f.DisplayName) ? "" : " — " + f.DisplayName)}"))
            .ToList();
        if (preset.HasValue) ParentCombo.EditValue = preset.Value;
    }

    private async Task LoadParentRoomsAsync(Guid? preset)
    {
        var repo = new HierarchyRepository(_dsn);
        var rooms = await repo.ListRoomsAsync(_tenantId, null);
        ParentCombo.ItemsSource = rooms
            .Select(rm => new ParentOption(rm.Id, $"{rm.RoomCode} ({rm.RoomType})"))
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

    private void LoadFromFloor()
    {
        var f = _floor!;
        CodeBox.Text = f.FloorCode;
        NameBox.Text = f.DisplayName ?? "";
        StatusCombo.EditValue = f.Status.ToString();
        LockCombo.EditValue = f.LockState.ToString();
        FloorNumberBox.EditValue = f.FloorNumber ?? 0;
        FloorMaxRooms.EditValue = f.MaxRooms ?? 0;
        NotesBox.Text = f.Notes ?? "";
    }

    private void LoadFromRoom()
    {
        var r = _room!;
        CodeBox.Text = r.RoomCode;
        // Room has no separate DisplayName — the grid just shows RoomCode.
        // Repurpose the name box to keep UX consistent (label says "Display name"
        // but for rooms we treat it as the room type label).
        NameBox.Text = r.RoomCode;
        NameBox.IsEnabled = false;
        StatusCombo.EditValue = r.Status.ToString();
        LockCombo.EditValue = r.LockState.ToString();
        RoomTypeCombo.EditValue = r.RoomType;
        RoomMaxRacks.EditValue = r.MaxRacks ?? 0;
        RoomEnvNotesBox.Text = r.EnvironmentalNotes ?? "";
        NotesBox.Text = r.Notes ?? "";
    }

    private void LoadFromRack()
    {
        var k = _rack!;
        CodeBox.Text = k.RackCode;
        NameBox.Text = k.RackCode;
        NameBox.IsEnabled = false;
        StatusCombo.EditValue = k.Status.ToString();
        LockCombo.EditValue = k.LockState.ToString();
        RackUHeightBox.EditValue = k.UHeight;
        RackRowBox.Text = k.Row ?? "";
        RackPositionBox.EditValue = k.Position ?? 0;
        RackMaxDevices.EditValue = k.MaxDevices ?? 0;
        NotesBox.Text = k.Notes ?? "";
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
                case "Region":   await SaveRegionAsync(repo); break;
                case "Site":     await SaveSiteAsync(repo); break;
                case "Building": await SaveBuildingAsync(repo); break;
                case "Floor":    await SaveFloorAsync(repo); break;
                case "Room":     await SaveRoomAsync(repo); break;
                case "Rack":     await SaveRackAsync(repo); break;
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
        var regionErrors = HierarchyValidation.ValidateRegion(r);
        if (regionErrors.Count > 0) throw new InvalidOperationException(regionErrors[0]);

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

        if (_mode == Mode.New && ParentCombo.EditValue is Guid regionId && regionId != Guid.Empty)
            s.RegionId = regionId;

        var siteErrors = HierarchyValidation.ValidateSite(s, _mode == Mode.New ? ValidationMode.New : ValidationMode.Edit);
        if (siteErrors.Count > 0) throw new InvalidOperationException(siteErrors[0]);

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

        if (_mode == Mode.New && ParentCombo.EditValue is Guid siteId && siteId != Guid.Empty)
            b.SiteId = siteId;

        var buildingErrors = HierarchyValidation.ValidateBuilding(b, _mode == Mode.New ? ValidationMode.New : ValidationMode.Edit);
        if (buildingErrors.Count > 0) throw new InvalidOperationException(buildingErrors[0]);

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

    private async Task SaveFloorAsync(HierarchyRepository repo)
    {
        var f = _floor!;
        f.FloorCode = CodeBox.Text.Trim();
        f.DisplayName = NullableText(NameBox.Text);
        f.Status = ParseStatus();
        f.LockState = ParseLock();
        f.FloorNumber = IntOrNull(FloorNumberBox.EditValue, treatZeroAsNull: false);
        f.MaxRooms = IntOrNull(FloorMaxRooms.EditValue);
        f.Notes = NullableText(NotesBox.Text);

        if (_mode == Mode.New && ParentCombo.EditValue is Guid buildingId && buildingId != Guid.Empty)
            f.BuildingId = buildingId;

        var floorErrors = HierarchyValidation.ValidateFloor(f, _mode == Mode.New ? ValidationMode.New : ValidationMode.Edit);
        if (floorErrors.Count > 0) throw new InvalidOperationException(floorErrors[0]);

        if (_mode == Mode.New)
        {
            f.Tags ??= new JsonObject();
            f.ExternalRefs ??= new JsonArray();
            SavedId = await repo.CreateFloorAsync(f, _userId);
        }
        else
        {
            await repo.UpdateFloorAsync(f, _userId);
            SavedId = f.Id;
        }
    }

    private async Task SaveRoomAsync(HierarchyRepository repo)
    {
        var r = _room!;
        r.RoomCode = CodeBox.Text.Trim();
        r.RoomType = RoomTypeCombo.EditValue as string ?? r.RoomType;
        r.Status = ParseStatus();
        r.LockState = ParseLock();
        r.MaxRacks = IntOrNull(RoomMaxRacks.EditValue);
        r.EnvironmentalNotes = NullableText(RoomEnvNotesBox.Text);
        r.Notes = NullableText(NotesBox.Text);

        if (_mode == Mode.New && ParentCombo.EditValue is Guid floorId && floorId != Guid.Empty)
            r.FloorId = floorId;

        var roomErrors = HierarchyValidation.ValidateRoom(r, _mode == Mode.New ? ValidationMode.New : ValidationMode.Edit);
        if (roomErrors.Count > 0) throw new InvalidOperationException(roomErrors[0]);

        if (_mode == Mode.New)
        {
            r.Tags ??= new JsonObject();
            r.ExternalRefs ??= new JsonArray();
            SavedId = await repo.CreateRoomAsync(r, _userId);
        }
        else
        {
            await repo.UpdateRoomAsync(r, _userId);
            SavedId = r.Id;
        }
    }

    private async Task SaveRackAsync(HierarchyRepository repo)
    {
        var k = _rack!;
        k.RackCode = CodeBox.Text.Trim();
        k.Status = ParseStatus();
        k.LockState = ParseLock();
        k.UHeight = IntOrNull(RackUHeightBox.EditValue) ?? 42;
        k.Row = NullableText(RackRowBox.Text);
        k.Position = IntOrNull(RackPositionBox.EditValue);
        k.MaxDevices = IntOrNull(RackMaxDevices.EditValue);
        k.Notes = NullableText(NotesBox.Text);

        if (_mode == Mode.New && ParentCombo.EditValue is Guid roomId && roomId != Guid.Empty)
            k.RoomId = roomId;

        var rackErrors = HierarchyValidation.ValidateRack(k, _mode == Mode.New ? ValidationMode.New : ValidationMode.Edit);
        if (rackErrors.Count > 0) throw new InvalidOperationException(rackErrors[0]);

        if (_mode == Mode.New)
        {
            k.Tags ??= new JsonObject();
            k.ExternalRefs ??= new JsonArray();
            SavedId = await repo.CreateRackAsync(k, _userId);
        }
        else
        {
            await repo.UpdateRackAsync(k, _userId);
            SavedId = k.Id;
        }
    }

    private EntityStatus ParseStatus()
        => Enum.TryParse<EntityStatus>(StatusCombo.EditValue as string, out var s) ? s : EntityStatus.Planned;

    private LockState ParseLock()
        => Enum.TryParse<LockState>(LockCombo.EditValue as string, out var l) ? l : LockState.Open;

    private static string? NullableText(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static int? IntOrNull(object? v, bool treatZeroAsNull = true)
    {
        if (v is null) return null;
        int? parsed = v switch
        {
            int i => i,
            decimal d => (int)d,
            _ => int.TryParse(v.ToString(), out var n) ? n : null
        };
        if (parsed is null) return null;
        return (treatZeroAsNull && parsed == 0) ? null : parsed;
    }

    private string BuildMetaString()
    {
        EntityBase? e = _region as EntityBase
                         ?? _site as EntityBase
                         ?? _building as EntityBase
                         ?? _floor as EntityBase
                         ?? _room as EntityBase
                         ?? _rack;
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
