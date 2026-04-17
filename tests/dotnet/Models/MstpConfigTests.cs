using Central.Engine.Models;

namespace Central.Tests.Models;

public class MstpConfigTests
{
    [Fact]
    public void PropertyChanged_AllFieldsFire()
    {
        var m = new MstpConfig();
        var changed = new List<string>();
        m.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        m.Id = 1;
        m.Building = "MEP-91";
        m.DeviceName = "CORE02";
        m.DeviceRole = "Master";
        m.MstpPriority = "6000";
        m.Notes = "MSTP master bridge";
        m.Status = "Active";

        Assert.Contains("Id", changed);
        Assert.Contains("Building", changed);
        Assert.Contains("DeviceName", changed);
        Assert.Contains("DeviceRole", changed);
        Assert.Contains("MstpPriority", changed);
        Assert.Contains("Notes", changed);
        Assert.Contains("Status", changed);
    }

    [Fact]
    public void Defaults()
    {
        var m = new MstpConfig();
        Assert.Equal(0, m.Id);
        Assert.Equal("", m.Building);
        Assert.Equal("", m.DeviceName);
        Assert.Equal("Active", m.Status);
    }
}
