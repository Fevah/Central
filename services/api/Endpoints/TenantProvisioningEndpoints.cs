using System.Text.Json;
using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

/// <summary>
/// Global Admin endpoints for tenant sizing / provisioning.
/// Companion to the Rust tenant-provisioner service — these endpoints queue jobs
/// and report status; the worker executes them.
/// </summary>
public static class TenantProvisioningEndpoints
{
    public static RouteGroupBuilder MapTenantProvisioningEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/global-admin/tenants/{id}/sizing — current sizing + recent jobs
        group.MapGet("/tenants/{id:guid}/sizing", async (Guid id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT t.sizing_model, t.provisioning_status, t.provisioning_error,
                       m.database_name, m.schema_name, m.k8s_namespace
                FROM central_platform.tenants t
                LEFT JOIN central_platform.tenant_connection_map m ON m.tenant_id = t.id
                WHERE t.id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return ApiProblem.NotFound($"Tenant {id} not found");

            var info = new
            {
                sizing_model = r.GetString(0),
                provisioning_status = r.GetString(1),
                provisioning_error = r.IsDBNull(2) ? null : r.GetString(2),
                database_name = r.IsDBNull(3) ? null : r.GetString(3),
                schema_name = r.IsDBNull(4) ? null : r.GetString(4),
                k8s_namespace = r.IsDBNull(5) ? null : r.GetString(5)
            };
            await r.CloseAsync();

            // Recent jobs
            await using var jobsCmd = new NpgsqlCommand(@"
                SELECT id, job_type, status, started_at, completed_at, error_message, retry_count
                FROM central_platform.provisioning_jobs
                WHERE tenant_id = @id ORDER BY created_at DESC LIMIT 10", conn);
            jobsCmd.Parameters.AddWithValue("id", id);
            await using var jr = await jobsCmd.ExecuteReaderAsync();
            var jobs = await EndpointHelpers.ReadRowsAsync(jr);

            return Results.Ok(new { sizing = info, recent_jobs = jobs });
        });

        // POST /api/global-admin/tenants/{id}/provision-dedicated — queue provisioning job
        group.MapPost("/tenants/{id:guid}/provision-dedicated", async (Guid id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();

            // Look up slug
            await using var slugCmd = new NpgsqlCommand(
                "SELECT slug, sizing_model FROM central_platform.tenants WHERE id = @id", conn);
            slugCmd.Parameters.AddWithValue("id", id);
            await using var sr = await slugCmd.ExecuteReaderAsync();
            if (!await sr.ReadAsync()) return ApiProblem.NotFound($"Tenant {id} not found");
            var slug = sr.GetString(0);
            var currentModel = sr.GetString(1);
            await sr.CloseAsync();

            if (currentModel == "dedicated")
                return ApiProblem.Conflict("Tenant is already provisioned as dedicated");

            var safeSlug = System.Text.RegularExpressions.Regex.Replace(slug, "[^a-zA-Z0-9_]", "_");
            var dashSlug = System.Text.RegularExpressions.Regex.Replace(slug, "[^a-zA-Z0-9-]", "-");
            var payload = JsonSerializer.Serialize(new
            {
                target_database = $"central_{safeSlug}",
                target_namespace = $"central-{dashSlug}"
            });

            await using var ins = new NpgsqlCommand(@"
                INSERT INTO central_platform.provisioning_jobs (tenant_id, job_type, payload)
                VALUES (@id, 'provision_dedicated', @payload::jsonb) RETURNING id", conn);
            ins.Parameters.AddWithValue("id", id);
            ins.Parameters.AddWithValue("payload", payload);
            var jobId = (long)(await ins.ExecuteScalarAsync())!;

            // Mark tenant
            await using var upd = new NpgsqlCommand(
                "UPDATE central_platform.tenants SET provisioning_status = 'provisioning' WHERE id = @id", conn);
            upd.Parameters.AddWithValue("id", id);
            await upd.ExecuteNonQueryAsync();

            return Results.Accepted($"/api/global-admin/tenants/{id}/sizing",
                new { job_id = jobId, status = "queued" });
        });

        // POST /api/global-admin/tenants/{id}/decommission-dedicated — revert to zoned
        group.MapPost("/tenants/{id:guid}/decommission-dedicated", async (Guid id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO central_platform.provisioning_jobs (tenant_id, job_type)
                VALUES (@id, 'decommission_dedicated') RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var jobId = (long)(await cmd.ExecuteScalarAsync())!;
            return Results.Accepted($"/api/global-admin/tenants/{id}/sizing",
                new { job_id = jobId, status = "queued" });
        });

        // GET /api/global-admin/provisioning-jobs — platform-wide job queue view
        group.MapGet("/provisioning-jobs", async (string? status, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(status) ? "" : "WHERE status = @status";
            await using var cmd = new NpgsqlCommand($@"
                SELECT p.*, t.slug as tenant_slug, t.display_name as tenant_name
                FROM central_platform.provisioning_jobs p
                JOIN central_platform.tenants t ON t.id = p.tenant_id
                {where}
                ORDER BY p.created_at DESC LIMIT 200", conn);
            if (!string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("status", status);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}
