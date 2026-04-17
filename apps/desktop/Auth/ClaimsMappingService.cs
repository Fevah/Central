using Central.Engine.Auth;
using Central.Persistence;

namespace Central.Desktop.Auth;

/// <summary>
/// Maps external IdP claims to Central roles using DB-stored claim_mappings rules.
/// First matching rule (by priority) wins.
/// </summary>
public class ClaimsMappingService : IClaimsMappingService
{
    private readonly DbRepository _repo;

    public ClaimsMappingService(DbRepository repo) => _repo = repo;

    public async Task<string> MapClaimsToRoleAsync(int providerId, Dictionary<string, List<string>> claims)
    {
        var mappings = await _repo.GetClaimMappingsAsync(providerId);
        if (mappings.Count == 0)
        {
            // Check global mappings (no provider_id)
            mappings = (await _repo.GetClaimMappingsAsync(null))
                .Where(m => m.IsEnabled).OrderBy(m => m.Priority).ToList();
        }

        foreach (var mapping in mappings.Where(m => m.IsEnabled).OrderBy(m => m.Priority))
        {
            if (!claims.TryGetValue(mapping.ClaimType, out var values)) continue;
            if (values.Contains(mapping.ClaimValue, StringComparer.OrdinalIgnoreCase))
                return mapping.TargetRole;
        }

        // Check provider config for default role
        var providers = await _repo.GetIdentityProvidersAsync(enabledOnly: true);
        var provider = providers.FirstOrDefault(p => p.Id == providerId);
        if (provider != null)
        {
            try
            {
                var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(provider.ConfigJson);
                if (config != null && config.TryGetValue("default_role", out var def))
                    return def.GetString() ?? "Viewer";
            }
            catch { }
        }

        return "Viewer"; // Ultimate fallback
    }
}
