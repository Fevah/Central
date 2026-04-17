using Central.Core.Models;

namespace Central.Tests.Models;

public class VlanEntryTests
{
    // ── IsBlockRoot ──

    [Theory]
    [InlineData("8", true)]
    [InlineData("16", true)]
    [InlineData("24", true)]
    [InlineData("32", true)]
    [InlineData("248", true)]
    [InlineData("1", false)]    // not divisible by 8
    [InlineData("9", false)]
    [InlineData("100", false)]  // 100 % 8 = 4
    [InlineData("256", false)]  // > 255
    [InlineData("0", true)]     // 0 % 8 == 0 and 0 <= 255
    [InlineData("", false)]     // not parseable
    [InlineData("abc", false)]  // not parseable
    public void IsBlockRoot_ByVlanId(string vlanId, bool expected)
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
    public void RowColor_RootDowngraded_ReturnsAmber()
    {
        // VlanId=8 is a block root; Subnet="/24" means downgraded
        var v = new VlanEntry { VlanId = "8", Subnet = "/24" };
        Assert.Equal(VlanEntry.RootDowngradedColor, v.RowColor);
    }

    [Fact]
    public void RowColor_RootNotDowngraded_Transparent()
    {
        // VlanId=8 is a block root; Subnet="/21" means NOT downgraded
        var v = new VlanEntry { VlanId = "8", Subnet = "/21" };
        Assert.Equal("Transparent", v.RowColor);
    }

    [Fact]
    public void RowColor_Default_ReturnsDefaultVlanColor()
    {
        var v = new VlanEntry { IsDefault = true };
        Assert.Equal(VlanEntry.DefaultVlanColor, v.RowColor);
    }

    [Fact]
    public void RowColor_Normal_ReturnsTransparent()
    {
        var v = new VlanEntry { VlanId = "101" };
        Assert.Equal("Transparent", v.RowColor);
    }

    [Fact]
    public void RowColor_BlockedOverridesDefault()
    {
        // Blocked takes priority over IsDefault
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
        var v = new VlanEntry { IsBlocked = true };
        Assert.Equal("Blocked /21", v.BlockLockedText);
    }

    [Fact]
    public void BlockLockedText_Neither()
    {
        var v = new VlanEntry();
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
    public void SitePrefix_MapsCorrectly(string site, string expectedPrefix)
    {
        var v = new VlanEntry { Site = site };
        Assert.Equal(expectedPrefix, v.SitePrefix);
    }

    // ── SiteNetwork ──

    [Fact]
    public void SiteNetwork_ReplacesPlaceholder()
    {
        var v = new VlanEntry { Site = "MEP-91", NetworkAddress = "10.x.101.0" };
        Assert.Equal("10.11.101.0", v.SiteNetwork);
    }

    [Fact]
    public void SiteNetwork_EmptySite_ReturnsEmpty()
    {
        var v = new VlanEntry { Site = "", NetworkAddress = "10.x.101.0" };
        Assert.Equal("", v.SiteNetwork);
    }

    [Fact]
    public void SiteNetwork_UnknownSite_ReturnsEmpty()
    {
        var v = new VlanEntry { Site = "UNKNOWN", NetworkAddress = "10.x.101.0" };
        Assert.Equal("", v.SiteNetwork);
    }

    [Fact]
    public void SiteNetwork_EmptyAddress_ReturnsEmpty()
    {
        var v = new VlanEntry { Site = "MEP-91", NetworkAddress = "" };
        Assert.Equal("", v.SiteNetwork);
    }

    // ── SiteGateway ──

    [Fact]
    public void SiteGateway_ReplacesPlaceholder()
    {
        var v = new VlanEntry { Site = "MEP-92", Gateway = "10.x.101.254" };
        Assert.Equal("10.12.101.254", v.SiteGateway);
    }

    [Fact]
    public void SiteGateway_EmptyGateway_ReturnsEmpty()
    {
        var v = new VlanEntry { Site = "MEP-91", Gateway = "" };
        Assert.Equal("", v.SiteGateway);
    }

    // ── BuildingNumberMap ──

    [Fact]
    public void BuildingNumberMap_CaseInsensitive()
    {
        Assert.True(VlanEntry.BuildingNumberMap.ContainsKey("mep-91"));
        Assert.True(VlanEntry.BuildingNumberMap.ContainsKey("MEP-91"));
        Assert.True(VlanEntry.BuildingNumberMap.ContainsKey("Mep-91"));
    }

    [Fact]
    public void BuildingNumberMap_HasExpectedSites()
    {
        Assert.True(VlanEntry.BuildingNumberMap.Count >= 20);
        Assert.Equal("11", VlanEntry.BuildingNumberMap["MEP-91"]);
        Assert.Equal("50", VlanEntry.BuildingNumberMap["RAD"]);
    }

    // ── PropertyChanged cascades ──

    [Fact]
    public void BlockLocked_NotifiesBlockLockedText_RowColor()
    {
        var v = new VlanEntry();
        var changed = new List<string>();
        v.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        v.BlockLocked = true;

        Assert.Contains("BlockLocked", changed);
        Assert.Contains("BlockLockedText", changed);
        Assert.Contains("RowColor", changed);
    }

    [Fact]
    public void IsBlocked_NotifiesRowColor_BlockLockedText()
    {
        var v = new VlanEntry();
        var changed = new List<string>();
        v.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        v.IsBlocked = true;

        Assert.Contains("IsBlocked", changed);
        Assert.Contains("RowColor", changed);
        Assert.Contains("BlockLockedText", changed);
    }

    [Fact]
    public void IsDefault_NotifiesRowColor()
    {
        var v = new VlanEntry();
        var changed = new List<string>();
        v.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        v.IsDefault = true;

        Assert.Contains("IsDefault", changed);
        Assert.Contains("RowColor", changed);
    }

    [Fact]
    public void Site_NotifiesSiteNetwork_SiteGateway_SitePrefix()
    {
        var v = new VlanEntry();
        var changed = new List<string>();
        v.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        v.Site = "MEP-91";

        Assert.Contains("Site", changed);
        Assert.Contains("SiteNetwork", changed);
        Assert.Contains("SiteGateway", changed);
        Assert.Contains("SitePrefix", changed);
    }

    [Fact]
    public void Subnet_NotifiesRowColor()
    {
        var v = new VlanEntry();
        var changed = new List<string>();
        v.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        v.Subnet = "/24";

        Assert.Contains("Subnet", changed);
        Assert.Contains("RowColor", changed);
    }
}
