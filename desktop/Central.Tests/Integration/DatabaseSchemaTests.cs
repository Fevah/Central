using Central.Data;
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
