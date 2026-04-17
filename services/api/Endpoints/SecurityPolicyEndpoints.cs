using System.Text.Json;
using Central.Data;
using Central.Security;
using Central.Tenancy;
using Npgsql;

namespace Central.Api.Endpoints;

/// <summary>
/// CRUD for ABAC security policies. Admin-only.
/// Policies control row-level access and field-level visibility per entity type.
/// </summary>
public static class SecurityPolicyEndpoints
{
    public static RouteGroupBuilder MapSecurityPolicyEndpoints(this RouteGroupBuilder group)
    {
        // GET / — list all policies for the current tenant
        group.MapGet("/", async (DbConnectionFactory db, TenantContext tenant) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT id, entity_type, policy_type, effect, conditions::text, hidden_fields,
                         priority, is_enabled, description, created_at
                  FROM central_platform.security_policies
                  WHERE tenant_id = @tid ORDER BY priority", conn);
            cmd.Parameters.AddWithValue("tid", tenant.TenantId);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // POST / — create a policy
        group.MapPost("/", async (DbConnectionFactory db, TenantContext tenant,
            SecurityPolicyEngine engine, JsonElement body) =>
        {
            var entityType = body.GetProperty("entity_type").GetString() ?? "";
            var policyType = body.TryGetProperty("policy_type", out var pt) ? pt.GetString() ?? "row" : "row";
            var effect = body.TryGetProperty("effect", out var ef) ? ef.GetString() ?? "allow" : "allow";
            var conditions = body.TryGetProperty("conditions", out var cond) ? cond.GetRawText() : "{}";
            var hiddenFields = body.TryGetProperty("hidden_fields", out var hf)
                ? hf.EnumerateArray().Select(e => e.GetString() ?? "").ToArray()
                : Array.Empty<string>();
            var priority = body.TryGetProperty("priority", out var pri) ? pri.GetInt32() : 100;
            var description = body.TryGetProperty("description", out var desc) ? desc.GetString() : null;

            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO central_platform.security_policies
                  (tenant_id, entity_type, policy_type, effect, conditions, hidden_fields, priority, description)
                  VALUES (@tid, @et, @pt, @eff, @cond::jsonb, @hf, @pri, @desc)
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("tid", tenant.TenantId);
            cmd.Parameters.AddWithValue("et", entityType);
            cmd.Parameters.AddWithValue("pt", policyType);
            cmd.Parameters.AddWithValue("eff", effect);
            cmd.Parameters.AddWithValue("cond", conditions);
            cmd.Parameters.AddWithValue("hf", hiddenFields);
            cmd.Parameters.AddWithValue("pri", priority);
            cmd.Parameters.AddWithValue("desc", (object?)description ?? DBNull.Value);
            var id = await cmd.ExecuteScalarAsync();

            // Refresh in-memory cache
            await RefreshPolicyCacheAsync(db, tenant, engine);

            return Results.Ok(new { id, entity_type = entityType, policy_type = policyType });
        });

        // PUT /{id} — update a policy
        group.MapPut("/{id:int}", async (int id, DbConnectionFactory db, TenantContext tenant,
            SecurityPolicyEngine engine, JsonElement body) =>
        {
            var entityType = body.GetProperty("entity_type").GetString() ?? "";
            var policyType = body.TryGetProperty("policy_type", out var pt) ? pt.GetString() ?? "row" : "row";
            var effect = body.TryGetProperty("effect", out var ef) ? ef.GetString() ?? "allow" : "allow";
            var conditions = body.TryGetProperty("conditions", out var cond) ? cond.GetRawText() : "{}";
            var hiddenFields = body.TryGetProperty("hidden_fields", out var hf)
                ? hf.EnumerateArray().Select(e => e.GetString() ?? "").ToArray()
                : Array.Empty<string>();
            var priority = body.TryGetProperty("priority", out var pri) ? pri.GetInt32() : 100;
            var isEnabled = body.TryGetProperty("is_enabled", out var en) ? en.GetBoolean() : true;
            var description = body.TryGetProperty("description", out var desc) ? desc.GetString() : null;

            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE central_platform.security_policies
                  SET entity_type=@et, policy_type=@pt, effect=@eff, conditions=@cond::jsonb,
                      hidden_fields=@hf, priority=@pri, is_enabled=@en, description=@desc
                  WHERE id=@id AND tenant_id=@tid", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("tid", tenant.TenantId);
            cmd.Parameters.AddWithValue("et", entityType);
            cmd.Parameters.AddWithValue("pt", policyType);
            cmd.Parameters.AddWithValue("eff", effect);
            cmd.Parameters.AddWithValue("cond", conditions);
            cmd.Parameters.AddWithValue("hf", hiddenFields);
            cmd.Parameters.AddWithValue("pri", priority);
            cmd.Parameters.AddWithValue("en", isEnabled);
            cmd.Parameters.AddWithValue("desc", (object?)description ?? DBNull.Value);
            var rows = await cmd.ExecuteNonQueryAsync();

            await RefreshPolicyCacheAsync(db, tenant, engine);

            return rows > 0 ? Results.Ok(new { updated = true }) : Results.NotFound();
        });

        // DELETE /{id}
        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db, TenantContext tenant,
            SecurityPolicyEngine engine) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "DELETE FROM central_platform.security_policies WHERE id=@id AND tenant_id=@tid", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("tid", tenant.TenantId);
            var rows = await cmd.ExecuteNonQueryAsync();

            await RefreshPolicyCacheAsync(db, tenant, engine);

            return rows > 0 ? Results.Ok(new { deleted = true }) : Results.NotFound();
        });

        // POST /evaluate — test a policy against sample data
        group.MapPost("/evaluate", (SecurityPolicyEngine engine, TenantContext tenant, JsonElement body) =>
        {
            var entityType = body.GetProperty("entity_type").GetString() ?? "";
            var userCtx = new SecurityContext
            {
                Username = body.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "",
                Role = body.TryGetProperty("role", out var r) ? r.GetString() ?? "" : "",
                Department = body.TryGetProperty("department", out var d) ? d.GetString() ?? "" : "",
                SecurityClearance = body.TryGetProperty("security_clearance", out var sc) ? sc.GetString() ?? "internal" : "internal"
            };
            var record = body.TryGetProperty("record", out var rec)
                ? JsonSerializer.Deserialize<Dictionary<string, object?>>(rec.GetRawText()) ?? new()
                : new Dictionary<string, object?>();

            var canAccess = engine.CanAccessRow(tenant.TenantSlug, entityType, userCtx, record);
            var hiddenFields = engine.GetHiddenFields(tenant.TenantSlug, entityType, userCtx);

            return Results.Ok(new
            {
                can_access = canAccess,
                hidden_fields = hiddenFields.ToArray()
            });
        });

        return group;
    }

    private static async Task RefreshPolicyCacheAsync(DbConnectionFactory db, TenantContext tenant, SecurityPolicyEngine engine)
    {
        await using var conn = new NpgsqlConnection(db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, entity_type, policy_type, effect, conditions::text, hidden_fields, priority, is_enabled
              FROM central_platform.security_policies WHERE tenant_id = @tid", conn);
        cmd.Parameters.AddWithValue("tid", tenant.TenantId);
        await using var r = await cmd.ExecuteReaderAsync();
        var policies = new List<SecurityPolicy>();
        while (await r.ReadAsync())
        {
            policies.Add(new SecurityPolicy
            {
                Id = r.GetInt32(0),
                EntityType = r.GetString(1),
                PolicyType = r.GetString(2),
                Effect = r.GetString(3),
                Conditions = r.IsDBNull(4) ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(4)),
                HiddenFields = r.IsDBNull(5) ? null : ((string[])r.GetValue(5)),
                Priority = r.GetInt32(6),
                IsEnabled = r.GetBoolean(7)
            });
        }
        engine.LoadPolicies(tenant.TenantSlug, policies);
    }
}
