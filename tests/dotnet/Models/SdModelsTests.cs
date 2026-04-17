using Central.Core.Models;

namespace Central.Tests.Models;

public class SdModelsTests
{
    // ── SdGroupCategory ──

    [Fact]
    public void SdGroupCategory_MemberCount_ReflectsMembers()
    {
        var cat = new SdGroupCategory { Name = "Veeva", Members = new() { "A", "B", "C" } };
        Assert.Equal(3, cat.MemberCount);
    }

    [Fact]
    public void SdGroupCategory_EmptyMembers_ZeroCount()
    {
        var cat = new SdGroupCategory { Name = "Empty" };
        Assert.Equal(0, cat.MemberCount);
    }

    // ── SdWeeklyTotal ──

    [Fact]
    public void SdWeeklyTotal_DayLabel_ReturnsAbbreviation()
    {
        var t = new SdWeeklyTotal { Day = new DateTime(2026, 3, 23) }; // Monday
        Assert.Equal("Mon", t.DayLabel);
    }

    // ── SdTechDaily ──

    [Fact]
    public void SdTechDaily_DayLabel_ReturnsAbbreviation()
    {
        var d = new SdTechDaily { Day = new DateTime(2026, 3, 27) }; // Friday
        Assert.Equal("Fri", d.DayLabel);
    }

    // ── SdAgingBucket ──

    [Fact]
    public void SdAgingBucket_Total_SumsAllBuckets()
    {
        var b = new SdAgingBucket { Days0to1 = 5, Days1to2 = 3, Days2to4 = 7, Days4to7 = 2, Days7Plus = 10 };
        Assert.Equal(27, b.Total);
    }

    // ── SdKpiSummary ──

    [Fact]
    public void SdKpiSummary_DefaultsToZero()
    {
        var kpi = new SdKpiSummary();
        Assert.Equal(0, kpi.Incoming);
        Assert.Equal(0, kpi.Resolutions);
        Assert.Equal(0, kpi.OpenCount);
        Assert.Equal(0, kpi.ActiveTechCount);
    }

    // ── SdGroup ──

    [Fact]
    public void SdGroup_PropertyChanged_Fires()
    {
        var g = new SdGroup();
        var changed = new List<string>();
        g.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        g.Name = "Test";
        g.IsActive = false;

        Assert.Contains("Name", changed);
        Assert.Contains("IsActive", changed);
    }

    // ── SdTechnician ──

    [Fact]
    public void SdTechnician_PropertyChanged_Fires()
    {
        var t = new SdTechnician();
        var changed = new List<string>();
        t.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        t.IsActive = false;

        Assert.Contains("IsActive", changed);
    }

    [Fact]
    public void SdTechnician_DefaultActive()
    {
        var t = new SdTechnician();
        Assert.True(t.IsActive);
    }
}
