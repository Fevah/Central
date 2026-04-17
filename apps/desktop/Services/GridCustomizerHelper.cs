using System.Text.Json;
using DevExpress.Xpf.Grid;
using Central.Engine.Auth;
using Central.Engine.Models;

namespace Central.Desktop.Services;

/// <summary>
/// Attaches "Customize Grid..." and "Manage Filters..." context menu items to any GridControl.
/// Loads/saves settings per-user per-panel via panel_customizations table.
/// </summary>
public static class GridCustomizerHelper
{
    /// <summary>
    /// Attach grid customizer + saved filters to a panel's grid. Call once during MainWindow_Loaded.
    /// </summary>
    public static void Attach(GridControl grid, TableView view, string panelName, Central.Persistence.DbRepository repo)
    {
        // Load existing customization on attach
        _ = LoadAndApplyAsync(grid, view, panelName, repo);

        // Add context menu items to both column header and row context menus
        view.ShowGridMenu += (_, e) =>
        {
            if (e.MenuType != GridMenuType.Column && e.MenuType != GridMenuType.RowCell) return;

            // Add separator before our custom items
            e.Customizations.Add(new DevExpress.Xpf.Bars.BarItemSeparator());

            // ── Customize Grid ──
            var customizeItem = new DevExpress.Xpf.Bars.BarButtonItem { Content = "Customize Grid..." };
            customizeItem.ItemClick += async (_, _) =>
            {
                var currentSettings = ReadCurrentSettings(view);
                var dialog = new Central.Module.Admin.Views.GridCustomizerDialog();
                dialog.GridSettings = currentSettings;
                if (dialog.ShowDialog() == true)
                {
                    ApplySettings(view, dialog.GridSettings);
                    await SaveGridSettingsAsync(panelName, dialog.GridSettings, repo);
                }
            };
            e.Customizations.Add(customizeItem);

            // ── Manage Filters ──
            var filterItem = new DevExpress.Xpf.Bars.BarButtonItem { Content = "Manage Saved Filters..." };
            filterItem.ItemClick += async (_, _) =>
            {
                var userId = AuthContext.Instance.CurrentUser?.Id ?? 0;
                if (userId == 0) return;

                var filters = await repo.GetSavedFiltersAsync(userId, panelName);
                var dialog = new Central.Module.Admin.Views.SavedFilterDialog
                {
                    PanelName = panelName,
                    CurrentFilterString = grid.FilterString ?? ""
                };
                dialog.Load(filters);

                dialog.SaveFilter = async (name, filterExpr) =>
                {
                    await repo.UpsertSavedFilterAsync(userId, panelName, name, filterExpr);
                    dialog.Load(await repo.GetSavedFiltersAsync(userId, panelName));
                };
                dialog.DeleteFilter = async id =>
                    await repo.DeleteSavedFilterAsync(id);
                dialog.SetDefaultFilter = async id =>
                    await repo.SetDefaultSavedFilterAsync(userId, panelName, id);

                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedFilterString))
                {
                    grid.FilterString = dialog.SelectedFilterString;
                }
            };
            e.Customizations.Add(filterItem);

            // ── Separator + Quick Filter Presets ──
            e.Customizations.Add(new DevExpress.Xpf.Bars.BarItemSeparator());

            // Load saved filters inline as quick-apply items
            var userId2 = AuthContext.Instance.CurrentUser?.Id ?? 0;
            if (userId2 > 0)
            {
                _ = Task.Run(async () =>
                {
                    var savedFilters = await repo.GetSavedFiltersAsync(userId2, panelName);
                    if (savedFilters.Count > 0)
                    {
                        grid.Dispatcher.Invoke(() =>
                        {
                            foreach (var sf in savedFilters.Take(10))
                            {
                                var quickItem = new DevExpress.Xpf.Bars.BarCheckItem
                                {
                                    Content = $"Filter: {sf.FilterName}",
                                    IsChecked = grid.FilterString == sf.FilterExpr
                                };
                                quickItem.ItemClick += (_, _) =>
                                {
                                    grid.FilterString = sf.FilterExpr;
                                };
                                e.Customizations.Add(quickItem);
                            }
                        });
                    }
                });
            }

