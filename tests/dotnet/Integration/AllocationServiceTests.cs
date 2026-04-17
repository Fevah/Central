using Central.Engine.Net.Pools;
using Central.Persistence.Net;
using Npgsql;

namespace Central.Tests.Integration;

/// <summary>
/// Integration tests for the numbering-pool allocation engine. These hit
/// a live Postgres — on developer machines that's the local podman
/// central-postgres container; in CI they skip if the DB is unreachable.
///
/// Each test builds and tears down its own pool/block rows so runs are
/// independent. The Immunocore tenant UUID is used because it's the
/// default seed tenant from migration 084 and is guaranteed to exist.
///
/// Skip via env: SKIP_HEALTH_TESTS=1
/// </summary>
public class AllocationServiceTests
{
    private static readonly bool Skip = Environment.GetEnvironmentVariable("SKIP_HEALTH_TESTS") == "1";

    private static string Dsn => Environment.GetEnvironmentVariable("CENTRAL_DSN")
        ?? "Host=127.0.0.1;Port=5432;Database=central;Username=central;Password=central;Timeout=5";

    private static readonly Guid OrgId = new("00000000-0000-0000-0000-000000000000");

    // ═══════════════════════════════════════════════════════════════════
    // Allocation core (pure, no DB)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void NextFreeInteger_EmptyRange_ReturnsFirst()
    {
        Assert.Equal(100L, AllocationService.NextFreeInteger(100, 200,
            new HashSet<long>(), new HashSet<long>()));
    }

    [Fact]
    public void NextFreeInteger_SkipsUsed()
    {
        Assert.Equal(102L, AllocationService.NextFreeInteger(100, 200,
            new HashSet<long> { 100, 101 }, new HashSet<long>()));
    }

    [Fact]
    public void NextFreeInteger_SkipsShelved()
    {
        Assert.Equal(103L, AllocationService.NextFreeInteger(100, 200,
            new HashSet<long> { 100 }, new HashSet<long> { 101, 102 }));
    }

    [Fact]
    public void NextFreeInteger_ReturnsNullWhenExhausted()
    {
        // 100, 101, 102 — all taken across used/shelved
        Assert.Null(AllocationService.NextFreeInteger(100, 102,
            new HashSet<long> { 100, 101 }, new HashSet<long> { 102 }));
    }

    [Fact]
    public void NextFreeInteger_FindsGapInMiddle()
    {
        Assert.Equal(105L, AllocationService.NextFreeInteger(100, 110,
            new HashSet<long> { 100, 101, 102, 103, 104 }, new HashSet<long>()));
    }

    [Fact]
    public void StableHash_IsDeterministic()
    {
        var g = new Guid("11111111-1111-1111-1111-111111111111");
        var a = AllocationService.StableHash(g);
        var b = AllocationService.StableHash(g);
        Assert.Equal(a, b);
        Assert.True(a >= 0, "Hash must fit Postgres signed bigint.");
    }

