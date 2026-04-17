using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.Grid;
using Central.Engine.Models;
using Central.Engine.Services;

namespace Central.Module.Admin.Views;

public partial class IntegrationsPanel : System.Windows.Controls.UserControl
{
    public ObservableCollection<Integration> Integrations { get; } = new();
    public ObservableCollection<IntegrationLogEntry> LogEntries { get; } = new();

    // Delegates wired by shell
    public Func<Integration, Task>? SaveIntegration { get; set; }
    public Func<int, string, string, Task>? SaveCredential { get; set; }
    public Func<int, Task<List<IntegrationLogEntry>>>? LoadLog { get; set; }
    public Func<int, string, Task<string?>>? GetCredential { get; set; }

    private IntegrationService? _activeService;

    public IntegrationsPanel()
    {
        InitializeComponent();
        IntegrationsGrid.ItemsSource = Integrations;
        LogGrid.ItemsSource = LogEntries;
    }

    public GridControl Grid => IntegrationsGrid;

    private async void IntegrationsGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        if (e.NewItem is not Integration integration) return;

        // Load credentials into form
        if (GetCredential != null)
        {
            var config = System.Text.Json.JsonDocument.Parse(integration.ConfigJson).RootElement;
            OAuthUrlEdit.EditValue = config.TryGetProperty("oauth_url", out var ou) ? ou.GetString() : "";
            ClientIdEdit.EditValue = await GetCredential(integration.Id, "client_id") ?? "";
            ClientSecretEdit.EditValue = await GetCredential(integration.Id, "client_secret") ?? "";
            RefreshTokenEdit.EditValue = await GetCredential(integration.Id, "refresh_token") ?? "";
            ConfigJsonEdit.EditValue = integration.ConfigJson;
        }

        // Load log
        if (LoadLog != null)
        {
            var entries = await LoadLog(integration.Id);
            LogEntries.Clear();
            foreach (var entry in entries) LogEntries.Add(entry);
        }

        // Create service instance
        _activeService = new IntegrationService(integration.Name)
        {
            OAuthUrl = OAuthUrlEdit.EditValue?.ToString() ?? "",
            BaseUrl = integration.BaseUrl,
            ClientId = ClientIdEdit.EditValue?.ToString(),
            ClientSecret = ClientSecretEdit.EditValue?.ToString(),
            RefreshToken = RefreshTokenEdit.EditValue?.ToString()
        };

        StatusLabel.Text = integration.IsEnabled ? "Enabled" : "Disabled";
    }

    private async void ExchangeCode_Click(object sender, RoutedEventArgs e)
    {
        if (_activeService == null || IntegrationsGrid.CurrentItem is not Integration integration) return;

        var authCode = AuthCodeEdit.EditValue?.ToString();
        if (string.IsNullOrWhiteSpace(authCode))
        {
            StatusLabel.Text = "Enter auth code first";
            return;
        }

        _activeService.ClientId = ClientIdEdit.EditValue?.ToString();
        _activeService.ClientSecret = ClientSecretEdit.EditValue?.ToString();
        _activeService.OAuthUrl = OAuthUrlEdit.EditValue?.ToString() ?? "";

        StatusLabel.Text = "Exchanging code...";
        var (refreshToken, accessToken, error) = await _activeService.ExchangeAuthCodeAsync(authCode);

        if (error != null)
        {
            StatusLabel.Text = $"Error: {error}";
            return;
        }

        RefreshTokenEdit.EditValue = refreshToken;
        StatusLabel.Text = $"Success — refresh token obtained. Access token valid for 1 hour.";

        // Save credentials encrypted
        if (SaveCredential != null && refreshToken != null)
        {
            await SaveCredential(integration.Id, "refresh_token", refreshToken);
            if (accessToken != null)
                await SaveCredential(integration.Id, "access_token", accessToken);
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        if (_activeService == null) { StatusLabel.Text = "Select an integration first"; return; }

        StatusLabel.Text = "Testing connection...";
        var (ok, message) = await _activeService.TestConnectionAsync();
        StatusLabel.Text = ok ? $"✅ {message}" : $"❌ {message}";
    }

    private async void SetupOAuth_Click(object sender, RoutedEventArgs e)
    {
        if (IntegrationsGrid.CurrentItem is not Integration integration) return;

        // Open browser to Zoho OAuth consent page
        var clientId = ClientIdEdit.EditValue?.ToString();
        if (string.IsNullOrEmpty(clientId))
        {
            StatusLabel.Text = "Enter Client ID first";
            return;
        }

        var config = System.Text.Json.JsonDocument.Parse(integration.ConfigJson).RootElement;
        var dc = config.TryGetProperty("data_centre", out var dcProp) ? dcProp.GetString() : "eu";
        var domain = dc == "eu" ? "zoho.eu" : dc == "in" ? "zoho.in" : "zoho.com";

        var url = $"https://accounts.{domain}/oauth/v2/auth?response_type=code&client_id={clientId}&scope=SDPOnDemand.requests.ALL,SDPOnDemand.assets.ALL&redirect_uri=https://localhost&access_type=offline";

        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }

        StatusLabel.Text = "Browser opened — authorize and paste the code from the redirect URL";
    }

    private async void ToggleEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (IntegrationsGrid.CurrentItem is not Integration integration) return;
        integration.IsEnabled = !integration.IsEnabled;
        if (SaveIntegration != null) await SaveIntegration(integration);
        StatusLabel.Text = integration.IsEnabled ? "Integration enabled" : "Integration disabled";
    }

    private async void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        if (IntegrationsGrid.CurrentItem is not Integration integration) return;

        // Save credentials
        if (SaveCredential != null)
        {
            var clientId = ClientIdEdit.EditValue?.ToString() ?? "";
            var clientSecret = ClientSecretEdit.EditValue?.ToString() ?? "";
            if (!string.IsNullOrEmpty(clientId)) await SaveCredential(integration.Id, "client_id", clientId);
            if (!string.IsNullOrEmpty(clientSecret)) await SaveCredential(integration.Id, "client_secret", clientSecret);
        }

        // Save config JSON
        var configJson = ConfigJsonEdit.EditValue?.ToString();
        if (!string.IsNullOrEmpty(configJson))
            integration.ConfigJson = configJson;

        // Save integration
        if (SaveIntegration != null) await SaveIntegration(integration);

        StatusLabel.Text = "Configuration saved";
    }
}
