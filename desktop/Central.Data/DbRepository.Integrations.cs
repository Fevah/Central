using Npgsql;
using Central.Core.Models;
using Central.Core.Auth;

namespace Central.Data;

public partial class DbRepository
{
    public async Task<List<Integration>> GetIntegrationsAsync()
    {
        var list = new List<Integration>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, display_name, integration_type, base_url, is_enabled, config_json FROM integrations ORDER BY display_name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new Integration
            {
                Id = r.GetInt32(0), Name = r.GetString(1), DisplayName = r.GetString(2),
                IntegrationType = r.GetString(3), BaseUrl = r.IsDBNull(4) ? "" : r.GetString(4),
                IsEnabled = r.GetBoolean(5), ConfigJson = r.GetString(6)
            });
        return list;
    }

    public async Task UpsertIntegrationAsync(Integration i)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(i.Id > 0
            ? "UPDATE integrations SET display_name=@dn, base_url=@url, is_enabled=@en, config_json=@cfg::jsonb, updated_at=NOW() WHERE id=@id"
            : "INSERT INTO integrations (name, display_name, integration_type, base_url, is_enabled, config_json) VALUES (@n, @dn, @t, @url, @en, @cfg::jsonb) RETURNING id", conn);
        if (i.Id > 0) cmd.Parameters.AddWithValue("id", i.Id);
        cmd.Parameters.AddWithValue("n", i.Name);
        cmd.Parameters.AddWithValue("dn", i.DisplayName);
        cmd.Parameters.AddWithValue("t", i.IntegrationType);
        cmd.Parameters.AddWithValue("url", i.BaseUrl);
        cmd.Parameters.AddWithValue("en", i.IsEnabled);
        cmd.Parameters.AddWithValue("cfg", i.ConfigJson);
        if (i.Id == 0) i.Id = (int)(await cmd.ExecuteScalarAsync())!;
        else await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveIntegrationCredentialAsync(int integrationId, string key, string plainValue)
    {
        var encrypted = CredentialEncryptor.Encrypt(plainValue);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO integration_credentials (integration_id, key, value)
              VALUES (@iid, @k, @v)
              ON CONFLICT (integration_id, key) DO UPDATE SET value=@v, updated_at=NOW()", conn);
        cmd.Parameters.AddWithValue("iid", integrationId);
        cmd.Parameters.AddWithValue("k", key);
        cmd.Parameters.AddWithValue("v", encrypted);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string?> GetIntegrationCredentialAsync(int integrationId, string key)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT value FROM integration_credentials WHERE integration_id=@iid AND key=@k", conn);
        cmd.Parameters.AddWithValue("iid", integrationId);
        cmd.Parameters.AddWithValue("k", key);
        var result = await cmd.ExecuteScalarAsync() as string;
        if (result == null) return null;
        try { return CredentialEncryptor.Decrypt(result); }
        catch { return result; } // fallback: return as-is if not encrypted
    }

    public async Task<List<IntegrationLogEntry>> GetIntegrationLogAsync(int integrationId, int limit = 50)
    {
        var list = new List<IntegrationLogEntry>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, integration_id, action, status, message, duration_ms, created_at FROM integration_log WHERE integration_id=@iid ORDER BY created_at DESC LIMIT @lim", conn);
        cmd.Parameters.AddWithValue("iid", integrationId);
        cmd.Parameters.AddWithValue("lim", limit);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new IntegrationLogEntry
            {
                Id = r.GetInt32(0), IntegrationId = r.GetInt32(1), Action = r.GetString(2),
                Status = r.GetString(3), Message = r.IsDBNull(4) ? null : r.GetString(4),
                DurationMs = r.IsDBNull(5) ? null : r.GetInt32(5), CreatedAt = r.GetDateTime(6)
            });
        return list;
    }

    public async Task LogIntegrationAsync(int integrationId, string action, string status, string? message, int? durationMs = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO integration_log (integration_id, action, status, message, duration_ms) VALUES (@iid, @a, @s, @m, @d)", conn);
        cmd.Parameters.AddWithValue("iid", integrationId);
        cmd.Parameters.AddWithValue("a", action);
        cmd.Parameters.AddWithValue("s", status);
        cmd.Parameters.AddWithValue("m", (object?)message ?? DBNull.Value);
        cmd.Parameters.AddWithValue("d", (object?)durationMs ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Get an integration by name (e.g. "manageengine").</summary>
    public async Task<Integration?> GetIntegrationByNameAsync(string name)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, display_name, integration_type, base_url, is_enabled, config_json FROM integrations WHERE name=@n", conn);
        cmd.Parameters.AddWithValue("n", name);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new Integration
        {
            Id = r.GetInt32(0), Name = r.GetString(1), DisplayName = r.GetString(2),
            IntegrationType = r.GetString(3), BaseUrl = r.IsDBNull(4) ? "" : r.GetString(4),
            IsEnabled = r.GetBoolean(5), ConfigJson = r.GetString(6)
        };
    }
}
