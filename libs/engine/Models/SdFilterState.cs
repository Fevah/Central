namespace Central.Engine.Models;

/// <summary>
/// Single source of truth for all SD dashboard filter state.
/// Built by SdSettingsPanel, consumed by all SD load methods.
/// Every query that needs filtering reads from this object — no direct panel access.
/// </summary>
public class SdFilterState
{
    public DateTime RangeStart { get; set; }
    public DateTime RangeEnd { get; set; }
    public string Bucket { get; set; } = "day"; // "day", "week", "month"

    /// <summary>null = all techs. Empty list with "__NONE__" = no techs.</summary>
    public List<string>? SelectedTechs { get; set; }
    /// <summary>null = all groups. Empty list with "__NONE__" = no groups.</summary>
    public List<string>? SelectedGroups { get; set; }

    // Chart overlay toggles
    public bool ShowOpenLine { get; set; } = true;
    public bool ShowResolutionLine { get; set; } = true;
    public bool ShowTotalCreatedLine { get; set; }
    public bool ShowTotalClosedLine { get; set; }
    public bool ShowTargetLine { get; set; } = true;
    public bool ShowKpiCards { get; set; } = true;
    public bool ShowBarLabels { get; set; }
    public int ChartType { get; set; } // 0=SideBySide, 1=Stacked, 2=FullStacked, 3=SbsStacked, 4=Line, 5=Spline, 6=Area, 7=StackedArea
    public int BarStyle { get; set; } // 0=Flat, 1=Gradient, 2=Glass, 3=3D
    public int ChartTheme { get; set; } // 0=Dark, 1=Light, 2=Blue, 3=Colourful

    // Grid display options
    public bool ShowGroupPanel { get; set; } = true;
    public bool ShowAutoFilter { get; set; } = true;
    public bool ShowTotalSummary { get; set; } = true;
    public bool AlternateRows { get; set; } = true;
    public bool ShowSearchPanel { get; set; } = true;
    public bool ShowFilterPanel { get; set; } = true;
    public int GridStyle { get; set; } // 0=Default, 1=Compact, 2=Comfortable

    /// <summary>Format a bucket label based on the current scale.</summary>
    public string FormatLabel(DateTime day)
    {
        return Bucket switch
        {
            "month" => day.ToString("MMM yy"),
            "week" => $"W/C {day:MMM d}",
            _ => (RangeEnd - RangeStart).TotalDays <= 14
                ? day.ToString("ddd d")
                : day.ToString("MMM d")
        };
    }

    /// <summary>Build a default filter state for "This Week".</summary>
    public static SdFilterState Default()
    {
        var today = DateTime.Today;
        var dow = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var weekStart = today.AddDays(-dow);
        return new SdFilterState { RangeStart = weekStart, RangeEnd = weekStart.AddDays(7), Bucket = "day" };
    }
}
