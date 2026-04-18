using Central.Engine.Models;
using Central.Persistence;
using Npgsql;

namespace Central.Tests.Integration;

/// <summary>
/// Phase 4f parity check — the legacy <c>GetSwitchesAsync</c> (reads
/// from <c>public.switches</c>) and the new
/// <c>GetSwitchesFromNetDeviceAsync</c> (reads from <c>net.device</c>
/// joined with building / loopback / ip_address) must return the same
/// SwitchRecord set for the same tenant. The dual-write trigger
/// (migration 090) is what makes this true — any drift here means the
/// trigger has a gap.
///
/// Skip via env: SKIP_HEALTH_TESTS=1
/// </summary>
public class DeviceReaderParityTests
{
    private static readonly bool Skip = Environment.GetEnvironmentVariable("SKIP_HEALTH_TESTS") == "1";

    private static string Dsn => Environment.GetEnvironmentVariable("CENTRAL_DSN")
        ?? "Host=127.0.0.1;Port=5432;Database=central;Username=central;Password=central;Timeout=5";

    [Fact]
    public async Task LegacyAndNewReaders_ReturnSameSwitchRecordsByHostname()
    {
        if (Skip || !await CanReachDb()) return;

        var repo = new DbRepository(Dsn);

        var legacy = await repo.GetSwitchesAsync();
        var modern = await repo.GetSwitchesFromNetDeviceAsync();

        Assert.Equal(legacy.Count, modern.Count);

        // Hostname is the natural key in both shapes; compare the
        // intersecting fields row by row.
        var lByName = legacy.ToDictionary(s => s.Hostname);
        var mByName = modern.ToDictionary(s => s.Hostname);

        Assert.Equal(lByName.Keys.OrderBy(k => k), mByName.Keys.OrderBy(k => k));

        foreach (var host in lByName.Keys)
        {
            var l = lByName[host];
            var m = mByName[host];
            Assert.Equal(l.Hostname, m.Hostname);
            Assert.Equal(l.Site, m.Site);
            Assert.Equal(l.ManagementIp, m.ManagementIp);
            Assert.Equal(l.LoopbackIp, m.LoopbackIp);
            Assert.Equal(l.HardwareModel, m.HardwareModel);
            // last_ping_* come from the same columns on both sides
            // (dual-write mirrors them), so values must match.
            Assert.Equal(l.LastPingOk, m.LastPingOk);
            Assert.Equal(l.LastSshOk, m.LastSshOk);
        }
    }

    [Fact]
    public async Task SiteFilter_WorksOnBothReaders()
    {
        if (Skip || !await CanReachDb()) return;

        var repo = new DbRepository(Dsn);

        var legacy = await repo.GetSwitchesAsync(new List<string> { "MEP-91" });
        var modern = await repo.GetSwitchesFromNetDeviceAsync(new List<string> { "MEP-91" });

        Assert.Equal(legacy.Count, modern.Count);
        Assert.All(legacy, s => Assert.Equal("MEP-91", s.Site));
        Assert.All(modern, s => Assert.Equal("MEP-91", s.Site));
    }

    [Fact]
    public async Task EmptySiteFilter_ReturnsNoRowsOnBothReaders()
    {
        if (Skip || !await CanReachDb()) return;

        // An explicitly-empty list means "user has no site access" —
        // both readers should emit zero rows rather than everything.
        var repo = new DbRepository(Dsn);

        var legacy = await repo.GetSwitchesAsync(new List<string>());
        var modern = await repo.GetSwitchesFromNetDeviceAsync(new List<string>());

        Assert.Empty(legacy);
        Assert.Empty(modern);
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
