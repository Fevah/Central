using Central.Engine.Net.Pools;
using Central.Persistence.Net;
using Npgsql;

namespace Central.Tests.Integration;

/// <summary>
/// Live-DB integration tests for <see cref="IpAllocationService"/>.
/// Each test provisions its own pool / subnet fixtures and tears
/// them down in <c>finally</c> so parallel runs are independent.
///
/// Skip via env: SKIP_HEALTH_TESTS=1
/// </summary>
public class IpAllocationServiceTests
{
    private static readonly bool Skip = Environment.GetEnvironmentVariable("SKIP_HEALTH_TESTS") == "1";

    private static string Dsn => Environment.GetEnvironmentVariable("CENTRAL_DSN")
        ?? "Host=127.0.0.1;Port=5432;Database=central;Username=central;Password=central;Timeout=5";

    private static readonly Guid OrgId = new("00000000-0000-0000-0000-000000000000");

    // ─── IP allocation ────────────────────────────────────────────────────

    [Fact]
    public async Task AllocateIp_Slash24_SkipsNetworkAddressReturnsDotOne()
    {
        if (Skip || !await CanReachDb()) return;

        var (poolId, subnetId) = await SetupPoolAndSubnetAsync("ip-alloc-24", "192.0.2.0/24");
        try
        {
            var svc = new IpAllocationService(Dsn);
            var first = await svc.AllocateNextIpAsync(subnetId, OrgId);
            var second = await svc.AllocateNextIpAsync(subnetId, OrgId);

            Assert.Equal("192.0.2.1", first.Address);
            Assert.Equal("192.0.2.2", second.Address);
        }
        finally { await CleanupPoolAsync(poolId); }
    }

    [Fact]
    public async Task AllocateIp_Slash30_OnlyTwoUsableAddresses()
    {
        if (Skip || !await CanReachDb()) return;

        var (poolId, subnetId) = await SetupPoolAndSubnetAsync("ip-alloc-30", "192.0.2.0/30");
        try
        {
            var svc = new IpAllocationService(Dsn);

            var a = await svc.AllocateNextIpAsync(subnetId, OrgId);
            var b = await svc.AllocateNextIpAsync(subnetId, OrgId);

            Assert.Equal("192.0.2.1", a.Address);
            Assert.Equal("192.0.2.2", b.Address);

            // .3 is broadcast, .0 is network — no third address.
            await Assert.ThrowsAsync<PoolExhaustedException>(() =>
                svc.AllocateNextIpAsync(subnetId, OrgId));
        }
        finally { await CleanupPoolAsync(poolId); }
    }

    [Fact]
    public async Task AllocateIp_Slash31_UsesBothAddresses()
    {
        if (Skip || !await CanReachDb()) return;

        var (poolId, subnetId) = await SetupPoolAndSubnetAsync("ip-alloc-31", "192.0.2.0/31");
        try
        {
            var svc = new IpAllocationService(Dsn);

            var a = await svc.AllocateNextIpAsync(subnetId, OrgId);
            var b = await svc.AllocateNextIpAsync(subnetId, OrgId);

            // RFC 3021: no broadcast on /31, both halves usable.
            Assert.Equal("192.0.2.0", a.Address);
            Assert.Equal("192.0.2.1", b.Address);

            await Assert.ThrowsAsync<PoolExhaustedException>(() =>
                svc.AllocateNextIpAsync(subnetId, OrgId));
        }
        finally { await CleanupPoolAsync(poolId); }
    }

    [Fact]
    public async Task AllocateIp_SkipsShelvedValues()
    {
        if (Skip || !await CanReachDb()) return;

        var (poolId, subnetId) = await SetupPoolAndSubnetAsync("ip-alloc-shelf", "192.0.2.0/24");
        try
        {
            var svc = new IpAllocationService(Dsn);
            var allocSvc = new AllocationService(Dsn);

            // Shelve 192.0.2.1 (the next candidate)
            await allocSvc.RetireAsync(OrgId, ShelfResourceType.Ip, "192.0.2.1",
                TimeSpan.FromHours(1), reason: "test");

            var first = await svc.AllocateNextIpAsync(subnetId, OrgId);
            Assert.Equal("192.0.2.2", first.Address);
        }
        finally { await CleanupPoolAsync(poolId); }
    }

    // ─── Subnet carving ───────────────────────────────────────────────────

    [Fact]
    public async Task AllocateSubnet_EmptyPool_StartsAtPoolAddress()
    {
        if (Skip || !await CanReachDb()) return;

        var poolId = await SetupPoolAsync("sub-carve-empty", "192.0.2.0/24");
        try
        {
            var svc = new IpAllocationService(Dsn);

            var s = await svc.AllocateSubnetAsync(
                poolId, OrgId, prefixLength: 30,
                subnetCode: "SUB-A", displayName: "subnet A",
                scopeLevel: PoolScopeLevel.Free, scopeEntityId: null);

            Assert.Equal("192.0.2.0/30", s.Network);
        }
        finally { await CleanupPoolAsync(poolId); }
    }

