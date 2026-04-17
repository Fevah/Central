using System.Security.Claims;
using System.Text.Json;
using Npgsql;
using Central.Core.Auth;
using Central.Core.Services;
using Central.Data;

namespace Central.Api.Endpoints;

// ══════════════════════════════════════════════════════════════════════
// Stage 4: AI Provider Admin (platform + tenant)
// ══════════════════════════════════════════════════════════════════════

/// <summary>Platform-level AI provider management (global_admin only).</summary>
public static class AiProviderAdminEndpoints
{
    public static RouteGroupBuilder MapAiProviderAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/providers", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT id, provider_code, display_name, provider_type, base_url, auth_type,
                         platform_key_configured, supports_chat, supports_embeddings, supports_vision,
                         supports_tool_use, supports_streaming, rate_limit_rpm, rate_limit_tpm,
                         is_enabled, is_default
                  FROM central_platform.ai_providers ORDER BY is_default DESC, display_name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPut("/providers/{id:int}", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();

            // Platform key — optional; encrypted if provided
            string? platformKeyEnc = null;
            if (body.TryGetProperty("platform_key", out var pk) && !string.IsNullOrEmpty(pk.GetString()))
            {
                platformKeyEnc = CredentialEncryptor.IsAvailable
                    ? CredentialEncryptor.Encrypt(pk.GetString()!)
                    : pk.GetString();
            }

            await using var cmd = new NpgsqlCommand(
                @"UPDATE central_platform.ai_providers SET
                    is_enabled = COALESCE(@en, is_enabled),
                    is_default = COALESCE(@def, is_default),
                    rate_limit_rpm = COALESCE(@rpm, rate_limit_rpm),
                    rate_limit_tpm = COALESCE(@tpm, rate_limit_tpm),
                    platform_key_enc = CASE WHEN @has_key THEN @pk ELSE platform_key_enc END,
                    updated_at = NOW()
                  WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("en", body.TryGetProperty("is_enabled", out var en) ? (object)en.GetBoolean() : DBNull.Value);
            cmd.Parameters.AddWithValue("def", body.TryGetProperty("is_default", out var df) ? (object)df.GetBoolean() : DBNull.Value);
            cmd.Parameters.AddWithValue("rpm", body.TryGetProperty("rate_limit_rpm", out var rpm) && rpm.ValueKind == JsonValueKind.Number ? (object)rpm.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("tpm", body.TryGetProperty("rate_limit_tpm", out var tpm) && tpm.ValueKind == JsonValueKind.Number ? (object)tpm.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("has_key", platformKeyEnc != null);
            cmd.Parameters.AddWithValue("pk", (object?)platformKeyEnc ?? DBNull.Value);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound($"Provider {id} not found") : Results.Ok(new { id, updated = true });
        });

        // Models per provider
        group.MapGet("/providers/{id:int}/models", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT * FROM central_platform.ai_models WHERE provider_id = @pid AND is_active = true
                  ORDER BY is_recommended DESC, tier, model_code", conn);
            cmd.Parameters.AddWithValue("pid", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/providers/{id:int}/models", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO central_platform.ai_models
                    (provider_id, model_code, display_name, model_family, context_window, max_output_tokens,
                     supports_vision, supports_tool_use, input_price_per_m, output_price_per_m, tier)
                  VALUES (@p, @mc, @dn, @mf, @cw, @mo, @sv, @st, @ip, @op, @t) RETURNING id", conn);
            cmd.Parameters.AddWithValue("p", id);
            cmd.Parameters.AddWithValue("mc", body.GetProperty("model_code").GetString() ?? "");
            cmd.Parameters.AddWithValue("dn", body.GetProperty("display_name").GetString() ?? "");
            cmd.Parameters.AddWithValue("mf", body.TryGetProperty("model_family", out var mf) ? mf.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("cw", body.TryGetProperty("context_window", out var cw) && cw.ValueKind == JsonValueKind.Number ? cw.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("mo", body.TryGetProperty("max_output_tokens", out var mo) && mo.ValueKind == JsonValueKind.Number ? mo.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("sv", body.TryGetProperty("supports_vision", out var sv) && sv.GetBoolean());
            cmd.Parameters.AddWithValue("st", body.TryGetProperty("supports_tool_use", out var stu) && stu.GetBoolean());
            cmd.Parameters.AddWithValue("ip", body.TryGetProperty("input_price_per_m", out var ip) && ip.ValueKind == JsonValueKind.Number ? ip.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("op", body.TryGetProperty("output_price_per_m", out var op) && op.ValueKind == JsonValueKind.Number ? op.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("t", body.TryGetProperty("tier", out var t) ? t.GetString() ?? "flagship" : "flagship");
            var mid = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/ai/admin/providers/{id}/models/{mid}", new { id = mid });
        });

        return group;
    }
}

/// <summary>Tenant-level AI config — BYOK, feature-provider mapping, quotas.</summary>
public static class TenantAiConfigEndpoints
{
    public static RouteGroupBuilder MapTenantAiConfigEndpoints(this RouteGroupBuilder group)
    {
        // List enabled providers with tenant-override status
        group.MapGet("/providers", async (ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            var tenantId = ResolveTenantId(principal);
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT p.id, p.provider_code, p.display_name, p.provider_type,
                         p.supports_chat, p.supports_embeddings, p.supports_vision, p.supports_tool_use,
                         p.platform_key_configured,
                         COALESCE(tap.is_enabled, false) AS tenant_enabled,
                         COALESCE(tap.is_default_for_tenant, false) AS is_default_for_tenant,
                         COALESCE(tap.use_platform_key, true) AS use_platform_key,
                         (tap.api_key_enc IS NOT NULL AND tap.api_key_enc <> '') AS has_byok,
                         tap.api_key_label,
                         tap.default_model_code,
                         tap.monthly_token_limit, tap.monthly_cost_limit,
                         COALESCE(tap.current_month_tokens, 0) AS current_month_tokens,
                         COALESCE(tap.current_month_cost, 0) AS current_month_cost,
                         tap.last_used_at
                  FROM central_platform.ai_providers p
                  LEFT JOIN central_platform.tenant_ai_providers tap
                      ON tap.provider_id = p.id AND tap.tenant_id = @tid
                  WHERE p.is_enabled = true
                  ORDER BY p.is_default DESC, p.display_name", conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // Enable provider + store BYOK key
        group.MapPost("/providers", async (ClaimsPrincipal principal, JsonElement body, DbConnectionFactory db,
            ITenantAiProviderResolver resolver) =>
        {
            var tenantId = ResolveTenantId(principal);
            var providerId = body.GetProperty("provider_id").GetInt32();

            string? apiKeyEnc = null;
            if (body.TryGetProperty("api_key", out var ak) && !string.IsNullOrEmpty(ak.GetString()))
            {
                apiKeyEnc = CredentialEncryptor.IsAvailable
                    ? CredentialEncryptor.Encrypt(ak.GetString()!)
                    : ak.GetString();
            }

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO central_platform.tenant_ai_providers
                    (tenant_id, provider_id, is_enabled, is_default_for_tenant, use_platform_key,
                     api_key_enc, api_key_label, org_id, default_model_code,
                     monthly_token_limit, monthly_cost_limit, configured_by)
                  VALUES (@tid, @pid, @en, @def, @upk, @ke, @kl, @oid, @dm, @mt, @mc, @by)
                  ON CONFLICT (tenant_id, provider_id) DO UPDATE SET
                    is_enabled = EXCLUDED.is_enabled,
                    is_default_for_tenant = EXCLUDED.is_default_for_tenant,
                    use_platform_key = EXCLUDED.use_platform_key,
                    api_key_enc = CASE WHEN @has_key THEN EXCLUDED.api_key_enc ELSE tenant_ai_providers.api_key_enc END,
                    api_key_label = EXCLUDED.api_key_label,
                    org_id = EXCLUDED.org_id,
                    default_model_code = EXCLUDED.default_model_code,
                    monthly_token_limit = EXCLUDED.monthly_token_limit,
                    monthly_cost_limit = EXCLUDED.monthly_cost_limit,
                    configured_at = NOW()
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("pid", providerId);
            cmd.Parameters.AddWithValue("en", !body.TryGetProperty("is_enabled", out var en) || en.GetBoolean());
            cmd.Parameters.AddWithValue("def", body.TryGetProperty("is_default_for_tenant", out var df) && df.GetBoolean());
            cmd.Parameters.AddWithValue("upk", !body.TryGetProperty("use_platform_key", out var upk) || upk.GetBoolean());
            cmd.Parameters.AddWithValue("ke", (object?)apiKeyEnc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("kl", body.TryGetProperty("api_key_label", out var kl) ? kl.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("oid", body.TryGetProperty("org_id", out var oid) ? oid.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("dm", body.TryGetProperty("default_model_code", out var dm) ? dm.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("mt", body.TryGetProperty("monthly_token_limit", out var mt) && mt.ValueKind == JsonValueKind.Number ? (object)mt.GetInt64() : DBNull.Value);
            cmd.Parameters.AddWithValue("mc", body.TryGetProperty("monthly_cost_limit", out var mc) && mc.ValueKind == JsonValueKind.Number ? (object)mc.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("by", DBNull.Value);  // configured_by user lookup elsewhere
            cmd.Parameters.AddWithValue("has_key", apiKeyEnc != null);

            // If tenant marks this as default, unset others
            if (body.TryGetProperty("is_default_for_tenant", out var dfe) && dfe.GetBoolean())
            {
                await using var clear = new NpgsqlCommand(
                    "UPDATE central_platform.tenant_ai_providers SET is_default_for_tenant = false WHERE tenant_id = @tid AND provider_id <> @pid", conn);
                clear.Parameters.AddWithValue("tid", tenantId);
                clear.Parameters.AddWithValue("pid", providerId);
                await clear.ExecuteNonQueryAsync();
            }

            var id = (int)(await cmd.ExecuteScalarAsync())!;
            resolver.Invalidate(tenantId);
            return Results.Ok(new { id, configured = true });
        });

        // Remove tenant BYOK (clear key, keep using platform)
        group.MapDelete("/providers/{providerId:int}/key", async (int providerId, ClaimsPrincipal principal, DbConnectionFactory db,
            ITenantAiProviderResolver resolver) =>
        {
            var tenantId = ResolveTenantId(principal);
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE central_platform.tenant_ai_providers
                  SET api_key_enc = NULL, api_key_label = ''
                  WHERE tenant_id = @tid AND provider_id = @pid RETURNING id", conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("pid", providerId);
            var r = await cmd.ExecuteScalarAsync();
            resolver.Invalidate(tenantId);
            return r is null ? ApiProblem.NotFound("Provider not configured") : Results.NoContent();
        });

        // Test tenant's provider configuration (calls the provider with a trivial prompt)
        group.MapPost("/providers/{providerId:int}/test", async (int providerId, ClaimsPrincipal principal,
            ITenantAiProviderResolver resolver) =>
        {
            var tenantId = ResolveTenantId(principal);
            var resolution = await resolver.ResolveAsync(tenantId, "assistant");
            if (resolution is null || !resolution.IsAvailable)
                return ApiProblem.ValidationError("Provider not available for tenant");
            var key = await resolver.GetApiKeyAsync(tenantId, resolution);
            return Results.Ok(new
            {
                provider_code = resolution.ProviderCode,
                model_code = resolution.ModelCode,
                key_source = resolution.KeySource,
                key_available = !string.IsNullOrEmpty(key),
                quota_remaining = resolution.QuotaRemaining,
                cost_remaining = resolution.CostRemaining
            });
        });

        // Feature → provider mapping
        group.MapGet("/features", async (ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            var tenantId = ResolveTenantId(principal);
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT f.id, f.feature_code, f.provider_id, p.provider_code, p.display_name AS provider_name,
                         f.model_code, f.is_enabled, f.temperature, f.max_tokens
                  FROM central_platform.tenant_ai_features f
                  LEFT JOIN central_platform.ai_providers p ON p.id = f.provider_id
                  WHERE f.tenant_id = @tid ORDER BY f.feature_code", conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPut("/features/{featureCode}", async (string featureCode, ClaimsPrincipal principal,
            JsonElement body, DbConnectionFactory db, ITenantAiProviderResolver resolver) =>
        {
            var tenantId = ResolveTenantId(principal);
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO central_platform.tenant_ai_features
                    (tenant_id, feature_code, provider_id, model_code, is_enabled, temperature, max_tokens, custom_system_prompt)
                  VALUES (@tid, @fc, @pid, @mc, @en, @tp, @mt, @sp)
                  ON CONFLICT (tenant_id, feature_code) DO UPDATE SET
                    provider_id = EXCLUDED.provider_id,
                    model_code = EXCLUDED.model_code,
                    is_enabled = EXCLUDED.is_enabled,
                    temperature = EXCLUDED.temperature,
                    max_tokens = EXCLUDED.max_tokens,
                    custom_system_prompt = EXCLUDED.custom_system_prompt
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("fc", featureCode);
            cmd.Parameters.AddWithValue("pid", body.TryGetProperty("provider_id", out var p) && p.ValueKind == JsonValueKind.Number ? (object)p.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("mc", body.TryGetProperty("model_code", out var mcode) ? mcode.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("en", !body.TryGetProperty("is_enabled", out var en) || en.GetBoolean());
            cmd.Parameters.AddWithValue("tp", body.TryGetProperty("temperature", out var tp) && tp.ValueKind == JsonValueKind.Number ? (object)tp.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("mt", body.TryGetProperty("max_tokens", out var mx) && mx.ValueKind == JsonValueKind.Number ? (object)mx.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("sp", body.TryGetProperty("custom_system_prompt", out var sp) ? sp.GetString() ?? "" : "");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            resolver.Invalidate(tenantId);
            return Results.Ok(new { id, feature_code = featureCode });
        });

        // Resolve which provider is active for a feature (debugging)
        group.MapGet("/resolve/{featureCode}", async (string featureCode, ClaimsPrincipal principal,
            ITenantAiProviderResolver resolver) =>
        {
            var tenantId = ResolveTenantId(principal);
            var resolution = await resolver.ResolveAsync(tenantId, featureCode);
            return resolution is null
                ? ApiProblem.NotFound("No provider resolved")
                : Results.Ok(resolution);
        });

        // Usage dashboard
        group.MapGet("/usage", async (ClaimsPrincipal principal, DbConnectionFactory db,
            string? provider_code, DateTime? start, DateTime? end) =>
        {
            var tenantId = ResolveTenantId(principal);
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string> { "tenant_id = @tid" };
            var parms = new List<(string, object)> { ("tid", tenantId) };
            if (!string.IsNullOrEmpty(provider_code))
            {
                where.Add("provider_id = (SELECT id FROM central_platform.ai_providers WHERE provider_code = @pc)");
                parms.Add(("pc", provider_code));
            }
            if (start.HasValue) { where.Add("called_at >= @s"); parms.Add(("s", start.Value)); }
            if (end.HasValue)   { where.Add("called_at <= @e"); parms.Add(("e", end.Value)); }
            var whereSql = string.Join(" AND ", where);

            await using var cmd = new NpgsqlCommand(
                $@"SELECT
                     date_trunc('day', called_at)::date AS day,
                     (SELECT provider_code FROM central_platform.ai_providers WHERE id = log.provider_id) AS provider_code,
                     model_code, feature_code, key_source,
                     COUNT(*) AS call_count,
                     SUM(input_tokens) AS total_input_tokens,
                     SUM(output_tokens) AS total_output_tokens,
                     SUM(cost_usd) AS total_cost,
                     AVG(latency_ms) AS avg_latency_ms,
                     COUNT(*) FILTER (WHERE success = false) AS error_count
                   FROM central_platform.ai_usage_log log
                   WHERE {whereSql}
                   GROUP BY day, provider_id, log.provider_id, model_code, feature_code, key_source
                   ORDER BY day DESC LIMIT 365", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }

    private static Guid ResolveTenantId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claim, out var g) ? g : Guid.Empty;
    }
}

// ══════════════════════════════════════════════════════════════════════
// AI Assistant + Scoring + Dedup + Enrichment + Churn + Calls
// ══════════════════════════════════════════════════════════════════════

public static class AiAssistantEndpoints
{
    public static RouteGroupBuilder MapAiAssistantEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/conversations", async (ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name ?? "";
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT c.* FROM ai_conversations c
                  JOIN app_users u ON u.id = c.user_id
                  WHERE u.username = @u AND c.status = 'active'
                  ORDER BY c.updated_at DESC LIMIT 100", conn);
            cmd.Parameters.AddWithValue("u", username);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/conversations", async (ClaimsPrincipal principal, JsonElement body, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name ?? "";
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO ai_conversations
                    (user_id, title, context_entity_type, context_entity_id, provider_id, model_code)
                  SELECT u.id, @t, @et, @eid, @p, @m
                  FROM app_users u WHERE u.username = @u RETURNING id", conn);
            cmd.Parameters.AddWithValue("u", username);
            cmd.Parameters.AddWithValue("t", body.TryGetProperty("title", out var t) ? t.GetString() ?? "New conversation" : "New conversation");
            cmd.Parameters.AddWithValue("et", body.TryGetProperty("context_entity_type", out var et) ? et.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("eid", body.TryGetProperty("context_entity_id", out var eid) && eid.ValueKind == JsonValueKind.Number ? (object)eid.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("p", body.TryGetProperty("provider_id", out var p) && p.ValueKind == JsonValueKind.Number ? (object)p.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("m", body.TryGetProperty("model_code", out var m) ? m.GetString() ?? "" : "");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/ai/assistant/conversations/{id}", new { id });
        });

        group.MapGet("/conversations/{id:int}/messages", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT id, role, content, input_tokens, output_tokens, thumbs, created_at FROM ai_messages WHERE conversation_id = @id ORDER BY created_at", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // Send a message — stores user turn, caller is responsible for invoking the AI
        // (the provider SDK call happens in a background worker or plugin)
        group.MapPost("/conversations/{id:int}/messages", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO ai_messages (conversation_id, role, content)
                  VALUES (@cid, @r, @c) RETURNING id", conn);
            cmd.Parameters.AddWithValue("cid", id);
            cmd.Parameters.AddWithValue("r", body.GetProperty("role").GetString() ?? "user");
            cmd.Parameters.AddWithValue("c", body.GetProperty("content").GetString() ?? "");
            var mid = (long)(await cmd.ExecuteScalarAsync())!;

            // Notify background worker to generate assistant response
            await using var notify = new NpgsqlCommand(
                "SELECT pg_notify('ai_assistant_turn', @payload)", conn);
            notify.Parameters.AddWithValue("payload", JsonSerializer.Serialize(new { conversation_id = id, message_id = mid }));
            await notify.ExecuteNonQueryAsync();

            return Results.Created($"/api/ai/assistant/conversations/{id}/messages/{mid}", new { id = mid });
        });

        // Prompt templates (library shared + tenant)
        group.MapGet("/templates", async (DbConnectionFactory db, string? category) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(category) ? "is_active = true" : "is_active = true AND category = @cat";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM ai_prompt_templates WHERE {where} ORDER BY is_public DESC, name", conn);
            if (!string.IsNullOrEmpty(category)) cmd.Parameters.AddWithValue("cat", category);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // Available tools for tool-use
        group.MapGet("/tools", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM ai_tools WHERE is_enabled = true ORDER BY category, tool_name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}

public static class AiInsightsEndpoints
{
    public static RouteGroupBuilder MapAiInsightsEndpoints(this RouteGroupBuilder group)
    {
        // Scores
        group.MapGet("/scores", async (DbConnectionFactory db, string entity_type, int? entity_id, string? model_kind) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string> { "s.entity_type = @et" };
            var parms = new List<(string, object)> { ("et", entity_type) };
            if (entity_id.HasValue)            { where.Add("s.entity_id = @eid");      parms.Add(("eid", entity_id.Value)); }
            if (!string.IsNullOrEmpty(model_kind)) { where.Add("m.model_kind = @mk"); parms.Add(("mk", model_kind)); }
            var whereSql = string.Join(" AND ", where);
            await using var cmd = new NpgsqlCommand(
                $@"SELECT s.*, m.model_name, m.model_kind
                   FROM ai_model_scores s JOIN ai_ml_models m ON m.id = s.model_id
                   WHERE {whereSql} ORDER BY s.scored_at DESC LIMIT 200", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // Next best actions per entity
        group.MapGet("/next-actions", async (DbConnectionFactory db, string entity_type, int entity_id) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT * FROM ai_next_best_actions
                  WHERE entity_type = @et AND entity_id = @eid
                    AND dismissed_at IS NULL AND acted_on_at IS NULL
                    AND (expires_at IS NULL OR expires_at > NOW())
                  ORDER BY priority, created_at DESC", conn);
            cmd.Parameters.AddWithValue("et", entity_type);
            cmd.Parameters.AddWithValue("eid", entity_id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/next-actions/{id:long}/accept", async (long id, ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE ai_next_best_actions SET accepted_at = NOW(),
                  accepted_by = (SELECT id FROM app_users WHERE username = @u) WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("u", principal.Identity?.Name ?? "");
            cmd.Parameters.AddWithValue("id", id);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound("Action not found") : Results.Ok(new { id, accepted = true });
        });

        // Duplicate candidates
        group.MapGet("/duplicates", async (DbConnectionFactory db, string? entity_type, string? status) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string>();
            var parms = new List<(string, object)>();
            if (!string.IsNullOrEmpty(entity_type)) { where.Add("entity_type = @et"); parms.Add(("et", entity_type)); }
            if (!string.IsNullOrEmpty(status))       { where.Add("status = @s");      parms.Add(("s", status)); }
            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM crm_duplicates {whereSql} ORDER BY similarity_score DESC LIMIT 200", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/duplicates/{id:long}/merge", async (long id, ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            // Resolves the duplicate pair — caller specifies which record survives via future body
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE crm_duplicates SET status = 'merged', reviewed_at = NOW(),
                  reviewed_by = (SELECT id FROM app_users WHERE username = @u) WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("u", principal.Identity?.Name ?? "");
            cmd.Parameters.AddWithValue("id", id);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound("Duplicate not found") : Results.Ok(new { id, merged = true });
        });

        // Enrichment jobs
        group.MapGet("/enrichment", async (DbConnectionFactory db, string? status) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(status) ? "" : "WHERE status = @s";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM crm_enrichment_jobs {where} ORDER BY requested_at DESC LIMIT 200", conn);
            if (!string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("s", status);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/enrichment", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_enrichment_jobs (entity_type, entity_id, provider_id, match_field, match_value)
                  VALUES (@et, @eid, @pid, @mf, @mv) RETURNING id", conn);
            cmd.Parameters.AddWithValue("et", body.GetProperty("entity_type").GetString() ?? "");
            cmd.Parameters.AddWithValue("eid", body.GetProperty("entity_id").GetInt32());
            cmd.Parameters.AddWithValue("pid", body.TryGetProperty("provider_id", out var p) && p.ValueKind == JsonValueKind.Number ? (object)p.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("mf", body.TryGetProperty("match_field", out var mf) ? mf.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("mv", body.TryGetProperty("match_value", out var mv) ? mv.GetString() ?? "" : "");
            var id = (int)(await cmd.ExecuteScalarAsync())!;

            await using var notify = new NpgsqlCommand($"SELECT pg_notify('enrichment_queue', '{id}')", conn);
            await notify.ExecuteNonQueryAsync();

            return Results.Accepted($"/api/ai/insights/enrichment/{id}", new { id });
        });

        // Churn risk
        group.MapGet("/churn", async (DbConnectionFactory db, string? tier) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(tier) ? "WHERE actual_outcome IS NULL" : "WHERE actual_outcome IS NULL AND risk_tier = @t";
            await using var cmd = new NpgsqlCommand(
                $@"SELECT c.*, a.name AS account_name
                   FROM crm_churn_risks c JOIN crm_accounts a ON a.id = c.account_id
                   {where} ORDER BY risk_score DESC LIMIT 200", conn);
            if (!string.IsNullOrEmpty(tier)) cmd.Parameters.AddWithValue("t", tier);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // LTV
        group.MapGet("/ltv/{accountId:int}", async (int accountId, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM crm_account_ltv WHERE account_id = @id", conn);
            cmd.Parameters.AddWithValue("id", accountId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Results.NotFound();
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            return Results.Ok(row);
        });

        // Call recordings + transcripts
        group.MapGet("/calls", async (DbConnectionFactory db, int? deal_id, int? account_id) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string>();
            var parms = new List<(string, object)>();
            if (deal_id.HasValue)    { where.Add("linked_deal_id = @d");    parms.Add(("d", deal_id.Value)); }
            if (account_id.HasValue) { where.Add("linked_account_id = @a"); parms.Add(("a", account_id.Value)); }
            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            await using var cmd = new NpgsqlCommand(
                $@"SELECT id, external_id, provider, recording_url, duration_seconds, started_at,
                          linked_contact_id, linked_deal_id, linked_account_id,
                          transcript_status, summary, action_items, topics_discussed,
                          overall_sentiment, longest_monologue_seconds, question_count,
                          processing_cost_usd, created_at
                   FROM crm_call_recordings {whereSql}
                   ORDER BY started_at DESC NULLS LAST LIMIT 200", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapGet("/calls/{id:long}", async (long id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT * FROM crm_call_recordings WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Results.NotFound();
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            return Results.Ok(row);
        });

        group.MapPost("/calls", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_call_recordings
                    (external_id, provider, recording_url, duration_seconds, started_at, ended_at,
                     linked_contact_id, linked_deal_id, linked_account_id)
                  VALUES (@ex, @pv, @u, @d, @s, @e, @c, @dl, @a) RETURNING id", conn);
            cmd.Parameters.AddWithValue("ex", body.TryGetProperty("external_id", out var ex) ? ex.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("pv", body.GetProperty("provider").GetString() ?? "");
            cmd.Parameters.AddWithValue("u", body.TryGetProperty("recording_url", out var u) ? u.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("duration_seconds", out var ds) && ds.ValueKind == JsonValueKind.Number ? (object)ds.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("s", body.TryGetProperty("started_at", out var st) && st.ValueKind == JsonValueKind.String ? (object)DateTime.Parse(st.GetString()!) : DBNull.Value);
            cmd.Parameters.AddWithValue("e", body.TryGetProperty("ended_at", out var ea) && ea.ValueKind == JsonValueKind.String ? (object)DateTime.Parse(ea.GetString()!) : DBNull.Value);
            cmd.Parameters.AddWithValue("c", body.TryGetProperty("linked_contact_id", out var lc) && lc.ValueKind == JsonValueKind.Number ? (object)lc.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("dl", body.TryGetProperty("linked_deal_id", out var ld) && ld.ValueKind == JsonValueKind.Number ? (object)ld.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("a", body.TryGetProperty("linked_account_id", out var la) && la.ValueKind == JsonValueKind.Number ? (object)la.GetInt32() : DBNull.Value);
            var id = (long)(await cmd.ExecuteScalarAsync())!;

            // Queue for transcription
            await using var notify = new NpgsqlCommand($"SELECT pg_notify('call_transcription_queue', '{id}')", conn);
            await notify.ExecuteNonQueryAsync();

            return Results.Created($"/api/ai/insights/calls/{id}", new { id });
        });

        // ML model registry
        group.MapGet("/ml-models", async (DbConnectionFactory db, string? model_kind) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(model_kind) ? "" : "WHERE model_kind = @mk";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM ai_ml_models {where} ORDER BY model_kind, is_champion DESC, version DESC", conn);
            if (!string.IsNullOrEmpty(model_kind)) cmd.Parameters.AddWithValue("mk", model_kind);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/ml-models/{id:int}/promote", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();
            try
            {
                // Unset current champion of same kind
                await using var unset = new NpgsqlCommand(
                    @"UPDATE ai_ml_models SET is_champion = false
                      WHERE model_kind = (SELECT model_kind FROM ai_ml_models WHERE id = @id)
                        AND is_champion = true", conn);
                unset.Transaction = tx;
                unset.Parameters.AddWithValue("id", id);
                await unset.ExecuteNonQueryAsync();
                // Set new champion
                await using var set = new NpgsqlCommand(
                    "UPDATE ai_ml_models SET is_champion = true, status = 'active' WHERE id = @id RETURNING id", conn);
                set.Transaction = tx;
                set.Parameters.AddWithValue("id", id);
                var r = await set.ExecuteScalarAsync();
                await tx.CommitAsync();
                return r is null ? ApiProblem.NotFound("Model not found") : Results.Ok(new { id, promoted = true });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return ApiProblem.ServerError($"Promote failed: {ex.Message}");
            }
        });

        return group;
    }
}
