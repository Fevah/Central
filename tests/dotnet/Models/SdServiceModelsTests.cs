using Central.Engine.Models;

namespace Central.Tests.Models;

public class SdServiceModelsTests
{
    // ── SdGroupCategory ──

    [Fact]
    public void SdGroupCategory_Defaults()
    {
        var gc = new SdGroupCategory();
        Assert.Equal("", gc.Name);
        Assert.True(gc.IsActive);
        Assert.Equal(0, gc.SortOrder);
        Assert.NotNull(gc.Members);
        Assert.Empty(gc.Members);
    }

    [Fact]
    public void SdGroupCategory_MemberCount()
    {
        var gc = new SdGroupCategory { Members = new List<string> { "IT", "HR", "Finance" } };
        Assert.Equal(3, gc.MemberCount);
    }

    [Fact]
    public void SdGroupCategory_PropertyChanged_Fires()
    {
        var gc = new SdGroupCategory();
        var changed = new List<string>();
        gc.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        gc.Name = "Infrastructure";
        Assert.Contains("Name", changed);
    }

    // ── SdGroup ──

    [Fact]
    public void SdGroup_Defaults()
    {
        var g = new SdGroup();
        Assert.Equal("", g.Name);
        Assert.True(g.IsActive);
    }

    [Fact]
    public void SdGroup_PropertyChanged_Fires()
    {
        var g = new SdGroup();
        var changed = new List<string>();
        g.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        g.Name = "Network";
        Assert.Contains("Name", changed);
    }

    // ── SdRequester ──

    [Fact]
    public void SdRequester_Defaults()
    {
        var r = new SdRequester();
        Assert.Equal("", r.Name);
        Assert.Equal("", r.Email);
        Assert.False(r.IsVip);
        Assert.Equal(0, r.OpenTickets);
    }

    // ── SdTechnician ──

    [Fact]
    public void SdTechnician_Defaults()
    {
        var t = new SdTechnician();
        Assert.Equal("", t.Name);
        Assert.True(t.IsActive);
        Assert.Equal(0, t.OpenTickets);
        Assert.Equal(0, t.ResolvedTickets);
    }

    [Fact]
    public void SdTechnician_PropertyChanged_IsActive()
    {
        var t = new SdTechnician();
        var changed = new List<string>();
        t.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        t.IsActive = false;
        Assert.Contains("IsActive", changed);
    }

    // ── SdKpiSummary ──

    [Fact]
    public void SdKpiSummary_Defaults()
    {
        var kpi = new SdKpiSummary();
        Assert.Equal(0, kpi.Incoming);
        Assert.Equal(0, kpi.Resolutions);
        Assert.Equal(0.0, kpi.AvgResolutionHours);
    }

    // ── SdTeam ──

    [Fact]
    public void SdTeam_Defaults()
    {
        var t = new SdTeam();
        Assert.Equal("", t.Name);
        Assert.NotNull(t.Members);
        Assert.Empty(t.Members);
    }

    // ── SdWeeklyTotal ──

    [Fact]
    public void SdWeeklyTotal_DayLabel()
    {
        var wt = new SdWeeklyTotal { Day = new DateTime(2026, 3, 30) }; // Monday
        Assert.Equal("Mon", wt.DayLabel);
    }

    // ── SdAgingBucket ──

    [Fact]
    public void SdAgingBucket_Total()
    {
        var ab = new SdAgingBucket
        {
            Days0to1 = 5, Days1to2 = 3, Days2to4 = 2, Days4to7 = 1, Days7Plus = 4
        };
        Assert.Equal(15, ab.Total);
    }

    [Fact]
    public void SdAgingBucket_Total_Empty()
    {
        var ab = new SdAgingBucket();
        Assert.Equal(0, ab.Total);
    }

    // ── SdTechDaily ──

    [Fact]
    public void SdTechDaily_DayLabel()
    {
        var td = new SdTechDaily { Day = new DateTime(2026, 3, 30) };
        Assert.Equal("Mon", td.DayLabel);
    }
}
