using Central.Engine.Net.Devices;

namespace Central.Tests.Net;

/// <summary>
/// Pure tests for <see cref="DeviceNamingService"/>. Mirrors the
/// <c>LinkNamingServiceTests</c> shape — same brace-substitution
/// semantics with device-side tokens.
/// </summary>
public class DeviceNamingServiceTests
{
    [Fact]
    public void ExpandsCoreTemplateWithZeroPad()
    {
        // The seeded Core template produces "MEP-91-CORE02" on the
        // imported Immunocore MEP-91-CORE02 switch — the byte-for-byte
        // invariant customers care about for config generation.
        var result = DeviceNamingService.Expand(
            "{building_code}-CORE{instance}",
            new DeviceNamingContext { BuildingCode = "MEP-91", Instance = 2 });
        Assert.Equal("MEP-91-CORE02", result);
    }

    [Fact]
    public void ExpandsL1CoreTemplate()
    {
        var result = DeviceNamingService.Expand(
            "{building_code}-L1-CORE{instance}",
            new DeviceNamingContext { BuildingCode = "MEP-93", Instance = 2 });
        Assert.Equal("MEP-93-L1-CORE02", result);
    }

    [Fact]
    public void GenericTemplateUsesRoleCode()
    {
        var result = DeviceNamingService.Expand(
            "{building_code}-{role_code}{instance}",
            new DeviceNamingContext
            {
                BuildingCode = "MEP-94", RoleCode = "STOR", Instance = 1,
            });
        Assert.Equal("MEP-94-STOR01", result);
    }

    [Fact]
    public void InstancePaddingRespectsOverride()
    {
        var result = DeviceNamingService.Expand(
            "{building_code}-SW{instance}",
            new DeviceNamingContext
            {
                BuildingCode = "MEP-91", Instance = 7, InstancePadding = 3,
            });
        Assert.Equal("MEP-91-SW007", result);
    }

    [Fact]
    public void InstancePaddingZeroDisablesPad()
    {
        var result = DeviceNamingService.Expand(
            "{building_code}-SW{instance}",
            new DeviceNamingContext
            {
                BuildingCode = "MEP-91", Instance = 7, InstancePadding = 0,
            });
        Assert.Equal("MEP-91-SW7", result);
    }

    [Fact]
    public void MissingInstanceCollapsesToEmpty()
    {
        // Template author included {instance} but caller didn't know
        // which number to pick yet — defensive, doesn't throw.
        var result = DeviceNamingService.Expand(
            "{building_code}-CORE{instance}",
            new DeviceNamingContext { BuildingCode = "MEP-91" });
        Assert.Equal("MEP-91-CORE", result);
    }

    [Fact]
    public void UnknownTokenPassesThroughVerbatim()
    {
        var result = DeviceNamingService.Expand(
            "{biulding_code}-CORE{instance}",   // typo
            new DeviceNamingContext { BuildingCode = "MEP-91", Instance = 2 });
        Assert.Equal("{biulding_code}-CORE02", result);
    }

    [Fact]
    public void UnmatchedBraceEmitsTailVerbatim()
    {
        var result = DeviceNamingService.Expand(
            "{building_code}-CORE{instance",
            new DeviceNamingContext { BuildingCode = "MEP-91", Instance = 2 });
        Assert.Equal("MEP-91-CORE{instance", result);
    }

    [Fact]
    public void EmptyTemplateReturnsEmpty()
    {
        Assert.Equal("", DeviceNamingService.Expand("", new DeviceNamingContext()));
    }

    [Fact]
    public void RegionAndSiteTokensAvailable()
    {
        // Customers with a multi-region naming convention can include
        // {region_code} and {site_code} — confirm they bind.
        var result = DeviceNamingService.Expand(
            "{region_code}-{site_code}-{building_code}-{role_code}{instance}",
            new DeviceNamingContext
            {
                RegionCode = "UK", SiteCode = "MP", BuildingCode = "MEP-91",
                RoleCode = "Core", Instance = 2,
            });
        Assert.Equal("UK-MP-MEP-91-Core02", result);
    }

    [Fact]
    public void RackCodeTokenAvailable()
    {
        // Used by TOR switches that include the rack in the hostname.
        var result = DeviceNamingService.Expand(
            "{building_code}-{rack_code}-TOR{instance}",
            new DeviceNamingContext
            {
                BuildingCode = "MEP-91", RackCode = "R12", Instance = 1,
            });
        Assert.Equal("MEP-91-R12-TOR01", result);
    }
}
