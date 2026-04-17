using System.Windows;
using Central.Core.Models;
using WC = System.Windows.Controls;
using WMedia = System.Windows.Media;

namespace Central.Module.CRM.Views;

public partial class CrmDashboardPanel : System.Windows.Controls.UserControl
{
    private CrmDataService? _data;

    public CrmDashboardPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public void SetDsn(string dsn) => _data = new CrmDataService(dsn);

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_data == null) return;
        try
        {
            var kpi = await _data.LoadKpiSummaryAsync();
            var pipeline = await _data.LoadPipelineSummaryAsync();

            // Sales KPI cards
            SalesKpis.Children.Clear();
            SalesKpis.Children.Add(KpiCardBuilder.Build("Open Deals", kpi.OpenDeals, 0, false));
            SalesKpis.Children.Add(KpiCardBuilder.Build("Pipeline Value", (double)kpi.OpenPipelineValue, 0, false, "int"));
            SalesKpis.Children.Add(KpiCardBuilder.Build("Weighted Pipeline", (double)kpi.WeightedPipeline, 0, false, "int"));
            SalesKpis.Children.Add(KpiCardBuilder.Build("Revenue MTD", (double)kpi.RevenueThisMonth, 0, false, "int"));

            // Customer KPI cards
            CustomerKpis.Children.Clear();
            CustomerKpis.Children.Add(KpiCardBuilder.Build("Customers", kpi.Customers, 0, false));
            CustomerKpis.Children.Add(KpiCardBuilder.Build("Prospects", kpi.Prospects, 0, false));
            CustomerKpis.Children.Add(KpiCardBuilder.Build("New Leads", kpi.NewLeads, 0, false));
            CustomerKpis.Children.Add(KpiCardBuilder.Build("Overdue Tasks", kpi.OverdueActivities, 0, true));

            // Pipeline stages
            PipelineStages.Children.Clear();
            foreach (var stage in pipeline)
            {
                var row = BuildStageRow(stage);
                PipelineStages.Children.Add(row);
            }
        }
        catch (Exception ex)
        {
            Central.Core.Services.NotificationService.Instance?.Error($"CRM dashboard load failed: {ex.Message}");
        }
    }

    private WC.Grid BuildStageRow(PipelineStageSummary s)
    {
        var grid = new WC.Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = new GridLength(140) });

        var stageLabel = new WC.TextBlock
        {
            Text = $"{s.Stage} ({s.DealCount})",
            Foreground = ParseColor("#C0C0C0"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        WC.Grid.SetColumn(stageLabel, 0);
        grid.Children.Add(stageLabel);

        // Progress bar (visual representation of value)
        var bar = new WC.ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = Math.Min(100, (double)s.TotalValue / 100000),   // scale — 100k fills bar
            Height = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ParseColor("#5B8AF5"),
            Background = ParseColor("#2B2B2B")
        };
        WC.Grid.SetColumn(bar, 1);
        grid.Children.Add(bar);

        var value = new WC.TextBlock
        {
            Text = $"£{s.TotalValue:N0} / £{s.WeightedValue:N0}",
            Foreground = ParseColor("#909090"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(8, 0, 0, 0)
        };
        WC.Grid.SetColumn(value, 2);
        grid.Children.Add(value);

        return grid;
    }

    private static WMedia.SolidColorBrush ParseColor(string hex)
        => new((WMedia.Color)WMedia.ColorConverter.ConvertFromString(hex)!);
}