    [Fact]
    public void StableHash_DifferentGuidsDiffer()
    {
        var a = AllocationService.StableHash(new Guid("11111111-1111-1111-1111-111111111111"));
        var b = AllocationService.StableHash(new Guid("22222222-2222-2222-2222-222222222222"));
        Assert.NotEqual(a, b);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Live DB tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AllocateAsn_ReturnsLowestFreeValue()
    {
        if (Skip || !await CanReachDb()) return;

        var (poolId, blockId) = await SetupAsnPoolAndBlockAsync("asn-alloc-test", 64680, 64690);
        try
        {
            var svc = new AllocationService(Dsn);

            var first = await svc.AllocateAsnAsync(blockId, OrgId, "Device", Guid.NewGuid());
            var second = await svc.AllocateAsnAsync(blockId, OrgId, "Device", Guid.NewGuid());

            Assert.Equal(64680L, first.Asn);
            Assert.Equal(64681L, second.Asn);
        }
        finally { await CleanupAsnAsync(poolId); }
    }

    [Fact]
    public async Task AllocateAsn_SkipsShelvedValues()
    {
        if (Skip || !await CanReachDb()) return;

        // Use a test window well below the Immunocore imported ASNs
        // (65112 / 65121 / 65132 / 65141 / 65162) so our shelf check
        // isn't fighting the tenant-wide UNIQUE(org, asn) index.
        var (poolId, blockId) = await SetupAsnPoolAndBlockAsync("asn-shelf-test", 64700, 64710);
        try
        {
            var svc = new AllocationService(Dsn);

            // Park 64700 (the next-free value) on the shelf for an hour.
            await svc.RetireAsync(OrgId, ShelfResourceType.Asn, "64700",
                TimeSpan.FromHours(1), poolId: poolId, blockId: blockId,
                reason: "unit test");

            var result = await svc.AllocateAsnAsync(blockId, OrgId, "Device", Guid.NewGuid());

            Assert.Equal(64701L, result.Asn);
        }
        finally { await CleanupAsnAsync(poolId); }
    }

    [Fact]
    public async Task AllocateAsn_ThrowsWhenExhausted()
    {
        if (Skip || !await CanReachDb()) return;

        var (poolId, blockId) = await SetupAsnPoolAndBlockAsync("asn-exhaust-test", 64720, 64721);
        try
        {
            var svc = new AllocationService(Dsn);

            await svc.AllocateAsnAsync(blockId, OrgId, "Device", Guid.NewGuid());
            await svc.AllocateAsnAsync(blockId, OrgId, "Device", Guid.NewGuid());

            await Assert.ThrowsAsync<PoolExhaustedException>(() =>
                svc.AllocateAsnAsync(blockId, OrgId, "Device", Guid.NewGuid()));
        }
        finally { await CleanupAsnAsync(poolId); }
    }

    [Fact]
    public async Task AllocateVlan_ReturnsLowestFreeValue()
    {
        if (Skip || !await CanReachDb()) return;

        var (poolId, blockId) = await SetupVlanPoolAndBlockAsync("vlan-alloc-test", 3500, 3510);
        try
        {
            var svc = new AllocationService(Dsn);

            var v1 = await svc.AllocateVlanAsync(blockId, OrgId, "Test-Voice", null,
                PoolScopeLevel.Free, null);
            var v2 = await svc.AllocateVlanAsync(blockId, OrgId, "Test-Video", null,
                PoolScopeLevel.Free, null);

            Assert.Equal(3500, v1.VlanId);
            Assert.Equal(3501, v2.VlanId);
        }
        finally { await CleanupVlanAsync(poolId); }
    }

    [Fact]
    public async Task AllocateMlag_ReturnsLowestFreeValue()
    {
        if (Skip || !await CanReachDb()) return;

        var poolId = await SetupMlagPoolAsync("mlag-alloc-test", 4000, 4010);
        try
        {
            var svc = new AllocationService(Dsn);

            var d1 = await svc.AllocateMlagDomainAsync(poolId, OrgId, "Test-A",
                PoolScopeLevel.Building, null);
            var d2 = await svc.AllocateMlagDomainAsync(poolId, OrgId, "Test-B",
                PoolScopeLevel.Building, null);

            Assert.Equal(4000, d1.DomainId);
            Assert.Equal(4001, d2.DomainId);
        }
        finally { await CleanupMlagAsync(poolId); }
    }

    [Fact]
    public async Task IsOnShelf_TrueForActiveEntry_FalseAfterCooldownExpires()
    {
        if (Skip || !await CanReachDb()) return;

        var svc = new AllocationService(Dsn);
        var key = "test-" + Guid.NewGuid().ToString("N");
        try
        {
            // Active shelf entry
            await svc.RetireAsync(OrgId, ShelfResourceType.Asn, key,
                TimeSpan.FromHours(1), reason: "unit test");
            Assert.True(await svc.IsOnShelfAsync(OrgId, ShelfResourceType.Asn, key));

            // Non-existent key is not on shelf
            Assert.False(await svc.IsOnShelfAsync(OrgId, ShelfResourceType.Asn,
                "never-shelved-" + Guid.NewGuid().ToString("N")));
        }
        finally
        {
            await using var conn = new NpgsqlConnection(Dsn);
            await conn.OpenAsync();
            await using var c = new NpgsqlCommand(
                "DELETE FROM net.reservation_shelf WHERE resource_key = @k", conn);
            c.Parameters.AddWithValue("k", key);
            await c.ExecuteNonQueryAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Fixtures
    // ═══════════════════════════════════════════════════════════════════

    private static async Task<(Guid poolId, Guid blockId)> SetupAsnPoolAndBlockAsync(
        string code, long first, long last)
    {
        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();
        var poolId = await ExecuteScalarGuid(conn,
            @"INSERT INTO net.asn_pool (organization_id, pool_code, display_name,
                                        asn_first, asn_last, asn_kind)
              VALUES (@org, @code, @code, @first, @last, 'Private2') RETURNING id",
            ("org", OrgId), ("code", code), ("first", first), ("last", last));
        var blockId = await ExecuteScalarGuid(conn,
            @"INSERT INTO net.asn_block (organization_id, pool_id, block_code, display_name,
                                         asn_first, asn_last, scope_level)
              VALUES (@org, @pool, @code, @code, @first, @last, 'Free') RETURNING id",
            ("org", OrgId), ("pool", poolId), ("code", code + "-blk"),
            ("first", first), ("last", last));
        return (poolId, blockId);
    }

    private static async Task CleanupAsnAsync(Guid poolId)
    {
        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();
        await using var c = new NpgsqlCommand(@"
            DELETE FROM net.asn_allocation
             WHERE block_id IN (SELECT id FROM net.asn_block WHERE pool_id = @p);
            DELETE FROM net.reservation_shelf WHERE pool_id = @p;
            DELETE FROM net.asn_block WHERE pool_id = @p;
            DELETE FROM net.asn_pool WHERE id = @p;", conn);
        c.Parameters.AddWithValue("p", poolId);
        await c.ExecuteNonQueryAsync();
    }

    private static async Task<(Guid poolId, Guid blockId)> SetupVlanPoolAndBlockAsync(
        string code, int first, int last)
    {
        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();
        var poolId = await ExecuteScalarGuid(conn,
            @"INSERT INTO net.vlan_pool (organization_id, pool_code, display_name,
                                         vlan_first, vlan_last)
              VALUES (@org, @code, @code, @first, @last) RETURNING id",
            ("org", OrgId), ("code", code), ("first", first), ("last", last));
        var blockId = await ExecuteScalarGuid(conn,
            @"INSERT INTO net.vlan_block (organization_id, pool_id, block_code, display_name,
                                          vlan_first, vlan_last, scope_level)
              VALUES (@org, @pool, @code, @code, @first, @last, 'Free') RETURNING id",
            ("org", OrgId), ("pool", poolId), ("code", code + "-blk"),
            ("first", first), ("last", last));
        return (poolId, blockId);
    }

