using Central.Engine.Net.Servers;
using Central.Persistence.Net;
using Npgsql;

namespace Central.Tests.Integration;

/// <summary>
/// Phase 6e acceptance test — the "4-NIC fan-out" flow. Each test
/// provisions its own fixture (building + cores + ASN block + subnets)
/// and tears it down in <c>finally</c> so parallel runs don't collide.
///
/// Uses the Immunocore Server4NIC profile seeded in migration 094.
/// Shares the NetAllocations collection with the existing allocation
/// tests so we don't race on net.reservation_shelf.
///
/// Skip via env: SKIP_HEALTH_TESTS=1
/// </summary>
[Collection("NetAllocations")]
public class ServerCreationServiceTests
{
    private static readonly bool Skip = Environment.GetEnvironmentVariable("SKIP_HEALTH_TESTS") == "1";

    private static string Dsn => Environment.GetEnvironmentVariable("CENTRAL_DSN")
        ?? "Host=127.0.0.1;Port=5432;Database=central;Username=central;Password=central;Timeout=5";

    private static readonly Guid OrgId = new("00000000-0000-0000-0000-000000000000");

    [Fact]
    public async Task CreateWithFanOut_AllocatesAsnLoopbackAndFourNicsWithAlternatingSides()
    {
        if (Skip || !await CanReachDb()) return;

        const string tag = "srv-fanout";
        var fx = await SetUpFixtureAsync(tag);
        try
        {
            var svc = new ServerCreationService(Dsn);
            var profile = await new ServersRepository(Dsn).ListProfilesAsync(OrgId);
            var server4Nic = profile.Single(p => p.ProfileCode == "Server4NIC");

            var req = new ServerCreationRequest(
                OrganizationId:   OrgId,
                ServerProfileId:  server4Nic.Id,
                Hostname:         fx.Hostname,
                BuildingId:       fx.BuildingId,
                AsnBlockId:       fx.AsnBlockId,
                LoopbackSubnetId: fx.LoopbackSubnetId,
                NicSubnetId:      fx.NicSubnetId);

            var result = await svc.CreateWithFanOutAsync(req);

            // ASN allocated from the block we provided.
            Assert.NotNull(result.AsnAllocation);
            Assert.InRange(result.AsnAllocation!.Asn, 65800, 65810);

            // Loopback IP allocated from the loopback subnet.
            Assert.NotNull(result.LoopbackIp);
            Assert.StartsWith("198.51.50.", result.LoopbackIp!.Address);

            // Four NIC rows, alternating sides, all on the right cores.
            Assert.Equal(4, result.Nics.Count);
            Assert.Collection(result.Nics,
                n => { Assert.Equal(0, n.NicIndex); Assert.Equal(MlagSide.A, n.MlagSide); Assert.Equal(fx.SideACoreId, n.TargetDeviceId); },
                n => { Assert.Equal(1, n.NicIndex); Assert.Equal(MlagSide.B, n.MlagSide); Assert.Equal(fx.SideBCoreId, n.TargetDeviceId); },
                n => { Assert.Equal(2, n.NicIndex); Assert.Equal(MlagSide.A, n.MlagSide); Assert.Equal(fx.SideACoreId, n.TargetDeviceId); },
                n => { Assert.Equal(3, n.NicIndex); Assert.Equal(MlagSide.B, n.MlagSide); Assert.Equal(fx.SideBCoreId, n.TargetDeviceId); });

            // Each NIC got a distinct IP from the NIC subnet.
            var ipIds = result.Nics.Select(n => n.IpAddressId).ToList();
            Assert.Equal(4, ipIds.Distinct().Count());
            Assert.All(ipIds, id => Assert.NotNull(id));
        }
        finally { await TeardownFixtureAsync(fx, tag); }
    }

