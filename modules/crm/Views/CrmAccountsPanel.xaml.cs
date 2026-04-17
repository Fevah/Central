using System.Windows;

namespace Central.Module.CRM.Views;

public partial class CrmAccountsPanel : System.Windows.Controls.UserControl
{
    private CrmDataService? _data;

    public CrmAccountsPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public void SetDsn(string dsn)
    {
        _data = new CrmDataService(dsn);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_data == null) return;
        try
        {
            var accounts = await _data.LoadAccountsAsync();
            AccountsGrid.ItemsSource = accounts;
            CountText.Text = $"{accounts.Count} accounts";
        }
        catch (Exception ex)
        {
            Central.Core.Services.NotificationService.Instance?.Error($"Accounts load failed: {ex.Message}");
        }
    }

    public async Task RefreshAsync()
    {
        if (_data == null) return;
        var accounts = await _data.LoadAccountsAsync();
        AccountsGrid.ItemsSource = accounts;
        CountText.Text = $"{accounts.Count} accounts";
    }
}
