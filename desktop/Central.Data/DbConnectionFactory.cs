using Npgsql;

namespace Central.Data;

/// <summary>
/// Creates Npgsql connections from DSN. Shared by API and Desktop.
/// </summary>
public class DbConnectionFactory
{
    private readonly string _dsn;

    public DbConnectionFactory(string dsn) => _dsn = dsn;

    /// <summary>The raw connection string (needed for LISTEN/NOTIFY and PermissionRepository).</summary>
    public string ConnectionString => _dsn;

    public NpgsqlConnection CreateConnection() => new(_dsn);

    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var conn = new NpgsqlConnection(_dsn);
        await conn.OpenAsync();
        return conn;
    }
}
