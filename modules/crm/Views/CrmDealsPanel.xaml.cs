using System.Windows;

namespace Central.Module.CRM.Views;

public partial class CrmDealsPanel : System.Windows.Controls.UserControl
{
    private CrmDataService? _data;

    public CrmDealsPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public void SetDsn(string dsn) => _data = new CrmDataService(dsn);

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_data == null) return;
        try
        {
            var deals = await _data.LoadDealsAsync();
            DealsGrid.ItemsSource = deals;
            var openCount = deals.Count(d => d.IsOpen);
            var openValue = deals.Where(d => d.IsOpen).Sum(d => d.Value ?? 0);
            var weighted = deals.Where(d => d.IsOpen).Sum(d => d.WeightedValue);
            SummaryText.Text = $"{deals.Count} deals — {openCount} open, £{openValue:N0} pipeline, £{weighted:N0} weighted";
        }
        catch (Exception ex)
        {
            Central.Core.Services.NotificationService.Instance?.Error($"Deals load failed: {ex.Message}");
        }
    }
}
