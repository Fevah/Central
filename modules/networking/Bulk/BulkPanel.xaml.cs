using System;
using System.Collections.Generic;
using System.IO;
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

namespace Central.Module.Networking.Bulk;

/// <summary>
/// Operator-facing bulk import + export workspace. One panel that
/// covers every entity in the engine's bulk surface
/// (devices / vlans / subnets / servers / links / dhcp-relay-targets):
/// download a CSV, edit it in Excel or in the embedded editor, and
/// validate-then-apply via the engine. Mirrors the round-trip flow
/// pinned by <c>tests/bulk_round_trip_integration.rs</c> so what
/// works in tests works here.
///
/// Dry-run is on by default — the apply button still confirms when the
/// checkbox is unticked, so the easiest path through the panel is
/// always validate-then-apply.
/// </summary>
public partial class BulkPanel : UserControl
{
    private string? _baseUrl;
    private Guid _tenantId;
    private int? _actorUserId;
    private CancellationTokenSource? _cts;

    /// <summary>Entity options exposed in the combo. Order = the
    /// natural reading order operators expect when looking at the
    /// networking estate (infra first, then VLAN/subnet, then
    /// servers/links/dhcp).</summary>
    private static readonly string[] EntityOptions = new[]
    {
        "Devices", "VLANs", "Subnets", "Servers", "Links", "DHCP relay targets",
    };

    /// <summary>Mode options exposed in the combo. Match the engine's
    /// <c>?mode=</c> query param — case-insensitive on the engine side
    /// but kept lowercase here for clarity.</summary>
    private static readonly string[] ModeOptions = new[] { "create", "upsert" };

    /// <summary>Transport format options. CSV keeps the in-panel
    /// editor as the source of truth (paste / edit / type); XLSX
    /// rounds-trips through the file picker because bytes aren't
    /// operator-editable in a text box.</summary>
    private static readonly string[] FormatOptions = new[] { "CSV", "XLSX" };

    public BulkPanel()
    {
        InitializeComponent();
        EntityCombo.ItemsSource = EntityOptions;
        EntityCombo.SelectedIndex = 0;
        ModeCombo.ItemsSource = ModeOptions;
        ModeCombo.SelectedIndex = 0;
        FormatCombo.ItemsSource = FormatOptions;
        FormatCombo.SelectedIndex = 0;
        StatusLabel.Text = "Pick an entity and click Export to download, or paste/load a CSV and Validate.";
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PanelMessageBus.Subscribe<NavigateToPanelMessage>(OnNavigate);
    }

    private bool IsXlsxMode => (FormatCombo.SelectedItem as string) == "XLSX";

