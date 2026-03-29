using Npgsql;

namespace Central.Tenancy;

/// <summary>
/// Creates and migrates tenant schemas.
/// Each tenant gets an isolated PostgreSQL schema with the full application tables.
/// Migrations are replayed per schema on provisioning.
/// </summary>
public class TenantSchemaManager
{
    private readonly string _connectionString;

    public TenantSchemaManager(string connectionString) => _connectionString = connectionString;

    /// <summary>Create a new tenant schema and apply all migrations.</summary>
    public async Task ProvisionTenantAsync(string schemaName, string migrationsDir)
    {
        ValidateSchemaName(schemaName);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Create schema
        await using var createCmd = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS {schemaName}", conn);
        await createCmd.ExecuteNonQueryAsync();

        // Set search path to the new schema
        await using var pathCmd = new NpgsqlCommand($"SET search_path TO {schemaName}", conn);
        await pathCmd.ExecuteNonQueryAsync();

        // Apply all migration files in order
        if (Directory.Exists(migrationsDir))
        {
            foreach (var file in Directory.GetFiles(migrationsDir, "*.sql").OrderBy(f => Path.GetFileName(f)))
            {
                try
                {
                    var sql = await File.ReadAllTextAsync(file);
                    await using var migCmd = new NpgsqlCommand(sql, conn);
                    migCmd.CommandTimeout = 120;
                    await migCmd.ExecuteNonQueryAsync();
                }
                catch { /* migration may fail if tables already exist — continue */ }
            }
        }
    }

    /// <summary>Drop a tenant schema (use with extreme caution).</summary>
    public async Task DropTenantSchemaAsync(string schemaName)
    {
        ValidateSchemaName(schemaName);
        if (schemaName is "public" or "central_platform")
            throw new InvalidOperationException("Cannot drop system schemas");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {schemaName} CASCADE", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>List all tenant schemas.</summary>
    public async Task<List<string>> ListTenantSchemasAsync()
    {
        var schemas = new List<string>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'tenant_%' ORDER BY schema_name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) schemas.Add(r.GetString(0));
        return schemas;
    }

    /// <summary>Ensure the platform schema exists with cross-tenant tables.</summary>
    public async Task EnsurePlatformSchemaAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("CREATE SCHEMA IF NOT EXISTS central_platform", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void ValidateSchemaName(string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z][a-zA-Z0-9_]{1,62}$"))
            throw new ArgumentException($"Invalid schema name: {name}");
    }
}
