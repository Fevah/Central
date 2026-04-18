using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Auth;

/// <summary>
/// Observable singleton holding current user, permissions, and allowed sites.
/// Based on TotalLink's AppContextViewModel pattern.
/// </summary>
public class AuthContext : IAuthContext
{
    private static AuthContext? _instance;
    public static AuthContext Instance => _instance ??= new();

    private AuthUser? _currentUser;
    private AuthStates _authState = AuthStates.NotAuthenticated;
    private HashSet<string> _permissions = new();
    private HashSet<string> _allowedSites = new();

    public AuthUser? CurrentUser
    {
        get => _currentUser;
        private set { _currentUser = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAuthenticated)); }
    }

    private Guid _currentTenantId = Guid.Empty;
    public Guid CurrentTenantId
    {
        get => _currentTenantId;
        set { _currentTenantId = value; OnPropertyChanged(); }
    }

    public AuthStates AuthState
    {
        get => _authState;
        set { _authState = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAuthenticated)); }
    }

    public IReadOnlySet<string> AllowedSites => _allowedSites;
    public bool IsAuthenticated => AuthState != AuthStates.NotAuthenticated;
    public bool IsSuperAdmin => (CurrentUser?.Priority ?? 0) >= 1000;

    // ── Permission checks ──

    /// <summary>Number of granted permissions.</summary>
    public int PermissionCount => _permissions.Count;

    public bool HasPermission(string code)
    {
        if (IsSuperAdmin) return true;
        return _permissions.Contains(code);
    }

    public bool HasAnyPermission(params string[] codes)
        => codes.Any(HasPermission);

    public bool HasSiteAccess(string building)
    {
        if (IsSuperAdmin) return true;
        if (_allowedSites.Count == 0) return true;  // No restrictions = all sites
        return _allowedSites.Contains(building);
    }

    // ── Login / session ──

    /// <summary>
    /// Set user + permissions. Called from bootstrap or login flow.
    /// </summary>
    public void SetSession(AuthUser user, IEnumerable<string> permissionCodes,
        IEnumerable<string> allowedSites, AuthStates authState = AuthStates.Windows)
    {
        CurrentUser = user;
        _permissions = new HashSet<string>(permissionCodes, StringComparer.OrdinalIgnoreCase);
        _allowedSites = new HashSet<string>(allowedSites, StringComparer.OrdinalIgnoreCase);
        AuthState = authState;
        PermissionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Update allowed sites without resetting the session.</summary>
    public void UpdateAllowedSites(IEnumerable<string> sites)
    {
        _allowedSites = new HashSet<string>(sites, StringComparer.OrdinalIgnoreCase);
        PermissionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Set offline/fallback mode with full admin permissions.
    /// </summary>
    public void SetOfflineAdmin(string username)
    {
        CurrentUser = new AuthUser
        {
            Username = username,
            DisplayName = username,
            RoleName = "Admin",
            Priority = 1000,
            IsActive = true
        };
        _permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _allowedSites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AuthState = AuthStates.Offline;
        PermissionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Logout()
    {
        CurrentUser = null;
        _permissions.Clear();
        _allowedSites.Clear();
        AuthState = AuthStates.NotAuthenticated;
        PermissionsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Backward compatibility with existing UserSession callers ──

    /// <summary>Legacy check: maps old boolean module permissions to new codes.</summary>
    public bool CanView(string module)
        => HasPermission($"{module}:read");

    public bool CanEdit(string module)
        => HasPermission($"{module}:write");

    public bool CanDelete(string module)
        => HasPermission($"{module}:delete");

    public bool CanViewReserved
        => HasPermission(P.DevicesReserved);

    public bool IsAdmin => IsSuperAdmin || (CurrentUser?.RoleName == "Admin");

    // ── Events ──

    public event EventHandler? PermissionsChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
