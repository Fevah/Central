namespace Central.Engine.Auth;

/// <summary>Maps external IdP claims to Central roles and permissions.</summary>
public interface IClaimsMappingService
{
    /// <summary>Map external claims to a Central role name.</summary>
    Task<string> MapClaimsToRoleAsync(int providerId, Dictionary<string, List<string>> claims);
}
