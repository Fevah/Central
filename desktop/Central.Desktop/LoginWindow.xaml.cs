using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Central.Core.Auth;
using Central.Desktop.Auth;

namespace Central.Desktop;

public partial class LoginWindow : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _dsn;
    private readonly AuthenticationService _authService;

    /// <summary>Auth mode after successful login.</summary>
    public enum LoginMode { Windows, Manual, Offline }

    /// <summary>The mode used for successful login.</summary>
    public LoginMode ResultMode { get; private set; }

    /// <summary>True if login was successful (dialog result).</summary>
    public bool LoginSucceeded { get; private set; }

    /// <summary>Display model for SSO provider buttons.</summary>
    public class SsoProviderButton
    {
        public int ProviderId { get; set; }
        public string DisplayText { get; set; } = "";
        public string ProviderType { get; set; } = "";
        public System.Windows.Media.Brush ButtonColor { get; set; } = System.Windows.Media.Brushes.SteelBlue;
    }

    public LoginWindow(string dsn)
    {
        _dsn = dsn;
        _authService = new AuthenticationService(dsn);
        InitializeComponent();
        WindowsUserText.Text = Environment.UserName;
    }

    /// <summary>Overload accepting a pre-built AuthenticationService.</summary>
    public LoginWindow(string dsn, AuthenticationService authService)
    {
        _dsn = dsn;
        _authService = authService;
        InitializeComponent();
        WindowsUserText.Text = Environment.UserName;
    }

    // ── Load SSO providers on window open ─────────────────────────────────

    private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var providers = await _authService.GetAvailableProvidersAsync();
            var ssoProviders = providers
                .Where(p => p.ProviderType != "local" && p.IsEnabled)
                .OrderBy(p => p.Priority)
                .ToList();

            if (ssoProviders.Count > 0)
            {
                var buttons = new ObservableCollection<SsoProviderButton>();
                foreach (var p in ssoProviders)
                {
                    buttons.Add(new SsoProviderButton
                    {
                        ProviderId = p.Id,
                        DisplayText = p.ProviderType switch
                        {
                            "entra_id" => $"Sign in with Microsoft ({p.Name})",
                            "okta" => $"Sign in with Okta ({p.Name})",
                            "saml2" => $"Sign in with SSO ({p.Name})",
                            _ => $"Sign in — {p.Name}"
                        },
                        ProviderType = p.ProviderType,
                        ButtonColor = p.ProviderType switch
                        {
                            "entra_id" => new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(0, 120, 212)), // Microsoft blue
                            "okta" => new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(0, 125, 193)),  // Okta blue
                            "saml2" => new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(75, 85, 99)),   // Neutral grey
                            _ => System.Windows.Media.Brushes.SteelBlue
                        }
                    });
                }

                SsoButtonsList.ItemsSource = buttons;
                SsoProvidersPanel.Visibility = Visibility.Visible;
            }
        }
        catch { /* non-critical — SSO buttons just won't appear */ }
    }

    // ── SSO IdP Discovery ─────────────────────────────────────────────────

    private async void SsoDiscover_Click(object sender, RoutedEventArgs e)
    {
        var email = SsoEmailEdit.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
        {
            StatusText.Text = "Enter your email address to discover your SSO provider.";
            return;
        }

        StatusText.Text = "Discovering identity provider...";
        try
        {
            var provider = await _authService.DiscoverProviderAsync(email);
            if (provider == null)
            {
                StatusText.Text = $"No SSO provider configured for {email.Split('@')[1]}. Use manual login.";
                return;
            }

            await AuthenticateWithProviderAsync(provider.Id, email);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Discovery failed: {ex.Message}";
        }
    }

    private void SsoEmail_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            SsoDiscover_Click(sender, e);
    }

    private async void SsoProvider_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not int providerId) return;
        var email = SsoEmailEdit.Text?.Trim();
        await AuthenticateWithProviderAsync(providerId, email);
    }

    private async Task AuthenticateWithProviderAsync(int providerId, string? email)
    {
        StatusText.Text = "Authenticating via SSO...";
        try
        {
            var result = await _authService.AuthenticateAsync(providerId, new AuthenticationRequest
            {
                Email = email
            });

            if (!result.Success)
            {
                StatusText.Text = result.ErrorMessage ?? "SSO authentication failed.";
                return;
            }

            var established = await _authService.EstablishSessionAsync(result);
            if (!established)
            {
                StatusText.Text = "Failed to establish session.";
                return;
            }

            await UpdateLastLoginAsync(AuthContext.Instance.CurrentUser?.Id ?? 0);
            ResultMode = LoginMode.Manual; // SSO uses same result path
            LoginSucceeded = true;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"SSO failed: {ex.Message}";
        }
    }

    // ── Windows Authentication ────────────────────────────────────────────

    private async void WindowsLogin_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Authenticating...";
        WindowsLoginButton.IsEnabled = false;

        try
        {
            var result = await _authService.AuthenticateWindowsAsync();
            if (!result.Success)
            {
                StatusText.Text = result.ErrorMessage ?? "Windows authentication failed.";
                WindowsLoginButton.IsEnabled = true;
                return;
            }

            var established = await _authService.EstablishSessionAsync(result);
            if (!established)
            {
                StatusText.Text = "Failed to establish session.";
                WindowsLoginButton.IsEnabled = true;
                return;
            }

            await UpdateLastLoginAsync(AuthContext.Instance.CurrentUser?.Id ?? 0);
            ResultMode = LoginMode.Windows;
            LoginSucceeded = true;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Login failed: {ex.Message}";
            WindowsLoginButton.IsEnabled = true;
        }
    }

    // ── Manual Login ──────────────────────────────────────────────────────

    private async void ManualLogin_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameEdit.Text?.Trim() ?? "";
        var password = PasswordEdit.Password ?? "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            StatusText.Text = "Username and password required.";
            return;
        }

        StatusText.Text = "Authenticating...";
        ManualLoginButton.IsEnabled = false;

        try
        {
            var result = await _authService.AuthenticateLocalAsync(username, password);
            if (!result.Success)
            {
                StatusText.Text = result.ErrorMessage ?? "Invalid username or password.";
                ManualLoginButton.IsEnabled = true;
                return;
            }

            var established = await _authService.EstablishSessionAsync(result);
            if (!established)
            {
                StatusText.Text = "Failed to establish session.";
                ManualLoginButton.IsEnabled = true;
                return;
            }

            await UpdateLastLoginAsync(AuthContext.Instance.CurrentUser?.Id ?? 0);
            ResultMode = LoginMode.Manual;
            LoginSucceeded = true;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Login failed: {ex.Message}";
            ManualLoginButton.IsEnabled = true;
        }
    }

    // ── Offline ───────────────────────────────────────────────────────────

    private void OfflineLogin_Click(object sender, RoutedEventArgs e)
    {
        AuthContext.Instance.SetOfflineAdmin(Environment.UserName);
        ResultMode = LoginMode.Offline;
        LoginSucceeded = true;
        DialogResult = true;
    }

    private async Task UpdateLastLoginAsync(int userId)
    {
        if (userId <= 0) return;
        try
        {
            using var conn = new Npgsql.NpgsqlConnection(_dsn);
            await conn.OpenAsync();
            using var cmd = new Npgsql.NpgsqlCommand(
                "UPDATE app_users SET last_login_at = now(), login_count = COALESCE(login_count, 0) + 1 WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", userId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { /* non-critical */ }
    }
}
