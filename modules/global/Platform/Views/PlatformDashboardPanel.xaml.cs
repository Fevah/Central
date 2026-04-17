using System.Collections.Generic;
using DevExpress.Xpf.Charts;

namespace Central.Module.Global.Platform;

public partial class PlatformDashboardPanel : System.Windows.Controls.UserControl
{
    public PlatformDashboardPanel()
    {
        InitializeComponent();
    }

    public void UpdateMetrics(long totalTenants, long activeTenants, long totalUsers, long verifiedUsers, long activeSubs)
    {
        TotalTenantsText.Text = totalTenants.ToString();
        ActiveTenantsText.Text = $"{activeTenants} active";
        TotalUsersText.Text = totalUsers.ToString();
        VerifiedUsersText.Text = $"{verifiedUsers} verified";
        ActiveSubsText.Text = activeSubs.ToString();
    }

    public void UpdateTierChart(List<(string Tier, int Count)> data)
    {
        TierPieSeries.Points.Clear();
        foreach (var (tier, count) in data)
            TierPieSeries.Points.Add(new SeriesPoint(tier, count));
    }

    public void UpdateUsersByTenantChart(List<(string Slug, int Count)> data)
    {
        UsersByTenantSeries.Points.Clear();
        foreach (var (slug, count) in data)
            UsersByTenantSeries.Points.Add(new SeriesPoint(slug, count));
    }

    public void UpdateModuleAdoptionChart(List<(string Module, int Count)> data)
    {
        ModuleAdoptionSeries.Points.Clear();
        foreach (var (module, count) in data)
            ModuleAdoptionSeries.Points.Add(new SeriesPoint(module, count));
    }
}
