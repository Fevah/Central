namespace Central.Core.Auth;

/// <summary>
/// User type constants matching the user_type column in app_users.
/// String-based to match existing DB schema (not a PG enum).
/// </summary>
public static class UserTypes
{
    public const string System = "System";
    public const string Admin = "Admin";
    public const string Standard = "Standard";
    public const string ActiveDirectory = "ActiveDirectory";
    public const string Service = "Service";

    public static readonly string[] All = [System, Admin, Standard, ActiveDirectory, Service];

    /// <summary>Returns true if the user type is protected from deletion.</summary>
    public static bool IsProtected(string? userType)
        => userType is System or Service;
}
