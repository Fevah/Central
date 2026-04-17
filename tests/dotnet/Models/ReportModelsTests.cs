using Central.Core.Models;

namespace Central.Tests.Models;

public class ReportModelsTests
{
    // ── SavedReport ──

    [Fact]
    public void SavedReport_Defaults()
    {
        var sr = new SavedReport();
        Assert.Equal("", sr.Name);
        Assert.Equal("", sr.Folder);
        Assert.Equal("{}", sr.QueryJson);
    }

    [Fact]
    public void SavedReport_DisplayPath_NoFolder()
    {
        var sr = new SavedReport { Name = "My Report", Folder = "" };
        Assert.Equal("My Report", sr.DisplayPath);
    }

    [Fact]
    public void SavedReport_DisplayPath_WithFolder()
    {
        var sr = new SavedReport { Name = "My Report", Folder = "Sprint Reports" };
        Assert.Equal("Sprint Reports/My Report", sr.DisplayPath);
    }

    [Fact]
    public void SavedReport_PropertyChanged_Fires()
    {
        var sr = new SavedReport();
        var changed = new List<string>();
        sr.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        sr.Name = "Updated";
        Assert.Contains("Name", changed);
    }

    // ── ReportFilter ──

    [Fact]
    public void ReportFilter_Defaults()
    {
        var rf = new ReportFilter();
        Assert.Equal("", rf.Field);
        Assert.Equal("=", rf.Operator);
        Assert.Equal("", rf.Value);
        Assert.Null(rf.Value2);
        Assert.Equal("AND", rf.Logic);
    }

    // ── ReportQuery ──

    [Fact]
    public void ReportQuery_Defaults()
    {
        var rq = new ReportQuery();
        Assert.Empty(rq.Columns);
        Assert.Empty(rq.Filters);
        Assert.Equal("", rq.SortField);
        Assert.Equal("ASC", rq.SortDirection);
        Assert.Equal("", rq.GroupField);
        Assert.Equal("task", rq.EntityType);
    }

    // ── Dashboard ──

    [Fact]
    public void Dashboard_Defaults()
    {
        var d = new Dashboard();
        Assert.Equal("", d.Name);
        Assert.Equal("{}", d.LayoutJson);
        Assert.Equal("", d.Template);
    }

    [Fact]
    public void Dashboard_PropertyChanged_Fires()
    {
        var d = new Dashboard();
        var changed = new List<string>();
        d.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        d.Name = "My Dashboard";
        Assert.Contains("Name", changed);
    }

    // ── DashboardTile ──

    [Fact]
    public void DashboardTile_Defaults()
    {
        var dt = new DashboardTile();
        Assert.Equal(1, dt.RowSpan);
        Assert.Equal(1, dt.ColSpan);
        Assert.Equal("Bar", dt.ChartType);
        Assert.Equal("", dt.Title);
        Assert.Equal("#3B82F6", dt.Color);
    }

    [Fact]
    public void DashboardTile_SetProperties()
    {
        var dt = new DashboardTile
        {
            Row = 1, Column = 2, RowSpan = 2, ColSpan = 3,
            ChartType = "Pie", Title = "Status", Color = "#FF0000"
        };
        Assert.Equal(1, dt.Row);
        Assert.Equal(2, dt.Column);
        Assert.Equal("Pie", dt.ChartType);
    }
}
