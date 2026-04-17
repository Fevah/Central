#if WINDOWS
using System.Windows;
using System.Windows.Media;
using WC = System.Windows.Controls;

namespace Central.Core.Models;

/// <summary>
/// Engine-level KPI card builder — creates styled Border cards with value, trend arrow, and % change.
/// Reusable across any module dashboard.
/// </summary>
public static class KpiCardBuilder
{
    /// <summary>Build a standard KPI card with trend comparison.</summary>
    /// <param name="title">Card title</param>
    /// <param name="current">Current period value</param>
    /// <param name="previous">Previous period value (for trend %)</param>
    /// <param name="lowerIsBetter">If true, a decrease is green (good); if false, an increase is green.</param>
    /// <param name="format">Value format — "int" (default), "hours" (1.1h), "pct" (85%)</param>
    public static WC.Border Build(string title, double current, double previous, bool lowerIsBetter, string format = "int")
    {
        var pctChange = previous > 0 ? (current - previous) / previous * 100.0 : 0;
        var isUp = pctChange > 0;
        var isGood = lowerIsBetter ? !isUp : isUp;
        var arrow = pctChange == 0 ? "" : isUp ? "\u25B2" : "\u25BC";
        var trendColor = pctChange == 0 ? "#808080" : isGood ? "#22C55E" : "#EF4444";
        var valueText = format switch
        {
            "hours" => $"{current:F1}h",
            "pct" => $"{current:F0}%",
            "float1" => $"{current:F1}",
            _ => $"{(int)current:N0}"
        };
        var pctText = pctChange == 0 ? "" : $"{Math.Abs(pctChange):F0}%";

        var panel = new WC.StackPanel { Margin = new Thickness(8, 6, 8, 6) };
        panel.Children.Add(new WC.TextBlock
        {
            Text = title, Foreground = Brush("#909090"), FontSize = 11, Margin = new Thickness(0, 0, 0, 2)
        });

        var row = new WC.StackPanel { Orientation = WC.Orientation.Horizontal };
        row.Children.Add(new WC.TextBlock
        {
            Text = valueText, FontSize = 22, FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#E0E0E0"), Margin = new Thickness(0, 0, 6, 0)
        });
        if (!string.IsNullOrEmpty(pctText))
        {
            row.Children.Add(new WC.TextBlock
            {
                Text = $"{arrow}{pctText}", FontSize = 11, Foreground = Brush(trendColor),
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 3)
            });
        }
        panel.Children.Add(row);

        return new WC.Border
        {
            Child = panel, BorderBrush = Brush("#333"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Margin = new Thickness(3)
        };
    }

    /// <summary>Build a ratio card (e.g. "1:40").</summary>
    public static WC.Border BuildRatio(string title, int numerator, int denominator, string color = "#5B8AF5")
    {
        var ratio = numerator > 0 ? denominator / (double)numerator : 0;
        var ratioText = numerator > 0 ? $"1:{ratio:F0}" : "\u2014";

        var panel = new WC.StackPanel { Margin = new Thickness(8, 6, 8, 6) };
        panel.Children.Add(new WC.TextBlock
        {
            Text = title, Foreground = Brush("#909090"), FontSize = 11, Margin = new Thickness(0, 0, 0, 2)
        });
        panel.Children.Add(new WC.TextBlock
        {
            Text = ratioText, FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = Brush(color)
        });

        return new WC.Border
        {
            Child = panel, BorderBrush = Brush("#333"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Margin = new Thickness(3)
        };
    }

    /// <summary>Build the standard SD KPI card set.</summary>
    public static WC.Border[] BuildCards(SdKpiSummary kpi) => new[]
    {
        Build("Incoming", kpi.Incoming, kpi.PrevIncoming, false),
        Build("Closed", kpi.Resolutions, kpi.PrevResolutions, false),
        Build("Escalations", kpi.Escalations, kpi.PrevEscalations, true),
        Build("SLA Compliant", kpi.SlaCompliant, kpi.PrevSlaCompliant, false),
        Build("Resolution Time", kpi.AvgResolutionHours, kpi.PrevAvgResolutionHours, true, "hours"),
        Build("Open", kpi.OpenCount, kpi.PrevOpenCount, true),
        BuildRatio("Tech:Ticket", kpi.ActiveTechCount, kpi.OpenCount),
    };

    private static SolidColorBrush Brush(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex));
}
#endif
