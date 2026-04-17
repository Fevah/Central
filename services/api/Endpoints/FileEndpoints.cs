using Npgsql;
using Central.Core.Services;
using Central.Data;

namespace Central.Api.Endpoints;

/// <summary>
/// File management API — upload, download, versioning, entity attachment.
/// </summary>
public static class FileEndpoints
{
    public static RouteGroupBuilder MapFileEndpoints(this RouteGroupBuilder group)
    {
        // Upload a file
        group.MapPost("/upload", async (HttpContext ctx, DbConnectionFactory db) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file == null) return Results.BadRequest("No file provided");

            var entityType = form["entity_type"].FirstOrDefault();
            var entityId = form["entity_id"].FirstOrDefault();
            var description = form["description"].FirstOrDefault() ?? "";

            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var data = ms.ToArray();
            var md5 = FileManagementService.ComputeMd5(data);

            var fileId = Guid.NewGuid();
            var svc = FileManagementService.Instance;

            // Route to storage-service if configured (CAS dedup)
            if (svc.UseStorageService)
            {
                try
                {
                    using var httpClient = new HttpClient();
                    using var uploadContent = new MultipartFormDataContent();
                    var streamContent = new StreamContent(new MemoryStream(data));
                    streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
                    uploadContent.Add(streamContent, "file", file.FileName);
                    var storageResp = await httpClient.PostAsync($"{svc.StorageServiceUrl}/api/v1/storage/objects/files/{fileId}", uploadContent);
                    if (storageResp.IsSuccessStatusCode)
                    {
                        var storageJson = await storageResp.Content.ReadAsStringAsync();
                        return Results.Ok(new { file_id = fileId, storage_response = storageJson, md5, routed_to = "storage-service" });
                    }
                }
                catch { /* storage-service unavailable — fall through to local */ }
            }

            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();

            // Create file record
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO file_store (id, filename, description, mime_type, file_size, entity_type, entity_id, md5_hash)
                  VALUES (@id, @fn, @desc, @mt, @sz, @et, @eid, @md5)", conn);
            cmd.Parameters.AddWithValue("id", fileId);
            cmd.Parameters.AddWithValue("fn", file.FileName);
            cmd.Parameters.AddWithValue("desc", description);
            cmd.Parameters.AddWithValue("mt", file.ContentType ?? "application/octet-stream");
            cmd.Parameters.AddWithValue("sz", data.Length);
            cmd.Parameters.AddWithValue("et", (object?)entityType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("eid", (object?)entityId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("md5", md5);
            await cmd.ExecuteNonQueryAsync();

            // Create version 1
            var versionId = Guid.NewGuid();
            if (svc.ShouldStoreInline(data.Length))
            {
                await using var verCmd = new NpgsqlCommand(
                    @"INSERT INTO file_versions (id, file_id, version_number, file_data, file_size, md5_hash)
                      VALUES (@id, @fid, 1, @data, @sz, @md5)", conn);
                verCmd.Parameters.AddWithValue("id", versionId);
                verCmd.Parameters.AddWithValue("fid", fileId);
                verCmd.Parameters.AddWithValue("data", data);
                verCmd.Parameters.AddWithValue("sz", data.Length);
                verCmd.Parameters.AddWithValue("md5", md5);
                await verCmd.ExecuteNonQueryAsync();
            }
            else
            {
                var path = svc.GetStoragePath(fileId, 1);
                await svc.SaveToFilesystemAsync(path, data);
                await using var verCmd = new NpgsqlCommand(
                    @"INSERT INTO file_versions (id, file_id, version_number, storage_path, file_size, md5_hash)
                      VALUES (@id, @fid, 1, @path, @sz, @md5)", conn);
                verCmd.Parameters.AddWithValue("id", versionId);
                verCmd.Parameters.AddWithValue("fid", fileId);
                verCmd.Parameters.AddWithValue("path", path);
                verCmd.Parameters.AddWithValue("sz", data.Length);
                verCmd.Parameters.AddWithValue("md5", md5);
                await verCmd.ExecuteNonQueryAsync();
            }

            return Results.Ok(new { file_id = fileId, version_id = versionId, filename = file.FileName, size = data.Length, md5 });
        }).DisableAntiforgery();

        // Download a file (latest version)
        group.MapGet("/{fileId:guid}/download", async (Guid fileId, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();

            // Get latest version
            await using var cmd = new NpgsqlCommand(
                @"SELECT v.file_data, v.storage_path, f.filename, f.mime_type
                  FROM file_versions v JOIN file_store f ON f.id = v.file_id
                  WHERE v.file_id = @fid ORDER BY v.version_number DESC LIMIT 1", conn);
            cmd.Parameters.AddWithValue("fid", fileId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Results.NotFound();

            byte[] data;
            if (!r.IsDBNull(0))
                data = (byte[])r[0];
            else if (!r.IsDBNull(1))
                data = await FileManagementService.Instance.ReadFromFilesystemAsync(r.GetString(1));
            else
                return Results.NotFound();

            var filename = r.GetString(2);
            var mimeType = r.GetString(3);
            return Results.File(data, mimeType, filename);
        });

        // List files for an entity
        group.MapGet("/", async (DbConnectionFactory db, string? entityType, string? entityId) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var where = new List<string>();
            if (!string.IsNullOrEmpty(entityType)) where.Add("entity_type = @et");
            if (!string.IsNullOrEmpty(entityId)) where.Add("entity_id = @eid");
            var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

            await using var cmd = new NpgsqlCommand(
                $"SELECT id, filename, description, mime_type, file_size, entity_type, entity_id, md5_hash, tags, created_at FROM file_store {whereClause} AND is_deleted = false ORDER BY created_at DESC LIMIT 100", conn);
            if (!string.IsNullOrEmpty(entityType)) cmd.Parameters.AddWithValue("et", entityType);
            if (!string.IsNullOrEmpty(entityId)) cmd.Parameters.AddWithValue("eid", entityId);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // Get file versions
        group.MapGet("/{fileId:guid}/versions", async (Guid fileId, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT id, file_id, version_number, file_size, md5_hash, created_at FROM file_versions WHERE file_id = @fid ORDER BY version_number DESC", conn);
            cmd.Parameters.AddWithValue("fid", fileId);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // Delete (soft delete)
        group.MapDelete("/{fileId:guid}", async (Guid fileId, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("UPDATE file_store SET is_deleted = true WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", fileId);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        return group;
    }
}
