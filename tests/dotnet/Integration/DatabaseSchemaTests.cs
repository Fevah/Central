using Central.Persistence;
using Npgsql;

namespace Central.Tests.Integration;

/// <summary>
/// Catches drift between what the desktop app expects
/// (<see cref="StartupHealthCheck"/>'s RequiredTables list) and what
/// the local PostgreSQL actually has. If this test fails, it means
/// a migration was renumbered, lost, or never applied to local dev —
/// exactly the class of bug the WPF app hits at startup but normal
/// unit tests never see.
///
/// Skip via env: SKIP_HEALTH_TESTS=1
/// </summary>
public class DatabaseSchemaTests
{
    private static readonly bool Skip = Environment.GetEnvironmentVariable("SKIP_HEALTH_TESTS") == "1";

    private static string Dsn => Environment.GetEnvironmentVariable("CENTRAL_DSN")
        ?? "Host=127.0.0.1;Port=5432;Database=central;Username=central;Password=central;Timeout=5";

    [Fact]
    public async Task StartupHealthCheck_ReportsHealthy()
    {
        if (Skip) return;
        if (!await CanReachDb()) return;   // ServiceHealthTests covers reachability separately

        var result = await StartupHealthCheck.CheckAsync(Dsn);

        Assert.True(result.IsHealthy,
            $"Schema drift — missing tables: {string.Join(", ", result.MissingTables)}. " +
            $"Apply pending migrations in db/migrations/ against {Dsn}.");
    }

    [Fact]
    public async Task StartupHealthCheck_SurfacesConnectionFailureCleanly()
    {
        // Even when the DB is unreachable, CheckAsync must return a
        // result (not throw) so startup can log and continue.
        var badDsn = "Host=127.0.0.1;Port=1;Database=nope;Username=nobody;Password=x;Timeout=1";

        var result = await StartupHealthCheck.CheckAsync(badDsn);

        Assert.False(result.IsHealthy);
        Assert.NotEmpty(result.Warnings);
    }

    /// <summary>
    /// Catches the class of drift that StartupHealthCheck misses: tables
    /// exist but queries reference columns that were never created.
    /// That's what caused "user corys not found" — app_users existed but
    /// the login query referenced password_changed_at / mfa_secret_enc
    /// which no migration defined.
    ///
    /// Each (table, column) pair below is read by a hot path at startup
    /// or login. If it's missing, the query throws, the repo catches it,
    /// and the user sees a misleading "not found" / empty state.
    /// </summary>
    [Theory]
    [InlineData("app_users", "password_changed_at")]
    [InlineData("app_users", "mfa_secret_enc")]
    [InlineData("app_users", "mfa_enabled")]
    [InlineData("app_users", "user_type")]
    [InlineData("app_users", "email")]
    [InlineData("identity_providers", "provider_type")]
    [InlineData("identity_providers", "config_json")]
    [InlineData("sync_configs", "agent_type")]
    [InlineData("sync_configs", "config_json")]
    [InlineData("sync_entity_maps", "sync_config_id")]
    [InlineData("sync_field_maps", "entity_map_id")]
    [InlineData("panel_customizations", "setting_json")]
    [InlineData("auth_events", "event_type")]
    public async Task CriticalColumn_Exists(string table, string column)
    {
        if (Skip) return;
        if (!await CanReachDb()) return;

        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM information_schema.columns " +
            "WHERE table_schema='public' AND table_name=@t AND column_name=@c)", conn);
        cmd.Parameters.AddWithValue("t", table);
        cmd.Parameters.AddWithValue("c", column);
        var exists = (bool)(await cmd.ExecuteScalarAsync())!;

        Assert.True(exists,
            $"Column drift — {table}.{column} missing. A query reads it but no " +
            $"migration creates it. Apply the migration that defines {column}.");
    }

    private static async Task<bool> CanReachDb()
    {
        try
        {
            await using var conn = new NpgsqlConnection(Dsn);
            await conn.OpenAsync();
            return true;
        }
        catch { return false; }
    }
}
