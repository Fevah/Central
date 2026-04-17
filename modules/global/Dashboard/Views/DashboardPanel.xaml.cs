using System.Windows;
using System.Windows.Controls;
using Central.Engine.Models;
using Central.Engine.Shell;
using Central.Engine.Services;
using Npgsql;
using WC = System.Windows.Controls;
using WMedia = System.Windows.Media;

namespace Central.Module.Global.Dashboard;

public partial class DashboardPanel : System.Windows.Controls.UserControl
{
    private string? _dsn;

    public DashboardPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        BtnPingAll.Click += (_, _) => PanelMessageBus.Publish(new NavigateToPanelMessage("switches", "action:ping-all"));
        BtnRefreshAll.Click += (_, _) => PanelMessageBus.Publish(new RefreshPanelMessage("*"));
        BtnExportAll.Click += (_, _) => PanelMessageBus.Publish(new NavigateToPanelMessage("devices", "action:export"));

        // Listen for refresh messages
        PanelMessageBus.Subscribe<RefreshPanelMessage>(msg =>
        {
            if (msg.TargetPanel is "DashboardPanel" or "*")
                Dispatcher.InvokeAsync(() => _ = LoadAsync());
        });
    }

    public void SetDsn(string dsn)
    {
        _dsn = dsn;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_dsn)) return;

        try
        {
            var stats = await LoadStatsAsync();
            BuildInfraCards(stats);
            BuildDeviceCards(stats);
            BuildTaskCards(stats);
            LoadActivity();
        }
        catch (Exception ex)
        {
            NotificationService.Instance?.Error($"Dashboard load failed: {ex.Message}");
        }
    }

    private void BuildInfraCards(DashboardStats s)
    {
        InfraCards.Children.Clear();
        InfraCards.Children.Add(KpiCardBuilder.Build("Total Switches", s.TotalSwitches, 0, false));
        InfraCards.Children.Add(KpiCardBuilder.Build("Online", s.SwitchesOnline, 0, false));
        InfraCards.Children.Add(KpiCardBuilder.Build("Offline", s.SwitchesOffline, 0, true));
        InfraCards.Children.Add(KpiCardBuilder.Build("Avg Latency", s.AvgPingMs, 0, true, "float1"));
        InfraCards.Children.Add(BuildStatusCard("DB", s.DbOnline));
    }

    private void BuildDeviceCards(DashboardStats s)
    {
        DeviceCards.Children.Clear();
        DeviceCards.Children.Add(KpiCardBuilder.Build("Total Devices", s.TotalDevices, 0, false));
        DeviceCards.Children.Add(KpiCardBuilder.Build("Active", s.DevicesActive, 0, false));
        DeviceCards.Children.Add(KpiCardBuilder.Build("Reserved", s.DevicesReserved, 0, false));
        DeviceCards.Children.Add(KpiCardBuilder.Build("VLANs", s.TotalVlans, 0, false));
        DeviceCards.Children.Add(KpiCardBuilder.Build("BGP Peers", s.TotalBgpPeers, 0, false));
    }

    private void BuildTaskCards(DashboardStats s)
    {
        TaskCards.Children.Clear();
        TaskCards.Children.Add(KpiCardBuilder.Build("Open Tasks", s.TasksOpen, 0, true));
        TaskCards.Children.Add(KpiCardBuilder.Build("In Progress", s.TasksInProgress, 0, false));
        TaskCards.Children.Add(KpiCardBuilder.Build("Completed", s.TasksCompleted, 0, false));
        TaskCards.Children.Add(KpiCardBuilder.Build("Overdue", s.TasksOverdue, 0, true));
    }

    private void LoadActivity()
    {
        var recent = NotificationService.Instance?.Recent;
        if (recent != null)
            ActivityList.ItemsSource = recent.Take(20).Select(n => new
            {
                Timestamp = n.Timestamp,
                Icon = n.Type switch
                {
                    NotificationType.Success => "\u2713",
                    NotificationType.Warning => "\u26A0",
                    NotificationType.Error   => "\u2717",
                    _                        => "\u2139"
                },
                Message = $"{n.Title}: {n.Message}"
            }).ToList();
    }

    private static WC.Border BuildStatusCard(string label, bool online)
    {
        var color = online ? "#22C55E" : "#EF4444";
        var text = online ? "Online" : "Offline";
        var panel = new WC.StackPanel { Margin = new Thickness(8, 6, 8, 6) };
        panel.Children.Add(new WC.TextBlock
        {
            Text = label, Foreground = new WMedia.SolidColorBrush((WMedia.Color)WMedia.ColorConverter.ConvertFromString("#909090")!),
            FontSize = 11, Margin = new Thickness(0, 0, 0, 2)
        });
        panel.Children.Add(new WC.TextBlock
        {
            Text = text, FontSize = 22, FontWeight = FontWeights.SemiBold,
            Foreground = new WMedia.SolidColorBrush((WMedia.Color)WMedia.ColorConverter.ConvertFromString(color)!)
        });
        return new WC.Border
        {
            Child = panel, BorderBrush = new WMedia.SolidColorBrush((WMedia.Color)WMedia.ColorConverter.ConvertFromString("#333")!),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Margin = new Thickness(3)
        };
    }

    private async Task<DashboardStats> LoadStatsAsync()
    {
        var stats = new DashboardStats();
        await using var conn = new NpgsqlConnection(_dsn);
        await conn.OpenAsync();

        stats.DbOnline = true;

        // Switch stats
        await using (var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*),
                   COUNT(*) FILTER (WHERE last_ping_ok = true),
                   COUNT(*) FILTER (WHERE last_ping_ok = false OR last_ping_ok IS NULL),
                   COALESCE(AVG(last_ping_ms) FILTER (WHERE last_ping_ok = true), 0)
            FROM switches", conn))
        {
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                stats.TotalSwitches = r.GetInt32(0);
                stats.SwitchesOnline = r.GetInt32(1);
                stats.SwitchesOffline = r.GetInt32(2);
                stats.AvgPingMs = r.GetDouble(3);
            }
        }

        // Device stats
        await using (var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*),
                   COUNT(*) FILTER (WHERE status = 'Active' OR status IS NULL),
                   COUNT(*) FILTER (WHERE status = 'Reserved')
            FROM switch_guide WHERE is_deleted IS NOT TRUE", conn))
        {
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                stats.TotalDevices = r.GetInt32(0);
                stats.DevicesActive = r.GetInt32(1);
                stats.DevicesReserved = r.GetInt32(2);
            }
        }

        // VLAN + BGP counts
        try
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT (SELECT COUNT(*) FROM vlan_inventory), (SELECT COUNT(*) FROM bgp_neighbors)", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                stats.TotalVlans = r.GetInt32(0);
                stats.TotalBgpPeers = r.GetInt32(1);
            }
        }
        catch { /* tables may not exist */ }

        // Task stats
        try
        {
            await using var cmd = new NpgsqlCommand(@"
                SELECT COUNT(*) FILTER (WHERE status IN ('New','Open','Reopened')),
                       COUNT(*) FILTER (WHERE status = 'In Progress'),
                       COUNT(*) FILTER (WHERE status IN ('Closed','Done','Resolved')),
                       COUNT(*) FILTER (WHERE due_date < NOW() AND status NOT IN ('Closed','Done','Resolved'))
                FROM tasks", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                stats.TasksOpen = r.GetInt32(0);
                stats.TasksInProgress = r.GetInt32(1);
                stats.TasksCompleted = r.GetInt32(2);
                stats.TasksOverdue = r.GetInt32(3);
            }
        }
        catch { /* tasks table may not exist */ }

        return stats;
    }

    private class DashboardStats
    {
        public int TotalSwitches, SwitchesOnline, SwitchesOffline;
        public double AvgPingMs;
        public int TotalDevices, DevicesActive, DevicesReserved;
        public int TotalVlans, TotalBgpPeers;
        public int TasksOpen, TasksInProgress, TasksCompleted, TasksOverdue;
        public bool DbOnline;
    }
}
