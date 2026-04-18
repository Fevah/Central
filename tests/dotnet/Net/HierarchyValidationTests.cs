using Central.Engine.Net.Dialogs;
using Central.Engine.Net.Hierarchy;
using static Central.Engine.Net.Dialogs.HierarchyValidation;

namespace Central.Tests.Net;

/// <summary>
/// Chunk-C dialog-validation tests for HierarchyDetailDialog. Every
/// validator on <see cref="HierarchyValidation"/> is exercised:
/// happy path, empty required fields, missing parent FK in New mode
/// (where applicable), and the "Edit mode doesn't need parent"
/// relaxation.
/// </summary>
public class HierarchyValidationTests
{
    // ── Region ──────────────────────────────────────────────────────

    [Fact]
    public void Region_HappyPath_NoErrors()
    {
        var r = new Region { RegionCode = "UK", DisplayName = "United Kingdom" };
        Assert.Empty(ValidateRegion(r));
    }

    [Fact]
    public void Region_MissingCodeAndName_ReportsBoth()
    {
        var errors = ValidateRegion(new Region());
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Contains("Code"));
        Assert.Contains(errors, e => e.Contains("Display name"));
    }

    [Fact]
    public void Region_WhitespaceCode_RejectedSameAsEmpty()
    {
        // Whitespace should be treated as missing — operators pasting
        // spaces into the code field shouldn't sneak past.
        var r = new Region { RegionCode = "   ", DisplayName = "X" };
        Assert.Contains(ValidateRegion(r), e => e.Contains("Code"));
    }

    // ── Site ────────────────────────────────────────────────────────

    [Fact]
    public void Site_New_RequiresRegionFk()
    {
        var s = new Site { SiteCode = "MP", DisplayName = "Milton Park" };
        // RegionId defaults to Guid.Empty — New mode must flag it.
        Assert.Contains(ValidateSite(s, Mode.New), e => e.Contains("Region"));
    }

    [Fact]
    public void Site_Edit_DoesNotRequireRegionFk()
    {
        // In Edit mode the parent was chosen at create time and is
        // immutable — a blank RegionId would be a bug elsewhere, not
        // a user-facing validation failure here.
        var s = new Site { SiteCode = "MP", DisplayName = "Milton Park" };
        Assert.Empty(ValidateSite(s, Mode.Edit));
    }

    [Fact]
    public void Site_NewWithParent_OK()
    {
        var s = new Site
        {
            SiteCode = "MP", DisplayName = "Milton Park",
            RegionId = Guid.NewGuid(),
        };
        Assert.Empty(ValidateSite(s, Mode.New));
    }

    // ── Building ────────────────────────────────────────────────────

    [Fact]
    public void Building_New_RequiresSiteFk()
    {
        var b = new Building { BuildingCode = "MEP-91", DisplayName = "Building 91" };
        Assert.Contains(ValidateBuilding(b, Mode.New), e => e.Contains("Site"));
    }

    [Fact]
    public void Building_ValidNew_OK()
    {
        var b = new Building
        {
            BuildingCode = "MEP-91", DisplayName = "Building 91",
            SiteId = Guid.NewGuid(),
        };
        Assert.Empty(ValidateBuilding(b, Mode.New));
    }

    // ── Floor / Room / Rack ────────────────────────────────────────
    // Thin — code + parent FK. Test the shape; don't repeat every
    // missing-field assertion.

    [Fact]
    public void Floor_New_RequiresBuildingFk()
    {
        Assert.Contains(
            ValidateFloor(new Floor { FloorCode = "1" }, Mode.New),
            e => e.Contains("Building"));
    }

    [Fact]
    public void Floor_NewWithBuilding_OK()
    {
        Assert.Empty(
            ValidateFloor(new Floor { FloorCode = "1", BuildingId = Guid.NewGuid() }, Mode.New));
    }

    [Fact]
    public void Floor_EmptyCode_Rejected()
    {
        Assert.Contains(
            ValidateFloor(new Floor { BuildingId = Guid.NewGuid() }, Mode.New),
            e => e.Contains("Code"));
    }

    [Fact]
    public void Room_New_RequiresFloorFk()
    {
        Assert.Contains(
            ValidateRoom(new Room { RoomCode = "MDF" }, Mode.New),
            e => e.Contains("Floor"));
    }

    [Fact]
    public void Rack_New_RequiresRoomFk()
    {
        Assert.Contains(
            ValidateRack(new Rack { RackCode = "R01" }, Mode.New),
            e => e.Contains("Room"));
    }

    [Fact]
    public void Rack_EditDoesNotRequireRoom()
    {
        Assert.Empty(
            ValidateRack(new Rack { RackCode = "R01" }, Mode.Edit));
    }
}
