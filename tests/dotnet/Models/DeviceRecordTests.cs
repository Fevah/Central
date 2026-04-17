using Central.Engine.Models;

namespace Central.Tests.Models;

public class DeviceRecordTests
{
    [Fact]
    public void IsLinked_True_WhenLinkedHostnameSet()
    {
        var d = new DeviceRecord { LinkedHostname = "MEP-91-CORE02" };
        Assert.True(d.IsLinked);
    }

    [Fact]
    public void IsLinked_False_WhenLinkedHostnameEmpty()
    {
        var d = new DeviceRecord { LinkedHostname = "" };
        Assert.False(d.IsLinked);
    }

    [Fact]
    public void IsActive_True_WhenStatusActive()
    {
        var d = new DeviceRecord { Status = "Active" };
        Assert.True(d.IsActive);
    }

    [Fact]
    public void IsActive_False_WhenStatusNotActive()
    {
        var d = new DeviceRecord { Status = "RESERVED" };
        Assert.False(d.IsActive);
    }

    [Fact]
    public void IsActive_False_WhenStatusDecommissioned()
    {
        var d = new DeviceRecord { Status = "Decommissioned" };
        Assert.False(d.IsActive);
    }

    [Fact]
    public void PropertyChanged_AllFieldsFire()
    {
        var d = new DeviceRecord();
        var changed = new List<string>();
        d.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        d.Id = "1";
        d.SwitchName = "CORE01";
        d.Site = "MEP-91";
        d.DeviceType = "Core";
        d.Building = "Building 91";
        d.Region = "UK";
        d.Status = "Active";
        d.Ip = "10.0.0.1";
        d.ManagementIp = "10.11.152.2";
        d.MgmtL3Ip = "10.11.120.1";
        d.LoopbackIp = "10.0.255.1";
        d.LoopbackSubnet = "/32";
        d.Asn = "65112";
        d.MlagDomain = "domain1";
        d.AeRange = "ae-1";
        d.Floor = "2";
        d.Rack = "R1";
        d.Model = "S5860-20SQ";
        d.SerialNumber = "SN123";
        d.UplinkSwitch = "CORE02";
        d.UplinkPort = "xe-1/1/30";
        d.Notes = "test";
        d.LinkedHostname = "CORE01";

        Assert.Contains("Id", changed);
        Assert.Contains("SwitchName", changed);
        Assert.Contains("Site", changed);
        Assert.Contains("DeviceType", changed);
        Assert.Contains("Building", changed);
        Assert.Contains("Region", changed);
        Assert.Contains("Status", changed);
        Assert.Contains("Ip", changed);
        Assert.Contains("ManagementIp", changed);
        Assert.Contains("LoopbackIp", changed);
        Assert.Contains("Asn", changed);
        Assert.Contains("Floor", changed);
        Assert.Contains("Rack", changed);
        Assert.Contains("Model", changed);
        Assert.Contains("SerialNumber", changed);
        Assert.Contains("Notes", changed);
        Assert.Contains("LinkedHostname", changed);
    }

    [Fact]
    public void StatusColor_Active_Green()
    {
        // IconOverrideService without overrides loaded returns code defaults
        var d = new DeviceRecord { Status = "Active" };
        Assert.Equal("#22C55E", d.StatusColor);
    }

    [Fact]
    public void StatusColor_Reserved_Amber()
    {
        var d = new DeviceRecord { Status = "RESERVED" };
        Assert.Equal("#F59E0B", d.StatusColor);
    }

    [Fact]
    public void StatusColor_Decommissioned_Red()
    {
        var d = new DeviceRecord { Status = "Decommissioned" };
        Assert.Equal("#EF4444", d.StatusColor);
    }

    [Fact]
    public void StatusColor_Maintenance_Purple()
    {
        var d = new DeviceRecord { Status = "Maintenance" };
        Assert.Equal("#8B5CF6", d.StatusColor);
    }

    [Fact]
    public void StatusColor_Unknown_Grey()
    {
        var d = new DeviceRecord { Status = "SomethingElse" };
        Assert.Equal("#6B7280", d.StatusColor);
    }

    [Fact]
    public void Defaults_EmptyStrings()
    {
        var d = new DeviceRecord();
        Assert.Equal("", d.Id);
        Assert.Equal("", d.SwitchName);
        Assert.Equal("", d.Site);
        Assert.Equal("", d.Status);
        Assert.Equal("", d.Ip);
        Assert.Equal("", d.LinkedHostname);
        Assert.False(d.IsLinked);
    }
}
