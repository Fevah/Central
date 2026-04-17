using Central.Core.Auth;

namespace Central.Tests.Auth;

public class AuthContextTests
{
    [Fact]
    public void InitialState_NotAuthenticated()
    {
        var ctx = new AuthContext();
        Assert.False(ctx.IsAuthenticated);
        Assert.Null(ctx.CurrentUser);
        Assert.Equal(AuthStates.NotAuthenticated, ctx.AuthState);
    }

    [Fact]
    public void SetSession_SetsAuthState()
    {
        var ctx = new AuthContext();
        var user = new AuthUser { Id = 1, Username = "admin", RoleName = "Admin", Priority = 100 };
        // Use reflection to call SetSession since it's on the singleton
        ctx.SetSession(user, new[] { "devices:read" }, new[] { "MEP-91" }, AuthStates.Password);

        Assert.True(ctx.IsAuthenticated);
        Assert.Equal("admin", ctx.CurrentUser?.Username);
        Assert.Equal(AuthStates.Password, ctx.AuthState);
    }

    [Fact]
    public void HasPermission_Granted()
    {
        var ctx = new AuthContext();
        ctx.SetSession(
            new AuthUser { Id = 1, Username = "op", RoleName = "Operator", Priority = 50 },
            new[] { "devices:read", "devices:write", "switches:read" },
            Array.Empty<string>());

        Assert.True(ctx.HasPermission("devices:read"));
        Assert.True(ctx.HasPermission("devices:write"));
        Assert.False(ctx.HasPermission("admin:users"));
    }

    [Fact]
    public void HasPermission_SuperAdmin_AlwaysTrue()
    {
        var ctx = new AuthContext();
        ctx.SetSession(
            new AuthUser { Id = 1, Username = "super", RoleName = "Admin", Priority = 1000 },
            Array.Empty<string>(), // no explicit permissions
            Array.Empty<string>());

        Assert.True(ctx.HasPermission("anything:at:all"));
        Assert.True(ctx.IsSuperAdmin);
    }

    [Fact]
    public void HasSiteAccess_NoRestrictions()
    {
        var ctx = new AuthContext();
        ctx.SetSession(
            new AuthUser { Id = 1, Username = "u", RoleName = "Viewer" },
            Array.Empty<string>(),
            Array.Empty<string>()); // empty = all sites

        Assert.True(ctx.HasSiteAccess("MEP-91"));
        Assert.True(ctx.HasSiteAccess("anything"));
    }

    [Fact]
    public void HasSiteAccess_Restricted()
    {
        var ctx = new AuthContext();
        ctx.SetSession(
            new AuthUser { Id = 1, Username = "u", RoleName = "Viewer" },
            Array.Empty<string>(),
            new[] { "MEP-91", "MEP-92" });

        Assert.True(ctx.HasSiteAccess("MEP-91"));
        Assert.True(ctx.HasSiteAccess("MEP-92"));
        Assert.False(ctx.HasSiteAccess("MEP-93"));
    }

    [Fact]
    public void SetOfflineAdmin_FullPermissions()
    {
        var ctx = new AuthContext();
        ctx.SetOfflineAdmin("testuser");

        Assert.True(ctx.IsAuthenticated);
        Assert.Equal(AuthStates.Offline, ctx.AuthState);
        Assert.Equal("testuser", ctx.CurrentUser?.Username);
        Assert.True(ctx.IsSuperAdmin); // offline admin = priority 1000
    }

    [Fact]
    public void Logout_ClearsSession()
    {
        var ctx = new AuthContext();
        ctx.SetSession(
            new AuthUser { Id = 1, Username = "u", RoleName = "Admin", Priority = 100 },
            new[] { "devices:read" }, new[] { "MEP-91" });

        ctx.Logout();

        Assert.False(ctx.IsAuthenticated);
        Assert.Null(ctx.CurrentUser);
        Assert.Equal(AuthStates.NotAuthenticated, ctx.AuthState);
    }

    [Fact]
    public void UpdateAllowedSites_Changes()
    {
        var ctx = new AuthContext();
        ctx.SetSession(
            new AuthUser { Id = 1, Username = "u", RoleName = "Viewer" },
            Array.Empty<string>(), new[] { "MEP-91" });

        Assert.True(ctx.HasSiteAccess("MEP-91"));
        Assert.False(ctx.HasSiteAccess("MEP-92"));

        ctx.UpdateAllowedSites(new[] { "MEP-92", "MEP-93" });

        Assert.False(ctx.HasSiteAccess("MEP-91"));
        Assert.True(ctx.HasSiteAccess("MEP-92"));
    }

    [Fact]
    public void HasAnyPermission_OneMatch()
    {
        var ctx = new AuthContext();
        ctx.SetSession(
            new AuthUser { Id = 1, Username = "u", RoleName = "Operator", Priority = 50 },
            new[] { "devices:read" }, Array.Empty<string>());

        Assert.True(ctx.HasAnyPermission("admin:users", "devices:read"));
        Assert.False(ctx.HasAnyPermission("admin:users", "admin:roles"));
    }

    [Fact]
    public void PermissionCount_ReflectsGranted()
    {
        var ctx = new AuthContext();
        ctx.SetSession(
            new AuthUser { Id = 1, Username = "u", RoleName = "Op" },
            new[] { "a", "b", "c" }, Array.Empty<string>());

        Assert.Equal(3, ctx.PermissionCount);
    }

    [Fact]
    public void AuthStates_AllValues()
    {
        Assert.Equal(9, Enum.GetValues<AuthStates>().Length);
    }
}
