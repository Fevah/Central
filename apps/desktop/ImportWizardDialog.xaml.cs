using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Central.Core.Integration;

namespace Central.Desktop;

/// <summary>
/// Import Wizard — guided CSV-to-database import with field mapping.
/// Uses the sync engine's CsvImportAgent + field converters for transformation.
/// </summary>
public partial class ImportWizardDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly Data.DbRepository _repo;
    private List<Dictionary<string, object?>>? _csvRecords;
    private string[]? _csvHeaders;

    public class FieldMapping
    {
        public string SourceColumn { get; set; } = "";
        public string SampleValue { get; set; } = "";
        public string TargetColumn { get; set; } = "";
        public string ConverterType { get; set; } = "direct";
        public bool Skip { get; set; }
    }

    public ObservableCollection<FieldMapping> Mappings { get; } = new();

    public int ImportedCount { get; private set; }
    public int FailedCount { get; private set; }

    public ImportWizardDialog(string dsn)
    {
        _repo = new Data.DbRepository(dsn);
        InitializeComponent();
        _ = LoadTablesAsync();
    }

    private async Task LoadTablesAsync()
    {
        try
        {
            var tables = await _repo.GetTableListForSyncPublicAsync();
            TargetTableCombo.ItemsSource = tables;
        }
        catch { }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|TSV files (*.tsv)|*.tsv|All files (*.*)|*.*",
            Title = "Select Import File"
        };
        if (dlg.ShowDialog() != true) return;

        FilePathEdit.Text = dlg.FileName;
        LoadCsvPreview(dlg.FileName);
    }

    private void LoadCsvPreview(string filePath)
    {
        try
        {
            var agent = new CsvImportAgent();
            agent.InitializeAsync(new Dictionary<string, string>
            {
                ["file_path"] = filePath,
                ["delimiter"] = filePath.EndsWith(".tsv") ? "\t" : ",",
                ["has_header"] = "true"
            }).Wait();

            var result = agent.ReadAsync(new ReadRequest { MaxRecords = 100 }).Result;
            _csvRecords = result.Records;

            if (_csvRecords.Count > 0)
            {
                _csvHeaders = _csvRecords[0].Keys.ToArray();

                Mappings.Clear();
                foreach (var header in _csvHeaders)
                {
                    var sample = _csvRecords[0].GetValueOrDefault(header)?.ToString() ?? "";
                    Mappings.Add(new FieldMapping
                    {
                        SourceColumn = header,
                        SampleValue = sample.Length > 50 ? sample[..50] + "..." : sample,
                        TargetColumn = header.ToLowerInvariant().Replace(" ", "_"),
                        ConverterType = "direct"
                    });
                }
                MappingGrid.ItemsSource = Mappings;
                PreviewLabel.Text = $"Preview: {_csvRecords.Count} rows, {_csvHeaders.Length} columns";
            }
        }
        catch (Exception ex)
        {
            PreviewLabel.Text = $"Error reading file: {ex.Message}";
        }
    }

    private void TargetTable_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        // Auto-suggest target column names from the selected table
        // (Future: query pg_attribute for column names)
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var targetTable = TargetTableCombo.EditValue?.ToString();
        var upsertKey = UpsertKeyEdit.Text?.Trim() ?? "id";

        if (string.IsNullOrEmpty(targetTable) || _csvRecords == null || _csvRecords.Count == 0)
        {
            StatusLabel.Text = "Select a file and target table first";
            return;
        }

        var activeMappings = Mappings.Where(m => !m.Skip && !string.IsNullOrEmpty(m.TargetColumn)).ToList();
        if (activeMappings.Count == 0)
        {
            StatusLabel.Text = "No field mappings configured";
            return;
        }

        StatusLabel.Text = "Importing...";
        ImportedCount = 0;
        FailedCount = 0;

        // Build converters
        var converters = new Dictionary<string, IFieldConverter>
        {
            ["direct"] = new DirectConverter(),
            ["constant"] = new ConstantConverter(),
            ["expression"] = new Central.Core.Integration.ExpressionConverter(),
            ["date_format"] = new DateFormatConverter(),
            ["combine"] = new CombineConverter(),
            ["split"] = new SplitConverter()
        };

        // Read full file
        var agent = new CsvImportAgent();
        await agent.InitializeAsync(new Dictionary<string, string>
        {
            ["file_path"] = FilePathEdit.Text,
            ["delimiter"] = FilePathEdit.Text.EndsWith(".tsv") ? "\t" : ","
        });
        var fullResult = await agent.ReadAsync(new ReadRequest { MaxRecords = 100000 });

        foreach (var sourceRecord in fullResult.Records)
        {
            try
            {
                var targetRecord = new Dictionary<string, object?>();
                var context = new ConvertContext { SourceRecord = sourceRecord };

                foreach (var mapping in activeMappings)
                {
                    var value = sourceRecord.GetValueOrDefault(mapping.SourceColumn);
                    if (converters.TryGetValue(mapping.ConverterType, out var converter))
                        value = converter.Convert(value, "", context);
                    targetRecord[mapping.TargetColumn] = value;
                }

                await _repo.UpsertSyncRecordAsync(targetTable, targetRecord, upsertKey);
                ImportedCount++;
            }
            catch
            {
                FailedCount++;
            }
        }

        StatusLabel.Text = $"Import complete: {ImportedCount} imported, {FailedCount} failed";

        _ = Central.Core.Services.AuditService.Instance.LogAsync("Import", targetTable,
            details: $"CSV import: {ImportedCount} records from {Path.GetFileName(FilePathEdit.Text)}");

        if (FailedCount == 0)
            DialogResult = true;
    }
}
