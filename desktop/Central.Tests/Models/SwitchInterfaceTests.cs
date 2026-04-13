using Central.Core.Models;

namespace Central.Tests.Models;

public class SwitchInterfaceTests
{
    // ── StatusColor ──

    [Theory]
    [InlineData("Up", "#22C55E")]
    [InlineData("up", "#22C55E")]
    [InlineData("Down", "#EF4444")]
    [InlineData("down", "#EF4444")]
    [InlineData("", "#6B7280")]
    [InlineData("unknown", "#6B7280")]
    public void StatusColor_ByLinkStatus(string linkStatus, string expectedColor)
    {
        var iface = new SwitchInterface { LinkStatus = linkStatus };
        Assert.Equal(expectedColor, iface.StatusColor);
    }

    [Fact]
    public void LinkStatus_Change_NotifiesStatusColor()
    {
        var iface = new SwitchInterface();
        var changed = new List<string>();
        iface.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        iface.LinkStatus = "Up";

        Assert.Contains("LinkStatus", changed);
        Assert.Contains("StatusColor", changed);
    }

    [Fact]
    public void AdminStatus_Change_NotifiesStatusColor()
    {
        var iface = new SwitchInterface();
        var changed = new List<string>();
        iface.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        iface.AdminStatus = "Enabled";

        Assert.Contains("AdminStatus", changed);
        Assert.Contains("StatusColor", changed);
    }

    // ── Parse ──

    [Fact]
    public void Parse_PicOsFormat_ExtractsInterfaces()
    {
        var output = @"Interface            Management  Status  Flow Control  Duplex  Speed         Description
----------           ----------  ------  ------------  ------  -----         -----------
xe-1/1/1             Enabled     Up      Disabled      Full    10000         Uplink-CORE02
xe-1/1/2             Enabled     Down    Disabled      Full    10000         Server01
lo0                  Enabled     Up      Disabled      Full    0             Loopback
";
        var switchId = Guid.NewGuid();
        var result = SwitchInterface.Parse(switchId, output);

        Assert.Equal(3, result.Count);
        Assert.Equal("xe-1/1/1", result[0].InterfaceName);
        Assert.Equal("Enabled", result[0].AdminStatus);
        Assert.Equal("Up", result[0].LinkStatus);
        Assert.Equal("10000", result[0].Speed);
        Assert.Equal("Uplink-CORE02", result[0].Description);
        Assert.Equal(switchId, result[0].SwitchId);

        Assert.Equal("xe-1/1/2", result[1].InterfaceName);
        Assert.Equal("Down", result[1].LinkStatus);

        Assert.Equal("lo0", result[2].InterfaceName);
    }

