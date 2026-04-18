using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Central.ApiClient;
using Central.Engine.Shell;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using UserControl = System.Windows.Controls.UserControl;

namespace Central.Module.Networking.Validation;

/// <summary>
/// Phase-9a validation rules + violations panel. Two tabs:
/// <list type="bullet">
///   <item>Rules — per-tenant merged catalog (default + tenant override),
///     read-only grid. Toggling a rule's <c>enabled</c> or severity is a
///     ribbon action on the selected row (keeps the grid inline-edit-free
///     so the DX grid doesn't dispatch an accidental PUT on every
///     cell-focus change).</item>
///   <item>Violations — populated by <c>Run All</c> / <c>Run Selected</c>
///     ribbon actions. Sorted by severity descending so Errors surface
///     before Warnings before Info.</item>
/// </list>
///
/// <para>Driven entirely by the Rust networking-engine over
/// <see cref="NetworkingEngineClient"/>; no direct DB access.</para>
/// </summary>
public partial class ValidationPanel : UserControl
{
    private string? _baseUrl;
    private Guid _tenantId;
    private int? _actorUserId;
    private CancellationTokenSource? _cts;
    private List<ResolvedRuleDto> _rules = new();

    public ValidationPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PanelMessageBus.Subscribe<NavigateToPanelMessage>(OnNavigate);
        PanelMessageBus.Subscribe<RefreshPanelMessage>(OnRefresh);
    }

    public void SetContext(string baseUrl, Guid tenantId, int? actorUserId = null)
    {
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        if (IsLoaded) _ = ReloadAsync();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_baseUrl) && _tenantId != Guid.Empty)
            _ = ReloadAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts = null;
    }

    // ─── Message routing ─────────────────────────────────────────────────

    private void OnNavigate(NavigateToPanelMessage msg)
    {
        if (msg.TargetPanel != "validation") return;
        switch (msg.SelectItem as string)
        {
            case "action:runAll":      _ = RunAsync(null); break;
            case "action:runSelected": RunSelected();      break;
            case "action:editRule":    EditSelected();     break;
            default:                   _ = ReloadAsync();   break;
        }
    }

    /// <summary>Double-click a rule row to open the edit dialog — the
    /// main path for admins who are already hovering the grid.</summary>
    private void OnRuleDoubleClick(object sender, DevExpress.Xpf.Grid.RowDoubleClickEventArgs e)
        => EditSelected();

    private void OnRefresh(RefreshPanelMessage msg)
    {
        if (msg.TargetPanel != "validation") return;
        _ = ReloadAsync();
    }

    // ─── Rules: reload catalog + tenant state ───────────────────────────

    public async Task ReloadAsync()
    {
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty)
        {
            StatusLabel.Text = "No tenant context";
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        StatusLabel.Text = "Loading…";
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            _rules = await client.ListValidationRulesAsync(_tenantId, ct);
            RulesGrid.ItemsSource = _rules;

            var enabled = _rules.Count(r => r.EffectiveEnabled);
            var overrides = _rules.Count(r => r.HasTenantOverride);
            StatusLabel.Text = $"{_rules.Count} rules · {enabled} enabled · " +
                                $"{overrides} tenant override{(overrides == 1 ? "" : "s")} · " +
                                $"loaded {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException) { /* ignore */ }
        catch (NetworkingEngineException ex) { StatusLabel.Text = $"Engine error ({ex.StatusCode}): {ex.Message}"; }
        catch (HttpRequestException ex)     { StatusLabel.Text = $"Network error: {ex.Message}"; }
        catch (Exception ex)                { StatusLabel.Text = $"Load failed: {ex.Message}"; }
    }

    // ─── Run (all or selected rule) ─────────────────────────────────────

    private void RunSelected()
    {
        var rule = RulesGrid.CurrentItem as ResolvedRuleDto;
        if (rule is null)
        {
            MessageBox.Show(
                "Select a rule to run just that one, or use 'Run All'.",
                "No rule selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _ = RunAsync(rule.Code);
    }

    private async Task RunAsync(string? ruleCode)
    {
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty) return;

        var scope = ruleCode ?? "all enabled rules";
        RunSummaryLabel.Text = $"Running {scope}…";
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var result = await client.RunValidationAsync(_tenantId, ruleCode);
            // Sort by severity (Error > Warning > Info) then rule code
            // so the worst surfaces at the top — admins triage from there.
            var ordered = result.Violations
                .OrderBy(v => SeverityRank(v.Severity))
                .ThenBy(v => v.RuleCode)
                .ToList();
            ViolationsGrid.ItemsSource = ordered;

            RunSummaryLabel.Text =
                $"Ran {result.RulesRun} rule{(result.RulesRun == 1 ? "" : "s")} · " +
                $"{result.TotalViolations} violation{(result.TotalViolations == 1 ? "" : "s")} · " +
                $"{result.RulesWithFindings} rule{(result.RulesWithFindings == 1 ? "" : "s")} with findings · " +
                $"at {DateTime.Now:HH:mm:ss}";
        }
        catch (NetworkingEngineException ex)
        {
            RunSummaryLabel.Text = $"Engine error ({ex.StatusCode}): {ex.Message}";
        }
        catch (HttpRequestException ex)
        {
            RunSummaryLabel.Text = $"Network error: {ex.Message}";
        }
        catch (Exception ex)
        {
            RunSummaryLabel.Text = $"Run failed: {ex.Message}";
        }
    }

    private static int SeverityRank(string sev) => sev switch
    {
        "Error" => 0, "Warning" => 1, "Info" => 2, _ => 3,
    };

    // ─── Edit rule (full config) ────────────────────────────────────────

    private void EditSelected()
    {
        var rule = RulesGrid.CurrentItem as ResolvedRuleDto;
        if (rule is null)
        {
            MessageBox.Show(
                "Select a rule to edit.",
                "No rule selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty) return;

        var dialog = new EditRuleDialog(_baseUrl!, _tenantId, _actorUserId, rule)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() == true) _ = ReloadAsync();
    }
}
