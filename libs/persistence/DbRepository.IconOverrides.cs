using Npgsql;
using Central.Engine.Models;

namespace Central.Persistence;

public partial class DbRepository
{
    // ── Generalized Icon Overrides ────────────────────────────────────────

    /// <summary>Get all admin defaults for a context (e.g. "status.device", "device_type").</summary>
    public async Task<List<IconOverride>> GetIconDefaultsAsync(string context)
    {
        var list = new List<IconOverride>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, context, element_key, icon_name, icon_id, color FROM icon_defaults WHERE context=@c ORDER BY element_key", conn);
        cmd.Parameters.AddWithValue("c", context);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new IconOverride
            {
                Id = r.GetInt32(0), Context = r.GetString(1), ElementKey = r.GetString(2),
                IconName = r.IsDBNull(3) ? null : r.GetString(3),
                IconId = r.IsDBNull(4) ? null : r.GetInt32(4),
                Color = r.IsDBNull(5) ? null : r.GetString(5)
            });
        return list;
    }

    /// <summary>Get all admin defaults across all contexts.</summary>
    public async Task<List<IconOverride>> GetAllIconDefaultsAsync()
    {
        var list = new List<IconOverride>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, context, element_key, icon_name, icon_id, color FROM icon_defaults ORDER BY context, element_key", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new IconOverride
            {
                Id = r.GetInt32(0), Context = r.GetString(1), ElementKey = r.GetString(2),
                IconName = r.IsDBNull(3) ? null : r.GetString(3),
                IconId = r.IsDBNull(4) ? null : r.GetInt32(4),
                Color = r.IsDBNull(5) ? null : r.GetString(5)
            });
        return list;
    }

    /// <summary>Get user overrides for a context.</summary>
    public async Task<List<IconOverride>> GetUserIconOverridesAsync(int userId, string? context = null)
    {
        var list = new List<IconOverride>();
        await using var conn = await OpenConnectionAsync();
        var where = context != null ? "WHERE user_id=@uid AND COALESCE(context, element_type)=@c" : "WHERE user_id=@uid";
        await using var cmd = new NpgsqlCommand(
            $"SELECT id, COALESCE(context, element_type), element_key, icon_name, icon_id, color FROM user_icon_overrides {where} ORDER BY 2, 3", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        if (context != null) cmd.Parameters.AddWithValue("c", context);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new IconOverride
            {
                Id = r.GetInt32(0), Context = r.GetString(1), ElementKey = r.GetString(2),
                IconName = r.IsDBNull(3) ? null : r.GetString(3),
                IconId = r.IsDBNull(4) ? null : r.GetInt32(4),
                Color = r.IsDBNull(5) ? null : r.GetString(5)
            });
        return list;
    }

    /// <summary>Upsert an admin icon default.</summary>
    public async Task UpsertIconDefaultAsync(IconOverride ov)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO icon_defaults (context, element_key, icon_name, icon_id, color)
              VALUES (@ctx, @key, @name, @iid, @col)
              ON CONFLICT (context, element_key) DO UPDATE SET
                icon_name=@name, icon_id=@iid, color=@col, updated_at=NOW()", conn);
        cmd.Parameters.AddWithValue("ctx", ov.Context);
        cmd.Parameters.AddWithValue("key", ov.ElementKey);
        cmd.Parameters.AddWithValue("name", (object?)ov.IconName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("iid", (object?)ov.IconId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("col", (object?)ov.Color ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Upsert a user icon override.</summary>
    public async Task UpsertUserIconOverrideAsync(int userId, IconOverride ov)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO user_icon_overrides (user_id, context, element_key, icon_name, icon_id, color)
              VALUES (@uid, @ctx, @key, @name, @iid, @col)
              ON CONFLICT (user_id, context, element_key) DO UPDATE SET
                icon_name=@name, icon_id=@iid, color=@col, updated_at=NOW()", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("ctx", ov.Context);
        cmd.Parameters.AddWithValue("key", ov.ElementKey);
        cmd.Parameters.AddWithValue("name", (object?)ov.IconName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("iid", (object?)ov.IconId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("col", (object?)ov.Color ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Delete an admin icon default.</summary>
    public async Task DeleteIconDefaultAsync(string context, string elementKey)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM icon_defaults WHERE context=@c AND element_key=@k", conn);
        cmd.Parameters.AddWithValue("c", context);
        cmd.Parameters.AddWithValue("k", elementKey);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Delete a user icon override.</summary>
    public async Task DeleteUserIconOverrideAsync(int userId, string context, string elementKey)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM user_icon_overrides WHERE user_id=@uid AND context=@c AND element_key=@k", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("c", context);
        cmd.Parameters.AddWithValue("k", elementKey);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Delete all user icon overrides for a user.</summary>
    public async Task ResetAllUserIconOverridesAsync(int userId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM user_icon_overrides WHERE user_id=@uid", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Resolve an icon: user override → admin default → null (use code fallback).</summary>
    public async Task<IconOverride?> ResolveIconAsync(int userId, string context, string elementKey)
    {
        await using var conn = await OpenConnectionAsync();

        // Check user override first
        await using var uCmd = new NpgsqlCommand(
            "SELECT id, context, element_key, icon_name, icon_id, color FROM user_icon_overrides WHERE user_id=@uid AND context=@c AND element_key=@k", conn);
        uCmd.Parameters.AddWithValue("uid", userId);
        uCmd.Parameters.AddWithValue("c", context);
        uCmd.Parameters.AddWithValue("k", elementKey);
        await using var uRdr = await uCmd.ExecuteReaderAsync();
        if (await uRdr.ReadAsync())
            return new IconOverride
            {
                Id = uRdr.GetInt32(0), Context = uRdr.GetString(1), ElementKey = uRdr.GetString(2),
                IconName = uRdr.IsDBNull(3) ? null : uRdr.GetString(3),
                IconId = uRdr.IsDBNull(4) ? null : uRdr.GetInt32(4),
                Color = uRdr.IsDBNull(5) ? null : uRdr.GetString(5)
            };
        await uRdr.CloseAsync();

        // Fall back to admin default
        await using var aCmd = new NpgsqlCommand(
            "SELECT id, context, element_key, icon_name, icon_id, color FROM icon_defaults WHERE context=@c AND element_key=@k", conn);
        aCmd.Parameters.AddWithValue("c", context);
        aCmd.Parameters.AddWithValue("k", elementKey);
        await using var aRdr = await aCmd.ExecuteReaderAsync();
        if (await aRdr.ReadAsync())
            return new IconOverride
            {
                Id = aRdr.GetInt32(0), Context = aRdr.GetString(1), ElementKey = aRdr.GetString(2),
                IconName = aRdr.IsDBNull(3) ? null : aRdr.GetString(3),
                IconId = aRdr.IsDBNull(4) ? null : aRdr.GetInt32(4),
                Color = aRdr.IsDBNull(5) ? null : aRdr.GetString(5)
            };

        return null; // Use code fallback
    }
}
