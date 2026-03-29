using System.Security.Claims;
using Central.Data;

namespace Central.Api.Endpoints;

public static class SettingsEndpoints
{
    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder group)
    {
        // Export current user's settings as JSON
        group.MapGet("/export", async (HttpContext ctx, DbConnectionFactory db) =>
        {
            var userId = GetUserId(ctx);
            var username = ctx.User.Identity?.Name ?? "unknown";
            if (userId == 0) return Results.Unauthorized();

            var repo = new DbRepository(db.ConnectionString);

            var json = await Central.Core.Services.SettingsExportService.ExportAsync(
                async () =>
                {
                    var settings = new Dictionary<string, string>();
                    // Load from user_settings table
                    await using var conn = new Npgsql.NpgsqlConnection(db.ConnectionString);
                    await conn.OpenAsync();
                    await using var cmd = new Npgsql.NpgsqlCommand(
                        "SELECT setting_key, setting_value FROM user_settings WHERE user_id=@uid", conn);
                    cmd.Parameters.AddWithValue("uid", userId);
                    await using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                        settings[r.GetString(0)] = r.IsDBNull(1) ? "" : r.GetString(1);
                    return settings;
                },
                async () =>
                {
                    var prefs = await repo.GetNotificationPreferencesAsync(userId);
                    return prefs.Select(p => new Dictionary<string, object?>
                    {
                        ["event_type"] = p.EventType, ["channel"] = p.Channel, ["is_enabled"] = p.IsEnabled
                    }).ToList();
                },
                async () =>
                {
                    var customs = await repo.GetPanelCustomizationsAsync(userId);
                    return customs.Select(c => new Dictionary<string, object?>
                    {
                        ["panel"] = c.PanelName, ["type"] = c.SettingType, ["key"] = c.SettingKey, ["json"] = c.SettingJson
                    }).ToList();
                },
                async () =>
                {
                    var filters = await repo.GetSavedFiltersAsync("", userId);
                    return filters.Select(f => new Dictionary<string, object?>
                    {
                        ["panel"] = f.PanelName, ["name"] = f.FilterName, ["expr"] = f.FilterExpr, ["default"] = f.IsDefault
                    }).ToList();
                },
                username, userId);

            return Results.Text(json, "application/json");
        });

        return group;
    }

    private static int GetUserId(HttpContext ctx)
    {
        var claim = ctx.User.FindFirst(ClaimTypes.NameIdentifier) ?? ctx.User.FindFirst("sub");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}
