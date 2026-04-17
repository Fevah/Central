using System.Text.Json;
using Npgsql;
using Central.Data;

namespace Central.Api.Endpoints;

public static class IdentityProviderEndpoints
{
    public static RouteGroupBuilder MapIdentityProviderEndpoints(this RouteGroupBuilder group)
    {
        // ── Identity Providers ──

        group.MapGet("/providers", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT id, provider_type, name, is_enabled, is_default, priority, config_json::text, metadata_url FROM identity_providers ORDER BY priority", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPut("/providers", async (DbConnectionFactory db, JsonElement body) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var id = body.TryGetProperty("id", out var idp) ? idp.GetInt32() : 0;
            var providerType = body.GetProperty("provider_type").GetString() ?? "";
            var name = body.GetProperty("name").GetString() ?? "";
            var isEnabled = !body.TryGetProperty("is_enabled", out var en) || en.GetBoolean();
            var isDefault = body.TryGetProperty("is_default", out var def) && def.GetBoolean();
            var priority = body.TryGetProperty("priority", out var pri) ? pri.GetInt32() : 100;
            var configJson = body.TryGetProperty("config_json", out var cfg) ? cfg.GetRawText() : "{}";
            var metadataUrl = body.TryGetProperty("metadata_url", out var mu) ? mu.GetString() : null;

            await using var cmd = new NpgsqlCommand(id > 0
                ? @"UPDATE identity_providers SET provider_type=@pt, name=@n, is_enabled=@en, is_default=@def,
                    priority=@pri, config_json=@cfg::jsonb, metadata_url=@mu, updated_at=NOW() WHERE id=@id"
                : @"INSERT INTO identity_providers (provider_type, name, is_enabled, is_default, priority, config_json, metadata_url)
                    VALUES (@pt, @n, @en, @def, @pri, @cfg::jsonb, @mu) RETURNING id", conn);
            if (id > 0) cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("pt", providerType);
            cmd.Parameters.AddWithValue("n", name);
            cmd.Parameters.AddWithValue("en", isEnabled);
            cmd.Parameters.AddWithValue("def", isDefault);
            cmd.Parameters.AddWithValue("pri", priority);
            cmd.Parameters.AddWithValue("cfg", configJson);
            cmd.Parameters.AddWithValue("mu", (object?)metadataUrl ?? DBNull.Value);
            if (id == 0) id = (int)(await cmd.ExecuteScalarAsync())!;
            else await cmd.ExecuteNonQueryAsync();
            return Results.Ok(new { id });
        });

        group.MapDelete("/providers/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM identity_providers WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        // ── Domain Mappings ──

        group.MapGet("/domain-mappings", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT d.id, d.email_domain, d.provider_id, p.name as provider_name FROM idp_domain_mappings d JOIN identity_providers p ON p.id=d.provider_id ORDER BY d.email_domain", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPut("/domain-mappings", async (DbConnectionFactory db, JsonElement body) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var domain = body.GetProperty("email_domain").GetString() ?? "";
            var providerId = body.GetProperty("provider_id").GetInt32();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO idp_domain_mappings (email_domain, provider_id) VALUES (@d, @pid)
                  ON CONFLICT (email_domain) DO UPDATE SET provider_id=@pid", conn);
            cmd.Parameters.AddWithValue("d", domain.ToLowerInvariant());
            cmd.Parameters.AddWithValue("pid", providerId);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        // ── Claim Mappings ──

        group.MapGet("/claim-mappings", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT id, provider_id, claim_type, claim_value, target_role, priority, is_enabled FROM claim_mappings ORDER BY priority", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPut("/claim-mappings", async (DbConnectionFactory db, JsonElement body) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var id = body.TryGetProperty("id", out var idp) ? idp.GetInt32() : 0;
            var providerId = body.TryGetProperty("provider_id", out var pid) ? pid.GetInt32() : (int?)null;
            var claimType = body.GetProperty("claim_type").GetString() ?? "";
            var claimValue = body.GetProperty("claim_value").GetString() ?? "";
            var targetRole = body.GetProperty("target_role").GetString() ?? "Viewer";
            var priority = body.TryGetProperty("priority", out var pri) ? pri.GetInt32() : 100;

            await using var cmd = new NpgsqlCommand(id > 0
                ? "UPDATE claim_mappings SET provider_id=@pid, claim_type=@ct, claim_value=@cv, target_role=@tr, priority=@pri WHERE id=@id"
                : "INSERT INTO claim_mappings (provider_id, claim_type, claim_value, target_role, priority) VALUES (@pid, @ct, @cv, @tr, @pri) RETURNING id", conn);
            if (id > 0) cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("pid", (object?)providerId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ct", claimType);
            cmd.Parameters.AddWithValue("cv", claimValue);
            cmd.Parameters.AddWithValue("tr", targetRole);
            cmd.Parameters.AddWithValue("pri", priority);
            if (id == 0) id = (int)(await cmd.ExecuteScalarAsync())!;
            else await cmd.ExecuteNonQueryAsync();
            return Results.Ok(new { id });
        });

        // ── Auth Events ──

        group.MapGet("/auth-events", async (DbConnectionFactory db, int? limit) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                $"SELECT id, timestamp, event_type, provider_type, username, user_id, success, error_message FROM auth_events ORDER BY timestamp DESC LIMIT {Math.Min(limit ?? 200, 1000)}", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}
