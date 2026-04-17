using Npgsql;
using Central.Engine.Models;

namespace Central.Persistence;

public partial class DbRepository
{
    // ── Ribbon Pages ──────────────────────────────────────────────────

    public async Task<List<RibbonPageConfig>> GetRibbonPagesAsync()
    {
        var list = new List<RibbonPageConfig>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, header, sort_order, required_permission, icon_name, is_visible, is_system FROM ribbon_pages ORDER BY sort_order, header", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new RibbonPageConfig
            {
                Id = r.GetInt32(0),
                Header = r.GetString(1),
                SortOrder = r.GetInt32(2),
                RequiredPermission = r.IsDBNull(3) ? null : r.GetString(3),
                IconName = r.IsDBNull(4) ? null : r.GetString(4),
                IsVisible = r.GetBoolean(5),
                IsSystem = r.GetBoolean(6)
            });
        }
        return list;
    }

    public async Task UpsertRibbonPageAsync(RibbonPageConfig page)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(page.Id > 0
            ? @"UPDATE ribbon_pages SET header=@h, sort_order=@s, required_permission=@rp, icon_name=@icon, is_visible=@v, updated_at=NOW() WHERE id=@id"
            : @"INSERT INTO ribbon_pages (header, sort_order, required_permission, icon_name, is_visible) VALUES (@h, @s, @rp, @icon, @v) RETURNING id", conn);
        if (page.Id > 0) cmd.Parameters.AddWithValue("id", page.Id);
        cmd.Parameters.AddWithValue("h", page.Header);
        cmd.Parameters.AddWithValue("s", page.SortOrder);
        cmd.Parameters.AddWithValue("rp", (object?)page.RequiredPermission ?? DBNull.Value);
        cmd.Parameters.AddWithValue("icon", (object?)page.IconName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("v", page.IsVisible);
        if (page.Id == 0)
            page.Id = (int)(await cmd.ExecuteScalarAsync())!;
        else
            await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteRibbonPageAsync(int id)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM ribbon_pages WHERE id=@id AND is_system=FALSE", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Ribbon Groups ──────────────────────────────────────────────────

    public async Task<List<RibbonGroupConfig>> GetRibbonGroupsAsync(int? pageId = null)
    {
        var list = new List<RibbonGroupConfig>();
        await using var conn = await OpenConnectionAsync();
        var sql = @"SELECT g.id, g.page_id, g.header, g.sort_order, g.is_visible, p.header
                    FROM ribbon_groups g JOIN ribbon_pages p ON g.page_id = p.id";
        if (pageId.HasValue) sql += " WHERE g.page_id = @pid";
        sql += " ORDER BY p.sort_order, g.sort_order, g.header";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (pageId.HasValue) cmd.Parameters.AddWithValue("pid", pageId.Value);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new RibbonGroupConfig
            {
                Id = r.GetInt32(0),
                PageId = r.GetInt32(1),
                Header = r.GetString(2),
                SortOrder = r.GetInt32(3),
                IsVisible = r.GetBoolean(4),
                PageHeader = r.GetString(5)
            });
        }
        return list;
    }

    public async Task UpsertRibbonGroupAsync(RibbonGroupConfig group)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(group.Id > 0
            ? @"UPDATE ribbon_groups SET page_id=@pid, header=@h, sort_order=@s, is_visible=@v, updated_at=NOW() WHERE id=@id"
            : @"INSERT INTO ribbon_groups (page_id, header, sort_order, is_visible) VALUES (@pid, @h, @s, @v) RETURNING id", conn);
        if (group.Id > 0) cmd.Parameters.AddWithValue("id", group.Id);
        cmd.Parameters.AddWithValue("pid", group.PageId);
        cmd.Parameters.AddWithValue("h", group.Header);
        cmd.Parameters.AddWithValue("s", group.SortOrder);
        cmd.Parameters.AddWithValue("v", group.IsVisible);
        if (group.Id == 0)
            group.Id = (int)(await cmd.ExecuteScalarAsync())!;
        else
            await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteRibbonGroupAsync(int id)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM ribbon_groups WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Ribbon Items ──────────────────────────────────────────────────

    public async Task<List<RibbonItemConfig>> GetRibbonItemsAsync(int? groupId = null)
    {
        var list = new List<RibbonItemConfig>();
        await using var conn = await OpenConnectionAsync();
        var sql = @"SELECT i.id, i.group_id, i.content, i.item_type, i.sort_order, i.permission,
                           i.glyph, i.large_glyph, i.icon_id, i.command_type, i.command_param,
                           i.tooltip, i.is_visible, i.is_system, g.header, p.header
                    FROM ribbon_items i
                    JOIN ribbon_groups g ON i.group_id = g.id
                    JOIN ribbon_pages p ON g.page_id = p.id";
        if (groupId.HasValue) sql += " WHERE i.group_id = @gid";
        sql += " ORDER BY p.sort_order, g.sort_order, i.sort_order, i.content";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (groupId.HasValue) cmd.Parameters.AddWithValue("gid", groupId.Value);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new RibbonItemConfig
            {
                Id = r.GetInt32(0),
                GroupId = r.GetInt32(1),
                Content = r.GetString(2),
                ItemType = r.GetString(3),
                SortOrder = r.GetInt32(4),
                Permission = r.IsDBNull(5) ? null : r.GetString(5),
                Glyph = r.IsDBNull(6) ? null : r.GetString(6),
                LargeGlyph = r.IsDBNull(7) ? null : r.GetString(7),
                IconId = r.IsDBNull(8) ? null : r.GetInt32(8),
                CommandType = r.IsDBNull(9) ? null : r.GetString(9),
                CommandParam = r.IsDBNull(10) ? null : r.GetString(10),
                Tooltip = r.IsDBNull(11) ? null : r.GetString(11),
                IsVisible = r.GetBoolean(12),
                IsSystem = r.GetBoolean(13),
                GroupHeader = r.GetString(14),
                PageHeader = r.GetString(15)
            });
        }
        return list;
    }

    public async Task UpsertRibbonItemAsync(RibbonItemConfig item)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(item.Id > 0
            ? @"UPDATE ribbon_items SET group_id=@gid, content=@c, item_type=@t, sort_order=@s,
                permission=@perm, glyph=@g, large_glyph=@lg, icon_id=@iid, command_type=@ct,
                command_param=@cp, tooltip=@tip, is_visible=@v, updated_at=NOW() WHERE id=@id"
            : @"INSERT INTO ribbon_items (group_id, content, item_type, sort_order, permission, glyph,
                large_glyph, icon_id, command_type, command_param, tooltip, is_visible)
                VALUES (@gid, @c, @t, @s, @perm, @g, @lg, @iid, @ct, @cp, @tip, @v) RETURNING id", conn);
        if (item.Id > 0) cmd.Parameters.AddWithValue("id", item.Id);
        cmd.Parameters.AddWithValue("gid", item.GroupId);
        cmd.Parameters.AddWithValue("c", item.Content);
        cmd.Parameters.AddWithValue("t", item.ItemType);
        cmd.Parameters.AddWithValue("s", item.SortOrder);
        cmd.Parameters.AddWithValue("perm", (object?)item.Permission ?? DBNull.Value);
        cmd.Parameters.AddWithValue("g", (object?)item.Glyph ?? DBNull.Value);
        cmd.Parameters.AddWithValue("lg", (object?)item.LargeGlyph ?? DBNull.Value);
        cmd.Parameters.AddWithValue("iid", (object?)item.IconId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ct", (object?)item.CommandType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cp", (object?)item.CommandParam ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tip", (object?)item.Tooltip ?? DBNull.Value);
        cmd.Parameters.AddWithValue("v", item.IsVisible);
        if (item.Id == 0)
            item.Id = (int)(await cmd.ExecuteScalarAsync())!;
        else
            await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteRibbonItemAsync(int id)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM ribbon_items WHERE id=@id AND is_system=FALSE", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Saved Filters ──────────────────────────────────────────────────

    public async Task<List<Central.Engine.Models.SavedFilter>> GetSavedFiltersAsync(string panelName, int? userId)
    {
        var list = new List<Central.Engine.Models.SavedFilter>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, user_id, panel_name, filter_name, filter_expr, is_default, sort_order
              FROM saved_filters
              WHERE panel_name = @p AND (user_id IS NULL OR user_id = @u)
              ORDER BY sort_order, filter_name", conn);
        cmd.Parameters.AddWithValue("p", panelName);
        cmd.Parameters.AddWithValue("u", userId ?? (object)DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new Central.Engine.Models.SavedFilter
            {
                Id = r.GetInt32(0),
                UserId = r.IsDBNull(1) ? null : r.GetInt32(1),
                PanelName = r.GetString(2),
                FilterName = r.GetString(3),
                FilterExpr = r.GetString(4),
                IsDefault = r.GetBoolean(5),
                SortOrder = r.GetInt32(6)
            });
        }
        return list;
    }

    public async Task UpsertSavedFilterAsync(Central.Engine.Models.SavedFilter filter)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(filter.Id > 0
            ? @"UPDATE saved_filters SET filter_name=@n, filter_expr=@e, is_default=@d, sort_order=@s, updated_at=NOW() WHERE id=@id"
            : @"INSERT INTO saved_filters (user_id, panel_name, filter_name, filter_expr, is_default, sort_order)
                VALUES (@u, @p, @n, @e, @d, @s) RETURNING id", conn);
        if (filter.Id > 0) cmd.Parameters.AddWithValue("id", filter.Id);
        cmd.Parameters.AddWithValue("u", filter.UserId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("p", filter.PanelName);
        cmd.Parameters.AddWithValue("n", filter.FilterName);
        cmd.Parameters.AddWithValue("e", filter.FilterExpr);
        cmd.Parameters.AddWithValue("d", filter.IsDefault);
        cmd.Parameters.AddWithValue("s", filter.SortOrder);
        if (filter.Id == 0)
            filter.Id = (int)(await cmd.ExecuteScalarAsync())!;
        else
            await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteSavedFilterAsync(int id)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM saved_filters WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── User Ribbon Overrides ─────────────────────────────────────────

    public async Task<List<Central.Engine.Models.UserRibbonOverride>> GetUserRibbonOverridesAsync(int userId)
    {
        var list = new List<Central.Engine.Models.UserRibbonOverride>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, user_id, item_key, custom_icon, custom_text, is_hidden, sort_order FROM user_ribbon_overrides WHERE user_id=@u ORDER BY item_key", conn);
        cmd.Parameters.AddWithValue("u", userId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new Central.Engine.Models.UserRibbonOverride
            {
                Id = r.GetInt32(0), UserId = r.GetInt32(1), ItemKey = r.GetString(2),
                CustomIcon = r.IsDBNull(3) ? null : r.GetString(3),
                CustomText = r.IsDBNull(4) ? null : r.GetString(4),
                IsHidden = r.GetBoolean(5),
                SortOrder = r.IsDBNull(6) ? null : r.GetInt32(6)
            });
        }
        return list;
    }

    public async Task UpsertUserRibbonOverrideAsync(Central.Engine.Models.UserRibbonOverride ov)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO user_ribbon_overrides (user_id, item_key, custom_icon, custom_text, is_hidden, sort_order)
              VALUES (@u, @k, @icon, @text, @hide, @sort)
              ON CONFLICT (user_id, item_key) DO UPDATE SET
                custom_icon=@icon, custom_text=@text, is_hidden=@hide, sort_order=@sort, updated_at=NOW()", conn);
        cmd.Parameters.AddWithValue("u", ov.UserId);
        cmd.Parameters.AddWithValue("k", ov.ItemKey);
        cmd.Parameters.AddWithValue("icon", (object?)ov.CustomIcon ?? DBNull.Value);
        cmd.Parameters.AddWithValue("text", (object?)ov.CustomText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hide", ov.IsHidden);
        cmd.Parameters.AddWithValue("sort", (object?)ov.SortOrder ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteUserRibbonOverrideAsync(int userId, string itemKey)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM user_ribbon_overrides WHERE user_id=@u AND item_key=@k", conn);
        cmd.Parameters.AddWithValue("u", userId);
        cmd.Parameters.AddWithValue("k", itemKey);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Admin Ribbon Defaults ─────────────────────────────────────────

    public async Task<List<(string ItemKey, string? Icon, string? Text, bool IsHidden)>> GetAdminRibbonDefaultsAsync()
    {
        var list = new List<(string, string?, string?, bool)>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT item_key, default_icon, default_text, is_hidden FROM admin_ribbon_defaults", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetBoolean(3)));
        return list;
    }

    public async Task UpsertAdminRibbonDefaultAsync(string itemKey, string? icon, string? text, bool isHidden, int adminUserId,
        string? displayStyle = null, string? linkTarget = null)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO admin_ribbon_defaults (item_key, default_icon, default_text, is_hidden, updated_by, display_style, link_target)
              VALUES (@k, @icon, @text, @hide, @uid, @style, @link)
              ON CONFLICT (item_key) DO UPDATE SET default_icon=@icon, default_text=@text, is_hidden=@hide,
                updated_by=@uid, display_style=@style, link_target=@link, updated_at=NOW()", conn);
        cmd.Parameters.AddWithValue("k", itemKey);
        cmd.Parameters.AddWithValue("icon", (object?)icon ?? DBNull.Value);
        cmd.Parameters.AddWithValue("text", (object?)text ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hide", isHidden);
        cmd.Parameters.AddWithValue("uid", adminUserId);
        cmd.Parameters.AddWithValue("style", (object?)displayStyle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("link", (object?)linkTarget ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Load admin ribbon defaults + user overrides for global action icons/visibility.</summary>
    public async Task<Dictionary<(string Module, string Action), (string? Icon, bool? IsVisible)>> GetGlobalActionOverrideMapAsync(int? userId)
    {
        var map = new Dictionary<(string, string), (string?, bool?)>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT item_key, custom_icon, is_hidden FROM admin_ribbon_defaults WHERE item_key LIKE '%/%'", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = reader.GetString(0);
            var parts = key.Split('/', 2);
            if (parts.Length != 2) continue;
            var icon = reader.IsDBNull(1) ? null : reader.GetString(1);
            var hidden = !reader.IsDBNull(2) && reader.GetBoolean(2);
            map[(parts[0], parts[1])] = (icon, hidden ? false : null);
        }
        return map;
    }
}
