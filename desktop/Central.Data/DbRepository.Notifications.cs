using Npgsql;
using Central.Core.Models;

namespace Central.Data;

public partial class DbRepository
{
    // ── Notification Preferences ──────────────────────────────────────────

    public async Task<List<NotificationPreference>> GetNotificationPreferencesAsync(int userId)
    {
        var list = new List<NotificationPreference>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, user_id, event_type, channel, is_enabled FROM notification_preferences WHERE user_id=@uid ORDER BY event_type", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new NotificationPreference
            {
                Id = r.GetInt32(0), UserId = r.GetInt32(1), EventType = r.GetString(2),
                Channel = r.GetString(3), IsEnabled = r.GetBoolean(4)
            });
        return list;
    }

    public async Task UpsertNotificationPreferenceAsync(int userId, string eventType, string channel, bool isEnabled)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO notification_preferences (user_id, event_type, channel, is_enabled) VALUES (@uid, @et, @ch, @en)
              ON CONFLICT (user_id, event_type) DO UPDATE SET channel=@ch, is_enabled=@en", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("et", eventType);
        cmd.Parameters.AddWithValue("ch", channel);
        cmd.Parameters.AddWithValue("en", isEnabled);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Active Sessions ───────────────────────────────────────────────────

    public async Task<List<ActiveSession>> GetActiveSessionsAsync()
    {
        var list = new List<ActiveSession>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT s.id, s.user_id, s.auth_method, s.ip_address, s.machine_name, s.started_at, s.last_activity, s.expires_at, s.is_active, u.username, u.display_name
              FROM active_sessions s JOIN app_users u ON u.id = s.user_id
              WHERE s.is_active = true ORDER BY s.last_activity DESC", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ActiveSession
            {
                Id = r.GetInt32(0), UserId = r.GetInt32(1), AuthMethod = r.GetString(2),
                IpAddress = r.IsDBNull(3) ? null : r.GetString(3),
                MachineName = r.IsDBNull(4) ? null : r.GetString(4),
                StartedAt = r.GetDateTime(5), LastActivity = r.GetDateTime(6),
                ExpiresAt = r.IsDBNull(7) ? null : r.GetDateTime(7),
                IsActive = r.GetBoolean(8),
                Username = r.GetString(9), DisplayName = r.IsDBNull(10) ? null : r.GetString(10)
            });
        return list;
    }

    public async Task CreateSessionAsync(int userId, string token, string authMethod)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO active_sessions (user_id, session_token, auth_method, machine_name)
              VALUES (@uid, @tok, @auth, @machine)", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("tok", token);
        cmd.Parameters.AddWithValue("auth", authMethod);
        cmd.Parameters.AddWithValue("machine", Environment.MachineName);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task EndSessionAsync(string token)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE active_sessions SET is_active = false WHERE session_token = @tok", conn);
        cmd.Parameters.AddWithValue("tok", token);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ForceEndSessionAsync(int sessionId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE active_sessions SET is_active = false WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", sessionId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ForceEndAllSessionsAsync(int userId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE active_sessions SET is_active = false WHERE user_id = @uid AND is_active = true", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        await cmd.ExecuteNonQueryAsync();
    }
}
