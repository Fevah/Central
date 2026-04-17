using Central.Engine.Net;
using Central.Engine.Net.Hierarchy;

namespace Central.Tests.Net;

public class HierarchyModelTests
{
    [Fact]
    public void EntityBase_DefaultsAreSafe()
    {
        var r = new Region();
        Assert.Equal(EntityStatus.Planned, r.Status);
        Assert.Equal(LockState.Open, r.LockState);
        Assert.Equal(1, r.Version);
        Assert.Null(r.DeletedAt);
        Assert.NotNull(r.Tags);
        Assert.NotNull(r.ExternalRefs);
    }

    [Fact]
    public void Region_Defaults()
    {
        var r = new Region();
        Assert.Equal("None", r.B2bMeshPolicy);
        Assert.Equal("", r.RegionCode);
    }

    [Fact]
    public void SiteProfile_Defaults()
    {
        var p = new SiteProfile();
        Assert.Equal(12, p.DefaultMaxBuildings);
        Assert.True(p.AllowMixedBuildingProfiles);
        Assert.Equal(1, p.DefaultFloorsPerBuilding);
    }

    [Fact]
    public void Building_DefaultsToActiveNotReserved()
    {
        var b = new Building();
        Assert.False(b.IsReserved);
        Assert.Empty(b.B2bPartners);
    }

    [Fact]
    public void Rack_DefaultUHeightIs42()
    {
        Assert.Equal(42, new Rack().UHeight);
    }

    [Fact]
    public void Room_DefaultTypeIsDataHall()
    {
        Assert.Equal("DataHall", new Room().RoomType);
    }

    [Fact]
    public void FloorProfile_DefaultRackCountPerRoom()
    {
        Assert.Equal(10, new FloorProfile().DefaultRackCountPerRoom);
    }

    [Fact]
    public void EntityStatus_LifecycleTransitions()
    {
        // Sanity check the enum values exist in the expected order
        Assert.Equal(0, (int)EntityStatus.Planned);
        Assert.Equal(1, (int)EntityStatus.Reserved);
        Assert.Equal(2, (int)EntityStatus.Active);
        Assert.Equal(3, (int)EntityStatus.Deprecated);
        Assert.Equal(4, (int)EntityStatus.Retired);
    }

    [Fact]
    public void LockState_Progression()
    {
        Assert.Equal(0, (int)LockState.Open);
        Assert.Equal(1, (int)LockState.SoftLock);
        Assert.Equal(2, (int)LockState.HardLock);
        Assert.Equal(3, (int)LockState.Immutable);
    }
}
