using Npgsql;
using Central.Core.Models;

namespace Central.Data;

public partial class DbRepository
{
    // ── Soft-Delete Purge ─────────────────────────────────────────────────

    private static readonly string[] PurgeableTables = ["devices", "p2p_links", "b2b_links", "fw_links"];

    public async Task<Dictionary<string, int>> GetSoftDeletedCountsAsync()
    {
        var counts = new Dictionary<string, int>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        foreach (var table in PurgeableTables)
        {
            try
            {
                await using var cmd = new NpgsqlCommand(
                    $"SELECT COUNT(*) FROM {table} WHERE is_deleted = true", conn);
                var count = (long)(await cmd.ExecuteScalarAsync())!;
                if (count > 0) counts[table] = (int)count;
            }
            catch { } // table may not have is_deleted column
        }
        return counts;
    }

    public async Task<int> PurgeSoftDeletedAsync(string tableName)
    {
        if (!PurgeableTables.Contains(tableName)) return 0;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DELETE FROM {tableName} WHERE is_deleted = true", conn);
        return await cmd.ExecuteNonQueryAsync();
    }

    // ── Location CRUD ─────────────────────────────────────────────────────

    public async Task<List<Country>> GetCountriesAsync()
    {
        var list = new List<Country>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT id, code, name, sort_order FROM countries ORDER BY sort_order, name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new Country { Id = r.GetInt32(0), Code = r.GetString(1), Name = r.GetString(2), SortOrder = r.GetInt32(3) });
        return list;
    }

    public async Task UpsertCountryAsync(Country c)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (c.Id == 0)
        {
            await using var cmd = new NpgsqlCommand("INSERT INTO countries (code, name, sort_order) VALUES (@c, @n, @s) RETURNING id", conn);
            cmd.Parameters.AddWithValue("c", c.Code); cmd.Parameters.AddWithValue("n", c.Name); cmd.Parameters.AddWithValue("s", c.SortOrder);
            c.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand("UPDATE countries SET code=@c, name=@n, sort_order=@s WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", c.Id); cmd.Parameters.AddWithValue("c", c.Code);
            cmd.Parameters.AddWithValue("n", c.Name); cmd.Parameters.AddWithValue("s", c.SortOrder);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteCountryAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM countries WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Region>> GetRegionsAsync(int? countryId = null)
    {
        var list = new List<Region>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var where = countryId.HasValue ? "WHERE r.country_id=@cid" : "";
        await using var cmd = new NpgsqlCommand(
            $"SELECT r.id, r.country_id, r.code, r.name, r.sort_order, c.name FROM regions r JOIN countries c ON c.id=r.country_id {where} ORDER BY c.name, r.sort_order, r.name", conn);
        if (countryId.HasValue) cmd.Parameters.AddWithValue("cid", countryId.Value);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new Region { Id = r.GetInt32(0), CountryId = r.GetInt32(1), Code = r.GetString(2), Name = r.GetString(3), SortOrder = r.GetInt32(4), CountryName = r.GetString(5) });
        return list;
    }

    public async Task UpsertRegionAsync(Region rg)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (rg.Id == 0)
        {
            await using var cmd = new NpgsqlCommand("INSERT INTO regions (country_id, code, name, sort_order) VALUES (@cid, @c, @n, @s) RETURNING id", conn);
            cmd.Parameters.AddWithValue("cid", rg.CountryId); cmd.Parameters.AddWithValue("c", rg.Code);
            cmd.Parameters.AddWithValue("n", rg.Name); cmd.Parameters.AddWithValue("s", rg.SortOrder);
            rg.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand("UPDATE regions SET country_id=@cid, code=@c, name=@n, sort_order=@s WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", rg.Id); cmd.Parameters.AddWithValue("cid", rg.CountryId);
            cmd.Parameters.AddWithValue("c", rg.Code); cmd.Parameters.AddWithValue("n", rg.Name); cmd.Parameters.AddWithValue("s", rg.SortOrder);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteRegionAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM regions WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Reference Config ──────────────────────────────────────────────────

    public async Task<List<ReferenceConfig>> GetReferenceConfigsAsync()
    {
        var list = new List<ReferenceConfig>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, entity_type, prefix, suffix, pad_length, next_value, COALESCE(description,'') FROM reference_config ORDER BY entity_type", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ReferenceConfig
            {
                Id = r.GetInt32(0), EntityType = r.GetString(1), Prefix = r.GetString(2),
                Suffix = r.GetString(3), PadLength = r.GetInt32(4), NextValue = r.GetInt64(5),
                Description = r.GetString(6)
            });
        return list;
    }

    public async Task UpsertReferenceConfigAsync(ReferenceConfig rc)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO reference_config (entity_type, prefix, suffix, pad_length, next_value, description)
              VALUES (@et, @p, @s, @pl, @nv, @d)
              ON CONFLICT (entity_type) DO UPDATE SET prefix=@p, suffix=@s, pad_length=@pl, next_value=@nv, description=@d", conn);
        cmd.Parameters.AddWithValue("et", rc.EntityType); cmd.Parameters.AddWithValue("p", rc.Prefix);
        cmd.Parameters.AddWithValue("s", rc.Suffix); cmd.Parameters.AddWithValue("pl", rc.PadLength);
        cmd.Parameters.AddWithValue("nv", rc.NextValue); cmd.Parameters.AddWithValue("d", (object?)rc.Description ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string> GetNextReferenceAsync(string entityType)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT next_reference(@t)", conn);
        cmd.Parameters.AddWithValue("t", entityType);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    // ── Appointments ──────────────────────────────────────────────────────

    public async Task<List<Appointment>> GetAppointmentsAsync(DateTime start, DateTime end)
    {
        var list = new List<Appointment>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, subject, COALESCE(description,''), start_time, end_time, all_day, COALESCE(location,''),
                      resource_id, status, label, COALESCE(recurrence_info,''), task_id, ticket_id, created_by, created_at
               FROM appointments WHERE start_time < @e AND end_time > @s ORDER BY start_time", conn);
        cmd.Parameters.AddWithValue("s", start);
        cmd.Parameters.AddWithValue("e", end);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new Appointment
            {
                Id = r.GetInt32(0), Subject = r.GetString(1), Description = r.GetString(2),
                StartTime = r.GetDateTime(3), EndTime = r.GetDateTime(4), AllDay = r.GetBoolean(5),
                Location = r.GetString(6), ResourceId = r.IsDBNull(7) ? null : r.GetInt32(7),
                Status = r.GetInt32(8), Label = r.GetInt32(9), RecurrenceInfo = r.GetString(10),
                TaskId = r.IsDBNull(11) ? null : r.GetInt32(11), TicketId = r.IsDBNull(12) ? null : r.GetInt64(12),
                CreatedBy = r.IsDBNull(13) ? null : r.GetInt32(13), CreatedAt = r.GetDateTime(14)
            });
        return list;
    }

    public async Task UpsertAppointmentAsync(Appointment a)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (a.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO appointments (subject, description, start_time, end_time, all_day, location,
                      resource_id, status, label, recurrence_info, task_id, ticket_id, created_by)
                   VALUES (@sub, @desc, @s, @e, @ad, @loc, @rid, @st, @lb, @ri, @tid, @tkid, @cb) RETURNING id", conn);
            cmd.Parameters.AddWithValue("sub", a.Subject); cmd.Parameters.AddWithValue("desc", (object?)a.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("s", a.StartTime); cmd.Parameters.AddWithValue("e", a.EndTime);
            cmd.Parameters.AddWithValue("ad", a.AllDay); cmd.Parameters.AddWithValue("loc", (object?)a.Location ?? DBNull.Value);
            cmd.Parameters.AddWithValue("rid", (object?)a.ResourceId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("st", a.Status); cmd.Parameters.AddWithValue("lb", a.Label);
            cmd.Parameters.AddWithValue("ri", (object?)a.RecurrenceInfo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tid", (object?)a.TaskId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tkid", (object?)a.TicketId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cb", (object?)a.CreatedBy ?? DBNull.Value);
            a.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(
                @"UPDATE appointments SET subject=@sub, description=@desc, start_time=@s, end_time=@e,
                      all_day=@ad, location=@loc, resource_id=@rid, status=@st, label=@lb,
                      recurrence_info=@ri, task_id=@tid, ticket_id=@tkid, updated_at=NOW()
                   WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", a.Id);
            cmd.Parameters.AddWithValue("sub", a.Subject); cmd.Parameters.AddWithValue("desc", (object?)a.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("s", a.StartTime); cmd.Parameters.AddWithValue("e", a.EndTime);
            cmd.Parameters.AddWithValue("ad", a.AllDay); cmd.Parameters.AddWithValue("loc", (object?)a.Location ?? DBNull.Value);
            cmd.Parameters.AddWithValue("rid", (object?)a.ResourceId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("st", a.Status); cmd.Parameters.AddWithValue("lb", a.Label);
            cmd.Parameters.AddWithValue("ri", (object?)a.RecurrenceInfo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tid", (object?)a.TaskId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tkid", (object?)a.TicketId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteAppointmentAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM appointments WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<AppointmentResource>> GetAppointmentResourcesAsync()
    {
        var list = new List<AppointmentResource>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, user_id, display_name, color, is_active FROM appointment_resources ORDER BY display_name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AppointmentResource
            {
                Id = r.GetInt32(0), UserId = r.GetInt32(1), DisplayName = r.GetString(2),
                Color = r.GetString(3), IsActive = r.GetBoolean(4)
            });
        return list;
    }

    // ── Panel Customizations ──────────────────────────────────────────────

    public async Task<List<PanelCustomizationRecord>> GetPanelCustomizationsAsync(int userId, string? panelName = null)
    {
        var list = new List<PanelCustomizationRecord>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var where = panelName != null ? "WHERE user_id=@uid AND panel_name=@pn" : "WHERE user_id=@uid";
        await using var cmd = new NpgsqlCommand(
            $"SELECT id, user_id, panel_name, setting_type, setting_key, setting_json::text FROM panel_customizations {where} ORDER BY panel_name, setting_type", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        if (panelName != null) cmd.Parameters.AddWithValue("pn", panelName);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PanelCustomizationRecord
            {
                Id = r.GetInt32(0), UserId = r.GetInt32(1), PanelName = r.GetString(2),
                SettingType = r.GetString(3), SettingKey = r.GetString(4), SettingJson = r.GetString(5)
            });
        return list;
    }

    public async Task UpsertPanelCustomizationAsync(int userId, string panelName, string settingType, string settingKey, string settingJson)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO panel_customizations (user_id, panel_name, setting_type, setting_key, setting_json)
               VALUES (@uid, @pn, @st, @sk, @sj::jsonb)
               ON CONFLICT (user_id, panel_name, setting_type, setting_key) DO UPDATE SET setting_json=@sj::jsonb, updated_at=NOW()", conn);
        cmd.Parameters.AddWithValue("uid", userId); cmd.Parameters.AddWithValue("pn", panelName);
        cmd.Parameters.AddWithValue("st", settingType); cmd.Parameters.AddWithValue("sk", settingKey);
        cmd.Parameters.AddWithValue("sj", settingJson);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeletePanelCustomizationAsync(int userId, string panelName, string settingType)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM panel_customizations WHERE user_id=@uid AND panel_name=@pn AND setting_type=@st", conn);
        cmd.Parameters.AddWithValue("uid", userId); cmd.Parameters.AddWithValue("pn", panelName); cmd.Parameters.AddWithValue("st", settingType);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Saved Filter Convenience Overloads ───────────────────────────────

    public Task<List<Central.Core.Models.SavedFilter>> GetSavedFiltersAsync(int userId, string panelName)
        => GetSavedFiltersAsync(panelName, userId);

    public async Task UpsertSavedFilterAsync(int userId, string panelName, string name, string filterExpr)
    {
        await UpsertSavedFilterAsync(new Central.Core.Models.SavedFilter
        {
            UserId = userId, PanelName = panelName, FilterName = name, FilterExpr = filterExpr
        });
    }

    public async Task SetDefaultSavedFilterAsync(int userId, string panelName, int filterId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var clear = new NpgsqlCommand(
            "UPDATE saved_filters SET is_default=false WHERE panel_name=@p AND (user_id=@u OR user_id IS NULL)", conn);
        clear.Parameters.AddWithValue("p", panelName); clear.Parameters.AddWithValue("u", userId);
        await clear.ExecuteNonQueryAsync();
        await using var set = new NpgsqlCommand("UPDATE saved_filters SET is_default=true WHERE id=@id", conn);
        set.Parameters.AddWithValue("id", filterId);
        await set.ExecuteNonQueryAsync();
    }

    // ── AD User Bulk Upsert ───────────────────────────────────────────────

    public async Task BulkUpsertAdUsersAsync(List<AppUser> users)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        foreach (var u in users)
        {
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO app_users (username, display_name, role, is_active, user_type, email, department, title, phone, company, ad_guid, last_ad_sync)
                   VALUES (@user, @name, @role, @active, @type, @email, @dept, @title, @phone, @company, @guid, NOW())
                   ON CONFLICT (username) DO UPDATE SET
                      display_name=EXCLUDED.display_name, is_active=EXCLUDED.is_active,
                      email=EXCLUDED.email, department=EXCLUDED.department, title=EXCLUDED.title,
                      phone=EXCLUDED.phone, company=EXCLUDED.company, ad_guid=EXCLUDED.ad_guid, last_ad_sync=NOW()", conn);
            cmd.Parameters.AddWithValue("user", u.Username);
            cmd.Parameters.AddWithValue("name", (object?)u.DisplayName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("role", u.Role);
            cmd.Parameters.AddWithValue("active", u.IsActive);
            cmd.Parameters.AddWithValue("type", u.UserType);
            cmd.Parameters.AddWithValue("email", (object?)u.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("dept", (object?)u.Department ?? DBNull.Value);
            cmd.Parameters.AddWithValue("title", (object?)u.Title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("phone", (object?)u.Phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("company", (object?)u.Company ?? DBNull.Value);
            cmd.Parameters.AddWithValue("guid", (object?)u.AdGuid ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
