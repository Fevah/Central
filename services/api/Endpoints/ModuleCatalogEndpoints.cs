using System.Security.Cryptography;
using Npgsql;
using Central.Persistence;
using Central.Api.Services;

namespace Central.Api.Endpoints;

/// <summary>
/// Module-catalog API — Phases 1 + 2 of the module-update system
/// (see <c>docs/MODULE_UPDATE_SYSTEM.md</c>).
///
/// Phase 1 (read-only): GET /catalog, GET /{code}/versions.
/// Phase 2 (distribution): POST /publish (CI upload with SHA-256 +
/// size compute), GET /{code}/{version}/manifest (lightweight
/// metadata), GET /{code}/{version}/dll (bytes stream). Phase 3 adds
/// the AssemblyLoadContext hot-swap on the desktop.
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

        // ── Phase 2 endpoints ─────────────────────────────────────────────

        // POST /api/modules/publish — CI upload. multipart/form-data with:
        //   moduleCode (string, required)
        //   version (string, required — must parse as semver-ish)
        //   changeKind (string, required — one of HotSwap/SoftReload/FullRestart)
        //   minEngineContract (int, required)
        //   releaseNotes (string, optional)
        //   setAsCurrent (bool, optional — default true; bumps
        //                 module_catalog.current_version pointer)
        //   dll (file, required — the DLL bytes)
        //
        // SHA-256 + size are computed server-side (never trusted from the
        // client). UNIQUE (module_code, version) guards against
        // re-publishing an existing version without going through yank
        // first.
        group.MapPost("/publish", async (HttpContext http, DbConnectionFactory db, IModuleBlobStorage blobs) =>
        {
            if (!http.Request.HasFormContentType)
                return Results.BadRequest(new { error = "multipart/form-data required." });

            var form = await http.Request.ReadFormAsync();

            var moduleCode       = form["moduleCode"].ToString();
            var version          = form["version"].ToString();
            var changeKind       = form["changeKind"].ToString();
            var minEngineStr     = form["minEngineContract"].ToString();
            var releaseNotes     = form["releaseNotes"].ToString();
            var setAsCurrentStr  = form["setAsCurrent"].ToString();
            var file             = form.Files.GetFile("dll");

            if (string.IsNullOrWhiteSpace(moduleCode))
                return Results.BadRequest(new { error = "moduleCode required." });
            if (string.IsNullOrWhiteSpace(version))
                return Results.BadRequest(new { error = "version required." });
            if (changeKind is not ("HotSwap" or "SoftReload" or "FullRestart"))
                return Results.BadRequest(new { error = "changeKind must be HotSwap, SoftReload, or FullRestart." });
            if (!int.TryParse(minEngineStr, out var minEngine) || minEngine < 1)
                return Results.BadRequest(new { error = "minEngineContract must be an integer >= 1." });
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "dll file required." });

            // Default setAsCurrent to true unless explicitly "false".
            var setAsCurrent = !string.Equals(setAsCurrentStr, "false", StringComparison.OrdinalIgnoreCase);

            // Compute SHA-256 + size while streaming into storage. Hashing
            // uses an in-memory buffer because the blob store API takes a
            // Stream — for typical module sizes (under 50MB) this is fine;
            // if modules ever get bigger we'd switch to a tee-stream.
            await using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);
            buffer.Position = 0;

            var sha256 = Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
            var size   = buffer.Length;

            buffer.Position = 0;
            await blobs.WriteAsync(moduleCode, version, buffer);

            // Single transaction for both writes — if module_catalog
            // update fails (e.g. moduleCode doesn't exist yet) the
            // module_versions insert rolls back.
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            try
            {
                await using var insert = new NpgsqlCommand(
                    @"INSERT INTO central_platform.module_versions
                        (module_code, version, change_kind, min_engine_contract,
                         blob_url, sha256, size_bytes, release_notes, published_at)
                      VALUES (@mc, @v, @ck, @me, @url, @sha, @sz, @rn, now())
                      RETURNING id, published_at", conn, tx);
                insert.Parameters.AddWithValue("mc", moduleCode);
                insert.Parameters.AddWithValue("v",  version);
                insert.Parameters.AddWithValue("ck", changeKind);
                insert.Parameters.AddWithValue("me", minEngine);
                // blob_url stores the relative coordinate the download
                // endpoint uses to look up the bytes; the actual backing
                // store can move (filesystem → MinIO) without rewriting
                // every row.
                insert.Parameters.AddWithValue("url", $"modules/{moduleCode}/{version}/module.dll");
                insert.Parameters.AddWithValue("sha", sha256);
                insert.Parameters.AddWithValue("sz",  size);
                insert.Parameters.AddWithValue("rn",  string.IsNullOrEmpty(releaseNotes) ? (object)DBNull.Value : releaseNotes);

                await using var reader = await insert.ExecuteReaderAsync();
                await reader.ReadAsync();
                var id          = reader.GetInt64(0);
                var publishedAt = reader.GetDateTime(1);
                await reader.CloseAsync();

                if (setAsCurrent)
                {
                    await using var bump = new NpgsqlCommand(
                        @"UPDATE central_platform.module_catalog
                             SET current_version = @v,
                                 current_version_updated_at = now()
                           WHERE code = @c", conn, tx);
                    bump.Parameters.AddWithValue("v", version);
                    bump.Parameters.AddWithValue("c", moduleCode);
                    var rows = await bump.ExecuteNonQueryAsync();
                    if (rows == 0)
                    {
                        // module_code not in catalog — the FK would have
                        // caught it on insert, but double-check. Roll back.
                        await tx.RollbackAsync();
                        await blobs.DeleteAsync(moduleCode, version);
                        return Results.BadRequest(new { error = $"Unknown moduleCode '{moduleCode}' — add to module_catalog first." });
                    }
                }

                await tx.CommitAsync();
                return Results.Ok(new
                {
                    id, moduleCode, version, changeKind,
                    minEngineContract = minEngine,
                    sha256, sizeBytes = size, publishedAt,
                    setAsCurrent
                });
            }
            catch (PostgresException pg) when (pg.SqlState == "23505")
            {
                // Duplicate (module_code, version). Clean up the blob we
                // just wrote so the filesystem doesn't drift from the DB.
                await tx.RollbackAsync();
                await blobs.DeleteAsync(moduleCode, version);
                return Results.Conflict(new
                {
                    error = $"Version '{version}' of module '{moduleCode}' already published. Yank before republishing.",
                    sqlState = pg.SqlState
                });
            }
            catch
            {
                await tx.RollbackAsync();
                await blobs.DeleteAsync(moduleCode, version);
                throw;
            }
        }).RequireAuthorization();

        // GET /api/modules/{code}/{version}/manifest — cheap metadata
        // lookup (no DLL bytes). Clients call this before download to
        // verify SHA-256 or decide whether to pull at all.
        group.MapGet("/{code}/{version}/manifest", async (string code, string version, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT change_kind, min_engine_contract, sha256, size_bytes,
                         release_notes, published_at, is_yanked, yanked_reason
                    FROM central_platform.module_versions
                   WHERE module_code = @c AND version = @v", conn);
            cmd.Parameters.AddWithValue("c", code);
            cmd.Parameters.AddWithValue("v", version);

            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
                return Results.NotFound(new { error = $"No published version {code}@{version}." });

            return Results.Ok(new
            {
                moduleCode        = code,
                version,
                changeKind        = r.GetString(0),
                minEngineContract = r.GetInt32(1),
                sha256            = r.IsDBNull(2) ? null : r.GetString(2),
                sizeBytes         = r.IsDBNull(3) ? (long?)null : r.GetInt64(3),
                releaseNotes      = r.IsDBNull(4) ? null : r.GetString(4),
                publishedAt       = r.GetDateTime(5),
                isYanked          = r.GetBoolean(6),
                yankedReason      = r.IsDBNull(7) ? null : r.GetString(7),
            });
        });

        // GET /api/modules/{code}/{version}/dll — the bytes. Streams
        // directly from the storage layer; no auth in Phase 2 since
        // module DLLs are non-sensitive (SHA-256-verified client-side).
        // Phase 4+ may add auth to enforce per-channel entitlements.
        group.MapGet("/{code}/{version}/dll", async (string code, string version, DbConnectionFactory db, IModuleBlobStorage blobs) =>
        {
            // Reject yanked versions by default — the bytes may still
            // be in storage for forensics but clients shouldn't fetch
            // them.
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT is_yanked, yanked_reason
                    FROM central_platform.module_versions
                   WHERE module_code = @c AND version = @v", conn);
            cmd.Parameters.AddWithValue("c", code);
            cmd.Parameters.AddWithValue("v", version);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
                return Results.NotFound(new { error = $"No published version {code}@{version}." });
            var yanked       = r.GetBoolean(0);
            var yankedReason = r.IsDBNull(1) ? null : r.GetString(1);
            await r.CloseAsync();

            if (yanked)
                return Results.StatusCode(StatusCodes.Status410Gone); // Gone

            var stream = await blobs.OpenReadAsync(code, version);
            if (stream is null)
                return Results.NotFound(new { error = $"DLL bytes for {code}@{version} not in blob storage (row predates Phase 2 or was deleted)." });

            var _ = yankedReason; // reserved — include as a header in a future revision
            return Results.File(stream, "application/octet-stream", fileDownloadName: $"{code}-{version}.dll");
        });

        return group;
    }
}