    [Fact]
    public async Task AllocateSubnet_WalksPastExistingSubnets()
    {
        if (Skip || !await CanReachDb()) return;

        var poolId = await SetupPoolAsync("sub-carve-walk", "192.0.2.0/24");
        try
        {
            var svc = new IpAllocationService(Dsn);

            var a = await svc.AllocateSubnetAsync(poolId, OrgId, 30, "A", "A",
                PoolScopeLevel.Free, null);
            var b = await svc.AllocateSubnetAsync(poolId, OrgId, 30, "B", "B",
                PoolScopeLevel.Free, null);
            var c = await svc.AllocateSubnetAsync(poolId, OrgId, 30, "C", "C",
                PoolScopeLevel.Free, null);

            Assert.Equal("192.0.2.0/30", a.Network);
            Assert.Equal("192.0.2.4/30", b.Network);
            Assert.Equal("192.0.2.8/30", c.Network);
        }
        finally { await CleanupPoolAsync(poolId); }
    }

    [Fact]
    public async Task AllocateSubnet_RejectsPrefixLargerThanPool()
    {
        if (Skip || !await CanReachDb()) return;

        var poolId = await SetupPoolAsync("sub-carve-oversize", "192.0.2.0/28");
        try
        {
            var svc = new IpAllocationService(Dsn);
            // Pool is /28 — asking for a /16 inside it is nonsense.
            await Assert.ThrowsAsync<AllocationRangeException>(() =>
                svc.AllocateSubnetAsync(poolId, OrgId, 16, "too-big", "too-big",
                    PoolScopeLevel.Free, null));
        }
        finally { await CleanupPoolAsync(poolId); }
    }

    [Fact]
    public async Task AllocateSubnet_ExhaustsCleanly()
    {
        if (Skip || !await CanReachDb()) return;

        // /30 pool holds exactly one /30 — carving a second /30 should throw.
        var poolId = await SetupPoolAsync("sub-carve-exhaust", "192.0.2.0/30");
        try
        {
            var svc = new IpAllocationService(Dsn);

            var a = await svc.AllocateSubnetAsync(poolId, OrgId, 30, "A", "A",
                PoolScopeLevel.Free, null);
            Assert.Equal("192.0.2.0/30", a.Network);

            await Assert.ThrowsAsync<PoolExhaustedException>(() =>
                svc.AllocateSubnetAsync(poolId, OrgId, 30, "B", "B",
                    PoolScopeLevel.Free, null));
        }
        finally { await CleanupPoolAsync(poolId); }
    }

    // ─── Fixtures ─────────────────────────────────────────────────────────

    private static async Task<Guid> SetupPoolAsync(string code, string cidr)
    {
        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO net.ip_pool (organization_id, pool_code, display_name, network, address_family)
              VALUES (@org, @code, @code, @cidr::cidr, 'v4') RETURNING id", conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        cmd.Parameters.AddWithValue("code", code);
        cmd.Parameters.AddWithValue("cidr", cidr);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<(Guid poolId, Guid subnetId)> SetupPoolAndSubnetAsync(string code, string subnetCidr)
    {
        // Size the pool /16 around the subnet so the /24/30/31 tests
        // don't trip the pool-contains-subnet invariant.
        var poolId = await SetupPoolAsync(code, "192.0.0.0/16");

        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO net.subnet (organization_id, pool_id, subnet_code, display_name,
                                      network, scope_level)
              VALUES (@org, @pool, @code, @code, @cidr::cidr, 'Free') RETURNING id", conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        cmd.Parameters.AddWithValue("pool", poolId);
        cmd.Parameters.AddWithValue("code", code + "-sub");
        cmd.Parameters.AddWithValue("cidr", subnetCidr);
        var subnetId = (Guid)(await cmd.ExecuteScalarAsync())!;
        return (poolId, subnetId);
    }

    private static async Task CleanupPoolAsync(Guid poolId)
    {
        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            DELETE FROM net.reservation_shelf WHERE pool_id = @p OR resource_type IN ('ip','subnet');
            DELETE FROM net.ip_address
             WHERE subnet_id IN (SELECT id FROM net.subnet WHERE pool_id = @p);
            DELETE FROM net.subnet WHERE pool_id = @p;
            DELETE FROM net.ip_pool WHERE id = @p;", conn);
        cmd.Parameters.AddWithValue("p", poolId);
        await cmd.ExecuteNonQueryAsync();
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
