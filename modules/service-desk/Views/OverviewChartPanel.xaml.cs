using System.Windows.Input;
using DevExpress.Xpf.Charts;
using Central.Engine.Models;

namespace Central.Module.ServiceDesk.Views;

public partial class OverviewChartPanel : System.Windows.Controls.UserControl
{
    private List<SdWeeklyTotal> _data = new();

    public OverviewChartPanel()
    {
        InitializeComponent();
    }

    /// <summary>Chart drill-down: seriesName ("Issues created"/"Issues closed"), bucketDate.</summary>
    public event Func<string, DateTime, Task>? ChartDrillDownRequested;

    /// <summary>KPI drill-down: kpiName ("Incoming","Closed","Escalations","Open",etc).</summary>
    public event Func<string, Task>? KpiDrillDownRequested;

    private SdKpiSummary? _lastKpi;

    public void LoadKpi(SdKpiSummary kpi)
    {
        _lastKpi = kpi;
        var cards = KpiCardBuilder.BuildCards(kpi);
        // Wire double-click on each card
        var labels = new[] { "Incoming", "Closed", "Escalations", "SLA Compliant", "Resolution Time", "Open", "Tech:Ticket" };
        for (int i = 0; i < cards.Length && i < labels.Length; i++)
        {
            var label = labels[i];
            cards[i].MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) KpiDrillDownRequested?.Invoke(label);
            };
            cards[i].Cursor = System.Windows.Input.Cursors.Hand;
        }
        KpiCards.ItemsSource = cards;
    }

    public void LoadOverview(List<SdWeeklyTotal> data)
    {
        _data = data;
        CreatedSeries.DataSource = data;
        CompletedSeries.DataSource = data;
        ResolutionSeries.DataSource = data;
        OpenSeries.DataSource = data;

        if (OverviewChart.Diagram is XYDiagram2D diag && diag.SecondaryAxesY.Count >= 2)
        {
            XYDiagram2D.SetSeriesAxisY(ResolutionSeries, diag.SecondaryAxesY[0]);
            XYDiagram2D.SetSeriesAxisY(OpenSeries, diag.SecondaryAxesY[1]);
        }

        // Compute running cumulative totals
        int cumCreated = 0, cumClosed = 0;
        foreach (var item in data)
        {
            cumCreated += item.Created;
            cumClosed += item.Completed;
            item.CumulativeCreated = cumCreated;
            item.CumulativeClosed = cumClosed;
        }

        TotalCreatedLine.DataSource = data;
        TotalClosedLine.DataSource = data;

        var totalCreated = data.Sum(d => d.Created);
        var totalCompleted = data.Sum(d => d.Completed);
        var openNow = data.LastOrDefault()?.OpenCount ?? 0;
        var avgResHrs = _lastKpi?.AvgResolutionHours ?? 0;
        var avgText = avgResHrs >= 24 ? $"{avgResHrs / 24.0:F1}d" : $"{avgResHrs:F1}h";
        var rate = totalCreated > 0 ? totalCompleted * 100 / totalCreated : 0;
        SummaryLabel.Text = $"{totalCreated} created  |  {totalCompleted} closed ({rate}%)  |  {avgText} avg resolution  |  {openNow} open";
    }

    private void OverviewChart_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var pos = e.GetPosition(OverviewChart);
            var hitInfo = OverviewChart.CalcHitInfo(pos);

            string? seriesName = null;
            string? label = null;

            // Method 1: direct hit on series point
            if (hitInfo?.InSeriesPoint == true)
            {
                seriesName = hitInfo.Series?.DisplayName;
                label = hitInfo.SeriesPoint?.Argument?.ToString();
            }
            // Method 2: hit on series (bar area but not exact point)
            else if (hitInfo?.InSeries == true)
            {
                seriesName = hitInfo.Series?.DisplayName;
                // Use crosshair to find the argument
                var diagram = OverviewChart.Diagram as DevExpress.Xpf.Charts.XYDiagram2D;
                if (diagram != null)
                {
                    try
                    {
                        var dp = diagram.PointToDiagram(pos);
                        label = dp?.QualitativeArgument;
                    }
                    catch { }
                }
            }
            // Method 3: anywhere in diagram — find nearest argument
            else if (hitInfo?.InDiagram == true)
            {
                seriesName = "Issues created";
                var diagram = OverviewChart.Diagram as DevExpress.Xpf.Charts.XYDiagram2D;
                if (diagram != null)
                {
                    try
                    {
                        var dp = diagram.PointToDiagram(pos);
                        label = dp?.QualitativeArgument;
                    }
                    catch { }
                }
            }

            if (!string.IsNullOrEmpty(label))
            {
                var match = _data.FirstOrDefault(d => d.Label == label);
                if (match != null)
                    ChartDrillDownRequested?.Invoke(seriesName ?? "Issues created", match.Day);
            }
        }
        catch { }
    }

}
