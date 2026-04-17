using System.Windows.Input;
using System.Windows.Media;
using DevExpress.Xpf.Charts;
using Central.Engine.Models;
using Central.Engine.Widgets;

namespace Central.Module.ServiceDesk.Views;

public partial class AgingChartPanel : System.Windows.Controls.UserControl
{
    private List<SdAgingBucket> _allData = new();

    private static readonly (string Name, string Field, string Color, double MinDays, double MaxDays)[] Buckets =
    {
        ("0\u20131 day",  "Days0to1", "#22C55E", 0, 1),
        ("1\u20132 days", "Days1to2", "#3B82F6", 1, 2),
        ("2\u20134 days", "Days2to4", "#F59E0B", 2, 4),
        ("4\u20137 days", "Days4to7", "#F97316", 4, 7),
        ("7+ days",       "Days7Plus", "#EF4444", 7, 99999),
    };

    public AgingChartPanel() => InitializeComponent();

    public event Func<string, string, double, double, Task>? DrillDownRequested;

    public void LoadTechnicians(List<string> names, List<SdTeam> teams)
    {
        TechFilterHelper.BuildFilter(FilterPanel, names, teams, ApplyFilter);
    }

    public void LoadData(List<SdAgingBucket> data)
    {
        _allData = data;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var techs = TechFilterHelper.GetChecked(FilterPanel);
        var filtered = techs != null ? _allData.Where(d => techs.Contains(d.TechnicianName)).ToList() : _allData;
        Render(filtered);
    }

    private void Render(List<SdAgingBucket> data)
    {
        var diagram = AgingChart.Diagram as XYDiagram2D;
        if (diagram == null) return;
        diagram.Series.Clear();

        foreach (var (name, field, color, _, _) in Buckets)
        {
            var series = new BarSideBySideSeries2D
            {
                DisplayName = name, CrosshairLabelPattern = "{S}: {V}",
                Model = new BorderlessSimpleBar2DModel(),
                Brush = new SolidColorBrush(ParseColor(color))
            };

            foreach (var bucket in data)
            {
                var val = field switch
                {
                    "Days0to1" => bucket.Days0to1, "Days1to2" => bucket.Days1to2,
                    "Days2to4" => bucket.Days2to4, "Days4to7" => bucket.Days4to7,
                    "Days7Plus" => bucket.Days7Plus, _ => 0
                };
                series.Points.Add(new SeriesPoint(bucket.TechnicianName, val));
            }
            diagram.Series.Add(series);
        }
    }

    private void AgingChart_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var pos = e.GetPosition(AgingChart);
            var hitInfo = AgingChart.CalcHitInfo(pos);
            if (hitInfo == null) return;

            string? techName = null;
            string? bucketName = null;

            // Direct hit on a point
            if (hitInfo.InSeriesPoint)
            {
                techName = hitInfo.SeriesPoint?.Argument?.ToString();
                bucketName = hitInfo.Series?.DisplayName;
            }
            // Hit on series bar body
            else if (hitInfo.InSeries)
            {
                bucketName = hitInfo.Series?.DisplayName;
                // Find nearest point in this series by X distance
                if (hitInfo.Series is BarSideBySideSeries2D barSeries)
                {
                    foreach (var sp in barSeries.Points)
                    {
                        // Just use the first non-zero point's tech name as the best guess
                        // DX doesn't expose screen coords per point easily
                        techName = sp.Argument?.ToString();
                        if (!string.IsNullOrEmpty(techName)) break;
                    }
                    // Better: use diagram coordinate mapping
                    try
                    {
                        var diagram = AgingChart.Diagram as XYDiagram2D;
                        var dp = diagram?.PointToDiagram(pos);
                        if (dp?.QualitativeArgument != null) techName = dp.QualitativeArgument;
                    }
                    catch { }
                }
            }
            // Hit anywhere in diagram
            else if (hitInfo.InDiagram)
            {
                try
                {
                    var diagram = AgingChart.Diagram as XYDiagram2D;
                    var dp = diagram?.PointToDiagram(pos);
                    if (dp?.QualitativeArgument != null)
                    {
                        techName = dp.QualitativeArgument;
                        bucketName = Buckets[0].Name; // default to first bucket
                    }
                }
                catch { }
            }

            if (string.IsNullOrEmpty(techName) || string.IsNullOrEmpty(bucketName)) return;

            var match = Buckets.FirstOrDefault(b => b.Name == bucketName);
            if (string.IsNullOrEmpty(match.Name)) return;

            DrillDownRequested?.Invoke(techName, bucketName, match.MinDays, match.MaxDays);
        }
        catch { }
    }

    private static System.Windows.Media.Color ParseColor(string hex)
        => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
}