    [Fact]
    public async Task CreateWithFanOut_OptionalPiecesSkipCleanlyWhenNotProvided()
    {
        if (Skip || !await CanReachDb()) return;

        const string tag = "srv-minimal";
        var fx = await SetUpFixtureAsync(tag);
        try
        {
            var svc = new ServerCreationService(Dsn);
            var server4Nic = (await new ServersRepository(Dsn).ListProfilesAsync(OrgId))
                .Single(p => p.ProfileCode == "Server4NIC");

            // No ASN block, no loopback subnet, no NIC subnet. Server
            // and NIC rows still land, just without those FK bindings.
            var req = new ServerCreationRequest(
                OrganizationId:  OrgId,
                ServerProfileId: server4Nic.Id,
                Hostname:        fx.Hostname,
                BuildingId:      fx.BuildingId);

            var result = await svc.CreateWithFanOutAsync(req);

            Assert.Null(result.AsnAllocation);
            Assert.Null(result.LoopbackIp);
            Assert.Equal(4, result.Nics.Count);
            Assert.All(result.Nics, n => Assert.Null(n.IpAddressId));
            // Target devices still resolve from the building cores —
            // that part doesn't need an allocation.
            Assert.All(result.Nics, n => Assert.NotNull(n.TargetDeviceId));
        }
        finally { await TeardownFixtureAsync(fx, tag); }
    }

    [Fact]
    public async Task CreateWithFanOut_OneCoreBuildingPutsSideBAsNull()
    {
        if (Skip || !await CanReachDb()) return;

        const string tag = "srv-one-core";
        var fx = await SetUpFixtureAsync(tag, singleCore: true);
        try
        {
            var svc = new ServerCreationService(Dsn);
            var server4Nic = (await new ServersRepository(Dsn).ListProfilesAsync(OrgId))
                .Single(p => p.ProfileCode == "Server4NIC");

            var req = new ServerCreationRequest(
                OrganizationId:  OrgId,
                ServerProfileId: server4Nic.Id,
                Hostname:        fx.Hostname,
                BuildingId:      fx.BuildingId);

            var result = await svc.CreateWithFanOutAsync(req);

            // Side A NICs (indices 0 and 2) land on the one core;
            // side B NICs (1 and 3) have null target_device. Still a
            // legitimate state — operator sees an incomplete fan-out
            // in the grid.
            Assert.Equal(fx.SideACoreId, result.Nics[0].TargetDeviceId);
            Assert.Equal(fx.SideACoreId, result.Nics[2].TargetDeviceId);
            Assert.Null(result.Nics[1].TargetDeviceId);
            Assert.Null(result.Nics[3].TargetDeviceId);
        }
        finally { await TeardownFixtureAsync(fx, tag); }
    }

    [Fact]
    public async Task CreateWithFanOut_ProfileNotFound_Throws()
    {
        if (Skip || !await CanReachDb()) return;

        var svc = new ServerCreationService(Dsn);
        var req = new ServerCreationRequest(
            OrganizationId:  OrgId,
            ServerProfileId: Guid.NewGuid(),   // doesn't exist
            Hostname:        "nope",
            BuildingId:      null);

        await Assert.ThrowsAsync<ServerProfileNotFoundException>(() =>
            svc.CreateWithFanOutAsync(req));
    }

    // ─── Fixture ─────────────────────────────────────────────────────────

    private sealed record Fixture(
        string Hostname,
        Guid   BuildingId,
        Guid?  SideACoreId,
        Guid?  SideBCoreId,
        Guid   AsnPoolId,
        Guid   AsnBlockId,
        Guid   IpPoolId,
        Guid   LoopbackSubnetId,
        Guid   NicSubnetId);

    /// <summary>
    /// Per-test CIDR bands so parallel test-class runs (or orphaned
    /// leftovers from earlier failed runs) don't collide on the GIST
    /// EXCLUDE constraint. Three tests, three octet-3 pairs.
    /// </summary>
    private static readonly Dictionary<string, (string lo, string nic)> CidrBands = new()
    {
        ["srv-fanout"]   = ("198.51.50.0/24",  "198.51.60.0/24"),
        ["srv-minimal"]  = ("198.51.70.0/24",  "198.51.80.0/24"),
        ["srv-one-core"] = ("198.51.90.0/24",  "198.51.100.0/24"),
    };

