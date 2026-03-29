namespace Central.Core.Models;

/// <summary>Data transfer object for the home dashboard KPIs.</summary>
public class DashboardData
{
    // Platform
    public int DeviceCount { get; set; }
    public int PrevDeviceCount { get; set; }
    public int SwitchCount { get; set; }
    public int PrevSwitchCount { get; set; }
    public int UserCount { get; set; }
    public int PrevUserCount { get; set; }
    public int LinkCount { get; set; }
    public int PrevLinkCount { get; set; }
    public int VlanCount { get; set; }
    public int PrevVlanCount { get; set; }
    public int OpenTasks { get; set; }
    public int PrevOpenTasks { get; set; }

    // Service Desk
    public int SdOpenTickets { get; set; }
    public int SdPrevOpenTickets { get; set; }
    public int SdClosedToday { get; set; }
    public int SdPrevClosedToday { get; set; }
    public double SdAvgResolutionHours { get; set; }
    public double SdPrevAvgResolutionHours { get; set; }
    public double SdSlaCompliancePct { get; set; }
    public double SdPrevSlaCompliancePct { get; set; }

    // System Health
    public int SyncConfigCount { get; set; }
    public int SyncFailures { get; set; }
    public int PrevSyncFailures { get; set; }
    public int AuthEvents24h { get; set; }
    public int PrevAuthEvents24h { get; set; }
    public int FailedLogins24h { get; set; }
    public int PrevFailedLogins24h { get; set; }

    // Activity
    public List<ActivityItem> RecentActivity { get; set; } = new();
}

/// <summary>Single activity feed item.</summary>
public class ActivityItem
{
    public string Time { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Message { get; set; } = "";
}
