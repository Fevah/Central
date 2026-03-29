namespace Central.Core.Auth;

/// <summary>
/// Guard helper for permission checks. Throws if denied.
/// </summary>
public static class PermissionGuard
{
    public static void Require(string code, IAuthContext? auth = null)
    {
        auth ??= AuthContext.Instance;
        if (!auth.HasPermission(code))
            throw new UnauthorizedAccessException($"Permission denied: {code}");
    }

    public static void RequireSite(string building, IAuthContext? auth = null)
    {
        auth ??= AuthContext.Instance;
        if (!auth.HasSiteAccess(building))
            throw new UnauthorizedAccessException($"Site access denied: {building}");
    }
}
