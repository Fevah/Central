using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Central.Core.Models;

namespace Central.Module.Tasks.Views;

public partial class SprintBurndownPanel : System.Windows.Controls.UserControl
{
    private bool _showHours;

    public SprintBurndownPanel()
    {
        InitializeComponent();
        MetricSelector.ItemsSource = new[] { "Points", "Hours" };
        MetricSelector.EditValue = "Points";
    }

    // Events
    public event Func<int, Task>? SprintChanged;
    public event Func<int, Task>? SnapshotRequested;

    public int? SelectedSprintId =>
        SprintSelector.EditValue is Sprint s && s.Id > 0 ? s.Id : null;

    public void SetSprints(IEnumerable<Sprint> sprints)
        => SprintSelector.ItemsSource = sprints;

    /// <summary>Load burndown data into the chart.</summary>
    public void LoadBurndown(List<SprintBurndownPoint> data, Sprint? sprint)
    {
        if (data.Count == 0 || sprint == null)
        {
            ActualSeries.DataSource = null;
            IdealSeries.DataSource = null;
            VelocityText.Text = "Velocity: —";
            CompletedText.Text = "";
            RemainingText.Text = "";
            return;
        }

        // Calculate ideal line
        var startDate = sprint.StartDate ?? data.First().SnapshotDate;
        var endDate = sprint.EndDate ?? data.Last().SnapshotDate;
        var totalDays = Math.Max(1, (endDate - startDate).Days);
        var startValue = _showHours ? data.First().HoursRemaining + data.First().HoursCompleted
                                     : data.First().PointsRemaining + data.First().PointsCompleted;

        foreach (var point in data)
        {
            var dayIndex = (point.SnapshotDate - startDate).Days;
            var idealRemaining = startValue * (1m - (decimal)dayIndex / totalDays);
            point.IdealPoints = _showHours ? null : Math.Max(0, idealRemaining);
            point.IdealHours = _showHours ? Math.Max(0, idealRemaining) : null;
        }

        // Set data binding based on metric
        if (_showHours)
        {
            ActualSeries.ValueDataMember = "HoursRemaining";
            IdealSeries.ValueDataMember = "IdealHours";
        }
        else
        {
            ActualSeries.ValueDataMember = "PointsRemaining";
            IdealSeries.ValueDataMember = "IdealPoints";
        }

        ActualSeries.DataSource = data;
        IdealSeries.DataSource = data;

        // Summary
        var last = data.Last();
        var unit = _showHours ? "h" : "pts";
        VelocityText.Text = sprint.VelocityPoints.HasValue
            ? $"Velocity: {(_showHours ? sprint.VelocityHours : sprint.VelocityPoints):n1} {unit}/sprint"
            : "Velocity: —";
        CompletedText.Text = $"Completed: {(_showHours ? last.HoursCompleted : last.PointsCompleted):n1} {unit}";
        RemainingText.Text = $"Remaining: {(_showHours ? last.HoursRemaining : last.PointsRemaining):n1} {unit}";
    }

    private void SprintSelector_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        if (SelectedSprintId.HasValue) SprintChanged?.Invoke(SelectedSprintId.Value);
    }

    private void Snapshot_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (SelectedSprintId.HasValue) SnapshotRequested?.Invoke(SelectedSprintId.Value);
    }

    private void MetricSelector_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        _showHours = MetricSelector.EditValue as string == "Hours";
        // Re-trigger data load if sprint is selected
        if (SelectedSprintId.HasValue) SprintChanged?.Invoke(SelectedSprintId.Value);
    }
}
