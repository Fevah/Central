using Central.Engine.Net;
using Central.Engine.Net.Pools;

namespace Central.Tests.Net;

public class PoolModelTests
{
    [Fact]
    public void AsnPool_DefaultsAreSafe()
    {
        var p = new AsnPool();
        Assert.Equal(AsnKind.Private2, p.AsnKind);
        Assert.Equal(EntityStatus.Planned, p.Status);
        Assert.Equal(LockState.Open, p.LockState);
        Assert.Equal(1, p.Version);
    }

    [Fact]
    public void AsnBlock_DefaultsToFreeScope()
    {
        var b = new AsnBlock();
        Assert.Equal(PoolScopeLevel.Free, b.ScopeLevel);
        Assert.Null(b.ScopeEntityId);
    }

    [Fact]
    public void IpPool_DefaultsToV4()
    {
        var p = new IpPool();
        Assert.Equal(IpAddressFamily.V4, p.AddressFamily);
    }

    [Fact]
    public void Subnet_DefaultsToFreeScope()
    {
        var s = new Subnet();
        Assert.Equal(PoolScopeLevel.Free, s.ScopeLevel);
        Assert.Null(s.ParentSubnetId);
        Assert.Null(s.VlanId);
    }

    [Fact]
    public void IpAddress_NotReservedByDefault()
    {
        var a = new IpAddress();
        Assert.False(a.IsReserved);
        Assert.Null(a.AssignedToType);
    }

    [Fact]
    public void VlanTemplate_NotDefaultByDefault()
    {
        var t = new VlanTemplate();
        Assert.False(t.IsDefault);
    }

    [Fact]
    public void Vlan_DefaultsToFreeScope()
    {
        var v = new Vlan();
        Assert.Equal(PoolScopeLevel.Free, v.ScopeLevel);
    }

    [Fact]
    public void MlagDomain_DefaultsToBuildingScope()
    {
        // Unlike most pool children, MLAG domains are building-scoped by
        // default — they're the primary "one per MLAG pair" entity, and
        // a pair is always in the same building.
        var d = new MlagDomain();
        Assert.Equal(PoolScopeLevel.Building, d.ScopeLevel);
    }

    [Fact]
    public void MstpPriorityRule_DefaultsToRegionScope()
    {
        var r = new MstpPriorityRule();
        Assert.Equal(PoolScopeLevel.Region, r.ScopeLevel);
    }

    [Fact]
    public void MstpPriorityRuleStep_HasEmptyMatchExpressionByDefault()
    {
        var s = new MstpPriorityRuleStep();
        Assert.NotNull(s.MatchExpression);
        Assert.Empty(s.MatchExpression);
    }

    [Fact]
    public void ShelfResourceType_AllSixValuesExist()
    {
        // These are the values the discriminator CHECK in migration 086
        // permits. If one is missing here, the lookup on the row will
        // throw when we try to parse it back.
        Assert.True(Enum.IsDefined(ShelfResourceType.Asn));
        Assert.True(Enum.IsDefined(ShelfResourceType.Ip));
        Assert.True(Enum.IsDefined(ShelfResourceType.Subnet));
        Assert.True(Enum.IsDefined(ShelfResourceType.Vlan));
        Assert.True(Enum.IsDefined(ShelfResourceType.Mlag));
        Assert.True(Enum.IsDefined(ShelfResourceType.Mstp));
    }

    [Fact]
    public void PoolScopeLevel_CoversHierarchyDown_To_Device()
    {
        // Subnet goes down to Room; device-scoped VLANs go down to Device.
        // If a new tier ever lands, this test catches any enum/CHECK
        // drift at the C# layer.
        Assert.True(Enum.IsDefined(PoolScopeLevel.Free));
        Assert.True(Enum.IsDefined(PoolScopeLevel.Region));
        Assert.True(Enum.IsDefined(PoolScopeLevel.Site));
        Assert.True(Enum.IsDefined(PoolScopeLevel.Building));
        Assert.True(Enum.IsDefined(PoolScopeLevel.Floor));
        Assert.True(Enum.IsDefined(PoolScopeLevel.Room));
        Assert.True(Enum.IsDefined(PoolScopeLevel.Device));
    }

    [Fact]
    public void AsnKind_AllThreeValuesExist()
    {
        Assert.True(Enum.IsDefined(AsnKind.Private2));
        Assert.True(Enum.IsDefined(AsnKind.Private4));
        Assert.True(Enum.IsDefined(AsnKind.Public));
    }

    [Fact]
    public void ReservationShelfEntry_DefaultsAreSafe()
    {
        var s = new ReservationShelfEntry();
        // AvailableAfter should be >= RetiredAt (enforced by DB CHECK too).
        Assert.True(s.AvailableAfter >= s.RetiredAt);
        Assert.Null(s.RetiredReason);
    }
}
