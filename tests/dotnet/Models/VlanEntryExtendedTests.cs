using Central.Engine.Models;

namespace Central.Tests.Models;

public class VlanEntryExtendedTests
{
    // ── IsBlockRoot ──

    [Theory]
    [InlineData("0", true)]
    [InlineData("8", true)]
    [InlineData("16", true)]
    [InlineData("248", true)]
    [InlineData("1", false)]
    [InlineData("7", false)]
    [InlineData("101", false)]
    [InlineData("256", false)]  // > 255
    [InlineData("", false)]
    [InlineData("abc", false)]
    public void IsBlockRoot_Various(string vlanId, bool expected)
    {
        var v = new VlanEntry { VlanId = vlanId };
        Assert.Equal(expected, v.IsBlockRoot);
    }

    // ── RowColor ──

    [Fact]
    public void RowColor_Blocked_ReturnsBlockedColor()
    {
        var v = new VlanEntry { IsBlocked = true };
        Assert.Equal(VlanEntry.BlockedColor, v.RowColor);
    }

    [Fact]
    public void RowColor_RootDowngraded_ReturnsDowngradedColor()
    {
        var v = new VlanEntry { VlanId = "8", Subnet = "/24" };
        Assert.Equal(VlanEntry.RootDowngradedColor, v.RowColor);
    }

    [Fact]
    public void RowColor_Default_ReturnsDefaultColor()
    {
        var v = new VlanEntry { IsDefault = true };
        Assert.Equal(VlanEntry.DefaultVlanColor, v.RowColor);
    }

    [Fact]
    public void RowColor_Normal_Transparent()
    {
        var v = new VlanEntry { VlanId = "101" };
        Assert.Equal("Transparent", v.RowColor);
    }

    [Fact]
    public void RowColor_BlockedPriority_OverDefault()
    {
        var v = new VlanEntry { IsBlocked = true, IsDefault = true };
        Assert.Equal(VlanEntry.BlockedColor, v.RowColor);
    }

    // ── BlockLockedText ──

    [Fact]
    public void BlockLockedText_Locked()
    {
        var v = new VlanEntry { BlockLocked = true };
        Assert.Equal("/21 LOCKED", v.BlockLockedText);
    }

    [Fact]
    public void BlockLockedText_Blocked()
    {
        var v = new VlanEntry { BlockLocked = false, IsBlocked = true };
        Assert.Equal("Blocked /21", v.BlockLockedText);
    }

    [Fact]
    public void BlockLockedText_Neither()
    {
        var v = new VlanEntry { BlockLocked = false, IsBlocked = false };
        Assert.Equal("", v.BlockLockedText);
    }

    // ── SitePrefix ──

    [Theory]
    [InlineData("MEP-91", "10.11")]
    [InlineData("MEP-92", "10.12")]
    [InlineData("MEP-96", "10.16")]
    [InlineData("GBG", "10.30")]
    [InlineData("RAD", "10.50")]
    [InlineData("UNKNOWN", "")]
    [InlineData("", "")]
    public void SitePrefix_KnownSites(string site, string expected)
    {
        var v = new VlanEntry { Site = site };
        Assert.Equal(expected, v.SitePrefix);
    }

    // ── SiteNetwork ──

    [Fact]
    public void SiteNetwork_ReplacesOctet()
    {
        var v = new VlanEntry { Site = "MEP-91", NetworkAddress = "10.x.101.0" };
        Assert.Equal("10.11.101.0", v.SiteNetwork);
    }

    [Fact]
    public void SiteNetwork_UnknownSite_Empty()
    {
        var v = new VlanEntry { Site = "UNKNOWN", NetworkAddress = "10.x.101.0" };
        Assert.Equal("", v.SiteNetwork);
    }

    [Fact]
    public void SiteNetwork_EmptySite_Empty()
    {
        var v = new VlanEntry { Site = "", NetworkAddress = "10.x.101.0" };
        Assert.Equal("", v.SiteNetwork);
    }

    // ── SiteGateway ──

    [Fact]
    public void SiteGateway_ReplacesOctet()
    {
        var v = new VlanEntry { Site = "MEP-92", Gateway = "10.x.101.254" };
        Assert.Equal("10.12.101.254", v.SiteGateway);
    }

    [Fact]
    public void SiteGateway_UnknownSite_Empty()
    {
        var v = new VlanEntry { Site = "UNKNOWN", Gateway = "10.x.101.254" };
        Assert.Equal("", v.SiteGateway);
    }

    // ── PropertyChanged cascades ──

    [Fact]
    public void PropertyChanged_BlockLocked_NotifiesBlockLockedText_RowColor()
    {
        var v = new VlanEntry();
        var changed = new List<string>();
        v.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        v.BlockLocked = true;
        Assert.Contains("BlockLockedText", changed);
        Assert.Contains("RowColor", changed);
    }

    [Fact]
    public void PropertyChanged_IsBlocked_NotifiesRowColor()
    {
        var v = new VlanEntry();
        var changed = new List<string>();
        v.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        v.IsBlocked = true;
        Assert.Contains("RowColor", changed);
    }

    [Fact]
    public void PropertyChanged_IsDefault_NotifiesRowColor()
    {
        var v = new VlanEntry();
        var changed = new List<string>();
        v.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        v.IsDefault = true;
        Assert.Contains("RowColor", changed);
    }

    [Fact]
    public void PropertyChanged_Site_NotifiesSiteNetwork_SiteGateway_SitePrefix()
    {
        var v = new VlanEntry();
        var changed = new List<string>();
        v.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        v.Site = "MEP-91";
        Assert.Contains("SiteNetwork", changed);
        Assert.Contains("SiteGateway", changed);
        Assert.Contains("SitePrefix", changed);
    }

    [Fact]
    public void PropertyChanged_Subnet_NotifiesRowColor()
    {
        var v = new VlanEntry();
        var changed = new List<string>();
        v.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        v.Subnet = "/24";
        Assert.Contains("RowColor", changed);
    }

    // ── DetailSites ──

    [Fact]
    public void DetailSites_DefaultEmpty()
    {
        var v = new VlanEntry();
        Assert.NotNull(v.DetailSites);
        Assert.Empty(v.DetailSites);
    }

    // ── BuildingNumberMap coverage ──

    [Fact]
    public void BuildingNumberMap_CaseInsensitive()
    {
        Assert.Equal("11", VlanEntry.BuildingNumberMap["mep-91"]);
        Assert.Equal("11", VlanEntry.BuildingNumberMap["MEP-91"]);
    }

    [Fact]
    public void BuildingNumberMap_ContainsAllExpectedSites()
    {
        Assert.True(VlanEntry.BuildingNumberMap.Count >= 19);
        Assert.True(VlanEntry.BuildingNumberMap.ContainsKey("EXP-01"));
        Assert.True(VlanEntry.BuildingNumberMap.ContainsKey("UK-RES01"));
        Assert.True(VlanEntry.BuildingNumberMap.ContainsKey("EU-RES01"));
        Assert.True(VlanEntry.BuildingNumberMap.ContainsKey("US-RES01"));
    }
}
