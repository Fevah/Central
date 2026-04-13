using Npgsql;
using Central.Core.Models;

namespace Central.Data;

public partial class DbRepository
{
    public async Task<List<ApiKeyRecord>> GetApiKeysAsync()
    {
        var list = new List<ApiKeyRecord>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, role, is_active, created_at, last_used_at, use_count, expires_at FROM api_keys ORDER BY name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ApiKeyRecord
            {
                Id = r.GetInt32(0), Name = r.GetString(1), Role = r.GetString(2),
                IsActive = r.GetBoolean(3),
                CreatedAt = r.IsDBNull(4) ? null : r.GetDateTime(4),
                LastUsedAt = r.IsDBNull(5) ? null : r.GetDateTime(5),
                UseCount = r.GetInt32(6),
                ExpiresAt = r.IsDBNull(7) ? null : r.GetDateTime(7)
            });
        return list;
    }

    /// <summary>Create a new API key. Returns the raw key (shown once, never stored).</summary>
    public async Task<string> CreateApiKeyAsync(string name, string role, int createdBy, DateTime? expiresAt = null)
    {
        var rawKey = $"ck_{Guid.NewGuid():N}"; // ck_ prefix for easy identification
        var hash = HashApiKey(rawKey);

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO api_keys (name, key_hash, role, created_by, expires_at)
              VALUES (@n, @h, @r, @cb, @exp)", conn);
        cmd.Parameters.AddWithValue("n", name);
        cmd.Parameters.AddWithValue("h", hash);
        cmd.Parameters.AddWithValue("r", role);
        cmd.Parameters.AddWithValue("cb", createdBy);
        cmd.Parameters.AddWithValue("exp", (object?)expiresAt ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();

        return rawKey;
    }

    public async Task RevokeApiKeyAsync(int id)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("UPDATE api_keys SET is_active = false WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteApiKeyAsync(int id)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM api_keys WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string HashApiKey(string key)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(bytes);
    }
}
