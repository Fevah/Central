using Central.Core.Models;

namespace Central.Tests.Models;

public class MasterDeviceTests
{
    [Fact]
    public void Defaults()
    {
        var m = new MasterDevice();
        Assert.Equal(Guid.Empty, m.Id);
        Assert.Equal("", m.DeviceName);
        Assert.Equal("", m.DeviceType);
        Assert.Equal("", m.Region);
        Assert.Equal("", m.Building);
        Assert.Equal("", m.Status);
        Assert.Equal("", m.PrimaryIp);
        Assert.Equal("", m.ManagementIp);
        Assert.Equal("", m.LoopbackIp);
        Assert.Equal("", m.LoopbackSubnet);
        Assert.Equal("", m.MgmtL3Ip);
        Assert.Equal("", m.Asn);
        Assert.Equal("", m.MlagDomain);
        Assert.Equal("", m.AeRange);
        Assert.Equal("", m.Model);
        Assert.Equal("", m.SerialNumber);
        Assert.Equal("", m.UplinkSwitch);
        Assert.Equal("", m.UplinkPort);
        Assert.Equal("", m.Notes);
        Assert.Equal(0, m.P2PLinkCount);
        Assert.Equal(0, m.B2BLinkCount);
        Assert.Equal(0, m.FWLinkCount);
        Assert.Equal("", m.MstpPriority);
        Assert.Equal("", m.MlagPeer);
        Assert.False(m.HasConfig);
    }

    [Fact]
    public void PropertyChanged_SelectedFields()
    {
        var m = new MasterDevice();
        var changed = new List<string>();
        m.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        m.DeviceName = "CORE01";
        m.Building = "B91";
        m.P2PLinkCount = 5;
        m.HasConfig = true;

        Assert.Contains("DeviceName", changed);
        Assert.Contains("Building", changed);
        Assert.Contains("P2PLinkCount", changed);
        Assert.Contains("HasConfig", changed);
    }

    [Fact]
    public void AllLinkCounts_SetCorrectly()
    {
        var m = new MasterDevice { P2PLinkCount = 3, B2BLinkCount = 2, FWLinkCount = 1 };
        Assert.Equal(3, m.P2PLinkCount);
        Assert.Equal(2, m.B2BLinkCount);
        Assert.Equal(1, m.FWLinkCount);
    }
}
