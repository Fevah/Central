using System.Windows.Input;
using System.Windows.Media;
using DevExpress.Xpf.Charts;
using Central.Engine.Models;
using Central.Engine.Widgets;

namespace Central.Module.ServiceDesk.Views;

public partial class TechClosuresPanel : System.Windows.Controls.UserControl
{
    private int _expectedDaily = 5;
    private List<SdTechDaily> _allData = new();

    private static readonly string[] Colors =
        { "#3B82F6", "#22C55E", "#F59E0B", "#EF4444", "#8B5CF6", "#06B6D4", "#F97316", "#EC4899", "#14B8A6", "#A855F7" };

    public TechClosuresPanel() => InitializeComponent();

    /// <summary>Raised on bar double-click. Args: techName, day label, day date.</summary>
    public event Func<string, DateTime, Task>? DrillDownRequested;

    public int ExpectedDaily
    {
        get => _expectedDaily;
        set { _expectedDaily = value; ExpectedLabel.Text = value.ToString(); }
    }

    public void LoadTechnicians(List<string> names, List<SdTeam> teams)
    {
        TechFilterHelper.BuildFilter(FilterPanel, names, teams, ApplyFilter);
    }

    public void LoadData(List<SdTechDaily> data)
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

    private void Render(List<SdTechDaily> data)
    {
        var diagram = TechChart.Diagram as XYDiagram2D;
        if (diagram == null) return;
        diagram.Series.Clear();

        int ci = 0;
        foreach (var g in data.GroupBy(d => d.TechnicianName))
        {
            var series = new BarSideBySideSeries2D
            {
                DisplayName = g.Key,
                ArgumentDataMember = "DayLabel", ValueDataMember = "Closed",
                DataSource = g.ToList(), CrosshairLabelPattern = "{S}: {V}",
                Model = new BorderlessSimpleBar2DModel(),
                Brush = new SolidColorBrush(ParseColor(Colors[ci++ % Colors.Length]))
            };
            diagram.Series.Add(series);
        }

        if (diagram.AxisY is AxisY2D axisY)
        {
            axisY.ConstantLinesInFront.Clear();
            axisY.ConstantLinesInFront.Add(new ConstantLine
            {
                Value = _expectedDaily,
                Title = new ConstantLineTitle { Content = $"Target ({_expectedDaily}/day)" },
                Brush = new SolidColorBrush(ParseColor("#F59E0B")),
                LineStyle = new LineStyle { Thickness = 2, DashStyle = new System.Windows.Media.DashStyle(new double[] { 4, 2 }, 0) }
            });
        }
    }

    private void TechChart_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var pos = e.GetPosition(TechChart);
            var hitInfo = TechChart.CalcHitInfo(pos);

            string? techName = null;
            string? dayLabel = null;

            if (hitInfo?.InSeriesPoint == true)
            {
                techName = hitInfo.Series?.DisplayName;
                dayLabel = hitInfo.SeriesPoint?.Argument?.ToString();
            }
            else if (hitInfo?.InSeries == true)
            {
                techName = hitInfo.Series?.DisplayName;
                var diagram = TechChart.Diagram as DevExpress.Xpf.Charts.XYDiagram2D;
                try { dayLabel = diagram?.PointToDiagram(pos)?.QualitativeArgument; } catch { }
            }

            if (string.IsNullOrEmpty(techName) || string.IsNullOrEmpty(dayLabel)) return;

            var match = _allData.FirstOrDefault(d => d.TechnicianName == techName && d.DayLabel == dayLabel);
            if (match != null)
                DrillDownRequested?.Invoke(techName, match.Day);
        }
        catch { }
    }

    private static System.Windows.Media.Color ParseColor(string hex)
        => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
}
