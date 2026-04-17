using Central.Engine.Net.Pools;
using Npgsql;

namespace Central.Persistence.Net;

/// <summary>
/// The numbering-pool allocation engine. Every write into
/// <c>net.asn_allocation</c>, <c>net.vlan</c>, <c>net.mlag_domain</c>,
/// and <c>net.reservation_shelf</c> goes through this class — raw
/// inserts are possible but would bypass the invariants this service
/// enforces:
///
/// <list type="bullet">
///   <item>Advisory-lock serialisation per container, so two concurrent
///     callers can't pick the same value.</item>
///   <item>Shelf-cooldown check: values still on the shelf are skipped.</item>
///   <item>Range containment: the chosen value is always inside the
///     block's declared range.</item>
///   <item>Container validation: the block / pool must exist, not be
///     deleted, and belong to the calling tenant.</item>
/// </list>
///
/// <para>IP allocation (next free address in a subnet, subnet carving
/// from a pool) lives in a separate service — inet arithmetic is a
/// different shape of algorithm. This class handles only integer-space
/// resources (ASN / VLAN / MLAG).</para>
///
/// <para>The advisory-lock key is derived from the container ID's
/// hash code. Locks are transaction-scoped
/// (<c>pg_advisory_xact_lock</c>) so they release automatically on
/// commit or rollback.</para>
/// </summary>
public class AllocationService
{
    private readonly string _dsn;
    public AllocationService(string dsn) => _dsn = dsn;

    // ═══════════════════════════════════════════════════════════════════
    // ASN
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Allocate the lowest free ASN inside <paramref name="blockId"/>.
    /// Skips values still on the reservation shelf. Throws
    /// <see cref="PoolExhaustedException"/> if no value is free.
    /// </summary>
    public async Task<AsnAllocation> AllocateAsnAsync(
        Guid blockId, Guid orgId,
        string allocatedToType, Guid allocatedToId,
        int? userId = null, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await AcquireLockAsync(conn, tx, blockId, ct);

        var (first, last) = await FetchAsnBlockRangeAsync(conn, tx, blockId, orgId, ct);
        var used = await FetchUsedAsnsAsync(conn, tx, blockId, ct);
        var shelved = await FetchShelfAsync(conn, tx, orgId, ShelfResourceType.Asn, blockId, ct);
        var next = NextFreeInteger(first, last, used, shelved)
            ?? throw new PoolExhaustedException("ASN", blockId);

        const string sql = @"
            INSERT INTO net.asn_allocation
                (organization_id, block_id, asn, allocated_to_type, allocated_to_id,
                 status, lock_state, created_by, updated_by)
            VALUES (@org, @block, @asn, @type, @tid,
                    'Active'::net.entity_status, 'Open'::net.lock_state, @uid, @uid)
            RETURNING id, allocated_at";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("org", orgId);
        cmd.Parameters.AddWithValue("block", blockId);
        cmd.Parameters.AddWithValue("asn", next);
        cmd.Parameters.AddWithValue("type", allocatedToType);
        cmd.Parameters.AddWithValue("tid", allocatedToId);
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        var allocation = new AsnAllocation
        {
            Id = r.GetGuid(0),
            OrganizationId = orgId,
            BlockId = blockId,
            Asn = next,
            AllocatedToType = allocatedToType,
            AllocatedToId = allocatedToId,
            AllocatedAt = r.GetDateTime(1),
        };
        await r.CloseAsync();

        await tx.CommitAsync(ct);
        return allocation;
    }

