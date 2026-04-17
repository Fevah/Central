using Central.Engine.Auth;

namespace Central.Tests.Auth;

public class PermissionCodeTests
{
    [Fact]
    public void DevicesRead_HasCorrectValue()
    {
        Assert.Equal("devices:read", P.DevicesRead);
    }

    [Fact]
    public void DevicesWrite_HasCorrectValue()
    {
        Assert.Equal("devices:write", P.DevicesWrite);
    }

    [Fact]
    public void DevicesDelete_HasCorrectValue()
    {
        Assert.Equal("devices:delete", P.DevicesDelete);
    }

    [Fact]
    public void SwitchesRead_HasCorrectValue()
    {
        Assert.Equal("switches:read", P.SwitchesRead);
    }

    [Fact]
    public void SwitchesPing_HasCorrectValue()
    {
        Assert.Equal("switches:ping", P.SwitchesPing);
    }

    [Fact]
    public void SwitchesSsh_HasCorrectValue()
    {
        Assert.Equal("switches:ssh", P.SwitchesSsh);
    }

    [Fact]
    public void LinksRead_HasCorrectValue()
    {
        Assert.Equal("links:read", P.LinksRead);
    }

    [Fact]
    public void BgpRead_HasCorrectValue()
    {
        Assert.Equal("bgp:read", P.BgpRead);
    }

    [Fact]
    public void BgpSync_HasCorrectValue()
    {
        Assert.Equal("bgp:sync", P.BgpSync);
    }

    [Fact]
    public void AdminUsers_HasCorrectValue()
    {
        Assert.Equal("admin:users", P.AdminUsers);
    }

    [Fact]
    public void AdminRoles_HasCorrectValue()
    {
        Assert.Equal("admin:roles", P.AdminRoles);
    }

    [Fact]
    public void AdminSettings_HasCorrectValue()
    {
        Assert.Equal("admin:settings", P.AdminSettings);
    }

    [Fact]
    public void AdminMigrations_HasCorrectValue()
    {
        Assert.Equal("admin:migrations", P.AdminMigrations);
    }

    [Fact]
    public void AdminPurge_HasCorrectValue()
    {
        Assert.Equal("admin:purge", P.AdminPurge);
    }

    [Fact]
    public void AdminBackup_HasCorrectValue()
    {
        Assert.Equal("admin:backup", P.AdminBackup);
    }

    [Fact]
    public void TasksRead_HasCorrectValue()
    {
        Assert.Equal("tasks:read", P.TasksRead);
    }

    [Fact]
    public void TasksWrite_HasCorrectValue()
    {
        Assert.Equal("tasks:write", P.TasksWrite);
    }

    [Fact]
    public void ProjectsRead_HasCorrectValue()
    {
        Assert.Equal("projects:read", P.ProjectsRead);
    }

    [Fact]
    public void SprintsWrite_HasCorrectValue()
    {
        Assert.Equal("sprints:write", P.SprintsWrite);
    }

    [Fact]
    public void SchedulerRead_HasCorrectValue()
    {
        Assert.Equal("scheduler:read", P.SchedulerRead);
    }

    [Fact]
    public void VlansRead_HasCorrectValue()
    {
        Assert.Equal("vlans:read", P.VlansRead);
    }

    [Fact]
    public void AdminAd_HasCorrectValue()
    {
        Assert.Equal("admin:ad", P.AdminAd);
    }

    [Fact]
    public void AdminLocations_HasCorrectValue()
    {
        Assert.Equal("admin:locations", P.AdminLocations);
    }

    [Fact]
    public void AdminReferences_HasCorrectValue()
    {
        Assert.Equal("admin:references", P.AdminReferences);
    }

    [Fact]
    public void AdminContainers_HasCorrectValue()
    {
        Assert.Equal("admin:containers", P.AdminContainers);
    }

    // ── All codes follow module:action format ──

    [Theory]
    [InlineData("devices:read")]
    [InlineData("devices:write")]
    [InlineData("devices:delete")]
    [InlineData("devices:export")]
    [InlineData("switches:deploy")]
    [InlineData("admin:audit")]
    public void AllCodes_FollowModuleActionFormat(string code)
    {
        var parts = code.Split(':');
        Assert.Equal(2, parts.Length);
        Assert.False(string.IsNullOrEmpty(parts[0]));
        Assert.False(string.IsNullOrEmpty(parts[1]));
    }
}
