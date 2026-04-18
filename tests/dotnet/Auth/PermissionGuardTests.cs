using System.ComponentModel;
using Central.Engine.Auth;

namespace Central.Tests.Auth;

public class PermissionGuardTests
{
    private class FakeAuthContext : IAuthContext
    {
        private readonly HashSet<string> _perms;
        private readonly HashSet<string> _sites;

        public FakeAuthContext(IEnumerable<string>? perms = null, IEnumerable<string>? sites = null)
        {
            _perms = new HashSet<string>(perms ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            _sites = new HashSet<string>(sites ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }

        public AuthUser? CurrentUser => new AuthUser { Username = "test" };
        public AuthStates AuthState => AuthStates.Password;
        public Guid CurrentTenantId => Guid.Empty;
        public IReadOnlySet<string> AllowedSites => _sites;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;

        public bool HasPermission(string code) => _perms.Contains(code);
        public bool HasSiteAccess(string building) => _sites.Count == 0 || _sites.Contains(building);
        public bool HasAnyPermission(params string[] codes) => codes.Any(HasPermission);

        public event EventHandler? PermissionsChanged;
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void Require_WithPermission_DoesNotThrow()
    {
        var auth = new FakeAuthContext(perms: new[] { "devices:read" });
        PermissionGuard.Require("devices:read", auth);
    }

    [Fact]
    public void Require_WithoutPermission_Throws()
    {
        var auth = new FakeAuthContext();
        Assert.Throws<UnauthorizedAccessException>(() =>
            PermissionGuard.Require("admin:users", auth));
    }

    [Fact]
    public void Require_ThrowsMessageContainsCode()
    {
        var auth = new FakeAuthContext();
        var ex = Assert.Throws<UnauthorizedAccessException>(() =>
            PermissionGuard.Require("switches:ssh", auth));
        Assert.Contains("switches:ssh", ex.Message);
    }

    [Fact]
    public void RequireSite_WithAccess_DoesNotThrow()
    {
        var auth = new FakeAuthContext(sites: new[] { "MEP-91", "MEP-92" });
        PermissionGuard.RequireSite("MEP-91", auth);
    }

    [Fact]
    public void RequireSite_WithoutAccess_Throws()
    {
        var auth = new FakeAuthContext(sites: new[] { "MEP-91" });
        Assert.Throws<UnauthorizedAccessException>(() =>
            PermissionGuard.RequireSite("MEP-99", auth));
    }

    [Fact]
    public void RequireSite_NoRestrictions_AllAllowed()
    {
        var auth = new FakeAuthContext(sites: Enumerable.Empty<string>());
        PermissionGuard.RequireSite("ANY-SITE", auth);
    }

    [Fact]
    public void Require_CaseInsensitive_Works()
    {
        var auth = new FakeAuthContext(perms: new[] { "Devices:Read" });
        PermissionGuard.Require("devices:read", auth);
    }
}
