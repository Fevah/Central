using Central.Engine.Net.Pools;
using Npgsql;

namespace Central.Persistence.Net;

/// <summary>
/// IPv4 address + subnet allocation. Sibling to
/// <see cref="AllocationService"/> (which handles integer-space
/// resources — ASN / VLAN / MLAG); split out because inet arithmetic
/// doesn't share code with the integer path.
///
/// <para>Same invariants, enforced the same way:</para>
/// <list type="bullet">
///   <item>Advisory-lock per container (subnet for IP alloc, pool for
///     subnet carving) so concurrent callers can't race.</item>
///   <item>Shelf cool-down applied — shelved IPs / CIDRs are skipped
///     until <c>available_after</c> has passed.</item>
///   <item>Range containment: returned IP is always inside the
///     subnet's host range; returned CIDR always fits inside the
///     pool.</item>
///   <item>No-overlap for subnets is the DB's job via the GIST
///     EXCLUDE constraint — this service picks the candidate, the DB
///     enforces the invariant.</item>
/// </list>
///
/// <para>IPv4 only for now. IPv6 carver lands with Phase 3g.</para>
/// </summary>
public class IpAllocationService
{
    private readonly string _dsn;
    public IpAllocationService(string dsn) => _dsn = dsn;

    // ═══════════════════════════════════════════════════════════════════
    // IP address (next free in subnet)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Allocate the lowest free IPv4 address inside
    /// <paramref name="subnetId"/>. For /30 and larger the network and
    /// broadcast addresses are always skipped; /31 and /32 use every
    /// bit of the range per RFC 3021.
    /// </summary>
    public async Task<IpAddress> AllocateNextIpAsync(
        Guid subnetId, Guid orgId,
        string? assignedToType = null, Guid? assignedToId = null,
        int? userId = null, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await AcquireLockAsync(conn, tx, subnetId, ct);

        var cidr = await FetchSubnetCidrStringAsync(conn, tx, subnetId, orgId, ct);
        var addrStr = IsV6(cidr)
            ? await PickNextIpV6Async(conn, tx, subnetId, orgId, cidr, ct)
            : await PickNextIpV4Async(conn, tx, subnetId, orgId, cidr, ct);
        const string sql = @"
            INSERT INTO net.ip_address
                (organization_id, subnet_id, address, assigned_to_type, assigned_to_id,
                 is_reserved, status, lock_state, created_by, updated_by)
            VALUES (@org, @subnet, @addr::inet, @type, @tid,
                    false, 'Active'::net.entity_status, 'Open'::net.lock_state, @uid, @uid)
            RETURNING id, assigned_at";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("org", orgId);
        cmd.Parameters.AddWithValue("subnet", subnetId);
        cmd.Parameters.AddWithValue("addr", addrStr);
        cmd.Parameters.AddWithValue("type", (object?)assignedToType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tid", (object?)assignedToId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        var result = new IpAddress
        {
            Id = r.GetGuid(0),
            OrganizationId = orgId,
            SubnetId = subnetId,
            Address = addrStr,
            AssignedToType = assignedToType,
            AssignedToId = assignedToId,
            IsReserved = false,
            AssignedAt = r.GetDateTime(1),
        };
        await r.CloseAsync();

        await tx.CommitAsync(ct);
        return result;
    }

    private static async Task<string> FetchSubnetCidrStringAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid subnetId, Guid orgId, CancellationToken ct)
    {
        const string sql = @"
            SELECT network::text
            FROM net.subnet
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("id", subnetId);
        cmd.Parameters.AddWithValue("org", orgId);
        var raw = await cmd.ExecuteScalarAsync(ct) as string
            ?? throw new AllocationContainerNotFoundException("subnet", subnetId);
        return raw;
    }

    private static bool IsV6(string cidr) => cidr.Contains(':');

    private static async Task<string> PickNextIpV4Async(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid subnetId, Guid orgId,
        string cidr, CancellationToken ct)
    {
        var (network, broadcast, prefix) = IpMath.ParseV4(cidr);
        var (first, last) = IpMath.HostRange(network, broadcast, prefix);

        var used = await FetchUsedIpsV4Async(conn, tx, subnetId, ct);
        var shelved = await FetchShelvedIpsV4Async(conn, tx, orgId, ct);

        for (var candidate = first; candidate <= last; candidate++)
        {
            if (used.Contains(candidate)) continue;
            if (shelved.Contains(candidate)) continue;
            return IpMath.ToIp(candidate);
        }
        throw new PoolExhaustedException("IP address", subnetId);
    }