    private static async Task<Fixture> SetUpFixtureAsync(string tag, bool singleCore = false)
    {
        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();

        // Defensive pre-cleanup: if a prior run of this exact tag crashed
        // mid-setup, its subnets linger and poison the next run via the
        // GIST EXCLUDE. Remove anything matching this tag's fingerprint.
        await PurgeByTagAsync(conn, tag);

        // region_code is varchar(8), site_code varchar(32), building_code
        // varchar(32). Keep the region short; site + building can take
        // the full tag.
        var shortTag = Math.Abs(tag.GetHashCode()).ToString("X")[..6];   // 6-char hex, fits in 8
        var regionId = await GetOrCreateRegionAsync(conn, "T" + shortTag, "Test region " + tag);
        var siteId   = await GetOrCreateSiteAsync(conn, regionId, "MP-" + tag, "MP test");
        var bld      = await InsertBuildingAsync(conn, siteId, tag.ToUpper() + "-B01", "Building for " + tag);

        // Cores — one or two depending on the test.
        var coreRoleId = await GetCoreRoleIdAsync(conn);
        var sideA = await InsertDeviceAsync(conn, bld, coreRoleId, $"{tag}-CORE01");
        Guid? sideB = singleCore ? null : await InsertDeviceAsync(conn, bld, coreRoleId, $"{tag}-CORE02");

        // ASN pool + block.
        var asnPoolId  = await InsertAsnPoolAsync(conn, tag, first: 65700, last: 65899);
        var asnBlockId = await InsertAsnBlockAsync(conn, asnPoolId, tag + "-blk", first: 65800, last: 65810);

        // IP pool + loopback + NIC subnet, bands per test.
        var band           = CidrBands[tag];
        var ipPoolId       = await InsertIpPoolAsync(conn, tag, cidr: "198.51.0.0/16");
        var loopbackSubId  = await InsertSubnetAsync(conn, ipPoolId, tag + "-lo",  band.lo);
        var nicSubId       = await InsertSubnetAsync(conn, ipPoolId, tag + "-nic", band.nic);

        return new Fixture(
            Hostname:         $"{tag.ToUpper()}-SRV01",
            BuildingId:       bld,
            SideACoreId:      sideA,
            SideBCoreId:      sideB,
            AsnPoolId:        asnPoolId,
            AsnBlockId:       asnBlockId,
            IpPoolId:         ipPoolId,
            LoopbackSubnetId: loopbackSubId,
            NicSubnetId:      nicSubId);
    }

    private static async Task TeardownFixtureAsync(Fixture fx, string tag)
    {
        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();
        // Use the same purge the setup uses — it's exhaustive and
        // parameterised by tag so we don't re-derive the list of
        // codes in two places.
        await PurgeByTagAsync(conn, tag);
    }