    public void SetContext(string baseUrl, Guid tenantId, int? actorUserId = null)
    {
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) { /* lazy — wait for user action */ }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts = null;
    }

    private void OnNavigate(NavigateToPanelMessage msg)
    {
        if (msg.TargetPanel != "bulk") return;
        switch (msg.SelectItem as string)
        {
            case "action:export":   _ = ExportAsync(); break;
            case "action:validate": _ = RunImportAsync(forceDryRun: true); break;
            case "action:apply":    _ = RunImportAsync(forceDryRun: false); break;
        }
    }

    // ─── Toolbar handlers ───────────────────────────────────────────────

    private void OnExport(object sender, RoutedEventArgs e) => _ = ExportAsync();

    private void OnOpenFile(object sender, RoutedEventArgs e)
    {
        // Filter adapts to the selected format. CSV loads into the
        // editor (operator can edit before Validate/Apply). XLSX is
        // binary — load bytes into a pending buffer and hint the
        // operator to Validate/Apply (which will bypass the editor
        // for XLSX and send the bytes directly).
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = IsXlsxMode
                ? "XLSX files (*.xlsx)|*.xlsx"
                : "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = IsXlsxMode ? "Load XLSX file" : "Load CSV body",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            if (IsXlsxMode)
            {
                _pendingXlsxBytes = File.ReadAllBytes(dlg.FileName);
                _pendingXlsxFileName = Path.GetFileName(dlg.FileName);
                // Editor shows an advisory note so operators know the
                // next Validate/Apply targets bytes, not typed text.
                CsvBox.Text =
                    $"[XLSX buffered — {_pendingXlsxFileName} · " +
                    $"{_pendingXlsxBytes.Length:N0} bytes]\r\n" +
                    "Click Validate or Apply to send the workbook to the engine.";
                StatusLabel.Text = $"Loaded {_pendingXlsxFileName} · {_pendingXlsxBytes.Length:N0} bytes buffered for import";
            }
            else
            {
                CsvBox.Text = File.ReadAllText(dlg.FileName);
                _pendingXlsxBytes = null;
                _pendingXlsxFileName = null;
                StatusLabel.Text = $"Loaded {Path.GetFileName(dlg.FileName)} · {CsvBox.Text.Length:N0} chars";
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Open failed: {ex.Message}";
        }
    }

    /// <summary>Buffered XLSX bytes from the file picker, waiting
    /// for the next Validate / Apply. null when we're in CSV mode
    /// or no file has been picked yet.</summary>
    private byte[]? _pendingXlsxBytes;
    private string? _pendingXlsxFileName;

    private void OnValidate(object sender, RoutedEventArgs e) => _ = RunImportAsync(forceDryRun: true);

    private void OnApply(object sender, RoutedEventArgs e)
    {
        // Confirmation gate: when the operator has the dry-run
        // checkbox UNticked, we're about to write to the DB.
        // Spell that out instead of trusting the checkbox label.
        if (!(DryRunCheck.IsChecked ?? true))
        {
            var entity = (string)EntityCombo.SelectedItem ?? "";
            var mode = (string)ModeCombo.SelectedItem ?? "create";
            var prompt = $"Apply this CSV to {entity} in {mode} mode?\n\n" +
                         "This will write to the database. " +
                         (mode == "upsert"
                             ? "Existing rows will be UPDATED, new rows INSERTED."
                             : "Existing rows will be REJECTED, new rows INSERTED.") +
                         "\n\nThe apply runs in a single transaction — any row error rolls back the whole batch.";
            if (MessageBox.Show(prompt, "Confirm bulk apply",
                    MessageBoxButton.OKCancel, MessageBoxImage.Warning,
                    MessageBoxResult.Cancel) != MessageBoxResult.OK)
                return;
        }
        _ = RunImportAsync(forceDryRun: false);
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        CsvBox.Clear();
        _pendingXlsxBytes = null;
        _pendingXlsxFileName = null;
        OutcomesGrid.ItemsSource = null;
        SummaryBar.Visibility = Visibility.Collapsed;
        StatusLabel.Text = "Cleared.";
    }

    // ─── Export ─────────────────────────────────────────────────────────

    private async Task ExportAsync()
    {
        if (!RequireContext()) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        ExportButton.IsEnabled = false;
        StatusLabel.Text = "Exporting…";
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl!);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var entityKey = (string)EntityCombo.SelectedItem;

            // CSV path: editor is the source of truth — always populate
            // it so the operator can edit in-panel. Offer a save dialog
            // too. XLSX path: fetch the CSV *as well* so the editor
            // still shows a readable preview (the engine's XLSX
            // endpoint returns binary bytes that a TextBox can't
            // render) but the save-to-file target is the XLSX bytes
            // from the dedicated endpoint.
            var csv = await FetchCsvAsync(client, entityKey, _tenantId, ct);
            CsvBox.Text = csv;

            if (IsXlsxMode)
            {
                var xlsx = await FetchXlsxAsync(client, entityKey, _tenantId, ct);
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "XLSX files (*.xlsx)|*.xlsx",
                    FileName = SuggestedFileName(entityKey, "xlsx"),
                    Title = "Save export",
                };
                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllBytes(dlg.FileName, xlsx);
                    StatusLabel.Text = $"Exported · saved to {Path.GetFileName(dlg.FileName)} · {xlsx.Length:N0} bytes · editor shows CSV preview";
                }
                else
                {
                    StatusLabel.Text = $"Exported into editor (CSV preview · {csv.Length:N0} chars) — XLSX not saved";
                }
            }
            else
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = SuggestedFileName(entityKey, "csv"),
                    Title = "Save export",
                };
                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllText(dlg.FileName, csv);
                    StatusLabel.Text = $"Exported · saved to {Path.GetFileName(dlg.FileName)} · {csv.Length:N0} chars";
                }
                else
                {
                    StatusLabel.Text = $"Exported into editor · {csv.Length:N0} chars (not saved to disk)";
                }
            }
        }
        catch (OperationCanceledException) { /* ignore */ }
        catch (NetworkingEngineException ex) { StatusLabel.Text = $"Engine error ({ex.StatusCode}): {ex.Message}"; }
        catch (HttpRequestException ex)     { StatusLabel.Text = $"Network error: {ex.Message}"; }
        catch (Exception ex)                { StatusLabel.Text = $"Export failed: {ex.Message}"; }
        finally { ExportButton.IsEnabled = true; }
    }

    private static Task<string> FetchCsvAsync(NetworkingEngineClient client, string entityKey,
        Guid tenantId, CancellationToken ct)
        => entityKey switch
        {
            "Devices"            => client.ExportDevicesCsvAsync(tenantId, ct),
            "VLANs"              => client.ExportVlansCsvAsync(tenantId, ct),
            "Subnets"            => client.ExportSubnetsCsvAsync(tenantId, ct),
            "Servers"            => client.ExportServersCsvAsync(tenantId, ct),
            "Links"              => client.ExportLinksCsvAsync(tenantId, ct),
            "DHCP relay targets" => client.ExportDhcpRelayTargetsCsvAsync(tenantId, ct),
            _ => throw new InvalidOperationException($"Unknown entity '{entityKey}'"),
        };

    /// <summary>XLSX-export transport — same switch shape as
    /// FetchCsvAsync but hits the <c>export.xlsx</c> endpoint variants
    /// which return the workbook bytes verbatim.</summary>
    private static Task<byte[]> FetchXlsxAsync(NetworkingEngineClient client, string entityKey,
        Guid tenantId, CancellationToken ct)
        => entityKey switch
        {
            "Devices"            => client.ExportDevicesXlsxAsync(tenantId, ct),
            "VLANs"              => client.ExportVlansXlsxAsync(tenantId, ct),
            "Subnets"            => client.ExportSubnetsXlsxAsync(tenantId, ct),
            "Servers"            => client.ExportServersXlsxAsync(tenantId, ct),
            "Links"              => client.ExportLinksXlsxAsync(tenantId, ct),
            "DHCP relay targets" => client.ExportDhcpRelayTargetsXlsxAsync(tenantId, ct),
            _ => throw new InvalidOperationException($"Unknown entity '{entityKey}'"),
        };

    // ─── Import (dry-run or apply) ──────────────────────────────────────

    private async Task RunImportAsync(bool forceDryRun)
    {
        if (!RequireContext()) return;

        // Precondition check adapts to the format:
        //   CSV  → editor text must be non-blank
        //   XLSX → bytes must be buffered from Open file
        if (IsXlsxMode)
        {
            if (_pendingXlsxBytes is null or { Length: 0 })
            {
                StatusLabel.Text = "XLSX mode needs a file — click Open file… and pick an .xlsx first.";
                return;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(CsvBox.Text))
            {
                StatusLabel.Text = "Editor is empty — paste, type, or load a CSV first.";
                return;
            }
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        ValidateButton.IsEnabled = false;
        ApplyButton.IsEnabled = false;
        var dryRun = forceDryRun || (DryRunCheck.IsChecked ?? true);
        var mode = (string)ModeCombo.SelectedItem ?? "create";
        StatusLabel.Text = dryRun ? "Validating…" : "Applying…";
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl!);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var entityKey = (string)EntityCombo.SelectedItem;
            ImportValidationResultDto result;
            if (IsXlsxMode)
            {
                result = await ImportXlsxBytesAsync(
                    client, entityKey, _tenantId, _pendingXlsxBytes!, dryRun, mode, ct);
            }
            else
            {
                result = await ImportCsvAsync(
                    client, entityKey, _tenantId, CsvBox.Text, dryRun, mode, ct);
            }
            ApplyResult(result);
        }
        catch (OperationCanceledException) { /* ignore */ }
        catch (NetworkingEngineException ex) { StatusLabel.Text = $"Engine error ({ex.StatusCode}): {ex.Message}"; }
        catch (HttpRequestException ex)     { StatusLabel.Text = $"Network error: {ex.Message}"; }
        catch (Exception ex)                { StatusLabel.Text = $"Import failed: {ex.Message}"; }
        finally
        {
            ValidateButton.IsEnabled = true;
            ApplyButton.IsEnabled = true;
        }
    }

    private static Task<ImportValidationResultDto> ImportCsvAsync(
        NetworkingEngineClient client, string entityKey, Guid tenantId, string body,
        bool dryRun, string mode, CancellationToken ct)
        => entityKey switch
        {
            "Devices"            => client.ImportDevicesCsvAsync(tenantId, body, dryRun, mode, ct),
            "VLANs"              => client.ImportVlansCsvAsync(tenantId, body, dryRun, mode, ct),
            "Subnets"            => client.ImportSubnetsCsvAsync(tenantId, body, dryRun, mode, ct),
            "Servers"            => client.ImportServersCsvAsync(tenantId, body, dryRun, mode, ct),
            "Links"              => client.ImportLinksCsvAsync(tenantId, body, dryRun, mode, ct),
            "DHCP relay targets" => client.ImportDhcpRelayTargetsCsvAsync(tenantId, body, dryRun, mode, ct),
            _ => throw new InvalidOperationException($"Unknown entity '{entityKey}'"),
        };

    /// <summary>XLSX-import transport — sends the raw bytes to the
    /// <c>import.xlsx</c> endpoint. Engine wraps calamine for parse
    /// → CSV conversion and runs the import through the same
    /// validate-then-apply pipeline as the CSV endpoint, so outcomes
    /// come back in the same ImportValidationResultDto shape.</summary>
    private static Task<ImportValidationResultDto> ImportXlsxBytesAsync(
        NetworkingEngineClient client, string entityKey, Guid tenantId, byte[] bytes,
        bool dryRun, string mode, CancellationToken ct)
        => entityKey switch
        {
            "Devices"            => client.ImportDevicesXlsxAsync(tenantId, bytes, dryRun, mode, ct),
            "VLANs"              => client.ImportVlansXlsxAsync(tenantId, bytes, dryRun, mode, ct),
            "Subnets"            => client.ImportSubnetsXlsxAsync(tenantId, bytes, dryRun, mode, ct),
            "Servers"            => client.ImportServersXlsxAsync(tenantId, bytes, dryRun, mode, ct),
            "Links"              => client.ImportLinksXlsxAsync(tenantId, bytes, dryRun, mode, ct),
            "DHCP relay targets" => client.ImportDhcpRelayTargetsXlsxAsync(tenantId, bytes, dryRun, mode, ct),
            _ => throw new InvalidOperationException($"Unknown entity '{entityKey}'"),
        };

    private void ApplyResult(ImportValidationResultDto result)
    {
        var rows = result.Outcomes
            .Select(o => new OutcomeRow(o.RowNumber, o.Ok, o.Identifier,
                                        o.Errors is { Count: > 0 } ? string.Join("; ", o.Errors) : ""))
            .ToList();
        OutcomesGrid.ItemsSource = rows;

        SummaryBar.Visibility = Visibility.Visible;
        // Banner reads as: "DRY-RUN · 5 valid / 1 invalid · NOT APPLIED"
        // The applied/dry-run state matters more than the row count so
        // it goes first in the bold label.
        var verb = result.DryRun ? "DRY-RUN" : (result.Applied ? "APPLIED" : "NOT APPLIED");
        SummaryLabel.Text = verb;
        SummaryDetail.Text = $"{result.TotalRows} rows · {result.Valid} valid · {result.Invalid} invalid";

        StatusLabel.Text = result.Applied
            ? $"Applied {result.Valid} row{(result.Valid == 1 ? "" : "s")} · {DateTime.Now:HH:mm:ss}"
            : (result.DryRun
                ? $"Validated · {result.Valid}/{result.TotalRows} pass · {DateTime.Now:HH:mm:ss}"
                : $"Not applied · {result.Invalid} invalid · {DateTime.Now:HH:mm:ss}");
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    private bool RequireContext()
    {
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty)
        {
            StatusLabel.Text = "No tenant context — set base URL + tenant first.";
            return false;
        }
        return true;
    }

    private static string SuggestedFileName(string entityKey, string ext)
    {
        var slug = entityKey.ToLowerInvariant().Replace(' ', '-');
        return $"{slug}-{DateTime.Now:yyyyMMdd-HHmmss}.{ext}";
    }
}

/// <summary>Flat row shape for the outcomes grid. Mirrors
/// <see cref="ImportRowOutcomeDto"/> with the errors flattened to a
/// single string so the grid can display them in one column.</summary>
internal sealed record OutcomeRow(int RowNumber, bool Ok, string Identifier, string ErrorText);