    private static async Task<string> PickNextIpV6Async(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid subnetId, Guid orgId,
        string cidr, CancellationToken ct)
    {
        var (network, last, _) = IpMath6.ParseV6(cidr);
        var (first, endUsable) = IpMath6.HostRange(network, last);

        var used = await FetchUsedIpsV6Async(conn, tx, subnetId, ct);
        var shelved = await FetchShelvedIpsV6Async(conn, tx, orgId, ct);

        // IPv6 subnets are enormous (/64 == 2^64 addresses), so a naive
        // "walk from first to last" would churn forever. In practice the
        // used + shelved sets for a fresh subnet are tiny, so we sort
        // them and pick the first gap starting at `first`.
        var blocked = new SortedSet<UInt128>(used);
        foreach (var s in shelved) blocked.Add(s);

        var candidate = first;
        foreach (var b in blocked)
        {
            if (b < candidate) continue;
            if (b > candidate) return IpMath6.ToIp(candidate);
            // b == candidate — bump past and keep scanning.
            if (candidate == endUsable)
                throw new PoolExhaustedException("IPv6 address", subnetId);
            candidate++;
        }
        if (candidate > endUsable)
            throw new PoolExhaustedException("IPv6 address", subnetId);
        return IpMath6.ToIp(candidate);
    }

