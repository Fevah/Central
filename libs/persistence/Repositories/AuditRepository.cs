using Npgsql;
using NpgsqlTypes;
using Central.Engine.Auth;
using Central.Engine.Services;

namespace Central.Persistence.Repositories;

/// <summary>
/// Append-only audit log repository.
/// Writes to audit_log table — never updates or deletes.
/// </summary>
public class AuditRepository : RepositoryBase, IAuditService
{
    public AuditRepository(string dsn) : base(dsn) { }

    public async Task LogAsync(string category, int entityId, string action,
        Dictionary<string, object?>? oldValue = null,
        Dictionary<string, object?>? newValue = null,
        string? summary = null)
    {
        await LogInternalAsync(category, entityId.ToString(), action, summary,
            oldValue != null ? System.Text.Json.JsonSerializer.Serialize(oldValue) : null,
            newValue != null ? System.Text.Json.JsonSerializer.Serialize(newValue) : null);
    }

    public async Task LogAsync(string category, string entityId, string action,
        string? summary = null)
    {
        await LogInternalAsync(category, entityId, action, summary, null, null);
    }

    private async Task LogInternalAsync(string category, string entityId, string action,
        string? summary, string? oldJson, string? newJson)
    {
        await SafeWriteAsync(async conn =>
        {
            // Check if audit_log table exists
            await using var check = new NpgsqlCommand(
                "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'audit_log')", conn);
            if (!(bool)(await check.ExecuteScalarAsync())!) return;

            var user = AuthContext.Instance.CurrentUser;
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO audit_log (username, category, entity_id, action, summary, old_value, new_value)
                VALUES (@username, @category, @entity_id, @action, @summary, @old_value, @new_value)", conn);

            cmd.Parameters.AddWithValue("username", user?.Username ?? "system");
            cmd.Parameters.AddWithValue("category", category);
            cmd.Parameters.AddWithValue("entity_id", entityId);
            cmd.Parameters.AddWithValue("action", action);
            cmd.Parameters.AddWithValue("summary", (object?)summary ?? DBNull.Value);
            cmd.Parameters.Add(new NpgsqlParameter("old_value", NpgsqlDbType.Jsonb)
                { Value = (object?)oldJson ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("new_value", NpgsqlDbType.Jsonb)
                { Value = (object?)newJson ?? DBNull.Value });

            await cmd.ExecuteNonQueryAsync();
        }, "AuditLog");
    }
}
