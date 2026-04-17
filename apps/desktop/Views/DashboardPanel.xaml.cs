using System.Collections.ObjectModel;
using System.Windows;
using Central.Engine.Models;

namespace Central.Desktop.Views;

/// <summary>
/// Home dashboard panel — KPI overview cards for platform, service desk, and system health.
/// Uses the engine-level KpiCardBuilder for consistent card styling.
/// </summary>
public partial class DashboardPanel : System.Windows.Controls.UserControl
{
    public DashboardPanel() => InitializeComponent();

    // Delegates for loading data
    public Func<Task<DashboardData>>? LoadDashboardData { get; set; }

    public async Task LoadAsync()
    {
        if (LoadDashboardData == null) return;

        try
        {
            var data = await LoadDashboardData();

            // Platform KPIs
            PlatformKpiPanel.Children.Clear();
            PlatformKpiPanel.Children.Add(KpiCardBuilder.Build("Total Devices", data.DeviceCount, data.PrevDeviceCount, false));
            PlatformKpiPanel.Children.Add(KpiCardBuilder.Build("Active Switches", data.SwitchCount, data.PrevSwitchCount, false));
            PlatformKpiPanel.Children.Add(KpiCardBuilder.Build("Active Users", data.UserCount, data.PrevUserCount, false));
            PlatformKpiPanel.Children.Add(KpiCardBuilder.Build("Total Links", data.LinkCount, data.PrevLinkCount, false));
            PlatformKpiPanel.Children.Add(KpiCardBuilder.Build("VLANs", data.VlanCount, data.PrevVlanCount, false));
            PlatformKpiPanel.Children.Add(KpiCardBuilder.Build("Tasks Open", data.OpenTasks, data.PrevOpenTasks, true));

            // Service Desk KPIs
            ServiceDeskKpiPanel.Children.Clear();
            ServiceDeskKpiPanel.Children.Add(KpiCardBuilder.Build("Open Tickets", data.SdOpenTickets, data.SdPrevOpenTickets, true));
            ServiceDeskKpiPanel.Children.Add(KpiCardBuilder.Build("Closed Today", data.SdClosedToday, data.SdPrevClosedToday, false));
            ServiceDeskKpiPanel.Children.Add(KpiCardBuilder.Build("Avg Resolution", data.SdAvgResolutionHours, data.SdPrevAvgResolutionHours, true, "hours"));
            ServiceDeskKpiPanel.Children.Add(KpiCardBuilder.Build("SLA Compliant", data.SdSlaCompliancePct, data.SdPrevSlaCompliancePct, false, "pct"));

            // System Health
            SystemHealthPanel.Children.Clear();
            SystemHealthPanel.Children.Add(KpiCardBuilder.Build("Sync Configs", data.SyncConfigCount, 0, false));
            SystemHealthPanel.Children.Add(KpiCardBuilder.Build("Last Sync Failures", data.SyncFailures, data.PrevSyncFailures, true));
            SystemHealthPanel.Children.Add(KpiCardBuilder.Build("Auth Events (24h)", data.AuthEvents24h, data.PrevAuthEvents24h, false));
            SystemHealthPanel.Children.Add(KpiCardBuilder.Build("Failed Logins (24h)", data.FailedLogins24h, data.PrevFailedLogins24h, true));

            // Recent Activity
            var activities = new ObservableCollection<ActivityItem>();
            foreach (var a in data.RecentActivity.Take(20))
                activities.Add(a);
            ActivityList.ItemsSource = activities;

            LastRefreshText.Text = $"Last refreshed: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            LastRefreshText.Text = $"Error: {ex.Message}";
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }
}
