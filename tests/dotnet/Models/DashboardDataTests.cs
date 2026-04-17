using Central.Engine.Models;

namespace Central.Tests.Models;

public class DashboardDataTests
{
    [Fact]
    public void Defaults()
    {
        var d = new DashboardData();
        Assert.Equal(0, d.DeviceCount);
        Assert.Equal(0, d.PrevDeviceCount);
        Assert.Equal(0, d.SwitchCount);
        Assert.Equal(0, d.UserCount);
        Assert.Equal(0, d.LinkCount);
        Assert.Equal(0, d.VlanCount);
        Assert.Equal(0, d.OpenTasks);
        Assert.Equal(0, d.SdOpenTickets);
        Assert.Equal(0.0, d.SdAvgResolutionHours);
        Assert.Equal(0.0, d.SdSlaCompliancePct);
        Assert.NotNull(d.RecentActivity);
        Assert.Empty(d.RecentActivity);
    }

    [Fact]
    public void RecentActivity_CanAddItems()
    {
        var d = new DashboardData();
        d.RecentActivity.Add(new ActivityItem { Time = "10:30", Icon = "+", Message = "Device added" });
        Assert.Single(d.RecentActivity);
        Assert.Equal("+", d.RecentActivity[0].Icon);
    }

    [Fact]
    public void ActivityItem_Defaults()
    {
        var a = new ActivityItem();
        Assert.Equal("", a.Time);
        Assert.Equal("", a.Icon);
        Assert.Equal("", a.Message);
    }
}
