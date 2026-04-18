using Npgsql;

namespace Central.Tests.Integration;

/// <summary>
/// Phase 5f — data-parity check between the three legacy link tables
/// (public.p2p_links, b2b_links, fw_links) and the unified
/// net.link + net.link_endpoint pair. Every active legacy row must
/// have exactly one matching net.link row (joined via
/// legacy_link_kind + legacy_link_id), carrying the same link_code,
/// vlan, subnet, and endpoint strings.
///
/// These tests are the safety net the plan flagged: "generated
/// configs identical to pre-migration output byte-for-byte". They run
/// in SQL, not by regenerating configs, because regenerating them
/// from two different sides would add its own failure mode. The
/// invariant we actually care about — "the underlying data is
/// preserved" — is checkable directly.
///
/// Skip via env: SKIP_HEALTH_TESTS=1
/// </summary>
public class LinkImportParityTests
{
    private static readonly bool Skip = Environment.GetEnvironmentVariable("SKIP_HEALTH_TESTS") == "1";

    private static string Dsn => Environment.GetEnvironmentVariable("CENTRAL_DSN")
        ?? "Host=127.0.0.1;Port=5432;Database=central;Username=central;Password=central;Timeout=5";

    private static readonly Guid OrgId = new("00000000-0000-0000-0000-000000000000");

    [Fact]
    public async Task EveryActiveLegacyLink_HasMatchingNetLink()
    {
        if (Skip || !await CanReachDb()) return;

        // Active rows on the legacy side (is_deleted IS NOT TRUE) must
        // land on net.link exactly once. Full-outer-join counts would
        // catch both orphans in either direction.
        var (legacy, mirrored, orphansLegacy, orphansNet) = await FetchMatchSummary();

        Assert.Equal(legacy, mirrored);
        Assert.Equal(0, orphansLegacy);
        Assert.Equal(0, orphansNet);
    }

    [Theory]
    [InlineData("p2p")]
    [InlineData("b2b")]
    [InlineData("fw")]
    public async Task LinkCode_MatchesLegacyLinkId(string kind)
    {
        if (Skip || !await CanReachDb()) return;

        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();

        // If link_code doesn't equal legacy.link_id, generated configs
        // pointing to that name would drift. This is the key string-
        // level parity invariant.
        var sql = kind switch
        {
            "p2p" => @"SELECT COUNT(*) FROM public.p2p_links p
                       JOIN net.link n ON n.legacy_link_kind='p2p' AND n.legacy_link_id=p.id
                      WHERE n.organization_id=@org AND NOT COALESCE(p.is_deleted,false)
                        AND n.link_code <> p.link_id",
            "b2b" => @"SELECT COUNT(*) FROM public.b2b_links p
                       JOIN net.link n ON n.legacy_link_kind='b2b' AND n.legacy_link_id=p.id
                      WHERE n.organization_id=@org AND NOT COALESCE(p.is_deleted,false)
                        AND n.link_code <> p.link_id",
            "fw"  => @"SELECT COUNT(*) FROM public.fw_links p
                       JOIN net.link n ON n.legacy_link_kind='fw' AND n.legacy_link_id=p.id
                      WHERE n.organization_id=@org AND NOT COALESCE(p.is_deleted,false)
                        AND n.link_code <> p.link_id",
            _ => throw new ArgumentException(kind)
        };

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        var mismatches = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(0, mismatches);
    }

    [Theory]
    [InlineData("p2p")]
    [InlineData("b2b")]
    [InlineData("fw")]
    public async Task EndpointInterfaceNames_MatchLegacyPorts(string kind)
    {
        if (Skip || !await CanReachDb()) return;

        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();

        // Every imported link has two endpoints. Endpoint 0 = A side,
        // 1 = B side. interface_name on each must match the legacy
        // port column it came from. Empty strings on both sides also
        // count as matching (the '' -> '' case).
        var sql = kind switch
        {
            "p2p" => @"
                SELECT COUNT(*) FROM public.p2p_links p
                  JOIN net.link n        ON n.legacy_link_kind='p2p' AND n.legacy_link_id=p.id
                  JOIN net.link_endpoint ea ON ea.link_id=n.id AND ea.endpoint_order=0
                  JOIN net.link_endpoint eb ON eb.link_id=n.id AND eb.endpoint_order=1
                 WHERE n.organization_id=@org AND NOT COALESCE(p.is_deleted,false)
                   AND (COALESCE(ea.interface_name,'') <> COALESCE(p.port_a,'')
                     OR COALESCE(eb.interface_name,'') <> COALESCE(p.port_b,''))",
            "b2b" => @"
                SELECT COUNT(*) FROM public.b2b_links p
                  JOIN net.link n        ON n.legacy_link_kind='b2b' AND n.legacy_link_id=p.id
                  JOIN net.link_endpoint ea ON ea.link_id=n.id AND ea.endpoint_order=0
                  JOIN net.link_endpoint eb ON eb.link_id=n.id AND eb.endpoint_order=1
                 WHERE n.organization_id=@org AND NOT COALESCE(p.is_deleted,false)
                   AND (COALESCE(ea.interface_name,'') <> COALESCE(p.port_a,'')
                     OR COALESCE(eb.interface_name,'') <> COALESCE(p.port_b,''))",
            "fw"  => @"
                SELECT COUNT(*) FROM public.fw_links p
                  JOIN net.link n        ON n.legacy_link_kind='fw' AND n.legacy_link_id=p.id
                  JOIN net.link_endpoint ea ON ea.link_id=n.id AND ea.endpoint_order=0
                  JOIN net.link_endpoint eb ON eb.link_id=n.id AND eb.endpoint_order=1
                 WHERE n.organization_id=@org AND NOT COALESCE(p.is_deleted,false)
                   AND (COALESCE(ea.interface_name,'') <> COALESCE(p.switch_port,'')
                     OR COALESCE(eb.interface_name,'') <> COALESCE(p.firewall_port,''))",
            _ => throw new ArgumentException(kind)
        };

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        var mismatches = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(0, mismatches);
    }

