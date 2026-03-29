using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Central.Api.Auth;

public class TokenService
{
    private readonly JwtSettings _settings;

    public TokenService(JwtSettings settings) => _settings = settings;

    /// <summary>Generate a JWT for an authenticated user with their permission claims.</summary>
    public string GenerateToken(string username, string roleName, IEnumerable<string> permissions)
        => GenerateToken(username, roleName, permissions, null, null, null);

    /// <summary>Generate a JWT with tenant context for multi-tenant scenarios.</summary>
    public string GenerateToken(string username, string roleName, IEnumerable<string> permissions,
        Guid? tenantId, string? tenantSlug, string? tenantTier, bool isGlobalAdmin = false)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new("role", roleName),
        };

        // Tenant claims
        if (tenantId.HasValue && tenantId != Guid.Empty)
            claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));
        if (!string.IsNullOrEmpty(tenantSlug))
            claims.Add(new Claim("tenant_slug", tenantSlug));
        if (!string.IsNullOrEmpty(tenantTier))
            claims.Add(new Claim("tenant_tier", tenantTier));
        if (isGlobalAdmin)
            claims.Add(new Claim("global_admin", "true"));

        // Add each permission as a separate claim
        foreach (var perm in permissions)
            claims.Add(new Claim("perm", perm));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_settings.ExpiryHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