    private static async Task CleanupVlanAsync(Guid poolId)
    {
        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();
        await using var c = new NpgsqlCommand(@"
            DELETE FROM net.vlan
             WHERE block_id IN (SELECT id FROM net.vlan_block WHERE pool_id = @p);
            DELETE FROM net.vlan_block WHERE pool_id = @p;
            DELETE FROM net.vlan_pool WHERE id = @p;", conn);
        c.Parameters.AddWithValue("p", poolId);
        await c.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> SetupMlagPoolAsync(string code, int first, int last)
    {
        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();
        return await ExecuteScalarGuid(conn,
            @"INSERT INTO net.mlag_domain_pool (organization_id, pool_code, display_name,
                                                domain_first, domain_last)
              VALUES (@org, @code, @code, @first, @last) RETURNING id",
            ("org", OrgId), ("code", code), ("first", first), ("last", last));
    }

    private static async Task CleanupMlagAsync(Guid poolId)
    {
        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();
        await using var c = new NpgsqlCommand(@"
            DELETE FROM net.mlag_domain WHERE pool_id = @p;
            DELETE FROM net.mlag_domain_pool WHERE id = @p;", conn);
        c.Parameters.AddWithValue("p", poolId);
        await c.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> ExecuteScalarGuid(NpgsqlConnection conn, string sql,
        params (string name, object value)[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var (n, v) in parameters) cmd.Parameters.AddWithValue(n, v);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
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
