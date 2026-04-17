using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Central.Engine.Models;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Central.Module.Projects;

/// <summary>Column mapping row for the import wizard.</summary>
public class ColumnMapping
{
    public string SourceColumn { get; set; } = "";
    public string TargetField { get; set; } = "(skip)";
    public string SampleValue { get; set; } = "";
    public string IsMapped => TargetField != "(skip)" ? "✓" : "";
}

public partial class TaskImportPanel : System.Windows.Controls.UserControl
{
    private readonly ObservableCollection<ColumnMapping> _mappings = new();
    private DataTable? _sourceData;
    private string _filePath = "";

    private static readonly string[] TaskFields = [
        "(skip)", "Title", "Description", "Status", "Priority", "TaskType", "Category",
        "Points", "WorkRemaining", "EstimatedHours", "Severity", "BugPriority", "Risk",
        "Building", "Tags", "StartDate", "FinishDate", "DueDate", "AssignedToName"
    ];

    public TaskImportPanel()
    {
        InitializeComponent();
        MappingGrid.ItemsSource = _mappings;
        FormatSelector.ItemsSource = new[] { "Excel (.xlsx)", "CSV (.csv)", "MS Project XML (.xml)" };
        TargetFieldCombo.ItemsSource = TaskFields;
    }

    // Events
    public event Func<List<TaskItem>, bool, Task<int>>? ImportTasks; // tasks, updateExisting → count imported
    public event Func<string, Task<DataTable>>? ParseFile;           // filePath → DataTable

    public void SetProjects(IEnumerable<TaskProject> projects)
        => ProjectSelector.ItemsSource = projects;

    public int? SelectedProjectId =>
        ProjectSelector.EditValue is TaskProject p && p.Id > 0 ? p.Id : null;

    private void Browse_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Excel Files|*.xlsx|CSV Files|*.csv|MS Project XML|*.xml|All Files|*.*",
            Title = "Select file to import"
        };
        if (dlg.ShowDialog() == true)
        {
            _filePath = dlg.FileName;
            FilePathText.Text = Path.GetFileName(_filePath);
            // Auto-detect format
            var ext = Path.GetExtension(_filePath).ToLower();
            FormatSelector.EditValue = ext switch
            {
                ".xlsx" => "Excel (.xlsx)",
                ".csv" => "CSV (.csv)",
                ".xml" => "MS Project XML (.xml)",
                _ => FormatSelector.EditValue
            };
        }
    }

    private void AutoMap_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // Auto-map source columns to task fields by name similarity
        foreach (var m in _mappings)
        {
            var source = m.SourceColumn.ToLower().Replace(" ", "").Replace("_", "");
            m.TargetField = TaskFields.FirstOrDefault(f =>
                f.ToLower().Replace(" ", "") == source ||
                source.Contains(f.ToLower())) ?? "(skip)";
        }
        MappingGrid.RefreshData();
        MappingStatusText.Text = $"{_mappings.Count(m => m.TargetField != "(skip)")} of {_mappings.Count} mapped";
    }

    private async void Preview_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_filePath) || ParseFile == null) return;
        try
        {
            ImportStatusText.Text = "Parsing...";
            _sourceData = await ParseFile(_filePath);
            if (_sourceData == null) return;

            // Build column mappings
            _mappings.Clear();
            foreach (DataColumn col in _sourceData.Columns)
            {
                var sample = _sourceData.Rows.Count > 0 ? _sourceData.Rows[0][col]?.ToString() ?? "" : "";
                _mappings.Add(new ColumnMapping { SourceColumn = col.ColumnName, SampleValue = sample });
            }
            MappingGrid.RefreshData();
            AutoMap_Click(sender, e);

            // Show preview
            PreviewGrid.ItemsSource = _sourceData.DefaultView;
            ImportStatusText.Text = $"{_sourceData.Rows.Count} rows loaded";
        }
        catch (Exception ex)
        {
            ImportStatusText.Text = $"Error: {ex.Message}";
        }
    }

    private async void Import_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_sourceData == null || ImportTasks == null) return;
        var projectId = SelectedProjectId;
        var updateExisting = UpdateExistingCheck.IsChecked == true;

        try
        {
            ImportButton.IsEnabled = false;
            ImportStatusText.Text = "Importing...";

            // Build TaskItem list from mappings
            var tasks = new List<TaskItem>();
            var fieldMap = _mappings.Where(m => m.TargetField != "(skip)")
                .ToDictionary(m => m.TargetField, m => m.SourceColumn);

            for (int i = 0; i < _sourceData.Rows.Count; i++)
            {
                var row = _sourceData.Rows[i];
                var task = new TaskItem
                {
                    ProjectId = projectId,
                    Status = "Open",
                    Priority = "Medium",
                    TaskType = "Task",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                if (fieldMap.TryGetValue("Title", out var titleCol))
                    task.Title = row[titleCol]?.ToString() ?? "";
                if (fieldMap.TryGetValue("Description", out var descCol))
                    task.Description = row[descCol]?.ToString() ?? "";
                if (fieldMap.TryGetValue("Status", out var statusCol))
                    task.Status = row[statusCol]?.ToString() ?? "Open";
                if (fieldMap.TryGetValue("Priority", out var priCol))
                    task.Priority = row[priCol]?.ToString() ?? "Medium";
                if (fieldMap.TryGetValue("TaskType", out var typeCol))
                    task.TaskType = row[typeCol]?.ToString() ?? "Task";
                if (fieldMap.TryGetValue("Category", out var catCol))
                    task.Category = row[catCol]?.ToString() ?? "";
                if (fieldMap.TryGetValue("Points", out var ptsCol) && decimal.TryParse(row[ptsCol]?.ToString(), out var pts))
                    task.Points = pts;
                if (fieldMap.TryGetValue("EstimatedHours", out var hrsCol) && decimal.TryParse(row[hrsCol]?.ToString(), out var hrs))
                    task.EstimatedHours = hrs;
                if (fieldMap.TryGetValue("Building", out var bldgCol))
                    task.Building = row[bldgCol]?.ToString() ?? "";
                if (fieldMap.TryGetValue("Tags", out var tagsCol))
                    task.Tags = row[tagsCol]?.ToString() ?? "";
                if (fieldMap.TryGetValue("Severity", out var sevCol))
                    task.Severity = row[sevCol]?.ToString() ?? "";
                if (fieldMap.TryGetValue("Risk", out var riskCol))
                    task.Risk = row[riskCol]?.ToString() ?? "";
                if (fieldMap.TryGetValue("StartDate", out var sdCol) && DateTime.TryParse(row[sdCol]?.ToString(), out var sd))
                    task.StartDate = sd;
                if (fieldMap.TryGetValue("FinishDate", out var fdCol) && DateTime.TryParse(row[fdCol]?.ToString(), out var fd))
                    task.FinishDate = fd;
                if (fieldMap.TryGetValue("DueDate", out var ddCol) && DateTime.TryParse(row[ddCol]?.ToString(), out var dd))
                    task.DueDate = dd;

                if (!string.IsNullOrEmpty(task.Title))
                    tasks.Add(task);

                ImportProgress.Value = (i + 1) * 100.0 / _sourceData.Rows.Count;
            }

            var count = await ImportTasks(tasks, updateExisting);
            ImportStatusText.Text = $"Imported {count} tasks";
            ImportProgress.Value = 100;
        }
        catch (Exception ex)
        {
            ImportStatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ImportButton.IsEnabled = true;
        }
    }
}
