using Npgsql;

namespace Central.Persistence;

/// <summary>
/// Base class for all Npgsql repositories.
/// Provides connection factory and safe write helpers.
/// </summary>
public abstract class RepositoryBase
{
    private readonly string _dsn;

    protected RepositoryBase(string dsn)
    {
        _dsn = dsn ?? throw new ArgumentNullException(nameof(dsn));
    }

    protected NpgsqlConnection CreateConnection() => new(_dsn);

    /// <summary>
    /// Execute a write operation with error handling and optional retry.
    /// </summary>
    protected async Task<bool> SafeWriteAsync(Func<NpgsqlConnection, Task> action, string context = "")
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await action(conn);
            return true;
        }
        catch (NpgsqlException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DB] {context}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Execute a read query returning a single value.
    /// </summary>
    protected async Task<T?> ScalarAsync<T>(string sql, params NpgsqlParameter[] parameters)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var p in parameters) cmd.Parameters.Add(p);
        var result = await cmd.ExecuteScalarAsync();
        return result is DBNull || result == null ? default : (T)result;
    }
}
