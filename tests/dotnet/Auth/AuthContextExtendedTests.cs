using Central.Core.Auth;

namespace Central.Tests.Auth;

public class AuthContextExtendedTests
{
    private AuthContext CreateFreshContext()
    {
        // AuthContext is a singleton, but we can test via SetSession/Logout
        var ctx = AuthContext.Instance;
        ctx.Logout(); // reset state
        return ctx;
    }

    // ── IsAuthenticated ──

    [Fact]
    public void IsAuthenticated_False_WhenNotAuthenticated()
    {
        var ctx = CreateFreshContext();
        Assert.False(ctx.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_True_AfterSetSession()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "test", Priority = 0 },
            new[] { "devices:read" }, Array.Empty<string>(), AuthStates.Password);
        Assert.True(ctx.IsAuthenticated);
    }

    // ── IsSuperAdmin ──

    [Fact]
    public void IsSuperAdmin_True_WhenPriorityGTE1000()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "admin", Priority = 1000 },
            Array.Empty<string>(), Array.Empty<string>());
        Assert.True(ctx.IsSuperAdmin);
    }

    [Fact]
    public void IsSuperAdmin_False_WhenPriorityBelow1000()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 100 },
            Array.Empty<string>(), Array.Empty<string>());
        Assert.False(ctx.IsSuperAdmin);
    }

    // ── HasPermission ──

    [Fact]
    public void HasPermission_True_WhenGranted()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0 },
            new[] { "devices:read", "switches:read" }, Array.Empty<string>());
        Assert.True(ctx.HasPermission("devices:read"));
    }

    [Fact]
    public void HasPermission_False_WhenNotGranted()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0 },
            new[] { "devices:read" }, Array.Empty<string>());
        Assert.False(ctx.HasPermission("admin:users"));
    }

    [Fact]
    public void HasPermission_True_ForSuperAdmin_EvenWithoutGrant()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "admin", Priority = 1000 },
            Array.Empty<string>(), Array.Empty<string>());
        Assert.True(ctx.HasPermission("admin:users"));
    }

    [Fact]
    public void HasPermission_CaseInsensitive()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0 },
            new[] { "Devices:Read" }, Array.Empty<string>());
        Assert.True(ctx.HasPermission("devices:read"));
    }

    // ── HasAnyPermission ──

    [Fact]
    public void HasAnyPermission_True_WhenOneMatches()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0 },
            new[] { "switches:read" }, Array.Empty<string>());
        Assert.True(ctx.HasAnyPermission("devices:read", "switches:read"));
    }

    [Fact]
    public void HasAnyPermission_False_WhenNoneMatch()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0 },
            new[] { "devices:read" }, Array.Empty<string>());
        Assert.False(ctx.HasAnyPermission("admin:users", "admin:roles"));
    }

    // ── HasSiteAccess ──

    [Fact]
    public void HasSiteAccess_True_WhenNoRestrictions()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0 },
            Array.Empty<string>(), Array.Empty<string>());
        Assert.True(ctx.HasSiteAccess("MEP-91"));
    }

    [Fact]
    public void HasSiteAccess_True_WhenSiteInList()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0 },
            Array.Empty<string>(), new[] { "MEP-91", "MEP-92" });
        Assert.True(ctx.HasSiteAccess("MEP-91"));
    }

    [Fact]
    public void HasSiteAccess_False_WhenSiteNotInList()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0 },
            Array.Empty<string>(), new[] { "MEP-91" });
        Assert.False(ctx.HasSiteAccess("MEP-93"));
    }

    [Fact]
    public void HasSiteAccess_True_ForSuperAdmin()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "admin", Priority = 1000 },
            Array.Empty<string>(), new[] { "MEP-91" });
        Assert.True(ctx.HasSiteAccess("MEP-93")); // super admin bypasses
    }

    // ── Legacy CanView/CanEdit/CanDelete ──

    [Fact]
    public void CanView_MapsToReadPermission()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0 },
            new[] { "devices:read" }, Array.Empty<string>());
        Assert.True(ctx.CanView("devices"));
        Assert.False(ctx.CanView("switches"));
    }

    [Fact]
    public void CanEdit_MapsToWritePermission()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0 },
            new[] { "devices:write" }, Array.Empty<string>());
        Assert.True(ctx.CanEdit("devices"));
        Assert.False(ctx.CanEdit("switches"));
    }

    [Fact]
    public void CanDelete_MapsToDeletePermission()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0 },
            new[] { "devices:delete" }, Array.Empty<string>());
        Assert.True(ctx.CanDelete("devices"));
        Assert.False(ctx.CanDelete("switches"));
    }

    // ── CanViewReserved ──

    [Fact]
    public void CanViewReserved_True_WhenGranted()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0 },
            new[] { P.DevicesReserved }, Array.Empty<string>());
        Assert.True(ctx.CanViewReserved);
    }

    [Fact]
    public void CanViewReserved_False_WhenNotGranted()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0 },
            new[] { "devices:read" }, Array.Empty<string>());
        Assert.False(ctx.CanViewReserved);
    }

    // ── IsAdmin ──

    [Fact]
    public void IsAdmin_True_WhenSuperAdmin()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "admin", Priority = 1000, RoleName = "Operator" },
            Array.Empty<string>(), Array.Empty<string>());
        Assert.True(ctx.IsAdmin);
    }

    [Fact]
    public void IsAdmin_True_WhenAdminRole()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "admin", Priority = 100, RoleName = "Admin" },
            Array.Empty<string>(), Array.Empty<string>());
        Assert.True(ctx.IsAdmin);
    }

    [Fact]
    public void IsAdmin_False_WhenNeitherSuperAdminNorAdminRole()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0, RoleName = "Viewer" },
            Array.Empty<string>(), Array.Empty<string>());
        Assert.False(ctx.IsAdmin);
    }

    // ── SetOfflineAdmin ──

    [Fact]
    public void SetOfflineAdmin_SetsOfflineState()
    {
        var ctx = CreateFreshContext();
        ctx.SetOfflineAdmin("testuser");
        Assert.Equal(AuthStates.Offline, ctx.AuthState);
        Assert.True(ctx.IsAuthenticated);
        Assert.NotNull(ctx.CurrentUser);
        Assert.Equal("testuser", ctx.CurrentUser!.Username);
        Assert.Equal("Admin", ctx.CurrentUser.RoleName);
        Assert.Equal(1000, ctx.CurrentUser.Priority);
        Assert.True(ctx.IsSuperAdmin);
    }

    // ── Logout ──

    [Fact]
    public void Logout_ResetsState()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "admin", Priority = 1000 },
            new[] { "devices:read" }, new[] { "MEP-91" });

        ctx.Logout();

        Assert.Null(ctx.CurrentUser);
        Assert.Equal(AuthStates.NotAuthenticated, ctx.AuthState);
        Assert.False(ctx.IsAuthenticated);
    }

    // ── UpdateAllowedSites ──

    [Fact]
    public void UpdateAllowedSites_ChangesSiteAccess()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0 },
            Array.Empty<string>(), new[] { "MEP-91" });

        Assert.True(ctx.HasSiteAccess("MEP-91"));
        Assert.False(ctx.HasSiteAccess("MEP-92"));

        ctx.UpdateAllowedSites(new[] { "MEP-92", "MEP-93" });

        Assert.False(ctx.HasSiteAccess("MEP-91"));
        Assert.True(ctx.HasSiteAccess("MEP-92"));
        Assert.True(ctx.HasSiteAccess("MEP-93"));
    }

    // ── PermissionCount ──

    [Fact]
    public void PermissionCount_ReflectsGranted()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user", Priority = 0 },
            new[] { "devices:read", "devices:write", "switches:read" }, Array.Empty<string>());
        Assert.Equal(3, ctx.PermissionCount);
    }

    [Fact]
    public void PermissionCount_Zero_AfterLogout()
    {
        var ctx = CreateFreshContext();
        Assert.Equal(0, ctx.PermissionCount);
    }

    // ── Events ──

    [Fact]
    public void PermissionsChanged_FiresOnSetSession()
    {
        var ctx = CreateFreshContext();
        bool fired = false;
        ctx.PermissionsChanged += (_, _) => fired = true;
        ctx.SetSession(new AuthUser { Username = "user" },
            Array.Empty<string>(), Array.Empty<string>());
        Assert.True(fired);
    }

    [Fact]
    public void PermissionsChanged_FiresOnLogout()
    {
        var ctx = CreateFreshContext();
        ctx.SetSession(new AuthUser { Username = "user" },
            Array.Empty<string>(), Array.Empty<string>());

        bool fired = false;
        ctx.PermissionsChanged += (_, _) => fired = true;
        ctx.Logout();
        Assert.True(fired);
    }

    [Fact]
    public void PropertyChanged_FiresOnAuthStateChange()
    {
        var ctx = CreateFreshContext();
        var changed = new List<string>();
        ctx.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        ctx.SetSession(new AuthUser { Username = "user" },
            Array.Empty<string>(), Array.Empty<string>());
        Assert.Contains("AuthState", changed);
        Assert.Contains("IsAuthenticated", changed);
    }
}
