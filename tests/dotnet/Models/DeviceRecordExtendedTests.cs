using Central.Engine.Models;

namespace Central.Tests.Models;

public class DeviceRecordExtendedTests
{
    [Fact]
    public void IsLinked_WithHostname_True()
    {
        var d = new DeviceRecord { LinkedHostname = "CORE01" };
        Assert.True(d.IsLinked);
    }

    [Fact]
    public void IsLinked_Empty_False()
    {
        var d = new DeviceRecord { LinkedHostname = "" };
        Assert.False(d.IsLinked);
    }

    [Fact]
    public void IsActive_Active_True()
    {
        var d = new DeviceRecord { Status = "Active" };
        Assert.True(d.IsActive);
    }

    [Fact]
    public void IsActive_Reserved_False()
    {
        var d = new DeviceRecord { Status = "RESERVED" };
        Assert.False(d.IsActive);
    }

    [Fact]
    public void IsActive_Decommissioned_False()
    {
        var d = new DeviceRecord { Status = "Decommissioned" };
        Assert.False(d.IsActive);
    }

    [Fact]
    public void PropertyChanged_Fires_OnAllKeys()
    {
        var d = new DeviceRecord();
        var changed = new List<string>();
        d.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        d.SwitchName = "SW1";
        d.Site = "MEP-91";
        d.DeviceType = "Switch";
        d.Building = "B91";
        d.Ip = "10.0.0.1";
        d.Status = "Active";
        d.Notes = "Test";

        Assert.Contains("SwitchName", changed);
        Assert.Contains("Site", changed);
        Assert.Contains("DeviceType", changed);
        Assert.Contains("Building", changed);
        Assert.Contains("Ip", changed);
        Assert.Contains("Status", changed);
        Assert.Contains("Notes", changed);
    }

    [Fact]
    public void DetailLinks_DefaultEmpty()
    {
        var d = new DeviceRecord();
        Assert.NotNull(d.DetailLinks);
        Assert.Empty(d.DetailLinks);
    }

    [Fact]
    public void Defaults_AllEmptyStrings()
    {
        var d = new DeviceRecord();
        Assert.Equal("", d.Id);
        Assert.Equal("", d.SwitchName);
        Assert.Equal("", d.Site);
        Assert.Equal("", d.DeviceType);
        Assert.Equal("", d.Building);
        Assert.Equal("", d.Region);
        Assert.Equal("", d.Status);
        Assert.Equal("", d.Ip);
        Assert.Equal("", d.ManagementIp);
        Assert.Equal("", d.LoopbackIp);
        Assert.Equal("", d.Asn);
        Assert.Equal("", d.Model);
        Assert.Equal("", d.SerialNumber);
        Assert.Equal("", d.Notes);
    }

    // ── DeviceLinkSummary ──

    [Fact]
    public void DeviceLinkSummary_Defaults()
    {
        var dls = new DeviceLinkSummary();
        Assert.Equal("", dls.LinkType);
        Assert.Equal(0, dls.LinkId);
        Assert.Equal("", dls.RemoteDevice);
        Assert.Equal("", dls.LocalPort);
        Assert.Equal("", dls.RemotePort);
        Assert.Equal("", dls.Vlan);
        Assert.Equal("", dls.Subnet);
        Assert.Equal("", dls.Status);
    }
}
