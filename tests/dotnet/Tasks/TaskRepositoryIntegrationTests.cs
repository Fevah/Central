using Central.Core.Models;
using Central.Data;

namespace Central.Tests.Tasks;

/// <summary>
/// Integration tests for task repository methods against live PostgreSQL.
/// Requires database to be running. Tests are skipped if DB unavailable.
/// </summary>
[Collection("Database")]
public class TaskRepositoryIntegrationTests
{
    private static readonly string? Dsn = Environment.GetEnvironmentVariable("CENTRAL_DSN")
        ?? "Host=localhost;Port=5432;Database=central;Username=central;Password=central;Include Error Detail=true";

    private DbRepository? GetRepo()
    {
        try
        {
            var repo = new DbRepository(Dsn!);
            // Quick connectivity test
            var task = repo.GetTaskProjectsAsync();
            task.Wait(TimeSpan.FromSeconds(3));
            return task.IsCompletedSuccessfully ? repo : null;
        }
        catch { return null; }
    }

    [Fact]
    public async Task GetTaskProjects_ReturnsDefaultProject()
    {
        var repo = GetRepo();
        if (repo == null) return; // skip if no DB

        var projects = await repo.GetTaskProjectsAsync();
        Assert.Contains(projects, p => p.Name == "Default Project");
    }

    [Fact]
    public async Task GetTasks_ReturnsSeededData()
    {
        var repo = GetRepo();
        if (repo == null) return;

        var tasks = await repo.GetTasksAsync();
        Assert.NotEmpty(tasks);
        Assert.Contains(tasks, t => t.Title == "Network Refresh MEP-91");
    }

    [Fact]
    public async Task GetTasks_FilterByProject()
    {
        var repo = GetRepo();
        if (repo == null) return;

        var projects = await repo.GetTaskProjectsAsync();
        var defaultProject = projects.First(p => p.Name == "Default Project");
        var tasks = await repo.GetTasksAsync(defaultProject.Id);
        Assert.All(tasks, t => Assert.Equal(defaultProject.Id, t.ProjectId));
    }

    [Fact]
    public async Task UpsertAndDeleteTask_RoundTrip()
    {
        var repo = GetRepo();
        if (repo == null) return;

        var task = new TaskItem
        {
            Title = $"IntTest_{Guid.NewGuid():N}",
            Status = "Open", Priority = "Medium", TaskType = "Task",
            Points = 3, Category = "TechDebt"
        };

        // Insert
        await repo.UpsertTaskAsync(task);
        Assert.True(task.Id > 0);

        // Read back
        var all = await repo.GetTasksAsync();
        var found = all.FirstOrDefault(t => t.Id == task.Id);
        Assert.NotNull(found);
        Assert.Equal("TechDebt", found.Category);
        Assert.Equal(3m, found.Points);

        // Update
        task.Status = "Done";
        task.Points = 5;
        await repo.UpsertTaskAsync(task);
        var updated = (await repo.GetTasksAsync()).First(t => t.Id == task.Id);
        Assert.Equal("Done", updated.Status);
        Assert.Equal(5m, updated.Points);
        Assert.NotNull(updated.CompletedAt);

        // Delete
        await repo.DeleteTaskAsync(task.Id);
        var after = (await repo.GetTasksAsync()).FirstOrDefault(t => t.Id == task.Id);
        Assert.Null(after);
    }

    [Fact]
    public async Task Portfolio_CRUD_RoundTrip()
    {
        var repo = GetRepo();
        if (repo == null) return;

        var portfolio = new Portfolio { Name = $"TestPortfolio_{Guid.NewGuid():N}" };
        await repo.UpsertPortfolioAsync(portfolio);
        Assert.True(portfolio.Id > 0);

        var all = await repo.GetPortfoliosAsync();
        Assert.Contains(all, p => p.Id == portfolio.Id);

        await repo.DeletePortfolioAsync(portfolio.Id);
        all = await repo.GetPortfoliosAsync();
        Assert.DoesNotContain(all, p => p.Id == portfolio.Id);
    }

    [Fact]
    public async Task Sprint_CRUD_RoundTrip()
    {
        var repo = GetRepo();
        if (repo == null) return;

        var projects = await repo.GetTaskProjectsAsync();
        var pid = projects.First().Id;

        var sprint = new Sprint
        {
            ProjectId = pid, Name = $"TestSprint_{Guid.NewGuid():N}",
            StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(14),
            Status = "Planning"
        };
        await repo.UpsertSprintAsync(sprint);
        Assert.True(sprint.Id > 0);

        var sprints = await repo.GetSprintsAsync(pid);
        Assert.Contains(sprints, s => s.Id == sprint.Id);

        await repo.DeleteSprintAsync(sprint.Id);
        sprints = await repo.GetSprintsAsync(pid);
        Assert.DoesNotContain(sprints, s => s.Id == sprint.Id);
    }

    [Fact]
    public async Task BoardColumns_LoadsSeeded()
    {
        var repo = GetRepo();
        if (repo == null) return;

        var projects = await repo.GetTaskProjectsAsync();
        var pid = projects.First().Id;

        var cols = await repo.GetBoardColumnsAsync(pid);
        Assert.Equal(5, cols.Count);
        Assert.Equal("Backlog", cols[0].ColumnName);
        Assert.Equal("Done", cols[4].ColumnName);
        Assert.Equal(5, cols[2].WipLimit); // In Progress
    }