    [Fact]
    public async Task B2BConfigJson_CarriesTxRxMediaSpeed()
    {
        if (Skip || !await CanReachDb()) return;

        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();

        // For B2B rows that had any of the optical properties set on
        // the legacy side, those exact strings must be present in
        // config_json on the net.link row.
        const string sql = @"
            SELECT COUNT(*) FROM public.b2b_links p
              JOIN net.link n ON n.legacy_link_kind='b2b' AND n.legacy_link_id=p.id
             WHERE n.organization_id=@org AND NOT COALESCE(p.is_deleted,false)
               AND ((NULLIF(p.tx,'')    IS NOT NULL AND (n.config_json->>'tx')    IS DISTINCT FROM p.tx)
                 OR (NULLIF(p.rx,'')    IS NOT NULL AND (n.config_json->>'rx')    IS DISTINCT FROM p.rx)
                 OR (NULLIF(p.media,'') IS NOT NULL AND (n.config_json->>'media') IS DISTINCT FROM p.media)
                 OR (NULLIF(p.speed,'') IS NOT NULL AND (n.config_json->>'speed') IS DISTINCT FROM p.speed))";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        var mismatches = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(0, mismatches);
    }

    [Fact]
    public async Task ExactlyTwoEndpointsPerLink()
    {
        if (Skip || !await CanReachDb()) return;

        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();

        // DB UNIQUE (link_id, endpoint_order) can't enforce "exactly
        // two" on its own — it only prevents duplicates. Verify the
        // cardinality invariant here.
        const string sql = @"
            SELECT COUNT(*) FROM net.link l
             WHERE l.organization_id=@org AND l.deleted_at IS NULL
               AND (SELECT COUNT(*) FROM net.link_endpoint e
                     WHERE e.link_id=l.id AND e.deleted_at IS NULL) <> 2";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        var offenders = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(0, offenders);
    }

    // ─── Helpers ───────────────────────────────────────────────────

    private static async Task<(long legacy, long mirrored, long orphansLegacy, long orphansNet)>
        FetchMatchSummary()
    {
        await using var conn = new NpgsqlConnection(Dsn);
        await conn.OpenAsync();

        const string sql = @"
            WITH legacy AS (
                SELECT 'p2p' AS kind, id FROM public.p2p_links WHERE NOT COALESCE(is_deleted,false)
                UNION ALL
                SELECT 'b2b', id FROM public.b2b_links WHERE NOT COALESCE(is_deleted,false)
                UNION ALL
                SELECT 'fw',  id FROM public.fw_links  WHERE NOT COALESCE(is_deleted,false)
            ),
            net_ AS (
                SELECT legacy_link_kind AS kind, legacy_link_id AS id FROM net.link
                 WHERE organization_id = @org
                   AND legacy_link_kind IS NOT NULL
                   AND deleted_at IS NULL
            )
            SELECT
              (SELECT COUNT(*) FROM legacy)                                        AS legacy_cnt,
              (SELECT COUNT(*) FROM net_)                                          AS mirrored_cnt,
              (SELECT COUNT(*) FROM legacy l LEFT JOIN net_ n USING(kind,id) WHERE n.id IS NULL) AS orphans_legacy,
              (SELECT COUNT(*) FROM net_ n LEFT JOIN legacy l USING(kind,id) WHERE l.id IS NULL) AS orphans_net";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", OrgId);
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return (r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3));
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
