namespace Central.Tests.Models;

/// <summary>
/// Tests the ribbon sync blocklist logic that prevents cleaned-up items
/// from being re-created during SyncModuleRibbonToDbAsync.
/// These mirror the static HashSets in MainWindow.
/// </summary>
public class RibbonSyncBlocklistTests
{
    // ── Page blocklist ──

    private static readonly HashSet<string> PageBlocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "Link Actions", "Admin Actions"
    };

    [Theory]
    [InlineData("Link Actions")]
    [InlineData("Admin Actions")]
    [InlineData("link actions")]  // case insensitive
    [InlineData("ADMIN ACTIONS")]
    public void PageBlocklist_BlocksOrphanedPages(string page)
    {
        Assert.Contains(page, PageBlocklist);
    }

    [Theory]
    [InlineData("Home")]
    [InlineData("Devices")]
    [InlineData("Admin")]
    [InlineData("Tasks")]
    public void PageBlocklist_AllowsValidPages(string page)
    {
        Assert.DoesNotContain(page, PageBlocklist);
    }

    // ── Panel blocklist ──

    private static readonly HashSet<string> PanelBlocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "Devices", "Backlog", "Sprint Plan", "Burndown", "Kanban",
        "Gantt", "QA / Bugs", "QA Dashboard", "Reports", "Dashboard",
        "Timesheet", "Activity", "My Tasks", "Portfolio", "Import", "Tasks",
        "Task Detail", "Ribbon Config"
    };

    [Theory]
    [InlineData("Backlog")]
    [InlineData("Sprint Plan")]
    [InlineData("Kanban")]
    [InlineData("Tasks")]
    [InlineData("Ribbon Config")]
    public void PanelBlocklist_BlocksCrossModulePanels(string panel)
    {
        Assert.Contains(panel, PanelBlocklist);
    }

    [Theory]
    [InlineData("IPAM")]
    [InlineData("Master")]
    [InlineData("ASN")]
    [InlineData("P2P")]
    [InlineData("B2B")]
    [InlineData("FW")]
    [InlineData("Switches")]
    [InlineData("BGP")]
    [InlineData("Details")]
    public void PanelBlocklist_AllowsDevicesPanels(string panel)
    {
        Assert.DoesNotContain(panel, PanelBlocklist);
    }

    // ── Item blocklist ──

    private static readonly HashSet<string> ItemBlocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "Home/Undo/Undo", "Home/Undo/Redo", "Home/Tools/Bulk Edit",
        "Switches/Tools/Bulk Edit",
        "Admin/Actions/New", "Admin/Data/Refresh",
        "Admin/Panels/Roles", "Admin/Panels/Lookups", "Admin/Panels/Global Actions...",
        "Tasks/Actions/Delete Task", "Tasks/View/Refresh"
    };

    [Theory]
    [InlineData("Home/Undo/Undo")]
    [InlineData("Home/Undo/Redo")]
    [InlineData("Home/Tools/Bulk Edit")]
    [InlineData("Switches/Tools/Bulk Edit")]
    [InlineData("Admin/Actions/New")]
    [InlineData("Admin/Data/Refresh")]
    [InlineData("Admin/Panels/Roles")]
    [InlineData("Admin/Panels/Lookups")]
    [InlineData("Admin/Panels/Global Actions...")]
    [InlineData("Tasks/Actions/Delete Task")]
    [InlineData("Tasks/View/Refresh")]
    public void ItemBlocklist_BlocksCleanedItems(string key)
    {
        Assert.Contains(key, ItemBlocklist);
    }

    [Theory]
    [InlineData("Home/Actions/Add")]
    [InlineData("Home/Actions/Undo")]
    [InlineData("Home/Actions/Bulk Edit")]
    [InlineData("Admin/Actions/Add")]
    [InlineData("Tasks/Actions/Add")]
    [InlineData("Tasks/Actions/Add Task")]
    [InlineData("Tasks/Actions/Delete")]
    public void ItemBlocklist_AllowsValidItems(string key)
    {
        Assert.DoesNotContain(key, ItemBlocklist);
    }

    [Fact]
    public void ItemBlocklist_CaseInsensitive()
    {
        Assert.Contains("home/undo/undo", ItemBlocklist);
        Assert.Contains("ADMIN/ACTIONS/NEW", ItemBlocklist);
    }
}