            // ── Configure Links ──
            var linkItem = new DevExpress.Xpf.Bars.BarButtonItem { Content = "Configure Links..." };
            linkItem.ItemClick += async (_, _) =>
            {
                var engine = Central.Engine.Shell.LinkEngine.Instance;
                var existingRules = engine.Rules
                    .Where(r => r.SourcePanel == panelName || r.TargetPanel == panelName)
                    .ToList();

                var dialog = new Central.Module.Admin.Views.LinkCustomizerDialog();
                dialog.SetPanelNames(engine.GetRegisteredGrids());
                dialog.Load(existingRules);

                if (dialog.ShowDialog() == true)
                {
                    // Remove old rules for this panel, add new ones
                    var oldRules = engine.Rules
                        .Where(r => r.SourcePanel == panelName || r.TargetPanel == panelName)
                        .ToList();
                    foreach (var old in oldRules) engine.RemoveRule(old);
                    foreach (var rule in dialog.Rules) engine.AddRule(rule);

                    // Persist
                    var userId = AuthContext.Instance.CurrentUser?.Id ?? 0;
                    if (userId > 0)
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(dialog.Rules.ToList());
                        await repo.UpsertPanelCustomizationAsync(userId, panelName, "link", "", json);
                    }
                }
            };
            e.Customizations.Add(linkItem);

            // ── Export to CSV ──
            var exportCsvItem = new DevExpress.Xpf.Bars.BarButtonItem { Content = "Export to CSV..." };
            exportCsvItem.ItemClick += (_, _) =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = ".csv",
                    FileName = $"{panelName}_{DateTime.Now:yyyyMMdd}.csv"
                };
                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        view.ExportToCsv(dlg.FileName);
                        Central.Engine.Services.NotificationService.Instance?.Success($"Exported to {dlg.FileName}");
                        _ = Central.Engine.Services.AuditService.Instance.LogExportAsync(panelName, $"CSV export: {dlg.FileName}");
                    }
                    catch (Exception ex)
                    {
                        Central.Engine.Services.NotificationService.Instance?.Error($"Export failed: {ex.Message}");
                    }
                }
            };
            e.Customizations.Add(exportCsvItem);

            // ── Export to Excel ──
            var exportXlsxItem = new DevExpress.Xpf.Bars.BarButtonItem { Content = "Export to Excel..." };
            exportXlsxItem.ItemClick += (_, _) =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    DefaultExt = ".xlsx",
                    FileName = $"{panelName}_{DateTime.Now:yyyyMMdd}.xlsx"
                };
                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        view.ExportToXlsx(dlg.FileName);
                        Central.Engine.Services.NotificationService.Instance?.Success($"Exported to {dlg.FileName}");
                        _ = Central.Engine.Services.AuditService.Instance.LogExportAsync(panelName, $"Excel export: {dlg.FileName}");
                    }
                    catch (Exception ex)
                    {
                        Central.Engine.Services.NotificationService.Instance?.Error($"Export failed: {ex.Message}");
                    }
                }
            };
            e.Customizations.Add(exportXlsxItem);

            // ── Export to PDF ──
            var exportPdfItem = new DevExpress.Xpf.Bars.BarButtonItem { Content = "Export to PDF..." };
            exportPdfItem.ItemClick += (_, _) =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF Document (*.pdf)|*.pdf|All files (*.*)|*.*",
                    DefaultExt = ".pdf",
                    FileName = $"{panelName}_{DateTime.Now:yyyyMMdd}.pdf"
                };
                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        view.ExportToPdf(dlg.FileName);
                        Central.Engine.Services.NotificationService.Instance?.Success($"Exported to {dlg.FileName}");
                        _ = Central.Engine.Services.AuditService.Instance.LogExportAsync(panelName, $"PDF export: {dlg.FileName}");
                    }
                    catch (Exception ex)
                    {
                        Central.Engine.Services.NotificationService.Instance?.Error($"Export failed: {ex.Message}");
                    }
                }
            };
            e.Customizations.Add(exportPdfItem);

            // ── Export to Clipboard ──
            var clipItem = new DevExpress.Xpf.Bars.BarButtonItem { Content = "Copy to Clipboard" };
            clipItem.ItemClick += (_, _) =>
            {
                try { grid.SelectAll(); grid.CopyToClipboard(); Central.Engine.Services.NotificationService.Instance?.Success("Copied to clipboard"); }
                catch { }
            };
            e.Customizations.Add(clipItem);

            // ── Clear Filter ──
            var clearItem = new DevExpress.Xpf.Bars.BarButtonItem { Content = "Clear All Filters" };
            clearItem.ItemClick += (_, _) => grid.FilterString = "";
            e.Customizations.Add(clearItem);

            e.Customizations.Add(new DevExpress.Xpf.Bars.BarItemSeparator());

            // ── Print Preview ──
            var printItem = new DevExpress.Xpf.Bars.BarButtonItem { Content = "Print Preview..." };
            printItem.ItemClick += (_, _) =>
            {
                try { view.ShowPrintPreview(null); }
                catch { Central.Engine.Services.NotificationService.Instance?.Error("Print preview not available"); }
            };
            e.Customizations.Add(printItem);

            // ── Column Chooser ──
            var colChooserItem = new DevExpress.Xpf.Bars.BarButtonItem { Content = "Column Chooser..." };
            colChooserItem.ItemClick += (_, _) =>
            {
                try { view.ShowColumnChooser(); }
                catch { }
            };
            e.Customizations.Add(colChooserItem);

            // ── Best Fit Columns ──
            var bestFitItem = new DevExpress.Xpf.Bars.BarButtonItem { Content = "Best Fit All Columns" };
            bestFitItem.ItemClick += (_, _) =>
            {
                try { view.BestFitColumns(); }
                catch { }
            };
            e.Customizations.Add(bestFitItem);

            // ── Select All / Deselect All ──
            var selectAllItem = new DevExpress.Xpf.Bars.BarButtonItem { Content = "Select All Rows" };
            selectAllItem.ItemClick += (_, _) => grid.SelectAll();
            e.Customizations.Add(selectAllItem);
        };

        // Register this grid with the LinkEngine for cross-panel filtering
        Central.Engine.Shell.LinkEngine.Instance.RegisterGrid(panelName, (field, op, value) =>
        {
            grid.Dispatcher.Invoke(() =>
            {
                if (value == null)
                    grid.FilterString = "";
                else
                    grid.FilterString = $"[{field}] {op} '{value}'";
            });
        });
    }

    private static GridSettings ReadCurrentSettings(TableView view)
    {
        return new GridSettings
        {
            RowHeight = (int)view.RowMinHeight,
            UseAlternatingRows = view.UseEvenRowBackground,
            ShowSummaryFooter = view.ShowTotalSummary,
            ShowGroupPanel = view.ShowGroupPanel,
            ShowAutoFilterRow = view.ShowAutoFilterRow
        };
    }

    private static void ApplySettings(TableView view, GridSettings settings)
    {
        view.RowMinHeight = settings.RowHeight;
        view.UseEvenRowBackground = settings.UseAlternatingRows;
        view.ShowTotalSummary = settings.ShowSummaryFooter;
        view.ShowGroupPanel = settings.ShowGroupPanel;
        view.ShowAutoFilterRow = settings.ShowAutoFilterRow;

        if (settings.HiddenColumns != null)
        {
            foreach (var col in view.Grid.Columns)
                col.Visible = !settings.HiddenColumns.Contains(col.FieldName);
        }
    }

    private static async Task LoadAndApplyAsync(GridControl grid, TableView view, string panelName, Central.Persistence.DbRepository repo)
    {
        try
        {
            var userId = AuthContext.Instance.CurrentUser?.Id ?? 0;
            if (userId == 0) return;

            // Apply saved grid settings
            var records = await repo.GetPanelCustomizationsAsync(userId, panelName);
            var gridRecord = records.FirstOrDefault(r => r.SettingType == "grid");
            if (gridRecord != null)
            {
                var settings = JsonSerializer.Deserialize<GridSettings>(gridRecord.SettingJson);
                if (settings != null)
                    grid.Dispatcher.Invoke(() => ApplySettings(view, settings));
            }

            // Apply default saved filter
            var filters = await repo.GetSavedFiltersAsync(userId, panelName);
            var defaultFilter = filters.FirstOrDefault(f => f.IsDefault);
            if (defaultFilter != null && !string.IsNullOrEmpty(defaultFilter.FilterExpr))
                grid.Dispatcher.Invoke(() => grid.FilterString = defaultFilter.FilterExpr);

            // Load link rules for this panel and add to the LinkEngine
            var linkRecords = await repo.GetPanelCustomizationsAsync(userId, panelName);
            var linkRecord = linkRecords.FirstOrDefault(r => r.SettingType == "link");
            if (linkRecord != null)
            {
                try
                {
                    var linkRules = System.Text.Json.JsonSerializer.Deserialize<List<Central.Engine.Models.LinkRule>>(linkRecord.SettingJson);
                    if (linkRules != null)
                        foreach (var rule in linkRules.Where(r => r.FilterOnSelect))
                            Central.Engine.Shell.LinkEngine.Instance.AddRule(rule);
                }
                catch { }
            }
        }
        catch { /* non-critical */ }
    }

    private static async Task SaveGridSettingsAsync(string panelName, GridSettings settings, Central.Persistence.DbRepository repo)
    {
        try
        {
            var userId = AuthContext.Instance.CurrentUser?.Id ?? 0;
            if (userId == 0) return;
            var json = JsonSerializer.Serialize(settings);
            await repo.UpsertPanelCustomizationAsync(userId, panelName, "grid", "", json);
        }
        catch { /* non-critical */ }
    }
}