    [Fact]
    public async Task CommitToSprint_SetsCommittedTo()
    {
        var repo = GetRepo();
        if (repo == null) return;

        var projects = await repo.GetTaskProjectsAsync();
        var pid = projects.First().Id;

        // Create a sprint + task
        var sprint = new Sprint { ProjectId = pid, Name = $"CommitTest_{Guid.NewGuid():N}", Status = "Active" };
        await repo.UpsertSprintAsync(sprint);

        var task = new TaskItem { Title = $"CommitTask_{Guid.NewGuid():N}", Status = "Open", Priority = "Medium", TaskType = "Task", ProjectId = pid };
        await repo.UpsertTaskAsync(task);

        // Commit
        await repo.CommitToSprintAsync(task.Id, sprint.Id);
        var tasks = await repo.GetTasksAsync(pid);
        var committed = tasks.First(t => t.Id == task.Id);
        Assert.Equal(sprint.Id, committed.CommittedTo);

        // Uncommit
        await repo.UncommitFromSprintAsync(task.Id);
        tasks = await repo.GetTasksAsync(pid);
        committed = tasks.First(t => t.Id == task.Id);
        Assert.Null(committed.CommittedTo);

        // Cleanup
        await repo.DeleteTaskAsync(task.Id);
        await repo.DeleteSprintAsync(sprint.Id);
    }

    [Fact]
    public async Task CustomColumn_CRUD_RoundTrip()
    {
        var repo = GetRepo();
        if (repo == null) return;

        var projects = await repo.GetTaskProjectsAsync();
        var pid = projects.First().Id;

        var col = new CustomColumn
        {
            ProjectId = pid, Name = $"TestCol_{Guid.NewGuid():N}",
            ColumnType = "DropList",
            Config = """{"options": ["A", "B", "C"]}"""
        };
        await repo.UpsertCustomColumnAsync(col);
        Assert.True(col.Id > 0);

        var cols = await repo.GetCustomColumnsAsync(pid);
        Assert.Contains(cols, c => c.Id == col.Id);
        Assert.Equal("DropList", cols.First(c => c.Id == col.Id).ColumnType);

        await repo.DeleteCustomColumnAsync(col.Id);
        cols = await repo.GetCustomColumnsAsync(pid);
        Assert.DoesNotContain(cols, c => c.Id == col.Id);
    }

    [Fact]
    public async Task TimeEntry_CRUD_RoundTrip()
    {
        var repo = GetRepo();
        if (repo == null) return;

        var tasks = await repo.GetTasksAsync();
        if (tasks.Count == 0) return;
        var taskId = tasks.First().Id;

        var entry = new TimeEntry
        {
            TaskId = taskId, UserId = 1, EntryDate = DateTime.Today,
            Hours = 2.5m, ActivityType = "Development", Notes = "Integration test"
        };
        await repo.UpsertTimeEntryAsync(entry);
        Assert.True(entry.Id > 0);

        var entries = await repo.GetTimeEntriesAsync(userId: 1, from: DateTime.Today, to: DateTime.Today);
        Assert.Contains(entries, e => e.Id == entry.Id);
        Assert.Equal(2.5m, entries.First(e => e.Id == entry.Id).Hours);

        await repo.DeleteTimeEntryAsync(entry.Id);
        entries = await repo.GetTimeEntriesAsync(userId: 1, from: DateTime.Today, to: DateTime.Today);
        Assert.DoesNotContain(entries, e => e.Id == entry.Id);
    }

    [Fact]
    public async Task ActivityFeed_AutoPopulatedByTrigger()
    {
        var repo = GetRepo();
        if (repo == null) return;

        // Creating a task should auto-insert into activity_feed via trigger
        var task = new TaskItem { Title = $"FeedTest_{Guid.NewGuid():N}", Status = "Open", Priority = "Medium", TaskType = "Task" };
        await repo.UpsertTaskAsync(task);

        var feed = await repo.GetActivityFeedAsync(limit: 5);
        Assert.Contains(feed, f => f.Summary != null && f.Summary.Contains(task.Title));

        await repo.DeleteTaskAsync(task.Id);
    }

    [Fact]
    public async Task SavedReport_CRUD_RoundTrip()
    {
        var repo = GetRepo();
        if (repo == null) return;

        var report = new SavedReport
        {
            Name = $"TestReport_{Guid.NewGuid():N}",
            Folder = "IntTests",
            QueryJson = """{"entity_type":"task","filters":[]}"""
        };
        await repo.UpsertSavedReportAsync(report);
        Assert.True(report.Id > 0);

        var reports = await repo.GetSavedReportsAsync();
        Assert.Contains(reports, r => r.Id == report.Id);

        await repo.DeleteSavedReportAsync(report.Id);
        reports = await repo.GetSavedReportsAsync();
        Assert.DoesNotContain(reports, r => r.Id == report.Id);
    }

    [Fact]
    public async Task SprintBurndown_Snapshot()
    {
        var repo = GetRepo();
        if (repo == null) return;

        var projects = await repo.GetTaskProjectsAsync();
        var pid = projects.First().Id;

        var sprint = new Sprint { ProjectId = pid, Name = $"BurndownTest_{Guid.NewGuid():N}", Status = "Active" };
        await repo.UpsertSprintAsync(sprint);

        await repo.SnapshotSprintBurndownAsync(sprint.Id);
        var data = await repo.GetSprintBurndownAsync(sprint.Id);
        Assert.NotEmpty(data);
        Assert.Equal(DateTime.Today, data[0].SnapshotDate.Date);

        await repo.DeleteSprintAsync(sprint.Id);
    }
}
