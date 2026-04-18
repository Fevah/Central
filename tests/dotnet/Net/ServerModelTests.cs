using Central.Engine.Net;
using Central.Engine.Net.Servers;

namespace Central.Tests.Net;

public class ServerModelTests
{
    [Fact]
    public void ServerProfile_DefaultsAreSafe()
    {
        var p = new ServerProfile();
        Assert.Equal(4, p.NicCount);
        Assert.Equal(32, p.DefaultLoopbackPrefix);
        Assert.Equal("{building_code}-SRV{instance}", p.NamingTemplate);
        Assert.Equal(EntityStatus.Planned, p.Status);
    }

    [Fact]
    public void Server_DefaultsToClosedMgmtPlane()
    {
        var s = new Server();
        Assert.Null(s.ManagementIp);
        Assert.Null(s.LastPingOk);
        Assert.Null(s.LegacyServerId);
        Assert.Null(s.AsnAllocationId);
        Assert.Null(s.LoopbackIpAddressId);
    }

    [Fact]
    public void ServerNic_DefaultsToZeroIndexNoneMlag()
    {
        var n = new ServerNic();
        Assert.Equal(0, n.NicIndex);
        Assert.Equal(MlagSide.None, n.MlagSide);
        Assert.False(n.AdminUp);
        Assert.Null(n.TargetPortId);
        Assert.Null(n.IpAddressId);
    }

    [Fact]
    public void MlagSide_ThreeValues()
    {
        Assert.True(Enum.IsDefined(MlagSide.None));
        Assert.True(Enum.IsDefined(MlagSide.A));
        Assert.True(Enum.IsDefined(MlagSide.B));
    }
}
