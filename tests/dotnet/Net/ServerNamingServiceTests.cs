using Central.Engine.Net.Servers;

namespace Central.Tests.Net;

/// <summary>
/// Pure tests for <see cref="ServerNamingService"/>. Same defensive
/// semantics as link + device naming services — unknown tokens pass
/// through verbatim, missing values collapse, padding defaults to 2.
/// </summary>
public class ServerNamingServiceTests
{
    [Fact]
    public void SeededTemplate_GivesSrv01PatternFromBuildingCode()
    {
        var result = ServerNamingService.Expand(
            "{building_code}-SRV{instance}",
            new ServerNamingContext { BuildingCode = "MEP-91", Instance = 1 });
        Assert.Equal("MEP-91-SRV01", result);
    }

    [Fact]
    public void AllTokensBound_LongForm()
    {
        var result = ServerNamingService.Expand(
            "{region_code}-{site_code}-{building_code}-{rack_code}-{profile_code}{instance}",
            new ServerNamingContext
            {
                RegionCode = "UK", SiteCode = "MP", BuildingCode = "MEP-91",
                RackCode = "R12", ProfileCode = "Srv4", Instance = 3,
            });
        Assert.Equal("UK-MP-MEP-91-R12-Srv403", result);
    }

    [Fact]
    public void Padding0DisablesZeroPad()
    {
        var result = ServerNamingService.Expand(
            "{building_code}-SRV{instance}",
            new ServerNamingContext
            {
                BuildingCode = "MEP-91", Instance = 9, InstancePadding = 0,
            });
        Assert.Equal("MEP-91-SRV9", result);
    }

    [Fact]
    public void Padding4PadsToFour()
    {
        var result = ServerNamingService.Expand(
            "{building_code}-SRV{instance}",
            new ServerNamingContext
            {
                BuildingCode = "MEP-91", Instance = 7, InstancePadding = 4,
            });
        Assert.Equal("MEP-91-SRV0007", result);
    }

    [Fact]
    public void MissingInstanceCollapses()
    {
        Assert.Equal("MEP-91-SRV",
            ServerNamingService.Expand("{building_code}-SRV{instance}",
                new ServerNamingContext { BuildingCode = "MEP-91" }));
    }

    [Fact]
    public void UnknownTokenPassesThroughVerbatim()
    {
        Assert.Equal("{biulding_code}-SRV01",
            ServerNamingService.Expand("{biulding_code}-SRV{instance}",
                new ServerNamingContext { BuildingCode = "MEP-91", Instance = 1 }));
    }

    [Fact]
    public void UnmatchedBraceEmitsTailVerbatim()
    {
        Assert.Equal("MEP-91-SRV{instance",
            ServerNamingService.Expand("{building_code}-SRV{instance",
                new ServerNamingContext { BuildingCode = "MEP-91", Instance = 2 }));
    }

    [Fact]
    public void EmptyTemplate_ReturnsEmpty()
    {
        Assert.Equal("",
            ServerNamingService.Expand("", new ServerNamingContext()));
    }
}
