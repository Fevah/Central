using Central.Engine.Models;

namespace Central.Tests.Models;

public class DashboardDataExtendedTests
{
    // ── DashboardData ──

    [Fact]
    public void DashboardData_AllPlatformDefaults()
    {
        var d = new DashboardData();
        Assert.Equal(0, d.DeviceCount);
        Assert.Equal(0, d.PrevDeviceCount);
        Assert.Equal(0, d.SwitchCount);
        Assert.Equal(0, d.PrevSwitchCount);
        Assert.Equal(0, d.UserCount);
        Assert.Equal(0, d.PrevUserCount);
        Assert.Equal(0, d.LinkCount);
        Assert.Equal(0, d.PrevLinkCount);
        Assert.Equal(0, d.VlanCount);
        Assert.Equal(0, d.PrevVlanCount);
        Assert.Equal(0, d.OpenTasks);
        Assert.Equal(0, d.PrevOpenTasks);
    }

    [Fact]
    public void DashboardData_AllServiceDeskDefaults()
    {
        var d = new DashboardData();
        Assert.Equal(0, d.SdOpenTickets);
        Assert.Equal(0, d.SdPrevOpenTickets);
        Assert.Equal(0, d.SdClosedToday);
        Assert.Equal(0, d.SdPrevClosedToday);
        Assert.Equal(0.0, d.SdAvgResolutionHours);
        Assert.Equal(0.0, d.SdPrevAvgResolutionHours);
        Assert.Equal(0.0, d.SdSlaCompliancePct);
        Assert.Equal(0.0, d.SdPrevSlaCompliancePct);
    }

    [Fact]
    public void DashboardData_SystemHealthDefaults()
    {
        var d = new DashboardData();
        Assert.Equal(0, d.SyncConfigCount);
        Assert.Equal(0, d.SyncFailures);
        Assert.Equal(0, d.PrevSyncFailures);
        Assert.Equal(0, d.AuthEvents24h);
        Assert.Equal(0, d.PrevAuthEvents24h);
        Assert.Equal(0, d.FailedLogins24h);
        Assert.Equal(0, d.PrevFailedLogins24h);
    }

    [Fact]
    public void DashboardData_RecentActivity_DefaultEmpty()
    {
        var d = new DashboardData();
        Assert.NotNull(d.RecentActivity);
        Assert.Empty(d.RecentActivity);
    }

    [Fact]
    public void DashboardData_SetPlatformCounts()
    {
        var d = new DashboardData
        {
            DeviceCount = 987,
            SwitchCount = 5,
            UserCount = 42,
            LinkCount = 150,
            VlanCount = 30,
            OpenTasks = 12
        };
        Assert.Equal(987, d.DeviceCount);
        Assert.Equal(5, d.SwitchCount);
        Assert.Equal(42, d.UserCount);
        Assert.Equal(150, d.LinkCount);
        Assert.Equal(30, d.VlanCount);
        Assert.Equal(12, d.OpenTasks);
    }

    [Fact]
    public void DashboardData_SetServiceDeskCounts()
    {
        var d = new DashboardData
        {
            SdOpenTickets = 45,
            SdClosedToday = 8,
            SdAvgResolutionHours = 12.5,
            SdSlaCompliancePct = 92.3
        };
        Assert.Equal(45, d.SdOpenTickets);
        Assert.Equal(8, d.SdClosedToday);
        Assert.Equal(12.5, d.SdAvgResolutionHours);
        Assert.Equal(92.3, d.SdSlaCompliancePct);
    }

    // ── ActivityItem ──

    [Fact]
    public void ActivityItem_Defaults()
    {
        var a = new ActivityItem();
        Assert.Equal("", a.Time);
        Assert.Equal("", a.Icon);
        Assert.Equal("", a.Message);
    }

    [Fact]
    public void ActivityItem_SetProperties()
    {
        var a = new ActivityItem
        {
            Time = "10:30 AM",
            Icon = "sync",
            Message = "Sync completed for ManageEngine"
        };
        Assert.Equal("10:30 AM", a.Time);
        Assert.Equal("sync", a.Icon);
        Assert.Equal("Sync completed for ManageEngine", a.Message);
    }

    [Fact]
    public void DashboardData_RecentActivity_CanBePopulated()
    {
        var d = new DashboardData
        {
            RecentActivity = new List<ActivityItem>
            {
                new() { Time = "09:00", Icon = "user", Message = "User logged in" },
                new() { Time = "09:05", Icon = "sync", Message = "Sync started" },
                new() { Time = "09:10", Icon = "check", Message = "Sync completed" }
            }
        };
        Assert.Equal(3, d.RecentActivity.Count);
        Assert.Equal("user", d.RecentActivity[0].Icon);
    }
}
