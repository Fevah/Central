using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Central.Engine.Models;

namespace Central.Module.Projects;

public partial class ReportBuilderPanel : System.Windows.Controls.UserControl
{
    private readonly ObservableCollection<ReportFilter> _filters = new();

    public ReportBuilderPanel()
    {
        InitializeComponent();
        FilterGrid.ItemsSource = _filters;
        EntityTypeSelector.ItemsSource = new[] { "task", "device", "switch" };

        // Available filter fields for tasks
        FieldCombo.ItemsSource = new[]
        {
            "Title", "Status", "Priority", "TaskType", "Category", "Severity", "BugPriority",
            "Risk", "Confidence", "AssignedToName", "CreatedByName", "Building", "Tags",
            "Points", "WorkRemaining", "EstimatedHours", "ActualHours",
            "StartDate", "FinishDate", "DueDate", "CreatedAt", "CompletedAt",
            "ProjectName", "SprintName", "CommittedToName", "IsEpic", "IsMilestone"
        };
    }

    // Events
    public event Func<ReportQuery, Task<DataTable>>? RunQuery;
    public event Func<SavedReport, Task>? SaveReport;
    public event Action<DataTable>? ExportRequested;

    public DevExpress.Xpf.Grid.GridControl Results => ResultsGrid;

    public void SetReports(IEnumerable<SavedReport> reports)
    {
        var items = new List<SavedReport> { new() { Id = 0, Name = "(New Report)" } };
        items.AddRange(reports);
        ReportSelector.ItemsSource = items;
    }

    private void ReportSelector_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        if (ReportSelector.EditValue is SavedReport report && report.Id > 0)
        {
            // Load filters from saved report
            try
            {
                var query = System.Text.Json.JsonSerializer.Deserialize<ReportQuery>(report.QueryJson);
                if (query != null)
                {
                    _filters.Clear();
                    foreach (var f in query.Filters) _filters.Add(f);
                }
            }
            catch { }
        }
    }

    private void AddFilter_Click(object sender, System.Windows.RoutedEventArgs e)
        => _filters.Add(new ReportFilter { Field = "Status", Operator = "=", Value = "", Logic = "AND" });

    private void ClearFilters_Click(object sender, System.Windows.RoutedEventArgs e)
        => _filters.Clear();

    private async void RunQuery_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (RunQuery == null) return;
        var query = BuildQuery();
        var result = await RunQuery(query);
        ResultsGrid.ItemsSource = result.DefaultView;
    }

    private async void SaveReport_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (SaveReport == null) return;
        var query = BuildQuery();
        var json = System.Text.Json.JsonSerializer.Serialize(query);
        var existing = ReportSelector.EditValue as SavedReport;
        var report = new SavedReport
        {
            Id = existing?.Id > 0 ? existing.Id : 0,
            Name = existing?.Name ?? $"Report {DateTime.Now:yyyy-MM-dd HH:mm}",
            QueryJson = json
        };
        await SaveReport(report);
    }

    private void ExportCsv_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ResultsGrid.ItemsSource is DataView dv)
            ExportRequested?.Invoke(dv.Table);
    }

    private ReportQuery BuildQuery()
    {
        return new ReportQuery
        {
            EntityType = EntityTypeSelector.EditValue as string ?? "task",
            Filters = _filters.ToList(),
            Columns = new List<string>() // empty = all columns
        };
    }
}
