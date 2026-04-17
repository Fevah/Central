using System.Text.Json;
using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

/// <summary>
/// Update management API — version checking, manifest serving, package download.
/// Used by: desktop update manager, CI/CD publish pipeline.
/// </summary>
public static class UpdateEndpoints
{
    public static RouteGroupBuilder MapUpdateEndpoints(this RouteGroupBuilder group)
    {
        // Check for available update
        group.MapGet("/check", async (string current, string platform, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT id, version, package_url, manifest_json::text, release_notes, is_mandatory, delta_from
                  FROM central_platform.client_versions
                  WHERE platform = @p AND version > @v
                  ORDER BY published_at DESC LIMIT 1", conn);
            cmd.Parameters.AddWithValue("p", platform);
            cmd.Parameters.AddWithValue("v", current);
            await using var r = await cmd.ExecuteReaderAsync();

            if (!await r.ReadAsync())
                return Results.Ok(new { up_to_date = true, current_version = current });

            JsonElement? manifest = r.IsDBNull(3) ? null : JsonSerializer.Deserialize<JsonElement>(r.GetString(3));
            var checksum = "";
            if (manifest.HasValue && manifest.Value.TryGetProperty("checksum", out var cs))
                checksum = cs.GetString() ?? "";

            return Results.Ok(new
            {
                up_to_date = false,
                version = r.GetString(1),
                package_url = r.GetString(2),
                checksum,
                release_notes = r.IsDBNull(4) ? "" : r.GetString(4),
                is_mandatory = r.GetBoolean(5),
                delta_from = r.IsDBNull(6) ? null : r.GetString(6)
            });
        });

        // Publish a new version (admin/CI)
        group.MapPost("/publish", async (DbConnectionFactory db, JsonElement body) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();

            var version = body.GetProperty("version").GetString() ?? "";
            var platform = body.TryGetProperty("platform", out var p) ? p.GetString() ?? "windows-x64" : "windows-x64";
            var packageUrl = body.GetProperty("package_url").GetString() ?? "";
            var manifestJson = body.TryGetProperty("manifest", out var m) ? m.GetRawText() : "{}";
            var releaseNotes = body.TryGetProperty("release_notes", out var rn) ? rn.GetString() : "";
            var isMandatory = body.TryGetProperty("is_mandatory", out var im) && im.GetBoolean();
            var channelName = body.TryGetProperty("channel", out var ch) ? ch.GetString() ?? "stable" : "stable";

            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO central_platform.client_versions (version, platform, package_url, manifest_json, release_notes, is_mandatory, channel_id)
                  SELECT @v, @p, @url, @mj::jsonb, @rn, @im, id FROM central_platform.release_channels WHERE name = @ch
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("v", version);
            cmd.Parameters.AddWithValue("p", platform);
            cmd.Parameters.AddWithValue("url", packageUrl);
            cmd.Parameters.AddWithValue("mj", manifestJson);
            cmd.Parameters.AddWithValue("rn", (object?)releaseNotes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("im", isMandatory);
            cmd.Parameters.AddWithValue("ch", channelName);

            var id = await cmd.ExecuteScalarAsync();
            return Results.Ok(new { id, version, platform, channel = channelName });
        });

        // List all versions
        group.MapGet("/versions", async (DbConnectionFactory db, string? platform) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var where = !string.IsNullOrEmpty(platform) ? "WHERE v.platform = @p" : "";
            await using var cmd = new NpgsqlCommand(
                $@"SELECT v.id, v.version, v.platform, v.published_at, v.is_mandatory, v.release_notes, c.name as channel
                   FROM central_platform.client_versions v
                   LEFT JOIN central_platform.release_channels c ON c.id = v.channel_id
                   {where} ORDER BY v.published_at DESC LIMIT 50", conn);
            if (!string.IsNullOrEmpty(platform)) cmd.Parameters.AddWithValue("p", platform);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // Client reports update result
        group.MapPost("/report", async (DbConnectionFactory db, JsonElement body) =>
        {
            var version = body.GetProperty("version").GetString() ?? "";
            var status = body.TryGetProperty("status", out var s) ? s.GetString() ?? "unknown" : "unknown";
            // Log the update result (could update client_installations table)
            return Results.Ok(new { reported = true, version, status });
        });

        return group;
    }
}
