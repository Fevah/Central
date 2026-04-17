using Npgsql;
using Central.Engine.Integration;

namespace Central.Persistence;

public partial class DbRepository
{
    // ── Sync Configs ──────────────────────────────────────────────────────

    public async Task<List<SyncConfig>> GetSyncConfigsAsync()
    {
        var list = new List<SyncConfig>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, agent_type, is_enabled, direction, schedule_cron, interval_minutes, max_concurrent, config_json::text, last_sync_at, last_sync_status, last_error FROM sync_configs ORDER BY name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SyncConfig
            {
                Id = r.GetInt32(0), Name = r.GetString(1), AgentType = r.GetString(2),
                IsEnabled = r.GetBoolean(3), Direction = r.GetString(4),
                ScheduleCron = r.IsDBNull(5) ? "" : r.GetString(5),
                IntervalMinutes = r.GetInt32(6), MaxConcurrent = r.GetInt32(7),
                ConfigJson = r.GetString(8),
                LastSyncAt = r.IsDBNull(9) ? null : r.GetDateTime(9),
                LastSyncStatus = r.GetString(10),
                LastError = r.IsDBNull(11) ? null : r.GetString(11)
            });
        return list;
    }

    public async Task UpsertSyncConfigAsync(SyncConfig sc)
    {
        await using var conn = await OpenConnectionAsync();
        if (sc.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO sync_configs (name, agent_type, is_enabled, direction, schedule_cron, interval_minutes, max_concurrent, config_json)
                  VALUES (@n, @at, @en, @dir, @cron, @int, @mc, @cfg::jsonb) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", sc.Name); cmd.Parameters.AddWithValue("at", sc.AgentType);
            cmd.Parameters.AddWithValue("en", sc.IsEnabled); cmd.Parameters.AddWithValue("dir", sc.Direction);
            cmd.Parameters.AddWithValue("cron", sc.ScheduleCron); cmd.Parameters.AddWithValue("int", sc.IntervalMinutes);
            cmd.Parameters.AddWithValue("mc", sc.MaxConcurrent); cmd.Parameters.AddWithValue("cfg", sc.ConfigJson);
            sc.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(
                @"UPDATE sync_configs SET name=@n, agent_type=@at, is_enabled=@en, direction=@dir,
                  schedule_cron=@cron, interval_minutes=@int, max_concurrent=@mc, config_json=@cfg::jsonb, updated_at=NOW()
                  WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", sc.Id); cmd.Parameters.AddWithValue("n", sc.Name);
            cmd.Parameters.AddWithValue("at", sc.AgentType); cmd.Parameters.AddWithValue("en", sc.IsEnabled);
            cmd.Parameters.AddWithValue("dir", sc.Direction); cmd.Parameters.AddWithValue("cron", sc.ScheduleCron);
            cmd.Parameters.AddWithValue("int", sc.IntervalMinutes); cmd.Parameters.AddWithValue("mc", sc.MaxConcurrent);
            cmd.Parameters.AddWithValue("cfg", sc.ConfigJson);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task UpdateSyncStatusAsync(int configId, string status, string? error = null)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE sync_configs SET last_sync_at=NOW(), last_sync_status=@s, last_error=@e, updated_at=NOW() WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", configId); cmd.Parameters.AddWithValue("s", status);
        cmd.Parameters.AddWithValue("e", (object?)error ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Entity Maps ───────────────────────────────────────────────────────

    public async Task<List<SyncEntityMap>> GetSyncEntityMapsAsync(int configId)
    {
        var list = new List<SyncEntityMap>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, sync_config_id, source_entity, target_table, mapping_type, is_enabled, sync_direction, filter_expr, upsert_key, sort_order FROM sync_entity_maps WHERE sync_config_id=@cid ORDER BY sort_order", conn);
        cmd.Parameters.AddWithValue("cid", configId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SyncEntityMap
            {
                Id = r.GetInt32(0), SyncConfigId = r.GetInt32(1), SourceEntity = r.GetString(2),
                TargetTable = r.GetString(3), MappingType = r.GetString(4), IsEnabled = r.GetBoolean(5),
                SyncDirection = r.GetString(6), FilterExpr = r.IsDBNull(7) ? "" : r.GetString(7),
                UpsertKey = r.GetString(8), SortOrder = r.GetInt32(9)
            });
        return list;
    }

    public async Task UpsertSyncEntityMapAsync(SyncEntityMap em)
    {
        await using var conn = await OpenConnectionAsync();
        if (em.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO sync_entity_maps (sync_config_id, source_entity, target_table, mapping_type, is_enabled, sync_direction, filter_expr, upsert_key, sort_order)
                  VALUES (@cid, @se, @tt, @mt, @en, @dir, @fe, @uk, @so) RETURNING id", conn);
            cmd.Parameters.AddWithValue("cid", em.SyncConfigId); cmd.Parameters.AddWithValue("se", em.SourceEntity);
            cmd.Parameters.AddWithValue("tt", em.TargetTable); cmd.Parameters.AddWithValue("mt", em.MappingType);
            cmd.Parameters.AddWithValue("en", em.IsEnabled); cmd.Parameters.AddWithValue("dir", em.SyncDirection);
            cmd.Parameters.AddWithValue("fe", em.FilterExpr); cmd.Parameters.AddWithValue("uk", em.UpsertKey);
            cmd.Parameters.AddWithValue("so", em.SortOrder);
            em.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(
                @"UPDATE sync_entity_maps SET source_entity=@se, target_table=@tt, mapping_type=@mt, is_enabled=@en,
                  sync_direction=@dir, filter_expr=@fe, upsert_key=@uk, sort_order=@so WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", em.Id); cmd.Parameters.AddWithValue("se", em.SourceEntity);
            cmd.Parameters.AddWithValue("tt", em.TargetTable); cmd.Parameters.AddWithValue("mt", em.MappingType);
            cmd.Parameters.AddWithValue("en", em.IsEnabled); cmd.Parameters.AddWithValue("dir", em.SyncDirection);
            cmd.Parameters.AddWithValue("fe", em.FilterExpr); cmd.Parameters.AddWithValue("uk", em.UpsertKey);
            cmd.Parameters.AddWithValue("so", em.SortOrder);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // ── Field Maps ────────────────────────────────────────────────────────

    public async Task<List<SyncFieldMap>> GetSyncFieldMapsAsync(int entityMapId)
    {
        var list = new List<SyncFieldMap>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, entity_map_id, source_field, target_column, converter_type, converter_expr, is_key, is_required, default_value, sort_order FROM sync_field_maps WHERE entity_map_id=@eid ORDER BY sort_order", conn);
        cmd.Parameters.AddWithValue("eid", entityMapId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SyncFieldMap
            {
                Id = r.GetInt32(0), EntityMapId = r.GetInt32(1), SourceField = r.GetString(2),
                TargetColumn = r.GetString(3), ConverterType = r.GetString(4),
                ConverterExpr = r.IsDBNull(5) ? "" : r.GetString(5),
                IsKey = r.GetBoolean(6), IsRequired = r.GetBoolean(7),
                DefaultValue = r.IsDBNull(8) ? null : r.GetString(8), SortOrder = r.GetInt32(9)
            });
        return list;
    }

    public async Task UpsertSyncFieldMapAsync(SyncFieldMap fm)
    {
        await using var conn = await OpenConnectionAsync();
        if (fm.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO sync_field_maps (entity_map_id, source_field, target_column, converter_type, converter_expr, is_key, is_required, default_value, sort_order)
                  VALUES (@eid, @sf, @tc, @ct, @ce, @ik, @ir, @dv, @so) RETURNING id", conn);
            cmd.Parameters.AddWithValue("eid", fm.EntityMapId); cmd.Parameters.AddWithValue("sf", fm.SourceField);
            cmd.Parameters.AddWithValue("tc", fm.TargetColumn); cmd.Parameters.AddWithValue("ct", fm.ConverterType);
            cmd.Parameters.AddWithValue("ce", fm.ConverterExpr); cmd.Parameters.AddWithValue("ik", fm.IsKey);
            cmd.Parameters.AddWithValue("ir", fm.IsRequired); cmd.Parameters.AddWithValue("dv", (object?)fm.DefaultValue ?? DBNull.Value);
            cmd.Parameters.AddWithValue("so", fm.SortOrder);
            fm.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(
                @"UPDATE sync_field_maps SET source_field=@sf, target_column=@tc, converter_type=@ct, converter_expr=@ce,
                  is_key=@ik, is_required=@ir, default_value=@dv, sort_order=@so WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", fm.Id); cmd.Parameters.AddWithValue("sf", fm.SourceField);
            cmd.Parameters.AddWithValue("tc", fm.TargetColumn); cmd.Parameters.AddWithValue("ct", fm.ConverterType);
            cmd.Parameters.AddWithValue("ce", fm.ConverterExpr); cmd.Parameters.AddWithValue("ik", fm.IsKey);
            cmd.Parameters.AddWithValue("ir", fm.IsRequired); cmd.Parameters.AddWithValue("dv", (object?)fm.DefaultValue ?? DBNull.Value);
            cmd.Parameters.AddWithValue("so", fm.SortOrder);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // ── Sync Log ──────────────────────────────────────────────────────────

    public async Task<List<SyncLogEntry>> GetSyncLogAsync(int configId, int limit = 50)
    {
        var list = new List<SyncLogEntry>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            $"SELECT id, sync_config_id, started_at, completed_at, status, entity_name, records_read, records_created, records_updated, records_failed, error_message, duration_ms FROM sync_log WHERE sync_config_id=@cid ORDER BY started_at DESC LIMIT {limit}", conn);
        cmd.Parameters.AddWithValue("cid", configId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SyncLogEntry
            {
                Id = r.GetInt64(0), SyncConfigId = r.GetInt32(1), StartedAt = r.GetDateTime(2),
                CompletedAt = r.IsDBNull(3) ? null : r.GetDateTime(3), Status = r.GetString(4),
                EntityName = r.IsDBNull(5) ? null : r.GetString(5),
                RecordsRead = r.GetInt32(6), RecordsCreated = r.GetInt32(7),
                RecordsUpdated = r.GetInt32(8), RecordsFailed = r.GetInt32(9),
                ErrorMessage = r.IsDBNull(10) ? null : r.GetString(10),
                DurationMs = r.IsDBNull(11) ? null : r.GetInt32(11)
            });
        return list;
    }

    public async Task InsertSyncLogAsync(int configId, string status, string? entityName, int read, int created, int updated, int failed, string? error)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO sync_log (sync_config_id, status, entity_name, records_read, records_created, records_updated, records_failed, error_message, completed_at, duration_ms)
              VALUES (@cid, @s, @en, @r, @c, @u, @f, @e, NOW(), 0)", conn);
        cmd.Parameters.AddWithValue("cid", configId); cmd.Parameters.AddWithValue("s", status);
        cmd.Parameters.AddWithValue("en", (object?)entityName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("r", read); cmd.Parameters.AddWithValue("c", created);
        cmd.Parameters.AddWithValue("u", updated); cmd.Parameters.AddWithValue("f", failed);
        cmd.Parameters.AddWithValue("e", (object?)error ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Generic upsert for sync target ────────────────────────────────────

    public async Task UpsertSyncRecordAsync(string tableName, Dictionary<string, object?> fields, string upsertKey)
    {
        // Validate table name against whitelist
        var validTables = await GetTableListForSyncAsync();
        if (!validTables.Contains(tableName)) throw new ArgumentException($"Invalid sync target table: {tableName}");

        await using var conn = await OpenConnectionAsync();

        var columns = fields.Keys.ToList();
        var paramNames = columns.Select((c, i) => $"@p{i}").ToList();
        var conflictCols = upsertKey.Split(',').Select(c => c.Trim()).ToList();
        var updateSet = columns.Where(c => !conflictCols.Contains(c))
            .Select((c, i) => $"{c}=EXCLUDED.{c}").ToList();

        var sql = $"INSERT INTO {tableName} ({string.Join(",", columns)}) VALUES ({string.Join(",", paramNames)})";
        if (updateSet.Count > 0)
            sql += $" ON CONFLICT ({upsertKey}) DO UPDATE SET {string.Join(",", updateSet)}";
        else
            sql += $" ON CONFLICT ({upsertKey}) DO NOTHING";

        await using var cmd = new NpgsqlCommand(sql, conn);
        for (int i = 0; i < columns.Count; i++)
            cmd.Parameters.AddWithValue($"p{i}", fields[columns[i]] ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<string>> GetTableListForSyncPublicAsync()
    {
        var set = await GetTableListForSyncAsync();
        return set.OrderBy(t => t).ToList();
    }

    private async Task<HashSet<string>> GetTableListForSyncAsync()
    {
        var tables = new HashSet<string>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname='public'", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) tables.Add(r.GetString(0));
        return tables;
    }
}
