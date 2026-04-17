using System.Windows;
using Central.Engine.Auth;

namespace Central.Desktop;

/// <summary>
/// MFA enrollment dialog — generates TOTP secret, shows QR URI,
/// verifies code, generates recovery codes.
/// </summary>
public partial class MfaEnrollmentDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _secret;
    private readonly string _qrUri;
    private List<string>? _recoveryCodes;

    public bool MfaEnabled { get; private set; }
    public string Secret => _secret;
    public List<string>? RecoveryCodes => _recoveryCodes;

    // Delegates wired by caller
    public Func<string, List<string>, Task>? OnMfaEnabled { get; set; }

    public MfaEnrollmentDialog(string accountName)
    {
        _secret = TotpService.GenerateSecret();
        _qrUri = TotpService.GenerateQrUri(_secret, accountName);

        InitializeComponent();

        SecretKeyText.Text = FormatSecret(_secret);
        QrUriText.Text = _qrUri;
    }

    private static string FormatSecret(string secret)
    {
        // Add spaces every 4 chars for readability
        var formatted = new System.Text.StringBuilder();
        for (int i = 0; i < secret.Length; i++)
        {
            if (i > 0 && i % 4 == 0) formatted.Append(' ');
            formatted.Append(secret[i]);
        }
        return formatted.ToString();
    }

    private void CopyUri_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText(_qrUri);
        Central.Engine.Services.NotificationService.Instance?.Success("QR URI copied to clipboard");
    }

    private async void Verify_Click(object sender, RoutedEventArgs e)
    {
        var code = VerifyCodeEdit.Text?.Trim() ?? "";
        if (code.Length != 6)
        {
            VerifyStatus.Text = "Enter a 6-digit code";
            VerifyStatus.Foreground = System.Windows.Media.Brushes.Orange;
            return;
        }

        if (TotpService.VerifyCode(_secret, code))
        {
            VerifyStatus.Text = "Verified! MFA is now enabled.";
            VerifyStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(34, 197, 94)); // green

            // Generate recovery codes
            _recoveryCodes = TotpService.GenerateRecoveryCodes();
            RecoveryCodesText.Text = string.Join("\n", _recoveryCodes);
            RecoveryCodesPanel.Visibility = Visibility.Visible;

            // Notify caller
            if (OnMfaEnabled != null)
                await OnMfaEnabled(_secret, _recoveryCodes);

            MfaEnabled = true;
        }
        else
        {
            VerifyStatus.Text = "Invalid code — check your authenticator app and try again";
            VerifyStatus.Foreground = System.Windows.Media.Brushes.Red;
        }
    }

    private void VerifyCode_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
            Verify_Click(sender, e);
    }

    private void CopyRecoveryCodes_Click(object sender, RoutedEventArgs e)
    {
        if (_recoveryCodes != null)
        {
            System.Windows.Clipboard.SetText(string.Join("\n", _recoveryCodes));
            Central.Engine.Services.NotificationService.Instance?.Success("Recovery codes copied");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
