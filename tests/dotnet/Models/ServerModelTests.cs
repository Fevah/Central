using Central.Engine.Models;

namespace Central.Tests.Models;

public class ServerModelTests
{
    [Fact]
    public void PopulateNicDetails_AllFourNics()
    {
        var srv = new Server
        {
            Nic1Ip = "10.0.0.1", Nic1Router = "CORE01", Nic1Subnet = "/24", Nic1Status = "Up",
            Nic2Ip = "10.0.1.1", Nic2Router = "CORE02", Nic2Subnet = "/24", Nic2Status = "Up",
            Nic3Ip = "10.0.2.1", Nic3Router = "CORE01", Nic3Subnet = "/24", Nic3Status = "Down",
            Nic4Ip = "10.0.3.1", Nic4Router = "CORE02", Nic4Subnet = "/24", Nic4Status = "Up"
        };

        srv.PopulateNicDetails();

        Assert.Equal(4, srv.DetailNics.Count);
        Assert.Equal("NIC 1", srv.DetailNics[0].Nic);
        Assert.Equal("10.0.0.1", srv.DetailNics[0].Ip);
        Assert.Equal("CORE01", srv.DetailNics[0].Router);
        Assert.Equal("NIC 4", srv.DetailNics[3].Nic);
    }

    [Fact]
    public void PopulateNicDetails_OnlyPopulatedNics()
    {
        var srv = new Server
        {
            Nic1Ip = "10.0.0.1", Nic1Router = "CORE01", Nic1Subnet = "/24", Nic1Status = "Up",
            Nic3Ip = "10.0.2.1", Nic3Router = "CORE01", Nic3Subnet = "/24", Nic3Status = "Up",
            // Nic2 and Nic4 left empty
        };

        srv.PopulateNicDetails();

        Assert.Equal(2, srv.DetailNics.Count);
        Assert.Equal("NIC 1", srv.DetailNics[0].Nic);
        Assert.Equal("NIC 3", srv.DetailNics[1].Nic);
    }

    [Fact]
    public void PopulateNicDetails_NoNics_EmptyList()
    {
        var srv = new Server();
        srv.PopulateNicDetails();
        Assert.Empty(srv.DetailNics);
    }

    [Fact]
    public void PopulateNicDetails_ClearsPrevious()
    {
        var srv = new Server { Nic1Ip = "10.0.0.1" };

        srv.PopulateNicDetails();
        Assert.Single(srv.DetailNics);

        // Call again — should not accumulate
        srv.PopulateNicDetails();
        Assert.Single(srv.DetailNics);
    }

    [Fact]
    public void Server_DefaultStatus_Reserved()
    {
        var srv = new Server();
        Assert.Equal("RESERVED", srv.Status);
    }

    [Fact]
    public void Server_PropertyChanged_Fires()
    {
        var srv = new Server();
        var changed = new List<string>();
        srv.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        srv.ServerName = "Prox01";
        srv.Building = "MEP-91";
        srv.ServerAs = "65112";
        srv.LoopbackIp = "10.0.255.1";
        srv.Status = "Active";

        Assert.Contains("ServerName", changed);
        Assert.Contains("Building", changed);
        Assert.Contains("ServerAs", changed);
        Assert.Contains("LoopbackIp", changed);
        Assert.Contains("Status", changed);
    }
}
