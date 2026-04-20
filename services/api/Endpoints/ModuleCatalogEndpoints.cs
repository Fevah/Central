using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

/// <summary>
/// Module-catalog API — Phase 1 of the module-update system
/// (see <c>docs/MODULE_UPDATE_SYSTEM.md</c>). Read-only in Phase 1;
/// Phase 2 adds <c>POST /publish</c> + <c>GET /{code}/{version}/dll</c>
/// for CI upload + client download of individual module DLLs.
///
/// Desktop client on boot calls <c>GET /api/modules/catalog</c>,
/// compares each returned row's <c>currentVersion</c> to the loaded
/// module's <see cref="Central.Engine.Modules.IModule.Version"/>,
/// and surfaces a banner when they differ.
/// </summary>
public static class ModuleCatalogEndpoints
{
    public static RouteGroupBuilder MapModuleCatalogEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/modules/catalog — list of published modules + their latest
        // version + change_kind + engine-contract floor. No tenant-level
        // filtering in Phase 1; license gating happens at the desktop shell
        // layer via IModuleLicenseGate.
        group.MapGet("/catalog", async (DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT mc.code,
                         mc.display_name,
                         mc.description,
                         mc.is_base,
                         mc.current_version,
                         mc.current_version_updated_at,
                         mv.change_kind,
                         mv.min_engine_contract,
                         mv.published_at,
                         mv.release_notes
                    FROM central_platform.module_catalog mc
                    LEFT JOIN LATERAL (
                        SELECT change_kind, min_engine_contract, published_at, release_notes
                          FROM central_platform.module_versions
                         WHERE module_code = mc.code
                           AND version = mc.current_version
                           AND is_yanked = false
                         ORDER BY published_at DESC
                         LIMIT 1
                    ) mv ON true
                   ORDER BY mc.code", conn);

            var rows = new List<object>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                rows.Add(new
                {
                    code                    = r.GetString(0),
                    displayName             = r.GetString(1),
                    description             = r.IsDBNull(2) ? null : r.GetString(2),
                    isBase                  = r.GetBoolean(3),
                    currentVersion          = r.IsDBNull(4) ? null : r.GetString(4),
                    currentVersionUpdatedAt = r.IsDBNull(5) ? (DateTime?)null : r.GetDateTime(5),
                    changeKind              = r.IsDBNull(6) ? null : r.GetString(6),
                    minEngineContract       = r.IsDBNull(7) ? (int?)null : r.GetInt32(7),
                    publishedAt             = r.IsDBNull(8) ? (DateTime?)null : r.GetDateTime(8),
                    releaseNotes            = r.IsDBNull(9) ? null : r.GetString(9),
                });
            }
            return Results.Ok(rows);
        });

        // GET /api/modules/{code}/versions — published history for one module.
        // Returns newest first. Includes yanked rows with is_yanked=true so
        // the admin UI can show them greyed out. Clients on Phase 1 ignore
        // yanked rows; Phase 4 uses this list to pick the highest
        // non-yanked version per tenant channel.
        group.MapGet("/{code}/versions", async (string code, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT version, change_kind, min_engine_contract, sha256, size_bytes,
                         release_notes, published_at, published_by, is_yanked,
                         yanked_at, yanked_reason
                    FROM central_platform.module_versions
                   WHERE module_code = @c
                   ORDER BY published_at DESC", conn);
            cmd.Parameters.AddWithValue("c", code);

            var rows = new List<object>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                rows.Add(new
                {
                    version           = r.GetString(0),
                    changeKind        = r.GetString(1),
                    minEngineContract = r.GetInt32(2),
                    sha256            = r.IsDBNull(3) ? null : r.GetString(3),
                    sizeBytes         = r.IsDBNull(4) ? (long?)null : r.GetInt64(4),
                    releaseNotes      = r.IsDBNull(5) ? null : r.GetString(5),
                    publishedAt       = r.GetDateTime(6),
                    publishedBy       = r.IsDBNull(7) ? (Guid?)null : r.GetGuid(7),
                    isYanked          = r.GetBoolean(8),
                    yankedAt          = r.IsDBNull(9) ? (DateTime?)null : r.GetDateTime(9),
                    yankedReason      = r.IsDBNull(10) ? null : r.GetString(10),
                });
            }

            if (rows.Count == 0)
                return Results.NotFound(new { error = $"Unknown module code '{code}'." });

            return Results.Ok(rows);
        });

        return group;
    }
}
