using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Central.Data;

namespace Central.Api.Middleware;

/// <summary>
/// API key authentication middleware for service-to-service calls.
/// Checks X-API-Key header against api_keys table.
/// Keys are stored as SHA256(key + salt) with per-key salt.
/// Falls through to JWT auth if no API key is provided.
/// </summary>
public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;

    public ApiKeyAuthMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Only check if X-API-Key header is present (otherwise fall through to JWT)
        if (!context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyValues))
        {
            await _next(context);
            return;
        }

        var apiKey = apiKeyValues.FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
        {
            await _next(context);
            return;
        }

        // Validate API key against database
        try
        {
            var db = context.RequestServices.GetRequiredService<DbConnectionFactory>();
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();

            // Query all active keys — must check each salt individually
            // For high-volume APIs, consider caching validated keys in memory with short TTL
            await using var cmd = new NpgsqlCommand(
                "SELECT id, name, role, key_hash, key_salt FROM api_keys WHERE is_active = true", conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            int? matchedKeyId = null;
            string? keyName = null;
            string? role = null;

            while (await reader.ReadAsync())
            {
                var storedHash = reader.GetString(3);
                var salt = reader.IsDBNull(4) ? "" : reader.GetString(4);
                var computedHash = HashApiKey(apiKey, salt);

                if (CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(storedHash),
                    Encoding.UTF8.GetBytes(computedHash)))
                {
                    matchedKeyId = reader.GetInt32(0);
                    keyName = reader.GetString(1);
                    role = reader.GetString(2);
                    break;
                }
            }
            await reader.CloseAsync();

            if (matchedKeyId.HasValue)
            {
                // Set authenticated identity from API key
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, $"apikey:{keyName}"),
                    new Claim(ClaimTypes.Role, role!),
                    new Claim("auth_method", "api_key")
                };
                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKey"));

                // Update last used
                await using var updateCmd = new NpgsqlCommand(
                    "UPDATE api_keys SET last_used_at = NOW(), use_count = COALESCE(use_count, 0) + 1 WHERE id = @id", conn);
                updateCmd.Parameters.AddWithValue("id", matchedKeyId.Value);
                await updateCmd.ExecuteNonQueryAsync();
            }
        }
        catch { /* fall through to JWT */ }

        await _next(context);
    }

    /// <summary>Hash an API key with salt using SHA256. Salt prevents rainbow table attacks.</summary>
    internal static string HashApiKey(string key, string salt = "")
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key + salt));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Generate a random salt for new API keys.</summary>
    internal static string GenerateSalt()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
}

public static class ApiKeyAuthExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder app)
        => app.UseMiddleware<ApiKeyAuthMiddleware>();
}
