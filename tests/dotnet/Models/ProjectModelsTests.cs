using Central.Core.Models;

namespace Central.Tests.Models;

public class ProjectModelsTests
{
    // ── Portfolio ──

    [Fact]
    public void Portfolio_Defaults()
    {
        var p = new Portfolio();
        Assert.Equal("", p.Name);
        Assert.Equal("", p.Description);
        Assert.Null(p.OwnerId);
        Assert.False(p.Archived);
        Assert.NotNull(p.Programmes);
        Assert.Empty(p.Programmes);
    }

    [Fact]
    public void Portfolio_PropertyChanged_Fires()
    {
        var p = new Portfolio();
        var changed = new List<string>();
        p.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        p.Name = "Test Portfolio";
        Assert.Contains("Name", changed);
    }

    // ── Programme ──

    [Fact]
    public void Programme_Defaults()
    {
        var prog = new Programme();
        Assert.Equal("", prog.Name);
        Assert.Null(prog.PortfolioId);
        Assert.NotNull(prog.Projects);
        Assert.Empty(prog.Projects);
    }

    [Fact]
    public void Programme_PropertyChanged_Fires()
    {
        var prog = new Programme();
        var changed = new List<string>();
        prog.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        prog.Name = "Test Programme";
        Assert.Contains("Name", changed);
    }

    // ── TaskProject ──

    [Fact]
    public void TaskProject_Defaults()
    {
        var tp = new TaskProject();
        Assert.Equal("FixedDuration", tp.SchedulingMethod);
        Assert.Equal("Agile", tp.DefaultMode);
        Assert.Equal("Scrum", tp.MethodTemplate);
        Assert.False(tp.Archived);
    }

    [Fact]
    public void TaskProject_DisplayName_Normal()
    {
        var tp = new TaskProject { Name = "My Project", Archived = false };
        Assert.Equal("My Project", tp.DisplayName);
    }

    [Fact]
    public void TaskProject_DisplayName_Archived()
    {
        var tp = new TaskProject { Name = "My Project", Archived = true };
        Assert.Equal("My Project (archived)", tp.DisplayName);
    }

    [Fact]
    public void TaskProject_PropertyChanged_Fires()
    {
        var tp = new TaskProject();
        var changed = new List<string>();
        tp.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        tp.Name = "Test";
        Assert.Contains("Name", changed);
    }

    // ── Sprint ──

    [Fact]
    public void Sprint_Defaults()
    {
        var s = new Sprint();
        Assert.Equal("Planning", s.Status);
        Assert.Null(s.StartDate);
        Assert.Null(s.EndDate);
    }

    [Fact]
    public void Sprint_DateRange_BothDates()
    {
        var s = new Sprint
        {
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 14)
        };
        Assert.Contains("2026-01-01", s.DateRange);
        Assert.Contains("2026-01-14", s.DateRange);
    }

    [Fact]
    public void Sprint_DateRange_MissingDates_Empty()
    {
        var s = new Sprint { StartDate = null, EndDate = null };
        Assert.Equal("", s.DateRange);
    }

    [Fact]
    public void Sprint_DisplayName_IncludesStatus()
    {
        var s = new Sprint { Name = "Sprint 1", Status = "Active" };
        Assert.Equal("Sprint 1 (Active)", s.DisplayName);
    }

    // ── Release ──

    [Fact]
    public void Release_Defaults()
    {
        var r = new Release();
        Assert.Equal("Planned", r.Status);
        Assert.Null(r.TargetDate);
    }

    [Fact]
    public void Release_PropertyChanged_Fires()
    {
        var r = new Release();
        var changed = new List<string>();
        r.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        r.Name = "v1.0";
        Assert.Contains("Name", changed);
    }

    // ── TaskLink ──

    [Fact]
    public void TaskLink_Defaults()
    {
        var tl = new TaskLink();
        Assert.Equal("relates_to", tl.LinkType);
        Assert.Equal(0, tl.LagDays);
    }

    [Fact]
    public void TaskLink_LinkDisplay()
    {
        var tl = new TaskLink { LinkType = "blocks", TargetTitle = "Fix the bug" };
        Assert.Equal("blocks: Fix the bug", tl.LinkDisplay);
    }

    // ── TaskDependency ──

    [Fact]
    public void TaskDependency_Defaults()
    {
        var td = new TaskDependency();
        Assert.Equal("FS", td.DepType);
        Assert.Equal(0, td.LagDays);
    }

    [Fact]
    public void TaskDependency_DepDisplay_NoLag()
    {
        var td = new TaskDependency { DepType = "FS", LagDays = 0, PredecessorTitle = "Setup" };
        Assert.Equal("FS: Setup", td.DepDisplay);
    }

    [Fact]
    public void TaskDependency_DepDisplay_WithLag()
    {
        var td = new TaskDependency { DepType = "SS", LagDays = 2, PredecessorTitle = "Design" };
        Assert.Equal("SS+2d: Design", td.DepDisplay);
    }

    // ── ProjectMember ──

    [Fact]
    public void ProjectMember_DefaultRole()
    {
        var pm = new ProjectMember();
        Assert.Equal("Member", pm.Role);
    }

    [Fact]
    public void ProjectMember_PropertyChanged_Fires()
    {
        var pm = new ProjectMember();
        var changed = new List<string>();
        pm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        pm.Role = "Lead";
        Assert.Contains("Role", changed);
    }
}