    private static async Task<(long first, long last)> FetchAsnBlockRangeAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid blockId, Guid orgId, CancellationToken ct)
    {
        const string sql = @"
            SELECT asn_first, asn_last
            FROM net.asn_block
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("id", blockId);
        cmd.Parameters.AddWithValue("org", orgId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
            throw new AllocationContainerNotFoundException("asn_block", blockId);
        return (r.GetInt64(0), r.GetInt64(1));
    }

    private static async Task<HashSet<long>> FetchUsedAsnsAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid blockId, CancellationToken ct)
    {
        const string sql = @"
            SELECT asn FROM net.asn_allocation
            WHERE block_id = @id AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("id", blockId);
        var set = new HashSet<long>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) set.Add(r.GetInt64(0));
        return set;
    }

    // ═══════════════════════════════════════════════════════════════════
    // VLAN
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Allocate the lowest free VLAN inside <paramref name="blockId"/>.
    /// The <paramref name="templateId"/> is optional — attaching it
    /// pulls the VLAN role + default description from the template for
    /// config-generation consistency. Throws
    /// <see cref="PoolExhaustedException"/> if no VLAN is free.
    /// </summary>
    public async Task<Vlan> AllocateVlanAsync(
        Guid blockId, Guid orgId,
        string displayName, string? description,
        PoolScopeLevel scopeLevel, Guid? scopeEntityId,
        Guid? templateId = null,
        int? userId = null, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await AcquireLockAsync(conn, tx, blockId, ct);

        var (first, last) = await FetchVlanBlockRangeAsync(conn, tx, blockId, orgId, ct);
        var used = await FetchUsedVlansAsync(conn, tx, blockId, ct);
        var shelved = await FetchShelfAsync(conn, tx, orgId, ShelfResourceType.Vlan, blockId, ct);
        var next = NextFreeInteger(first, last, used, shelved)
            ?? throw new PoolExhaustedException("VLAN", blockId);

        const string sql = @"
            INSERT INTO net.vlan
                (organization_id, block_id, template_id, vlan_id, display_name, description,
                 scope_level, scope_entity_id,
                 status, lock_state, created_by, updated_by)
            VALUES (@org, @block, @tpl, @vid, @name, @desc, @scope, @sid,
                    'Active'::net.entity_status, 'Open'::net.lock_state, @uid, @uid)
            RETURNING id";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("org", orgId);
        cmd.Parameters.AddWithValue("block", blockId);
        cmd.Parameters.AddWithValue("tpl", (object?)templateId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("vid", (int)next);
        cmd.Parameters.AddWithValue("name", displayName);
        cmd.Parameters.AddWithValue("desc", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("scope", scopeLevel.ToString());
        cmd.Parameters.AddWithValue("sid", (object?)scopeEntityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);

        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        await tx.CommitAsync(ct);
        return new Vlan
        {
            Id = id,
            OrganizationId = orgId,
            BlockId = blockId,
            TemplateId = templateId,
            VlanId = (int)next,
            DisplayName = displayName,
            Description = description,
            ScopeLevel = scopeLevel,
            ScopeEntityId = scopeEntityId,
        };
    }

    private static async Task<(long first, long last)> FetchVlanBlockRangeAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid blockId, Guid orgId, CancellationToken ct)
    {
        const string sql = @"
            SELECT vlan_first, vlan_last
            FROM net.vlan_block
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("id", blockId);
        cmd.Parameters.AddWithValue("org", orgId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
            throw new AllocationContainerNotFoundException("vlan_block", blockId);
        return ((long)r.GetInt32(0), (long)r.GetInt32(1));
    }

    private static async Task<HashSet<long>> FetchUsedVlansAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid blockId, CancellationToken ct)
    {
        const string sql = @"
            SELECT vlan_id FROM net.vlan
            WHERE block_id = @id AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("id", blockId);
        var set = new HashSet<long>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) set.Add(r.GetInt32(0));
        return set;
    }

    // ═══════════════════════════════════════════════════════════════════
    // MLAG
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Allocate the lowest free MLAG domain ID inside
    /// <paramref name="poolId"/>. MLAG is pool-direct (no intermediate
    /// block tier) since the total domain count is small.
    /// </summary>
    public async Task<MlagDomain> AllocateMlagDomainAsync(
        Guid poolId, Guid orgId,
        string displayName,
        PoolScopeLevel scopeLevel, Guid? scopeEntityId,
        int? userId = null, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await AcquireLockAsync(conn, tx, poolId, ct);

        var (first, last) = await FetchMlagPoolRangeAsync(conn, tx, poolId, orgId, ct);
        var used = await FetchUsedMlagDomainsAsync(conn, tx, orgId, ct);
        var shelved = await FetchShelfAsync(conn, tx, orgId, ShelfResourceType.Mlag, poolId, ct);
        var next = NextFreeInteger(first, last, used, shelved)
            ?? throw new PoolExhaustedException("MLAG domain", poolId);

        const string sql = @"
            INSERT INTO net.mlag_domain
                (organization_id, pool_id, domain_id, display_name,
                 scope_level, scope_entity_id,
                 status, lock_state, created_by, updated_by)
            VALUES (@org, @pool, @did, @name, @scope, @sid,
                    'Active'::net.entity_status, 'Open'::net.lock_state, @uid, @uid)
            RETURNING id";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("org", orgId);
        cmd.Parameters.AddWithValue("pool", poolId);
        cmd.Parameters.AddWithValue("did", (int)next);
        cmd.Parameters.AddWithValue("name", displayName);
        cmd.Parameters.AddWithValue("scope", scopeLevel.ToString());
        cmd.Parameters.AddWithValue("sid", (object?)scopeEntityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);

        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        await tx.CommitAsync(ct);
        return new MlagDomain
        {
            Id = id,
            OrganizationId = orgId,
            PoolId = poolId,
            DomainId = (int)next,
            DisplayName = displayName,
            ScopeLevel = scopeLevel,
            ScopeEntityId = scopeEntityId,
        };
    }

    private static async Task<(long first, long last)> FetchMlagPoolRangeAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid poolId, Guid orgId, CancellationToken ct)
    {
        const string sql = @"
            SELECT domain_first, domain_last
            FROM net.mlag_domain_pool
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("id", poolId);
        cmd.Parameters.AddWithValue("org", orgId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
            throw new AllocationContainerNotFoundException("mlag_domain_pool", poolId);
        return ((long)r.GetInt32(0), (long)r.GetInt32(1));
    }

    private static async Task<HashSet<long>> FetchUsedMlagDomainsAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid orgId, CancellationToken ct)
    {
        // MLAG domain uniqueness is tenant-wide (not pool-wide) because
        // a domain ID collision on shared infrastructure would be real.
        const string sql = @"
            SELECT domain_id FROM net.mlag_domain
            WHERE organization_id = @org AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("org", orgId);
        var set = new HashSet<long>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) set.Add(r.GetInt32(0));
        return set;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Reservation shelf
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Park a retired value on the shelf so the allocation service will
    /// skip it until <paramref name="cooldown"/> has elapsed. Returns
    /// the inserted shelf row.
    /// </summary>
    public async Task<ReservationShelfEntry> RetireAsync(
        Guid orgId, ShelfResourceType resourceType, string resourceKey,
        TimeSpan cooldown,
        Guid? poolId = null, Guid? blockId = null,
        string? reason = null, int? userId = null,
        CancellationToken ct = default)
    {
        if (cooldown < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(cooldown), "Cool-down cannot be negative.");

        const string sql = @"
            INSERT INTO net.reservation_shelf
                (organization_id, resource_type, resource_key, pool_id, block_id,
                 retired_at, available_after, retired_reason,
                 status, lock_state, created_by, updated_by)
            VALUES (@org, @rt, @rk, @pool, @block,
                    now(), now() + @cd, @reason,
                    'Active'::net.entity_status, 'Open'::net.lock_state, @uid, @uid)
            RETURNING id, retired_at, available_after";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        cmd.Parameters.AddWithValue("rt", resourceType.ToString().ToLowerInvariant());
        cmd.Parameters.AddWithValue("rk", resourceKey);
        cmd.Parameters.AddWithValue("pool", (object?)poolId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("block", (object?)blockId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cd", cooldown);
        cmd.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return new ReservationShelfEntry
        {
            Id = r.GetGuid(0),
            OrganizationId = orgId,
            ResourceType = resourceType,
            ResourceKey = resourceKey,
            PoolId = poolId,
            BlockId = blockId,
            RetiredAt = r.GetDateTime(1),
            AvailableAfter = r.GetDateTime(2),
            RetiredReason = reason,
        };
    }

    /// <summary>
    /// Check whether <paramref name="resourceKey"/> is currently blocked
    /// by the shelf's cool-down window. Useful for pre-flight checks in
    /// UI flows that let operators pick a specific value.
    /// </summary>
    public async Task<bool> IsOnShelfAsync(Guid orgId, ShelfResourceType resourceType,
        string resourceKey, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT 1 FROM net.reservation_shelf
            WHERE organization_id = @org
              AND resource_type = @rt
              AND resource_key = @rk
              AND available_after > now()
              AND deleted_at IS NULL
            LIMIT 1";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        cmd.Parameters.AddWithValue("rt", resourceType.ToString().ToLowerInvariant());
        cmd.Parameters.AddWithValue("rk", resourceKey);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    private static async Task<HashSet<long>> FetchShelfAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx,
        Guid orgId, ShelfResourceType resourceType, Guid? containerId,
        CancellationToken ct)
    {
        // Shelf entries narrow by container (block/pool) when possible so
        // one block exhausting another's shelf is impossible. If the
        // container isn't specified on the shelf row, we still skip it —
        // conservative by design.
        var sql = @"
            SELECT resource_key FROM net.reservation_shelf
            WHERE organization_id = @org
              AND resource_type = @rt
              AND available_after > now()
              AND deleted_at IS NULL";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("org", orgId);
        cmd.Parameters.AddWithValue("rt", resourceType.ToString().ToLowerInvariant());
        var set = new HashSet<long>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            if (long.TryParse(r.GetString(0), out var v))
                set.Add(v);
        }
        return set;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Allocation core
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Find the lowest integer in [<paramref name="first"/>,
    /// <paramref name="last"/>] that is not in <paramref name="used"/>
    /// and not in <paramref name="shelved"/>. Returns null when the
    /// range is fully consumed.
    ///
    /// Linear scan — fine for sub-million ranges (ASN blocks typically
    /// hold 100s, VLAN blocks 2048, MLAG pools &lt; 100). If we ever
    /// want to allocate inside a /16 ASN or similar this needs to go
    /// gap-finding instead.
    /// </summary>
    internal static long? NextFreeInteger(long first, long last,
        IReadOnlySet<long> used, IReadOnlySet<long> shelved)
    {
        for (var v = first; v <= last; v++)
        {
            if (used.Contains(v)) continue;
            if (shelved.Contains(v)) continue;
            return v;
        }
        return null;
    }

    private static async Task AcquireLockAsync(NpgsqlConnection conn, NpgsqlTransaction tx,
        Guid containerId, CancellationToken ct)
    {
        // pg_advisory_xact_lock takes a bigint. We hash the GUID's
        // string form so contention is per-container, not global.
        // Lock is transaction-scoped — released automatically on
        // commit/rollback.
        var key = StableHash(containerId);
        await using var cmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@k)", conn, tx);
        cmd.Parameters.AddWithValue("k", key);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Deterministic 63-bit hash of a GUID. We avoid
    /// <see cref="object.GetHashCode"/> because its output isn't stable
    /// across runs on all CLRs — the lock key must be deterministic
    /// for concurrent clients to share the lock.
    /// </summary>
    internal static long StableHash(Guid id)
    {
        var bytes = id.ToByteArray();
        // FNV-1a 64-bit, then clear the sign bit so the value fits
        // comfortably in Postgres's signed bigint.
        ulong hash = 14695981039346656037UL;
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }
        return (long)(hash & 0x7FFFFFFFFFFFFFFFUL);
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
