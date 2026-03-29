using Npgsql;

namespace Central.Tenancy;

/// <summary>
/// Creates DB connections scoped to the current tenant's schema.
/// Replaces DbConnectionFactory for multi-tenant scenarios.
/// Sets search_path on every connection to isolate tenant data.
/// </summary>
public interface ITenantConnectionFactory
{
    /// <summary>Open a connection with search_path set to the current tenant's schema.</summary>
    Task<NpgsqlConnection> OpenConnectionAsync();

    /// <summary>Open a connection to the platform schema (cross-tenant operations).</summary>
    Task<NpgsqlConnection> OpenPlatformConnectionAsync();

    /// <summary>The raw connection string (without schema scoping).</summary>
    string ConnectionString { get; }
}

public class TenantConnectionFactory : ITenantConnectionFactory
{
    private readonly string _connectionString;
    private readonly ITenantContext _tenantContext;

    public TenantConnectionFactory(string connectionString, ITenantContext tenantContext)
    {
        _connectionString = connectionString;
        _tenantContext = tenantContext;
    }

    public string ConnectionString => _connectionString;

    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Set search_path to tenant schema — this is the tenant isolation boundary
        if (_tenantContext.IsResolved && _tenantContext.SchemaName != "public")
        {
            await using var cmd = new NpgsqlCommand(
                $"SET search_path TO {EscapeIdentifier(_tenantContext.SchemaName)}, public", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        return conn;
    }

    public async Task<NpgsqlConnection> OpenPlatformConnectionAsync()
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("SET search_path TO central_platform, public", conn);
        await cmd.ExecuteNonQueryAsync();

        return conn;
    }

    /// <summary>Prevent SQL injection in schema names.</summary>
    private static string EscapeIdentifier(string identifier)
    {
        // Only allow alphanumeric + underscore
        if (System.Text.RegularExpressions.Regex.IsMatch(identifier, @"^[a-zA-Z0-9_]+$"))
            return identifier;
        throw new ArgumentException($"Invalid schema name: {identifier}");
    }
}
