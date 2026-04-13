using Central.Core.Models;

namespace Central.Tests.Models;

public class AsnDefinitionTests
{
    [Fact]
    public void DisplayText_WithDescription()
    {
        var asn = new AsnDefinition { Asn = "65112", Description = "Core switches", DeviceCount = 3 };
        Assert.Equal("65112 — Core switches  (3)", asn.DisplayText);
    }

    [Fact]
    public void DisplayText_NoDescription()
    {
        var asn = new AsnDefinition { Asn = "65112", Description = "", DeviceCount = 5 };
        Assert.Equal("65112  (5)", asn.DisplayText);
    }

    [Fact]
    public void DisplayText_ZeroDevices()
    {
        var asn = new AsnDefinition { Asn = "65000", Description = "Empty", DeviceCount = 0 };
        Assert.Equal("65000 — Empty  (0)", asn.DisplayText);
    }

    [Fact]
    public void DetailDevices_DefaultEmpty()
    {
        var asn = new AsnDefinition();
        Assert.NotNull(asn.DetailDevices);
        Assert.Empty(asn.DetailDevices);
    }

    [Fact]
    public void PropertyChanged_AllFields()
    {
        var asn = new AsnDefinition();
        var changed = new List<string>();
        asn.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        asn.Id = 1;
        asn.Asn = "65112";
        asn.Description = "Test";
        asn.AsnType = "Private";
        asn.SortOrder = 2;
        asn.DeviceCount = 5;
        asn.Devices = "A,B,C";

        Assert.Contains("Id", changed);
        Assert.Contains("Asn", changed);
        Assert.Contains("Description", changed);
        Assert.Contains("AsnType", changed);
        Assert.Contains("SortOrder", changed);
        Assert.Contains("DeviceCount", changed);
        Assert.Contains("Devices", changed);
    }

    [Fact]
    public void AsnBoundDevice_Defaults()
    {
        var d = new AsnBoundDevice();
        Assert.Equal("", d.SwitchName);
        Assert.Equal("", d.Building);
        Assert.Equal("", d.DeviceType);
        Assert.Equal("", d.PrimaryIp);
    }
}
