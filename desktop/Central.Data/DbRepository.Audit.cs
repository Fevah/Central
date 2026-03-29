using Npgsql;
using Central.Core.Services;

namespace Central.Data;

public partial class DbRepository
{
    public async Task InsertAuditEntryAsync(AuditEntry entry)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO audit_log (action, entity_type, entity_id, entity_name, username, user_id, details, before_json, after_json, created_at)
              VALUES (@a, @et, @eid, @en, @u, @uid, @d, @bj::jsonb, @aj::jsonb, @ts)", conn);
        cmd.Parameters.AddWithValue("a", entry.Action);
        cmd.Parameters.AddWithValue("et", entry.EntityType);
        cmd.Parameters.AddWithValue("eid", (object?)entry.EntityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("en", (object?)entry.EntityName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("u", (object?)entry.Username ?? DBNull.Value);
        cmd.Parameters.AddWithValue("uid", (object?)entry.UserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("d", (object?)entry.Details ?? DBNull.Value);
        cmd.Parameters.AddWithValue("bj", (object?)entry.BeforeJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("aj", (object?)entry.AfterJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ts", entry.Timestamp);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<AuditEntry>> GetAuditLogAsync(int limit = 200, string? entityType = null, string? username = null)
    {
        var list = new List<AuditEntry>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var where = new List<string>();
        if (!string.IsNullOrEmpty(entityType)) where.Add("entity_type = @et");
        if (!string.IsNullOrEmpty(username)) where.Add("username = @u");
        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

        await using var cmd = new NpgsqlCommand(
            $"SELECT action, entity_type, entity_id, entity_name, username, user_id, details, before_json::text, after_json::text, created_at FROM audit_log {whereClause} ORDER BY created_at DESC LIMIT {limit}", conn);
        if (!string.IsNullOrEmpty(entityType)) cmd.Parameters.AddWithValue("et", entityType);
        if (!string.IsNullOrEmpty(username)) cmd.Parameters.AddWithValue("u", username);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AuditEntry
            {
                Action = r.GetString(0), EntityType = r.GetString(1),
                EntityId = r.IsDBNull(2) ? null : r.GetString(2),
                EntityName = r.IsDBNull(3) ? null : r.GetString(3),
                Username = r.IsDBNull(4) ? null : r.GetString(4),
                UserId = r.IsDBNull(5) ? null : r.GetInt32(5),
                Details = r.IsDBNull(6) ? null : r.GetString(6),
                BeforeJson = r.IsDBNull(7) ? null : r.GetString(7),
                AfterJson = r.IsDBNull(8) ? null : r.GetString(8),
                Timestamp = r.GetDateTime(9)
            });
        return list;
    }

    // ── MFA Recovery Codes ──

    public async Task SaveRecoveryCodesAsync(int userId, List<string> codes)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Clear existing
        await using var del = new NpgsqlCommand("DELETE FROM mfa_recovery_codes WHERE user_id = @uid", conn);
        del.Parameters.AddWithValue("uid", userId);
        await del.ExecuteNonQueryAsync();

        // Insert new
        foreach (var code in codes)
        {
            var hash = Central.Core.Auth.PasswordHasher.Hash(code, userId.ToString());
            await using var ins = new NpgsqlCommand(
                "INSERT INTO mfa_recovery_codes (user_id, code_hash) VALUES (@uid, @hash)", conn);
            ins.Parameters.AddWithValue("uid", userId);
            ins.Parameters.AddWithValue("hash", hash);
            await ins.ExecuteNonQueryAsync();
        }
    }

    public async Task<bool> VerifyRecoveryCodeAsync(int userId, string code)
    {
        var hash = Central.Core.Auth.PasswordHasher.Hash(code, userId.ToString());
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE mfa_recovery_codes SET used_at = NOW() WHERE user_id = @uid AND code_hash = @hash AND used_at IS NULL RETURNING id", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("hash", hash);
        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }

    public async Task EnableMfaAsync(int userId, string encryptedSecret)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE app_users SET mfa_enabled = true, mfa_secret_enc = @s WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("s", encryptedSecret);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisableMfaAsync(int userId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE app_users SET mfa_enabled = false, mfa_secret_enc = NULL WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", userId);
        await cmd.ExecuteNonQueryAsync();
        // Also clear recovery codes
        await using var del = new NpgsqlCommand("DELETE FROM mfa_recovery_codes WHERE user_id = @uid", conn);
        del.Parameters.AddWithValue("uid", userId);
        await del.ExecuteNonQueryAsync();
    }
}
