using Central.Engine.Net.Pools;
using Central.Persistence.Net;
using Npgsql;

namespace Central.Tests.Integration;

/// <summary>
/// IPv6 integration tests for <see cref="IpAllocationService"/>. Same
/// shape as <c>IpAllocationServiceTests</c> — each test builds and
/// tears down its own pool + subnet. IPv6 tests use the 2001:db8::/32
/// documentation prefix (RFC 3849) so they can never collide with
/// real imported data.
///
/// Skip via env: SKIP_HEALTH_TESTS=1
/// </summary>
[Collection("NetAllocations")]
public class IpAllocationServiceV6Tests
{
    private static readonly bool Skip = Environment.GetEnvironmentVariable("SKIP_HEALTH_TESTS") == "1";

    private static string Dsn => Environment.GetEnvironmentVariable("CENTRAL_DSN")
        ?? "Host=127.0.0.1;Port=5432;Database=central;Username=central;Password=central;Timeout=5";

    private static readonly Guid OrgId = new("00000000-0000-0000-0000-000000000000");

    [Fact]
    public async Task AllocateIp_V6_UsesNetworkAddressAsFirstCandidate()
    {
        if (Skip || !await CanReachDb()) return;

        // IPv6 subnet: every address is usable (no broadcast, no RFC 3021 twist).
        var (poolId, subnetId) = await SetupPoolAndSubnetAsync("v6-alloc", "2001:db8:1::/64");
        try
        {
            var svc = new IpAllocationService(Dsn);
            var a = await svc.AllocateNextIpAsync(subnetId, OrgId);
            var b = await svc.AllocateNextIpAsync(subnetId, OrgId);

            Assert.Equal("2001:db8:1::", a.Address);
            Assert.Equal("2001:db8:1::1", b.Address);
        }
        finally { await CleanupPoolAsync(poolId); }
    }

    [Fact]
    public async Task AllocateIp_V6_SkipsShelvedValues()
    {
        if (Skip || !await CanReachDb()) return;

        var (poolId, subnetId) = await SetupPoolAndSubnetAsync("v6-alloc-shelf", "2001:db8:2::/64");
        try
        {
            var ipSvc = new IpAllocationService(Dsn);
            var shelfSvc = new AllocationService(Dsn);

            // Park the network address so the next candidate has to jump.
            await shelfSvc.RetireAsync(OrgId, ShelfResourceType.Ip, "2001:db8:2::",
                TimeSpan.FromHours(1), reason: "v6 test");

            var a = await ipSvc.AllocateNextIpAsync(subnetId, OrgId);
            Assert.Equal("2001:db8:2::1", a.Address);
        }
        finally { await CleanupPoolAsync(poolId); }
    }

    [Fact]
    public async Task AllocateSubnet_V6_EmptyPool_StartsAtPoolAddress()
    {
        if (Skip || !await CanReachDb()) return;

        var poolId = await SetupPoolAsync("v6-carve-empty", "2001:db8:10::/48");
        try
        {
            var svc = new IpAllocationService(Dsn);
            var s = await svc.AllocateSubnetAsync(
                poolId, OrgId, prefixLength: 64,
                subnetCode: "V6-SUB-A", displayName: "v6 subnet A",
                scopeLevel: PoolScopeLevel.Free, scopeEntityId: null);
            Assert.Equal("2001:db8:10::/64", s.Network);
        }
        finally { await CleanupPoolAsync(poolId); }
    }

    [Fact]
    public async Task AllocateSubnet_V6_WalksPastExistingSubnets()
    {
        if (Skip || !await CanReachDb()) return;

        var poolId = await SetupPoolAsync("v6-carve-walk", "2001:db8:20::/48");
        try
        {
            var svc = new IpAllocationService(Dsn);
            var a = await svc.AllocateSubnetAsync(poolId, OrgId, 64, "A", "A", PoolScopeLevel.Free, null);
            var b = await svc.AllocateSubnetAsync(poolId, OrgId, 64, "B", "B", PoolScopeLevel.Free, null);
            var c = await svc.AllocateSubnetAsync(poolId, OrgId, 64, "C", "C", PoolScopeLevel.Free, null);

            Assert.Equal("2001:db8:20::/64",    a.Network);
            Assert.Equal("2001:db8:20:1::/64",  b.Network);
            Assert.Equal("2001:db8:20:2::/64",  c.Network);
        }
        finally { await CleanupPoolAsync(poolId); }
    }

    [Fact]
    public async Task AllocateSubnet_V6_RejectsPrefixLargerThanPool()
    {
        if (Skip || !await CanReachDb()) return;

        var poolId = await SetupPoolAsync("v6-carve-oversize", "2001:db8:30::/48");
        try
        {
            var svc = new IpAllocationService(Dsn);
            // Pool is /48 — asking for a /32 (bigger) inside it is nonsense.
            await Assert.ThrowsAsync<AllocationRangeException>(() =>
                svc.AllocateSubnetAsync(poolId, OrgId, 32, "too-big", "too-big",
                    PoolScopeLevel.Free, null));
        }
        finally { await CleanupPoolAsync(poolId); }
    }

    [Fact]
    public async Task FamilyDispatch_V4AndV6PoolsCoexist()
    {
        if (Skip || !await CanReachDb()) return;

        // Sanity: carving v6 out of a v6 pool doesn't trip the parser
        // on a v4 pool in the same tenant.
        var v4PoolId = await SetupPoolAsync("v4-coexist", "198.51.100.0/24");
        var v6PoolId = await SetupPoolAsync("v6-coexist", "2001:db8:40::/48");
        try
        {
            var svc = new IpAllocationService(Dsn);
            var v4 = await svc.AllocateSubnetAsync(v4PoolId, OrgId, 30, "v4A", "v4A",
                PoolScopeLevel.Free, null);
            var v6 = await svc.AllocateSubnetAsync(v6PoolId, OrgId, 64, "v6A", "v6A",
                PoolScopeLevel.Free, null);

            Assert.Equal("198.51.100.0/30", v4.Network);
            Assert.Equal("2001:db8:40::/64", v6.Network);
        }
        finally
        {
            await CleanupPoolAsync(v4PoolId);
            await CleanupPoolAsync(v6PoolId);
        }
    }

    // ─── Fixtures ─────────────────────────────────────────────────────────

    private static async Task<Guid> SetupPoolAsync(string code, string cidr)
    {
        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO net.ip_pool (organization_id, pool_code, display_name, network, address_family)
              VALUES (@org, @code, @code, @cidr::cidr, @af) RETURNING id", conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        cmd.Parameters.AddWithValue("code", code);
        cmd.Parameters.AddWithValue("cidr", cidr);
        cmd.Parameters.AddWithValue("af", cidr.Contains(':') ? "v6" : "v4");
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<(Guid poolId, Guid subnetId)> SetupPoolAndSubnetAsync(
        string code, string subnetCidr)
    {
        // Pool must be a supernet of the test subnet.
        var poolCidr = subnetCidr.Contains(':') ? "2001:db8::/32" : "192.0.0.0/16";
        var poolId = await SetupPoolAsync(code, poolCidr);

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
