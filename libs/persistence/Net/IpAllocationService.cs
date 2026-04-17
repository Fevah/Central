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

        var (network, broadcast, prefix) = await FetchSubnetCidrAsync(conn, tx, subnetId, orgId, ct);
        var (first, last) = IpMath.HostRange(network, broadcast, prefix);

        var used = await FetchUsedIpsAsync(conn, tx, subnetId, ct);
        var shelved = await FetchShelvedIpsAsync(conn, tx, orgId, ct);

        long? next = null;
        for (var candidate = first; candidate <= last; candidate++)
        {
            if (used.Contains(candidate)) continue;
            if (shelved.Contains(candidate)) continue;
            next = candidate;
            break;
        }
        if (next is null)
            throw new PoolExhaustedException("IP address", subnetId);

        var addrStr = IpMath.ToIp(next.Value);
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

    private static async Task<(long network, long broadcast, int prefix)> FetchSubnetCidrAsync(
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
        return IpMath.ParseV4(raw);
    }

    private static async Task<HashSet<long>> FetchUsedIpsAsync(
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
            var s = r.GetString(0);
            // inet literals may carry a trailing /N on net.ip_address
            // though we insert them without one. Strip it defensively.
            var slash = s.IndexOf('/');
            if (slash > 0) s = s[..slash];
            set.Add(IpToLong(s));
        }
        return set;
    }

    private static async Task<HashSet<long>> FetchShelvedIpsAsync(
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
            set.Add(IpToLong(r.GetString(0)));
        return set;
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

        var (poolNetwork, poolBroadcast, poolPrefix) = await FetchPoolCidrAsync(conn, tx, poolId, orgId, ct);
        if (prefixLength < poolPrefix || prefixLength > 32)
            throw new AllocationRangeException("subnet prefix",
                prefixLength, poolPrefix, 32);

        var existing = await FetchSubnetRangesInPoolAsync(conn, tx, poolId, ct);
        var shelved = await FetchShelvedSubnetRangesAsync(conn, tx, orgId, ct);
        // Merge both "occupied" sets into one sorted list.
        var blocked = new List<(long first, long last)>(existing.Count + shelved.Count);
        blocked.AddRange(existing);
        blocked.AddRange(shelved);
        blocked.Sort((a, b) => a.first.CompareTo(b.first));

        var candidate = FindFreeAligned(poolNetwork, poolBroadcast, prefixLength, blocked)
            ?? throw new PoolExhaustedException($"subnet /{prefixLength}", poolId);

        var cidr = IpMath.ToCidr(candidate, prefixLength);
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

    private static async Task<(long network, long broadcast, int prefix)> FetchPoolCidrAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid poolId, Guid orgId, CancellationToken ct)
    {
        const string sql = @"
            SELECT network::text FROM net.ip_pool
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("id", poolId);
        cmd.Parameters.AddWithValue("org", orgId);
        var raw = await cmd.ExecuteScalarAsync(ct) as string
            ?? throw new AllocationContainerNotFoundException("ip_pool", poolId);
        return IpMath.ParseV4(raw);
    }

    private static async Task<List<(long first, long last)>> FetchSubnetRangesInPoolAsync(
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
            var (net, bcast, _) = IpMath.ParseV4(r.GetString(0));
            list.Add((net, bcast));
        }
        return list;
    }

    private static async Task<List<(long first, long last)>> FetchShelvedSubnetRangesAsync(
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
            try
            {
                var (net, bcast, _) = IpMath.ParseV4(r.GetString(0));
                list.Add((net, bcast));
            }
            catch (FormatException)
            {
                // Malformed shelf entries are skipped defensively rather
                // than aborting the whole allocation.
            }
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
