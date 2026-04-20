using Central.Engine.Net;
using Central.Engine.Net.Devices;

// Type alias resolves the `Module` naming collision introduced
// when the test project references Central.Module.* projects —
// `Module` on its own now binds to the Central.Module namespace.
using NetModule = Central.Engine.Net.Devices.Module;

namespace Central.Tests.Net;

public class DeviceModelTests
{
    [Fact]
    public void DeviceRole_DefaultsAreSafe()
    {
        var r = new DeviceRole();
        Assert.Equal(EntityStatus.Planned, r.Status);
        Assert.Equal(LockState.Open, r.LockState);
        Assert.Null(r.DefaultAsnKind);
        Assert.Null(r.DefaultLoopbackPrefix);
        // Default template is the generic shape — seeded roles
        // override via migration 093. This default is what a
        // newly-created role starts with if the admin doesn't set
        // one explicitly.
        Assert.Equal("{building_code}-{role_code}{instance}", r.NamingTemplate);
    }

    [Fact]
    public void Device_DefaultsToClosedMgmtPlane()
    {
        var d = new Device();
        // New devices should default to "no management yet" — the
        // dual-write trigger populates these fields on import.
        Assert.False(d.ManagementVrf);
        Assert.False(d.InbandEnabled);
        Assert.Null(d.ManagementIp);
        Assert.Null(d.LastPingOk);
    }

    [Fact]
    public void Module_DefaultsToLinecard()
    {
        var m = new NetModule();
        // Most chassis modules are linecards; PSU / transceiver are the
        // exceptions and callers set them explicitly.
        Assert.Equal(ModuleType.Linecard, m.ModuleType);
    }

    [Fact]
    public void Port_DefaultsToXeUnset()
    {
        var p = new Port();
        Assert.Equal("xe", p.InterfacePrefix);   // 10G+ is the fabric default on FS PicOS gear
        Assert.Equal(PortMode.Unset, p.PortMode);
        Assert.False(p.AdminUp);
        Assert.NotNull(p.ConfigJson);
        Assert.Empty(p.ConfigJson);
    }

    [Fact]
    public void AggregateEthernet_DefaultsToActiveLacpOneLink()
    {
        var ae = new AggregateEthernet();
        Assert.Equal(LacpMode.Active, ae.LacpMode);
        Assert.Equal(1, ae.MinLinks);
    }

    [Fact]
    public void Loopback_DefaultsStartAtZero()
    {
        var l = new Loopback();
        Assert.Equal(0, l.LoopbackNumber);       // lo0 is the natural default
        Assert.Null(l.IpAddressId);
    }

    [Fact]
    public void ModuleType_AllFiveValuesExist()
    {
        Assert.True(Enum.IsDefined(ModuleType.Linecard));
        Assert.True(Enum.IsDefined(ModuleType.Transceiver));
        Assert.True(Enum.IsDefined(ModuleType.PSU));
        Assert.True(Enum.IsDefined(ModuleType.Fan));
        Assert.True(Enum.IsDefined(ModuleType.Other));
    }

    [Fact]
    public void PortMode_AllFourValuesExist()
    {
        Assert.True(Enum.IsDefined(PortMode.Unset));
        Assert.True(Enum.IsDefined(PortMode.Access));
        Assert.True(Enum.IsDefined(PortMode.Trunk));
        Assert.True(Enum.IsDefined(PortMode.Routed));
    }

    [Fact]
    public void LacpMode_AllThreeValuesExist()
    {
        Assert.True(Enum.IsDefined(LacpMode.Active));
        Assert.True(Enum.IsDefined(LacpMode.Passive));
        Assert.True(Enum.IsDefined(LacpMode.Static));
    }

    [Fact]
    public void BuildingProfileRoleCount_DefaultsAreSafe()
    {
        var c = new BuildingProfileRoleCount();
        Assert.Equal(0, c.ExpectedCount);
        Assert.Equal(EntityStatus.Planned, c.Status);
    }
}
