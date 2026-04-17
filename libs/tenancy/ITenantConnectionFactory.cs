using Npgsql;

namespace Central.Tenancy;

/// <summary>
/// Creates DB connections scoped to the current tenant's schema or dedicated database.
/// Uses ITenantConnectionResolver when available to route enterprise (dedicated) tenants
/// to their own database.
/// </summary>
public interface ITenantConnectionFactory
{
    /// <summary>Open a connection scoped to the current tenant (schema for zoned, DB for dedicated).</summary>
    Task<NpgsqlConnection> OpenConnectionAsync();

    /// <summary>Open a connection to the platform schema (cross-tenant operations).</summary>
    Task<NpgsqlConnection> OpenPlatformConnectionAsync();

    /// <summary>The raw connection string to the default (shared) cluster.</summary>
    string ConnectionString { get; }
}

public class TenantConnectionFactory : ITenantConnectionFactory
{
    private readonly string _connectionString;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantConnectionResolver? _resolver;

    public TenantConnectionFactory(string connectionString, ITenantContext tenantContext)
        : this(connectionString, tenantContext, null) { }

    public TenantConnectionFactory(string connectionString, ITenantContext tenantContext, ITenantConnectionResolver? resolver)
    {
        _connectionString = connectionString;
        _tenantContext = tenantContext;
        _resolver = resolver;
    }

    public string ConnectionString => _connectionString;

    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        string effectiveConn = _connectionString;
        string effectiveSchema = _tenantContext.SchemaName;
        bool isDedicated = false;

        // When a resolver is available and a tenant is resolved, route to the mapped
        // database. Enterprise tenants get a dedicated DB; zoned tenants stay on shared.
        if (_resolver != null && _tenantContext.IsResolved && _tenantContext.TenantId != Guid.Empty)
        {
            try
            {
                var info = await _resolver.ResolveAsync(_tenantContext.TenantId);
                effectiveConn = info.ConnectionString;
                effectiveSchema = info.SchemaName;
                isDedicated = info.SizingModel == "dedicated";
            }
            catch { /* fall back to default */ }
        }

        var conn = new NpgsqlConnection(effectiveConn);
        await conn.OpenAsync();

        // For zoned tenants, set search_path to isolate their data by schema.
        // For dedicated tenants, the database itself is isolated — no search_path change needed.
        if (!isDedicated && !string.IsNullOrEmpty(effectiveSchema) && effectiveSchema != "public")
        {
            await using var cmd = new NpgsqlCommand(
                $"SET search_path TO {EscapeIdentifier(effectiveSchema)}, public", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        return conn;
    }

    public async Task<NpgsqlConnection> OpenPlatformConnectionAsync()
    {
        // Platform schema lives only on the shared cluster — never on tenant dedicated DBs
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("SET search_path TO central_platform, public", conn);
        await cmd.ExecuteNonQueryAsync();

        return conn;
    }

    /// <summary>Prevent SQL injection in schema names.</summary>
    private static string EscapeIdentifier(string identifier)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(identifier, @"^[a-zA-Z0-9_]+$"))
            return identifier;
        throw new ArgumentException($"Invalid schema name: {identifier}");
    }
}
