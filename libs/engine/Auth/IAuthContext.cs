using System.ComponentModel;

namespace Central.Engine.Auth;

/// <summary>
/// Application authentication and authorization context.
/// Observable singleton — UI binds directly for permission-gated visibility.
/// Replaces the old static UserSession class.
/// </summary>
public interface IAuthContext : INotifyPropertyChanged
{
    /// <summary>Current logged-in user. Null if not authenticated.</summary>
    AuthUser? CurrentUser { get; }

    /// <summary>Current tenant (organization) id. Guid.Empty when not yet
    /// resolved or when running outside the platform (e.g. a test
    /// harness that bypasses the login flow). Consumers that need
    /// tenant-scoped queries should check for Empty before issuing a
    /// query that would otherwise return zero rows.</summary>
    Guid CurrentTenantId { get; }

    /// <summary>Current authentication state.</summary>
    AuthStates AuthState { get; }

    /// <summary>Sites/buildings the current user can access. Empty = all sites.</summary>
    IReadOnlySet<string> AllowedSites { get; }

    bool IsAuthenticated { get; }
    bool IsSuperAdmin { get; }

    /// <summary>Check a single module:action permission code.</summary>
    bool HasPermission(string code);

    /// <summary>Check if user has any of the given permissions.</summary>
    bool HasAnyPermission(params string[] codes);

    /// <summary>Check if user can access a specific building/site.</summary>
    bool HasSiteAccess(string building);

    /// <summary>Fired when permissions change (role switch, re-login).</summary>
    event EventHandler? PermissionsChanged;
}

/// <summary>Minimal user record for auth context.</summary>
public class AuthUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public string UserType { get; set; } = "ActiveDirectory";
    public string PasswordHash { get; set; } = "";
    public string Salt { get; set; } = "";
    public string Email { get; set; } = "";

    // MFA
    public bool MfaEnabled { get; set; }
    public string MfaSecretEnc { get; set; } = "";

    // Password expiry
    public DateTime? PasswordChangedAt { get; set; }
}
