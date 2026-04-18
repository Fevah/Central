using System;
using System.Net.Http;
using System.Windows;
using Central.ApiClient;
using DevExpress.Xpf.Editors;

namespace Central.Module.Networking.Validation;

/// <summary>
/// Per-rule config editor. Exposes both levers the Rust side supports:
/// enabled (bool, nullable) and severity_override (Error/Warning/Info,
/// nullable). NULL values mean "use catalog default".
///
/// <para>Replaces the minimal "Toggle Rule" dialog that only let admins
/// flip enabled on/off. The two-field surface maps 1:1 to
/// <see cref="NetworkingEngineClient.SetRuleConfigAsync"/>.</para>
/// </summary>
public partial class EditRuleDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _baseUrl;
    private readonly Guid _tenantId;
    private readonly int? _actorUserId;
    private readonly ResolvedRuleDto _rule;

    public EditRuleDialog(string baseUrl, Guid tenantId, int? actorUserId,
        ResolvedRuleDto rule)
    {
        InitializeComponent();
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        _rule = rule;

        CodeLabel.Text = rule.Code;
        NameLabel.Text = rule.Name;
        DescriptionLabel.Text = rule.Description;
        CategoryLabel.Text = $"Category: {rule.Category}  ·  Default severity: {rule.DefaultSeverity}  ·  Default enabled: {(rule.DefaultEnabled ? "yes" : "no")}";

        EnabledCheck.IsChecked = rule.EffectiveEnabled;
        EnabledDefaultHint.Text = rule.DefaultEnabled ? "(default: enabled)" : "(default: disabled)";

        // Severity combo: (use default) / Error / Warning / Info.
        // HasTenantOverride alone doesn't tell us whether the severity
        // differs from default; compare strings to decide.
        var selected = rule.EffectiveSeverity == rule.DefaultSeverity
            ? "(use default)"
            : rule.EffectiveSeverity;
        SeverityCombo.EditValue = selected;
        SeverityDefaultHint.Text = $"(catalog default: {rule.DefaultSeverity})";
    }

    private async void OnOk(object sender, RoutedEventArgs e)
    {
        OkButton.IsEnabled = false;
        ClearError();
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            // Enabled: only send when the admin has diverged from default.
            // Sending null means "clear the override, use catalog
            // default" on the engine side.
            bool isEnabled = EnabledCheck.IsChecked == true;
            bool? enabledToSend = isEnabled == _rule.DefaultEnabled ? null : (bool?)isEnabled;

            string? severityToSend = null;
            var sel = (SeverityCombo.EditValue as string) ?? "(use default)";
            if (sel != "(use default)") severityToSend = sel;

            await client.SetRuleConfigAsync(_tenantId, _rule.Code,
                enabled: enabledToSend,
                severityOverride: severityToSend);

            DialogResult = true;
            Close();
        }
        catch (NetworkingEngineException ex) { ShowError($"Engine error ({ex.StatusCode}): {ex.Message}"); }
        catch (HttpRequestException ex)     { ShowError($"Network error: {ex.Message}"); }
        catch (Exception ex)                { ShowError($"Failed: {ex.Message}"); }
        finally { OkButton.IsEnabled = true; }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string msg) { ErrorLabel.Text = msg; ErrorLabel.Visibility = Visibility.Visible; }
    private void ClearError()          { ErrorLabel.Text = "";  ErrorLabel.Visibility = Visibility.Collapsed; }
}
