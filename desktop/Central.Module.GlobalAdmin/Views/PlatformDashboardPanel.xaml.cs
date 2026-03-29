namespace Central.Module.GlobalAdmin.Views;

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
}