    /// <summary>
    /// Remove any leftover rows from a prior crashed run of this tag.
    /// Order matters — FK chain from bottom up.
    /// </summary>
    private static async Task PurgeByTagAsync(NpgsqlConnection conn, string tag)
    {
        await using var cmd = new NpgsqlCommand(@"
            DELETE FROM net.server_nic WHERE server_id IN
                (SELECT s.id FROM net.server s WHERE s.hostname = @host);
            DELETE FROM net.server WHERE hostname = @host;
            DELETE FROM net.ip_address WHERE subnet_id IN
                (SELECT id FROM net.subnet WHERE subnet_code IN (@slo, @snic));
            DELETE FROM net.subnet  WHERE subnet_code IN (@slo, @snic);
            DELETE FROM net.ip_pool WHERE pool_code = @ippool;
            DELETE FROM net.asn_allocation WHERE block_id IN
                (SELECT id FROM net.asn_block WHERE block_code = @ablk);
            DELETE FROM net.asn_block WHERE block_code = @ablk;
            DELETE FROM net.asn_pool  WHERE pool_code  = @apool;
            DELETE FROM net.device    WHERE hostname IN (@coreA, @coreB);
            DELETE FROM net.building  WHERE building_code = @bld;
            DELETE FROM net.site      WHERE site_code = @site;
            ", conn);
        cmd.Parameters.AddWithValue("host",  tag.ToUpper() + "-SRV01");
        cmd.Parameters.AddWithValue("slo",   tag + "-lo");
        cmd.Parameters.AddWithValue("snic",  tag + "-nic");
        cmd.Parameters.AddWithValue("ippool", tag + "-ip");
        cmd.Parameters.AddWithValue("ablk",  tag + "-blk");
        cmd.Parameters.AddWithValue("apool", tag + "-asn");
        cmd.Parameters.AddWithValue("coreA", $"{tag}-CORE01");
        cmd.Parameters.AddWithValue("coreB", $"{tag}-CORE02");
        cmd.Parameters.AddWithValue("bld",   tag.ToUpper() + "-B01");
        cmd.Parameters.AddWithValue("site",  "MP-" + tag);
        await cmd.ExecuteNonQueryAsync();
    }

    // ─── Fixture helpers ─────────────────────────────────────────────────

    private static async Task<Guid> GetOrCreateRegionAsync(NpgsqlConnection conn, string code, string name)
    {
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO net.region (organization_id, region_code, display_name)
              VALUES (@org, @code, @name) RETURNING id", conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        cmd.Parameters.AddWithValue("code", code);
        cmd.Parameters.AddWithValue("name", name);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> GetOrCreateSiteAsync(NpgsqlConnection conn, Guid region, string code, string name)
    {
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO net.site (organization_id, region_id, site_code, display_name)
              VALUES (@org, @r, @code, @name) RETURNING id", conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        cmd.Parameters.AddWithValue("r", region);
        cmd.Parameters.AddWithValue("code", code);
        cmd.Parameters.AddWithValue("name", name);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> InsertBuildingAsync(NpgsqlConnection conn, Guid siteId, string code, string name)
    {
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO net.building (organization_id, site_id, building_code, display_name)
              VALUES (@org, @s, @code, @name) RETURNING id", conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        cmd.Parameters.AddWithValue("s", siteId);
        cmd.Parameters.AddWithValue("code", code);
        cmd.Parameters.AddWithValue("name", name);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> GetCoreRoleIdAsync(NpgsqlConnection conn)
    {
        await using var cmd = new NpgsqlCommand(
            @"SELECT id FROM net.device_role WHERE organization_id=@org AND role_code='Core'", conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> InsertDeviceAsync(NpgsqlConnection conn, Guid buildingId, Guid roleId, string hostname)
    {
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO net.device (organization_id, device_role_id, building_id, hostname, status)
              VALUES (@org, @r, @b, @h, 'Active') RETURNING id", conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        cmd.Parameters.AddWithValue("r", roleId);
        cmd.Parameters.AddWithValue("b", buildingId);
        cmd.Parameters.AddWithValue("h", hostname);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> InsertAsnPoolAsync(NpgsqlConnection conn, string tag, long first, long last)
    {
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO net.asn_pool (organization_id, pool_code, display_name, asn_first, asn_last, asn_kind)
              VALUES (@org, @c, @c, @f, @l, 'Private2') RETURNING id", conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        cmd.Parameters.AddWithValue("c", tag + "-asn");
        cmd.Parameters.AddWithValue("f", first);
        cmd.Parameters.AddWithValue("l", last);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> InsertAsnBlockAsync(NpgsqlConnection conn, Guid poolId, string code, long first, long last)
    {
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO net.asn_block (organization_id, pool_id, block_code, display_name, asn_first, asn_last, scope_level)
              VALUES (@org, @p, @c, @c, @f, @l, 'Free') RETURNING id", conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        cmd.Parameters.AddWithValue("p", poolId);
        cmd.Parameters.AddWithValue("c", code);
        cmd.Parameters.AddWithValue("f", first);
        cmd.Parameters.AddWithValue("l", last);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> InsertIpPoolAsync(NpgsqlConnection conn, string tag, string cidr)
    {
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO net.ip_pool (organization_id, pool_code, display_name, network, address_family)
              VALUES (@org, @c, @c, @net::cidr, 'v4') RETURNING id", conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        cmd.Parameters.AddWithValue("c", tag + "-ip");
        cmd.Parameters.AddWithValue("net", cidr);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> InsertSubnetAsync(NpgsqlConnection conn, Guid poolId, string code, string cidr)
    {
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO net.subnet (organization_id, pool_id, subnet_code, display_name, network, scope_level)
              VALUES (@org, @p, @c, @c, @n::cidr, 'Free') RETURNING id", conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        cmd.Parameters.AddWithValue("p", poolId);
        cmd.Parameters.AddWithValue("c", code);
        cmd.Parameters.AddWithValue("n", cidr);
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