    [Fact]
    public void Parse_EmptyOutput_ReturnsEmpty()
    {
        var result = SwitchInterface.Parse(Guid.NewGuid(), "");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_SkipsNonInterfaceLines()
    {
        var output = @"Interface  Status  Link  Speed  Description
---------  ------  ----  -----  -----------
xe-1/1/1   up      up    10G    Server
something  up      up    1G     Not an iface
vlan-101   up      up    N/A    VLAN 101
";
        var result = SwitchInterface.Parse(Guid.NewGuid(), output);
        // "something" doesn't have -, /, "lo", or "vlan" prefix — should be skipped
        Assert.Equal(2, result.Count);
        Assert.Equal("xe-1/1/1", result[0].InterfaceName);
        Assert.Equal("vlan-101", result[1].InterfaceName);
    }

    // ── MergeOptics ──

    [Fact]
    public void MergeOptics_UpdatesMatchingInterfaces()
    {
        var interfaces = new List<SwitchInterface>
        {
            new() { InterfaceName = "xe-1/1/1" },
            new() { InterfaceName = "xe-1/1/2" },
        };

        var optics = new List<InterfaceOptics>
        {
            new() { InterfaceName = "xe-1/1/1", ModuleType = "10G_SFP+", TxPowerDbm = -2.5m, RxPowerDbm = -8.3m, TempC = 35.5m },
        };

        SwitchInterface.MergeOptics(interfaces, optics);

        Assert.Equal("10G_SFP+", interfaces[0].ModuleType);
        Assert.Contains("-2.5", interfaces[0].TxPower);
        Assert.Contains("-8.3", interfaces[0].RxPower);
        Assert.Contains("35.5", interfaces[0].OpticsTemp);
        Assert.Equal("#22C55E", interfaces[0].RxColor); // > -20 = green

        // Unmatched interface should be unchanged
        Assert.Equal("", interfaces[1].ModuleType);
    }

    [Fact]
    public void MergeOptics_RxColor_Red_NoLight()
    {
        var interfaces = new List<SwitchInterface> { new() { InterfaceName = "xe-1/1/1" } };
        var optics = new List<InterfaceOptics> { new() { InterfaceName = "xe-1/1/1", RxPowerDbm = -35m } };

        SwitchInterface.MergeOptics(interfaces, optics);
        Assert.Equal("#EF4444", interfaces[0].RxColor); // <= -30 = red
    }

    [Fact]
    public void MergeOptics_RxColor_Yellow_Marginal()
    {
        var interfaces = new List<SwitchInterface> { new() { InterfaceName = "xe-1/1/1" } };
        var optics = new List<InterfaceOptics> { new() { InterfaceName = "xe-1/1/1", RxPowerDbm = -25m } };

        SwitchInterface.MergeOptics(interfaces, optics);
        Assert.Equal("#F59E0B", interfaces[0].RxColor); // -30 < x <= -20 = yellow
    }

    [Fact]
    public void MergeOptics_NullOptics_DoesNothing()
    {
        var interfaces = new List<SwitchInterface> { new() { InterfaceName = "xe-1/1/1" } };
        SwitchInterface.MergeOptics(interfaces, null!);
        Assert.Equal("", interfaces[0].ModuleType);
    }

    [Fact]
    public void MergeOptics_EmptyOptics_DoesNothing()
    {
        var interfaces = new List<SwitchInterface> { new() { InterfaceName = "xe-1/1/1" } };
        SwitchInterface.MergeOptics(interfaces, new List<InterfaceOptics>());
        Assert.Equal("", interfaces[0].ModuleType);
    }

    // ── MergeLldp ──

    [Fact]
    public void MergeLldp_MergesNeighborInfo()
    {
        var interfaces = new List<SwitchInterface>
        {
            new() { InterfaceName = "xe-1/1/1" },
            new() { InterfaceName = "xe-1/1/2" },
        };

        var lldpOutput = @"Local Interface  Chassis Id          Port Id      Management Address  Host Name
--------------  ---------           -------      ------------------  ---------
xe-1/1/1         aa:bb:cc:dd:ee:ff  xe-1/1/30    10.0.0.1            CORE02
";

        SwitchInterface.MergeLldp(interfaces, lldpOutput);

        Assert.Equal("CORE02", interfaces[0].LldpHost);
        Assert.Equal("xe-1/1/30", interfaces[0].LldpPort);

        // Unmatched interface should be unchanged
        Assert.Equal("", interfaces[1].LldpHost);
    }

    [Fact]
    public void MergeLldp_EmptyOutput_DoesNothing()
    {
        var interfaces = new List<SwitchInterface> { new() { InterfaceName = "xe-1/1/1" } };
        SwitchInterface.MergeLldp(interfaces, "");
        Assert.Equal("", interfaces[0].LldpHost);
    }

    [Fact]
    public void MergeLldp_NullOutput_DoesNothing()
    {
        var interfaces = new List<SwitchInterface> { new() { InterfaceName = "xe-1/1/1" } };
        SwitchInterface.MergeLldp(interfaces, null!);
        Assert.Equal("", interfaces[0].LldpHost);
    }
}
