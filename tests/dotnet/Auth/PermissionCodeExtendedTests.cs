using Central.Engine.Auth;

namespace Central.Tests.Auth;

public class PermissionCodeExtendedTests
{
    // ── Device permissions ──

    [Fact]
    public void DevicesRead_Value()
    {
        Assert.Equal("devices:read", P.DevicesRead);
    }

    [Fact]
    public void DevicesWrite_Value()
    {
        Assert.Equal("devices:write", P.DevicesWrite);
    }

    [Fact]
    public void DevicesDelete_Value()
    {
        Assert.Equal("devices:delete", P.DevicesDelete);
    }

    [Fact]
    public void DevicesExport_Value()
    {
        Assert.Equal("devices:export", P.DevicesExport);
    }

    [Fact]
    public void DevicesReserved_Value()
    {
        Assert.Equal("devices:reserved", P.DevicesReserved);
    }

    // ── Switch permissions ──

    [Fact]
    public void SwitchesRead_Value()
    {
        Assert.Equal("switches:read", P.SwitchesRead);
    }

    [Fact]
    public void SwitchesPing_Value()
    {
        Assert.Equal("switches:ping", P.SwitchesPing);
    }

    [Fact]
    public void SwitchesSsh_Value()
    {
        Assert.Equal("switches:ssh", P.SwitchesSsh);
    }

    [Fact]
    public void SwitchesSync_Value()
    {
        Assert.Equal("switches:sync", P.SwitchesSync);
    }

    [Fact]
    public void SwitchesDeploy_Value()
    {
        Assert.Equal("switches:deploy", P.SwitchesDeploy);
    }

    // ── Link permissions ──

    [Fact]
    public void LinksRead_Value()
    {
        Assert.Equal("links:read", P.LinksRead);
    }

    [Fact]
    public void LinksWrite_Value()
    {
        Assert.Equal("links:write", P.LinksWrite);
    }

    [Fact]
    public void LinksDelete_Value()
    {
        Assert.Equal("links:delete", P.LinksDelete);
    }

    // ── BGP permissions ──

    [Fact]
    public void BgpRead_Value()
    {
        Assert.Equal("bgp:read", P.BgpRead);
    }

    [Fact]
    public void BgpWrite_Value()
    {
        Assert.Equal("bgp:write", P.BgpWrite);
    }

    [Fact]
    public void BgpSync_Value()
    {
        Assert.Equal("bgp:sync", P.BgpSync);
    }

    // ── VLAN permissions ──

    [Fact]
    public void VlansRead_Value()
    {
        Assert.Equal("vlans:read", P.VlansRead);
    }

    [Fact]
    public void VlansWrite_Value()
    {
        Assert.Equal("vlans:write", P.VlansWrite);
    }

    // ── Admin permissions ──

    [Fact]
    public void AdminUsers_Value()
    {
        Assert.Equal("admin:users", P.AdminUsers);
    }

    [Fact]
    public void AdminRoles_Value()
    {
        Assert.Equal("admin:roles", P.AdminRoles);
    }

    [Fact]
    public void AdminLookups_Value()
    {
        Assert.Equal("admin:lookups", P.AdminLookups);
    }

    [Fact]
    public void AdminSettings_Value()
    {
        Assert.Equal("admin:settings", P.AdminSettings);
    }

    [Fact]
    public void AdminAudit_Value()
    {
        Assert.Equal("admin:audit", P.AdminAudit);
    }

    [Fact]
    public void AdminAd_Value()
    {
        Assert.Equal("admin:ad", P.AdminAd);
    }

    [Fact]
    public void AdminMigrations_Value()
    {
        Assert.Equal("admin:migrations", P.AdminMigrations);
    }

    [Fact]
    public void AdminPurge_Value()
    {
        Assert.Equal("admin:purge", P.AdminPurge);
    }

    [Fact]
    public void AdminBackup_Value()
    {
        Assert.Equal("admin:backup", P.AdminBackup);
    }

    [Fact]
    public void AdminLocations_Value()
    {
        Assert.Equal("admin:locations", P.AdminLocations);
    }

    [Fact]
    public void AdminReferences_Value()
    {
        Assert.Equal("admin:references", P.AdminReferences);
    }

    [Fact]
    public void AdminContainers_Value()
    {
        Assert.Equal("admin:containers", P.AdminContainers);
    }

    // ── Task permissions ──

    [Fact]
    public void TasksRead_Value()
    {
        Assert.Equal("tasks:read", P.TasksRead);
    }

    [Fact]
    public void TasksWrite_Value()
    {
        Assert.Equal("tasks:write", P.TasksWrite);
    }

    [Fact]
    public void TasksDelete_Value()
    {
        Assert.Equal("tasks:delete", P.TasksDelete);
    }

    // ── Project permissions ──

    [Fact]
    public void ProjectsRead_Value()
    {
        Assert.Equal("projects:read", P.ProjectsRead);
    }

    [Fact]
    public void ProjectsWrite_Value()
    {
        Assert.Equal("projects:write", P.ProjectsWrite);
    }

    [Fact]
    public void ProjectsDelete_Value()
    {
        Assert.Equal("projects:delete", P.ProjectsDelete);
    }

    [Fact]
    public void SprintsRead_Value()
    {
        Assert.Equal("sprints:read", P.SprintsRead);
    }

    [Fact]
    public void SprintsWrite_Value()
    {
        Assert.Equal("sprints:write", P.SprintsWrite);
    }

    [Fact]
    public void SprintsDelete_Value()
    {
        Assert.Equal("sprints:delete", P.SprintsDelete);
    }

    // ── Scheduler permissions ──

    [Fact]
    public void SchedulerRead_Value()
    {
        Assert.Equal("scheduler:read", P.SchedulerRead);
    }

    [Fact]
    public void SchedulerWrite_Value()
    {
        Assert.Equal("scheduler:write", P.SchedulerWrite);
    }

    // ── All codes follow module:action format ──

    [Fact]
    public void AllCodes_ContainColon()
    {
        var fields = typeof(P).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        foreach (var f in fields)
        {
            var value = f.GetValue(null) as string;
            Assert.NotNull(value);
            Assert.Contains(":", value!);
        }
    }

    [Fact]
    public void AllCodes_AreLowercase()
    {
        var fields = typeof(P).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        foreach (var f in fields)
        {
            var value = (string)f.GetValue(null)!;
            Assert.Equal(value, value.ToLowerInvariant());
        }
    }
}
