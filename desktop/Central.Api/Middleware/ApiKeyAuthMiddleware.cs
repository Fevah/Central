using System.Security.Claims;
using Npgsql;
using Central.Data;

namespace Central.Api.Middleware;

/// <summary>
/// API key authentication middleware for service-to-service calls.
/// Checks X-API-Key header against api_keys table.
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
            await using var cmd = new NpgsqlCommand(
                "SELECT id, name, role, is_active FROM api_keys WHERE key_hash = @hash AND is_active = true", conn);
            cmd.Parameters.AddWithValue("hash", HashApiKey(apiKey));
            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var keyName = reader.GetString(1);
                var role = reader.GetString(2);

                // Set authenticated identity from API key
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, $"apikey:{keyName}"),
                    new Claim(ClaimTypes.Role, role),
                    new Claim("auth_method", "api_key")
                };
                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKey"));

                // Update last used
                await reader.CloseAsync();
                await using var updateCmd = new NpgsqlCommand(
                    "UPDATE api_keys SET last_used_at = NOW(), use_count = COALESCE(use_count, 0) + 1 WHERE key_hash = @hash", conn);
                updateCmd.Parameters.AddWithValue("hash", HashApiKey(apiKey));
                await updateCmd.ExecuteNonQueryAsync();
            }
        }
        catch { /* fall through to JWT */ }

        await _next(context);
    }

    private static string HashApiKey(string key)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(bytes);
    }
}

public static class ApiKeyAuthExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder app)
        => app.UseMiddleware<ApiKeyAuthMiddleware>();
}