    private static async Task<HashSet<long>> FetchUsedIpsV4Async(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid subnetId, CancellationToken ct)
    {
        const string sql = @"
            SELECT address::text FROM net.ip_address
            WHERE subnet_id = @id AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("id", subnetId);
        var set = new HashSet<long>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var s = StripPrefix(r.GetString(0));
            if (!s.Contains(':')) set.Add(IpToLong(s));
        }
        return set;
    }

    private static async Task<HashSet<long>> FetchShelvedIpsV4Async(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid orgId, CancellationToken ct)
    {
        const string sql = @"
            SELECT resource_key FROM net.reservation_shelf
            WHERE organization_id = @org
              AND resource_type = 'ip'
              AND available_after > now()
              AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("org", orgId);
        var set = new HashSet<long>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var s = r.GetString(0);
            if (!s.Contains(':')) set.Add(IpToLong(s));
        }
        return set;
    }

    private static async Task<HashSet<UInt128>> FetchUsedIpsV6Async(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid subnetId, CancellationToken ct)
    {
        const string sql = @"
            SELECT address::text FROM net.ip_address
            WHERE subnet_id = @id AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("id", subnetId);
        var set = new HashSet<UInt128>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var s = StripPrefix(r.GetString(0));
            if (s.Contains(':')) set.Add(IpMath6.IpToUInt128(s));
        }
        return set;
    }

    private static async Task<HashSet<UInt128>> FetchShelvedIpsV6Async(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid orgId, CancellationToken ct)
    {
        const string sql = @"
            SELECT resource_key FROM net.reservation_shelf
            WHERE organization_id = @org
              AND resource_type = 'ip'
              AND available_after > now()
              AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("org", orgId);
        var set = new HashSet<UInt128>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var s = r.GetString(0);
            if (s.Contains(':')) set.Add(IpMath6.IpToUInt128(s));
        }
        return set;
    }

    private static string StripPrefix(string s)
    {
        var slash = s.IndexOf('/');
        return slash > 0 ? s[..slash] : s;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Subnet carving (next free /N in pool)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Carve a new subnet of <paramref name="prefixLength"/> bits out
    /// of <paramref name="poolId"/>. The service finds the lowest
    /// aligned candidate that doesn't overlap any existing subnet or
    /// shelved subnet CIDR, then inserts it. The DB's GIST EXCLUDE
    /// constraint is the last-line defence — if concurrent callers
    /// somehow race past the advisory lock, the insert fails with
    /// <c>exclusion_violation</c> rather than quietly producing
    /// overlapping subnets.
    /// </summary>
    public async Task<Subnet> AllocateSubnetAsync(
        Guid poolId, Guid orgId,
        int prefixLength,
        string subnetCode, string displayName,
        PoolScopeLevel scopeLevel, Guid? scopeEntityId,
        Guid? parentSubnetId = null,
        int? userId = null, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await AcquireLockAsync(conn, tx, poolId, ct);

        var poolCidr = await FetchPoolCidrStringAsync(conn, tx, poolId, orgId, ct);
        var cidr = IsV6(poolCidr)
            ? CarveV6(poolCidr, prefixLength, poolId,
                await FetchSubnetRangesInPoolV6Async(conn, tx, poolId, ct),
                await FetchShelvedSubnetRangesV6Async(conn, tx, orgId, ct))
            : CarveV4(poolCidr, prefixLength, poolId,
                await FetchSubnetRangesInPoolV4Async(conn, tx, poolId, ct),
                await FetchShelvedSubnetRangesV4Async(conn, tx, orgId, ct));
        const string sql = @"
            INSERT INTO net.subnet
                (organization_id, pool_id, parent_subnet_id, subnet_code, display_name,
                 network, scope_level, scope_entity_id,
                 status, lock_state, created_by, updated_by)
            VALUES (@org, @pool, @parent, @code, @name, @cidr::cidr, @scope, @sid,
                    'Active'::net.entity_status, 'Open'::net.lock_state, @uid, @uid)
            RETURNING id";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("org", orgId);
        cmd.Parameters.AddWithValue("pool", poolId);
        cmd.Parameters.AddWithValue("parent", (object?)parentSubnetId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("code", subnetCode);
        cmd.Parameters.AddWithValue("name", displayName);
        cmd.Parameters.AddWithValue("cidr", cidr);
        cmd.Parameters.AddWithValue("scope", scopeLevel.ToString());
        cmd.Parameters.AddWithValue("sid", (object?)scopeEntityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);

        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        await tx.CommitAsync(ct);
        return new Subnet
        {
            Id = id,
            OrganizationId = orgId,
            PoolId = poolId,
            ParentSubnetId = parentSubnetId,
            SubnetCode = subnetCode,
            DisplayName = displayName,
            Network = cidr,
            ScopeLevel = scopeLevel,
            ScopeEntityId = scopeEntityId,
        };
    }

    /// <summary>
    /// Gap-finder over a sorted "blocked" list. Walks the pool range
    /// in strides of <c>2^(32 - prefixLength)</c> bytes and returns
    /// the first stride that doesn't overlap any blocked range.
    /// </summary>
    internal static long? FindFreeAligned(long poolNetwork, long poolBroadcast,
        int prefixLength, IReadOnlyList<(long first, long last)> blockedSorted)
    {
        var stride = IpMath.BlockSize(prefixLength);
        var cursor = IpMath.AlignUp(poolNetwork, stride);
        var bi = 0;

        while (cursor + stride - 1 <= poolBroadcast)
        {
            var candidateLast = cursor + stride - 1;

            // Skip blocked ranges that end before the current cursor.
            while (bi < blockedSorted.Count && blockedSorted[bi].last < cursor)
                bi++;

            if (bi >= blockedSorted.Count)
                return cursor;                        // no more blockers, candidate is free

            var (bFirst, bLast) = blockedSorted[bi];
            if (candidateLast < bFirst)
                return cursor;                        // gap before the next blocker

            // Overlap — jump past it, realign, try again.
            cursor = IpMath.AlignUp(bLast + 1, stride);
        }
        return null;
    }

    private static async Task<string> FetchPoolCidrStringAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid poolId, Guid orgId, CancellationToken ct)
    {
        const string sql = @"
            SELECT network::text FROM net.ip_pool
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("id", poolId);
        cmd.Parameters.AddWithValue("org", orgId);
        return await cmd.ExecuteScalarAsync(ct) as string
            ?? throw new AllocationContainerNotFoundException("ip_pool", poolId);
    }

    private static string CarveV4(string poolCidr, int prefixLength, Guid poolId,
        List<(long first, long last)> existing,
        List<(long first, long last)> shelved)
    {
        var (poolNetwork, poolBroadcast, poolPrefix) = IpMath.ParseV4(poolCidr);
        if (prefixLength < poolPrefix || prefixLength > 32)
            throw new AllocationRangeException("subnet prefix", prefixLength, poolPrefix, 32);

        var blocked = new List<(long first, long last)>(existing.Count + shelved.Count);
        blocked.AddRange(existing);
        blocked.AddRange(shelved);
        blocked.Sort((a, b) => a.first.CompareTo(b.first));

        var candidate = FindFreeAligned(poolNetwork, poolBroadcast, prefixLength, blocked)
            ?? throw new PoolExhaustedException($"subnet /{prefixLength}", poolId);
        return IpMath.ToCidr(candidate, prefixLength);
    }

    private static string CarveV6(string poolCidr, int prefixLength, Guid poolId,
        List<(UInt128 first, UInt128 last)> existing,
        List<(UInt128 first, UInt128 last)> shelved)
    {
        var (poolNetwork, poolLast, poolPrefix) = IpMath6.ParseV6(poolCidr);
        if (prefixLength < poolPrefix || prefixLength > 128)
            throw new AllocationRangeException("subnet prefix", prefixLength, poolPrefix, 128);

        var blocked = new List<(UInt128 first, UInt128 last)>(existing.Count + shelved.Count);
        blocked.AddRange(existing);
        blocked.AddRange(shelved);
        blocked.Sort((a, b) => a.first.CompareTo(b.first));

        var candidate = FindFreeAlignedV6(poolNetwork, poolLast, prefixLength, blocked)
            ?? throw new PoolExhaustedException($"IPv6 subnet /{prefixLength}", poolId);
        return IpMath6.ToCidr(candidate, prefixLength);
    }

    /// <summary>
    /// IPv6 gap-finder. Same shape as <see cref="FindFreeAligned"/> but
    /// on <see cref="UInt128"/> — a /48 pool stride at /64 is 2^16
    /// candidates, still fits comfortably.
    /// </summary>
    internal static UInt128? FindFreeAlignedV6(UInt128 poolNetwork, UInt128 poolLast,
        int prefixLength, IReadOnlyList<(UInt128 first, UInt128 last)> blockedSorted)
    {
        var stride = IpMath6.BlockSize(prefixLength);
        if (stride == UInt128.Zero) return null;           // /0 makes no sense as a sub-alloc
        var cursor = IpMath6.AlignUp(poolNetwork, stride);
        var bi = 0;

        while (cursor + stride - UInt128.One <= poolLast)
        {
            var candidateLast = cursor + stride - UInt128.One;
            while (bi < blockedSorted.Count && blockedSorted[bi].last < cursor) bi++;
            if (bi >= blockedSorted.Count) return cursor;

            var (bFirst, bLast) = blockedSorted[bi];
            if (candidateLast < bFirst) return cursor;

            cursor = IpMath6.AlignUp(bLast + UInt128.One, stride);
        }
        return null;
    }

    private static async Task<List<(long first, long last)>> FetchSubnetRangesInPoolV4Async(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid poolId, CancellationToken ct)
    {
        const string sql = @"
            SELECT network::text FROM net.subnet
            WHERE pool_id = @id AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("id", poolId);
        var list = new List<(long first, long last)>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var cidr = r.GetString(0);
            if (!cidr.Contains(':'))
            {
                var (net, bcast, _) = IpMath.ParseV4(cidr);
                list.Add((net, bcast));
            }
        }
        return list;
    }

    private static async Task<List<(long first, long last)>> FetchShelvedSubnetRangesV4Async(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid orgId, CancellationToken ct)
    {
        const string sql = @"
            SELECT resource_key FROM net.reservation_shelf
            WHERE organization_id = @org
              AND resource_type = 'subnet'
              AND available_after > now()
              AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("org", orgId);
        var list = new List<(long first, long last)>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var cidr = r.GetString(0);
            if (cidr.Contains(':')) continue;
            try
            {
                var (net, bcast, _) = IpMath.ParseV4(cidr);
                list.Add((net, bcast));
            }
            catch (FormatException) { /* skip malformed */ }
        }
        return list;
    }

    private static async Task<List<(UInt128 first, UInt128 last)>> FetchSubnetRangesInPoolV6Async(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid poolId, CancellationToken ct)
    {
        const string sql = @"
            SELECT network::text FROM net.subnet
            WHERE pool_id = @id AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("id", poolId);
        var list = new List<(UInt128 first, UInt128 last)>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var cidr = r.GetString(0);
            if (cidr.Contains(':'))
            {
                var (net, last, _) = IpMath6.ParseV6(cidr);
                list.Add((net, last));
            }
        }
        return list;
    }

    private static async Task<List<(UInt128 first, UInt128 last)>> FetchShelvedSubnetRangesV6Async(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid orgId, CancellationToken ct)
    {
        const string sql = @"
            SELECT resource_key FROM net.reservation_shelf
            WHERE organization_id = @org
              AND resource_type = 'subnet'
              AND available_after > now()
              AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("org", orgId);
        var list = new List<(UInt128 first, UInt128 last)>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var cidr = r.GetString(0);
            if (!cidr.Contains(':')) continue;
            try
            {
                var (net, last, _) = IpMath6.ParseV6(cidr);
                list.Add((net, last));
            }
            catch (FormatException) { /* skip malformed */ }
        }
        return list;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Shared
    // ═══════════════════════════════════════════════════════════════════

    internal static long IpToLong(string ip)
    {
        var bytes = System.Net.IPAddress.Parse(ip).GetAddressBytes();
        return ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | ((long)bytes[2] << 8) | bytes[3];
    }

    private static async Task AcquireLockAsync(NpgsqlConnection conn, NpgsqlTransaction tx,
        Guid containerId, CancellationToken ct)
    {
        // Shares the hash routine with AllocationService so ASN/VLAN/
        // MLAG/IP/subnet allocations on independent containers don't
        // contend, and two IP allocations on the same subnet do.
        var key = AllocationService.StableHash(containerId);
        await using var cmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@k)", conn, tx);
        cmd.Parameters.AddWithValue("k", key);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new NpgsqlConnection(_dsn);
        return OpenAsyncCore(conn, ct);
    }
    private static async Task<NpgsqlConnection> OpenAsyncCore(NpgsqlConnection conn, CancellationToken ct)
    {
        await conn.OpenAsync(ct);
        return conn;
    }
}
