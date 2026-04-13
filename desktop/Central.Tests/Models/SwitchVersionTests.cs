using Central.Core.Models;

namespace Central.Tests.Models;

public class SwitchVersionTests
{
    [Fact]
    public void Parse_BasicOutput_ExtractsFields()
    {
        var switchId = Guid.NewGuid();
        var output = @"
Hardware Model: S5860-20SQ
Serial Number: SN12345678
System Uptime: 42 days 3:15:00
MAC Address: AA:BB:CC:DD:EE:FF
Linux Version: 4.19.67
Linux Released: 2024-01-15
Software Version: 4.6.0
Software Released: 2024-03-01
OVS Version: 2.9.0
OVS Released: 2024-02-01
".Trim();

        var v = SwitchVersion.Parse(switchId, output);

        Assert.Equal(switchId, v.SwitchId);
        Assert.Equal("S5860-20SQ", v.HardwareModel);
        Assert.Equal("SN12345678", v.SerialNumber);
        Assert.Contains("42 days", v.Uptime);
        Assert.Equal("AA:BB:CC:DD:EE:FF", v.MacAddress);
        Assert.Equal("4.19.67", v.LinuxVersion);
        Assert.Equal("2024-01-15", v.LinuxDate);
        Assert.Equal("4.6.0", v.L2L3Version);
        Assert.Equal("2024-03-01", v.L2L3Date);
        Assert.Equal("2.9.0", v.OvsVersion);
        Assert.Equal("2024-02-01", v.OvsDate);
        Assert.Equal(output, v.RawOutput);
    }

    [Fact]
    public void Parse_EmptyOutput_DefaultFields()
    {
        var v = SwitchVersion.Parse(Guid.NewGuid(), "");
        Assert.Equal("", v.HardwareModel);
        Assert.Equal("", v.SerialNumber);
        Assert.Equal("", v.MacAddress);
        Assert.Equal("", v.Uptime);
        Assert.Equal("", v.LinuxVersion);
    }

    [Fact]
    public void Parse_NoColons_SkipsLines()
    {
        var v = SwitchVersion.Parse(Guid.NewGuid(), "some output with no colon delimiters");
        Assert.Equal("", v.HardwareModel);
    }

    [Fact]
    public void Parse_MACEthernetVariant_ExtractsMac()
    {
        var output = "Base Ethernet MAC Address: 00:11:22:33:44:55";
        var v = SwitchVersion.Parse(Guid.NewGuid(), output);
        Assert.Equal("00:11:22:33:44:55", v.MacAddress);
    }

    [Fact]
    public void Parse_L2L3Version_FromOsKey()
    {
        var output = "L2/L3 Software Version: 5.0.1";
        var v = SwitchVersion.Parse(Guid.NewGuid(), output);
        Assert.Equal("5.0.1", v.L2L3Version);
    }

    [Fact]
    public void Parse_CapturedAt_IsSetToNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var v = SwitchVersion.Parse(Guid.NewGuid(), "");
        Assert.True(v.CapturedAt >= before);
        Assert.True(v.CapturedAt <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void Parse_WindowsLineEndings_Works()
    {
        var output = "Serial Number: ABC123\r\nHardware Model: S5860\r\n";
        var v = SwitchVersion.Parse(Guid.NewGuid(), output);
        Assert.Equal("ABC123", v.SerialNumber);
        Assert.Equal("S5860", v.HardwareModel);
    }
}
