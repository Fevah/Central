using System.Text.Json;
using System.Text.Json.Nodes;
using Central.Engine.Net;
using Central.Engine.Net.Hierarchy;
using Central.Engine.Net.Pools;
using Npgsql;

namespace Central.Persistence.Net;

/// <summary>
/// Repository for the numbering pool system — ASN / IP / VLAN / MLAG /
/// MSTP pools, blocks, templates, and the reservation shelf.
///
/// <para>Reads land here; pool + block CRUD lands here; <b>allocation
/// writes live in the Phase-3c Central.Engine.Net.Allocation service</b>
/// and are not exposed on this class. This preserves the invariant that
/// allocations only happen behind an advisory-lock-guarded service
/// call — raw SQL inserts into <c>net.asn_allocation</c> / etc. are
/// possible but never done from the C# side.</para>
///
/// <para>Enum columns use text casts (<c>@scope::text</c>) the same way
/// <see cref="HierarchyRepository"/> does, so no Npgsql data-source
/// enum registration is required.</para>
/// </summary>
public class PoolsRepository
{
    private readonly string _dsn;
    public PoolsRepository(string dsn) => _dsn = dsn;

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

    // ── Shared mappers (same shape as HierarchyRepository) ──────────────

    private const string BaseColumns =
        "status::text, lock_state::text, lock_reason, locked_by, locked_at, " +
        "created_at, created_by, updated_at, updated_by, deleted_at, deleted_by, " +
        "notes, tags::text, external_refs::text, version";

    private static EntityStatus ParseStatus(string s) => Enum.Parse<EntityStatus>(s);
    private static LockState ParseLock(string s) => Enum.Parse<LockState>(s);
    private static JsonObject ReadJsonObject(NpgsqlDataReader r, int idx)
        => r.IsDBNull(idx) ? new() : (JsonNode.Parse(r.GetString(idx)) as JsonObject) ?? new();
    private static JsonArray ReadJsonArray(NpgsqlDataReader r, int idx)
        => r.IsDBNull(idx) ? new() : (JsonNode.Parse(r.GetString(idx)) as JsonArray) ?? new();

    private static void PopulateBase(EntityBase e, NpgsqlDataReader r, int startCol)
    {
        e.Status = ParseStatus(r.GetString(startCol));
        e.LockState = ParseLock(r.GetString(startCol + 1));
        e.LockReason = r.IsDBNull(startCol + 2) ? null : r.GetString(startCol + 2);
        e.LockedBy = r.IsDBNull(startCol + 3) ? null : r.GetInt32(startCol + 3);
        e.LockedAt = r.IsDBNull(startCol + 4) ? null : r.GetDateTime(startCol + 4);
        e.CreatedAt = r.GetDateTime(startCol + 5);
        e.CreatedBy = r.IsDBNull(startCol + 6) ? null : r.GetInt32(startCol + 6);
        e.UpdatedAt = r.GetDateTime(startCol + 7);
        e.UpdatedBy = r.IsDBNull(startCol + 8) ? null : r.GetInt32(startCol + 8);
        e.DeletedAt = r.IsDBNull(startCol + 9) ? null : r.GetDateTime(startCol + 9);
        e.DeletedBy = r.IsDBNull(startCol + 10) ? null : r.GetInt32(startCol + 10);
        e.Notes = r.IsDBNull(startCol + 11) ? null : r.GetString(startCol + 11);
        e.Tags = ReadJsonObject(r, startCol + 12);
        e.ExternalRefs = ReadJsonArray(r, startCol + 13);
        e.Version = r.GetInt32(startCol + 14);
    }

    private static PoolScopeLevel ParseScope(string s) => Enum.Parse<PoolScopeLevel>(s);

    // ═══════════════════════════════════════════════════════════════════
    // ASN pools + blocks
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<AsnPool>> ListAsnPoolsAsync(Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, pool_code, display_name,
                   asn_first, asn_last, asn_kind, " + BaseColumns + @"
            FROM net.asn_pool
            WHERE organization_id = @org AND deleted_at IS NULL
            ORDER BY pool_code";
        return await ListAsync(sql, orgId, ReadAsnPool, ct);
    }

    public async Task<AsnPool?> GetAsnPoolAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, pool_code, display_name,
                   asn_first, asn_last, asn_kind, " + BaseColumns + @"
            FROM net.asn_pool
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadAsnPool, ct);
    }

    public async Task<Guid> CreateAsnPoolAsync(AsnPool e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.asn_pool (organization_id, pool_code, display_name,
                                      asn_first, asn_last, asn_kind,
                                      status, lock_state, notes, tags, external_refs,
                                      created_by, updated_by)
            VALUES (@org, @code, @name, @first, @last, @kind,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("code", e.PoolCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("first", e.AsnFirst);
        cmd.Parameters.AddWithValue("last", e.AsnLast);
        cmd.Parameters.AddWithValue("kind", e.AsnKind.ToString());
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateAsnPoolAsync(AsnPool e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.asn_pool SET
                pool_code     = @code,
                display_name  = @name,
                asn_first     = @first,
                asn_last      = @last,
                asn_kind      = @kind,
                status        = @status::net.entity_status,
                lock_state    = @lock::net.lock_state,
                lock_reason   = @lreason,
                notes         = @notes,
                tags          = @tags::jsonb,
                external_refs = @refs::jsonb,
                updated_at    = now(),
                updated_by    = @uid,
                version       = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("code", e.PoolCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("first", e.AsnFirst);
        cmd.Parameters.AddWithValue("last", e.AsnLast);
        cmd.Parameters.AddWithValue("kind", e.AsnKind.ToString());
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteAsnPoolAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.asn_pool", id, orgId, userId, ct);

    private static AsnPool ReadAsnPool(NpgsqlDataReader r)
    {
        var e = new AsnPool
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            PoolCode = r.GetString(2),
            DisplayName = r.GetString(3),
            AsnFirst = r.GetInt64(4),
            AsnLast = r.GetInt64(5),
            AsnKind = Enum.Parse<AsnKind>(r.GetString(6)),
        };
        PopulateBase(e, r, 7);
        return e;
    }

    // ── AsnBlock ────────────────────────────────────────────────────────

    public async Task<List<AsnBlock>> ListAsnBlocksAsync(Guid orgId, Guid? poolId, CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, pool_id, block_code, display_name,
                   asn_first, asn_last, scope_level::text, scope_entity_id, " + BaseColumns + @"
            FROM net.asn_block
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (poolId.HasValue ? " AND pool_id = @pool" : "") + @"
            ORDER BY asn_first";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (poolId.HasValue) cmd.Parameters.AddWithValue("pool", poolId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<AsnBlock>();
        while (await r.ReadAsync(ct)) list.Add(ReadAsnBlock(r));
        return list;
    }

    public async Task<AsnBlock?> GetAsnBlockAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, pool_id, block_code, display_name,
                   asn_first, asn_last, scope_level::text, scope_entity_id, " + BaseColumns + @"
            FROM net.asn_block
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadAsnBlock, ct);
    }

    public async Task<Guid> CreateAsnBlockAsync(AsnBlock e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.asn_block (organization_id, pool_id, block_code, display_name,
                                       asn_first, asn_last, scope_level, scope_entity_id,
                                       status, lock_state, notes, tags, external_refs,
                                       created_by, updated_by)
            VALUES (@org, @pool, @code, @name, @first, @last, @scope, @sid,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindAsnBlockWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateAsnBlockAsync(AsnBlock e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.asn_block SET
                pool_id         = @pool,
                block_code      = @code,
                display_name    = @name,
                asn_first       = @first,
                asn_last        = @last,
                scope_level     = @scope,
                scope_entity_id = @sid,
                status          = @status::net.entity_status,
                lock_state      = @lock::net.lock_state,
                lock_reason     = @lreason,
                notes           = @notes,
                tags            = @tags::jsonb,
                external_refs   = @refs::jsonb,
                updated_at      = now(),
                updated_by      = @uid,
                version         = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindAsnBlockWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteAsnBlockAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.asn_block", id, orgId, userId, ct);

    private static void BindAsnBlockWrite(NpgsqlCommand cmd, AsnBlock e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("pool", e.PoolId);
        cmd.Parameters.AddWithValue("code", e.BlockCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("first", e.AsnFirst);
        cmd.Parameters.AddWithValue("last", e.AsnLast);
        cmd.Parameters.AddWithValue("scope", e.ScopeLevel.ToString());
        cmd.Parameters.AddWithValue("sid", (object?)e.ScopeEntityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static AsnBlock ReadAsnBlock(NpgsqlDataReader r)
    {
        var e = new AsnBlock
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            PoolId = r.GetGuid(2),
            BlockCode = r.GetString(3),
            DisplayName = r.GetString(4),
            AsnFirst = r.GetInt64(5),
            AsnLast = r.GetInt64(6),
            ScopeLevel = ParseScope(r.GetString(7)),
            ScopeEntityId = r.IsDBNull(8) ? null : r.GetGuid(8),
        };
        PopulateBase(e, r, 9);
        return e;
    }

    // ── AsnAllocation (reads only — writes go through AllocationService) ──

    public async Task<List<AsnAllocation>> ListAsnAllocationsAsync(Guid orgId, Guid? blockId,
        CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, block_id, asn, allocated_to_type, allocated_to_id,
                   allocated_at, " + BaseColumns + @"
            FROM net.asn_allocation
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (blockId.HasValue ? " AND block_id = @block" : "") + @"
            ORDER BY asn";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (blockId.HasValue) cmd.Parameters.AddWithValue("block", blockId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<AsnAllocation>();
        while (await r.ReadAsync(ct)) list.Add(ReadAsnAllocation(r));
        return list;
    }

    private static AsnAllocation ReadAsnAllocation(NpgsqlDataReader r)
    {
        var e = new AsnAllocation
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            BlockId = r.GetGuid(2),
            Asn = r.GetInt64(3),
            AllocatedToType = r.GetString(4),
            AllocatedToId = r.GetGuid(5),
            AllocatedAt = r.GetDateTime(6),
        };
        PopulateBase(e, r, 7);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // IP pools + subnets
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<IpPool>> ListIpPoolsAsync(Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, pool_code, display_name,
                   network::text, address_family, " + BaseColumns + @"
            FROM net.ip_pool
            WHERE organization_id = @org AND deleted_at IS NULL
            ORDER BY pool_code";
        return await ListAsync(sql, orgId, ReadIpPool, ct);
    }

    public async Task<IpPool?> GetIpPoolAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, pool_code, display_name,
                   network::text, address_family, " + BaseColumns + @"
            FROM net.ip_pool
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadIpPool, ct);
    }

    public async Task<Guid> CreateIpPoolAsync(IpPool e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.ip_pool (organization_id, pool_code, display_name,
                                     network, address_family,
                                     status, lock_state, notes, tags, external_refs,
                                     created_by, updated_by)
            VALUES (@org, @code, @name, @net::cidr, @af,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindIpPoolWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateIpPoolAsync(IpPool e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.ip_pool SET
                pool_code      = @code,
                display_name   = @name,
                network        = @net::cidr,
                address_family = @af,
                status         = @status::net.entity_status,
                lock_state     = @lock::net.lock_state,
                lock_reason    = @lreason,
                notes          = @notes,
                tags           = @tags::jsonb,
                external_refs  = @refs::jsonb,
                updated_at     = now(),
                updated_by     = @uid,
                version        = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindIpPoolWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteIpPoolAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.ip_pool", id, orgId, userId, ct);

    private static void BindIpPoolWrite(NpgsqlCommand cmd, IpPool e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("code", e.PoolCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("net", e.Network);
        cmd.Parameters.AddWithValue("af", e.AddressFamily == IpAddressFamily.V6 ? "v6" : "v4");
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static IpPool ReadIpPool(NpgsqlDataReader r)
    {
        var e = new IpPool
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            PoolCode = r.GetString(2),
            DisplayName = r.GetString(3),
            Network = r.GetString(4),
            AddressFamily = r.GetString(5) == "v6" ? IpAddressFamily.V6 : IpAddressFamily.V4,
        };
        PopulateBase(e, r, 6);
        return e;
    }

    // ── Subnet (reads + CRUD) ───────────────────────────────────────────

    public async Task<List<Subnet>> ListSubnetsAsync(Guid orgId, Guid? poolId, CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, pool_id, parent_subnet_id, subnet_code, display_name,
                   network::text, scope_level::text, scope_entity_id, vlan_id, " + BaseColumns + @"
            FROM net.subnet
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (poolId.HasValue ? " AND pool_id = @pool" : "") + @"
            ORDER BY network";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (poolId.HasValue) cmd.Parameters.AddWithValue("pool", poolId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Subnet>();
        while (await r.ReadAsync(ct)) list.Add(ReadSubnet(r));
        return list;
    }

    public async Task<Subnet?> GetSubnetAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, pool_id, parent_subnet_id, subnet_code, display_name,
                   network::text, scope_level::text, scope_entity_id, vlan_id, " + BaseColumns + @"
            FROM net.subnet
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadSubnet, ct);
    }

    public async Task<Guid> CreateSubnetAsync(Subnet e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.subnet (organization_id, pool_id, parent_subnet_id, subnet_code,
                                    display_name, network, scope_level, scope_entity_id, vlan_id,
                                    status, lock_state, notes, tags, external_refs,
                                    created_by, updated_by)
            VALUES (@org, @pool, @parent, @code, @name, @net::cidr, @scope, @sid, @vlan,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindSubnetWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateSubnetAsync(Subnet e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.subnet SET
                pool_id           = @pool,
                parent_subnet_id  = @parent,
                subnet_code       = @code,
                display_name      = @name,
                network           = @net::cidr,
                scope_level       = @scope,
                scope_entity_id   = @sid,
                vlan_id           = @vlan,
                status            = @status::net.entity_status,
                lock_state        = @lock::net.lock_state,
                lock_reason       = @lreason,
                notes             = @notes,
                tags              = @tags::jsonb,
                external_refs     = @refs::jsonb,
                updated_at        = now(),
                updated_by        = @uid,
                version           = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindSubnetWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteSubnetAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.subnet", id, orgId, userId, ct);

    private static void BindSubnetWrite(NpgsqlCommand cmd, Subnet e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("pool", e.PoolId);
        cmd.Parameters.AddWithValue("parent", (object?)e.ParentSubnetId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("code", e.SubnetCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("net", e.Network);
        cmd.Parameters.AddWithValue("scope", e.ScopeLevel.ToString());
        cmd.Parameters.AddWithValue("sid", (object?)e.ScopeEntityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("vlan", (object?)e.VlanId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static Subnet ReadSubnet(NpgsqlDataReader r)
    {
        var e = new Subnet
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            PoolId = r.GetGuid(2),
            ParentSubnetId = r.IsDBNull(3) ? null : r.GetGuid(3),
            SubnetCode = r.GetString(4),
            DisplayName = r.GetString(5),
            Network = r.GetString(6),
            ScopeLevel = ParseScope(r.GetString(7)),
            ScopeEntityId = r.IsDBNull(8) ? null : r.GetGuid(8),
            VlanId = r.IsDBNull(9) ? null : r.GetGuid(9),
        };
        PopulateBase(e, r, 10);
        return e;
    }

    // ── IpAddress (reads only) ──────────────────────────────────────────

    public async Task<List<IpAddress>> ListIpAddressesAsync(Guid orgId, Guid? subnetId,
        CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, subnet_id, address::text,
                   assigned_to_type, assigned_to_id, is_reserved, assigned_at, " + BaseColumns + @"
            FROM net.ip_address
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (subnetId.HasValue ? " AND subnet_id = @subnet" : "") + @"
            ORDER BY address";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (subnetId.HasValue) cmd.Parameters.AddWithValue("subnet", subnetId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<IpAddress>();
        while (await r.ReadAsync(ct)) list.Add(ReadIpAddress(r));
        return list;
    }

    private static IpAddress ReadIpAddress(NpgsqlDataReader r)
    {
        var e = new IpAddress
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            SubnetId = r.GetGuid(2),
            Address = r.GetString(3),
            AssignedToType = r.IsDBNull(4) ? null : r.GetString(4),
            AssignedToId = r.IsDBNull(5) ? null : r.GetGuid(5),
            IsReserved = r.GetBoolean(6),
            AssignedAt = r.GetDateTime(7),
        };
        PopulateBase(e, r, 8);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // VLAN — template, pool, block, allocation
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<VlanTemplate>> ListVlanTemplatesAsync(Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, template_code, display_name, vlan_role,
                   description, is_default, " + BaseColumns + @"
            FROM net.vlan_template
            WHERE organization_id = @org AND deleted_at IS NULL
            ORDER BY template_code";
        return await ListAsync(sql, orgId, ReadVlanTemplate, ct);
    }

    public async Task<VlanTemplate?> GetVlanTemplateAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, template_code, display_name, vlan_role,
                   description, is_default, " + BaseColumns + @"
            FROM net.vlan_template
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadVlanTemplate, ct);
    }

    public async Task<Guid> CreateVlanTemplateAsync(VlanTemplate e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.vlan_template (organization_id, template_code, display_name,
                                           vlan_role, description, is_default,
                                           status, lock_state, notes, tags, external_refs,
                                           created_by, updated_by)
            VALUES (@org, @code, @name, @role, @desc, @def,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindVlanTemplateWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateVlanTemplateAsync(VlanTemplate e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.vlan_template SET
                template_code = @code,
                display_name  = @name,
                vlan_role     = @role,
                description   = @desc,
                is_default    = @def,
                status        = @status::net.entity_status,
                lock_state    = @lock::net.lock_state,
                lock_reason   = @lreason,
                notes         = @notes,
                tags          = @tags::jsonb,
                external_refs = @refs::jsonb,
                updated_at    = now(),
                updated_by    = @uid,
                version       = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindVlanTemplateWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteVlanTemplateAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.vlan_template", id, orgId, userId, ct);

    private static void BindVlanTemplateWrite(NpgsqlCommand cmd, VlanTemplate e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("code", e.TemplateCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("role", e.VlanRole);
        cmd.Parameters.AddWithValue("desc", (object?)e.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("def", e.IsDefault);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static VlanTemplate ReadVlanTemplate(NpgsqlDataReader r)
    {
        var e = new VlanTemplate
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            TemplateCode = r.GetString(2),
            DisplayName = r.GetString(3),
            VlanRole = r.GetString(4),
            Description = r.IsDBNull(5) ? null : r.GetString(5),
            IsDefault = r.GetBoolean(6),
        };
        PopulateBase(e, r, 7);
        return e;
    }

    // ── VlanPool ────────────────────────────────────────────────────────

    public async Task<List<VlanPool>> ListVlanPoolsAsync(Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, pool_code, display_name,
                   vlan_first, vlan_last, " + BaseColumns + @"
            FROM net.vlan_pool
            WHERE organization_id = @org AND deleted_at IS NULL
            ORDER BY pool_code";
        return await ListAsync(sql, orgId, ReadVlanPool, ct);
    }

    public async Task<VlanPool?> GetVlanPoolAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, pool_code, display_name,
                   vlan_first, vlan_last, " + BaseColumns + @"
            FROM net.vlan_pool
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadVlanPool, ct);
    }

    public async Task<Guid> CreateVlanPoolAsync(VlanPool e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.vlan_pool (organization_id, pool_code, display_name,
                                       vlan_first, vlan_last,
                                       status, lock_state, notes, tags, external_refs,
                                       created_by, updated_by)
            VALUES (@org, @code, @name, @first, @last,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindVlanPoolWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateVlanPoolAsync(VlanPool e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.vlan_pool SET
                pool_code     = @code,
                display_name  = @name,
                vlan_first    = @first,
                vlan_last     = @last,
                status        = @status::net.entity_status,
                lock_state    = @lock::net.lock_state,
                lock_reason   = @lreason,
                notes         = @notes,
                tags          = @tags::jsonb,
                external_refs = @refs::jsonb,
                updated_at    = now(),
                updated_by    = @uid,
                version       = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindVlanPoolWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteVlanPoolAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.vlan_pool", id, orgId, userId, ct);

    private static void BindVlanPoolWrite(NpgsqlCommand cmd, VlanPool e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("code", e.PoolCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("first", e.VlanFirst);
        cmd.Parameters.AddWithValue("last", e.VlanLast);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static VlanPool ReadVlanPool(NpgsqlDataReader r)
    {
        var e = new VlanPool
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            PoolCode = r.GetString(2),
            DisplayName = r.GetString(3),
            VlanFirst = r.GetInt32(4),
            VlanLast = r.GetInt32(5),
        };
        PopulateBase(e, r, 6);
        return e;
    }

    // ── VlanBlock ───────────────────────────────────────────────────────

    public async Task<List<VlanBlock>> ListVlanBlocksAsync(Guid orgId, Guid? poolId, CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, pool_id, block_code, display_name,
                   vlan_first, vlan_last, scope_level::text, scope_entity_id, " + BaseColumns + @"
            FROM net.vlan_block
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (poolId.HasValue ? " AND pool_id = @pool" : "") + @"
            ORDER BY vlan_first";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (poolId.HasValue) cmd.Parameters.AddWithValue("pool", poolId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<VlanBlock>();
        while (await r.ReadAsync(ct)) list.Add(ReadVlanBlock(r));
        return list;
    }

    public async Task<VlanBlock?> GetVlanBlockAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, pool_id, block_code, display_name,
                   vlan_first, vlan_last, scope_level::text, scope_entity_id, " + BaseColumns + @"
            FROM net.vlan_block
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadVlanBlock, ct);
    }

    public async Task<Guid> CreateVlanBlockAsync(VlanBlock e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.vlan_block (organization_id, pool_id, block_code, display_name,
                                        vlan_first, vlan_last, scope_level, scope_entity_id,
                                        status, lock_state, notes, tags, external_refs,
                                        created_by, updated_by)
            VALUES (@org, @pool, @code, @name, @first, @last, @scope, @sid,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindVlanBlockWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateVlanBlockAsync(VlanBlock e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.vlan_block SET
                pool_id         = @pool,
                block_code      = @code,
                display_name    = @name,
                vlan_first      = @first,
                vlan_last       = @last,
                scope_level     = @scope,
                scope_entity_id = @sid,
                status          = @status::net.entity_status,
                lock_state      = @lock::net.lock_state,
                lock_reason     = @lreason,
                notes           = @notes,
                tags            = @tags::jsonb,
                external_refs   = @refs::jsonb,
                updated_at      = now(),
                updated_by      = @uid,
                version         = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindVlanBlockWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteVlanBlockAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.vlan_block", id, orgId, userId, ct);

    private static void BindVlanBlockWrite(NpgsqlCommand cmd, VlanBlock e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("pool", e.PoolId);
        cmd.Parameters.AddWithValue("code", e.BlockCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("first", e.VlanFirst);
        cmd.Parameters.AddWithValue("last", e.VlanLast);
        cmd.Parameters.AddWithValue("scope", e.ScopeLevel.ToString());
        cmd.Parameters.AddWithValue("sid", (object?)e.ScopeEntityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static VlanBlock ReadVlanBlock(NpgsqlDataReader r)
    {
        var e = new VlanBlock
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            PoolId = r.GetGuid(2),
            BlockCode = r.GetString(3),
            DisplayName = r.GetString(4),
            VlanFirst = r.GetInt32(5),
            VlanLast = r.GetInt32(6),
            ScopeLevel = ParseScope(r.GetString(7)),
            ScopeEntityId = r.IsDBNull(8) ? null : r.GetGuid(8),
        };
        PopulateBase(e, r, 9);
        return e;
    }

    // ── Vlan (reads only — writes via AllocationService) ────────────────

    public async Task<List<Vlan>> ListVlansAsync(Guid orgId, Guid? blockId, CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, block_id, template_id, vlan_id, display_name,
                   description, scope_level::text, scope_entity_id, " + BaseColumns + @"
            FROM net.vlan
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (blockId.HasValue ? " AND block_id = @block" : "") + @"
            ORDER BY vlan_id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (blockId.HasValue) cmd.Parameters.AddWithValue("block", blockId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Vlan>();
        while (await r.ReadAsync(ct)) list.Add(ReadVlan(r));
        return list;
    }

    private static Vlan ReadVlan(NpgsqlDataReader r)
    {
        var e = new Vlan
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            BlockId = r.GetGuid(2),
            TemplateId = r.IsDBNull(3) ? null : r.GetGuid(3),
            VlanId = r.GetInt32(4),
            DisplayName = r.GetString(5),
            Description = r.IsDBNull(6) ? null : r.GetString(6),
            ScopeLevel = ParseScope(r.GetString(7)),
            ScopeEntityId = r.IsDBNull(8) ? null : r.GetGuid(8),
        };
        PopulateBase(e, r, 9);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // MLAG
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<MlagDomainPool>> ListMlagPoolsAsync(Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, pool_code, display_name,
                   domain_first, domain_last, " + BaseColumns + @"
            FROM net.mlag_domain_pool
            WHERE organization_id = @org AND deleted_at IS NULL
            ORDER BY pool_code";
        return await ListAsync(sql, orgId, ReadMlagPool, ct);
    }

    public async Task<MlagDomainPool?> GetMlagPoolAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, pool_code, display_name,
                   domain_first, domain_last, " + BaseColumns + @"
            FROM net.mlag_domain_pool
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadMlagPool, ct);
    }

    public async Task<Guid> CreateMlagPoolAsync(MlagDomainPool e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.mlag_domain_pool (organization_id, pool_code, display_name,
                                              domain_first, domain_last,
                                              status, lock_state, notes, tags, external_refs,
                                              created_by, updated_by)
            VALUES (@org, @code, @name, @first, @last,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindMlagPoolWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateMlagPoolAsync(MlagDomainPool e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.mlag_domain_pool SET
                pool_code     = @code,
                display_name  = @name,
                domain_first  = @first,
                domain_last   = @last,
                status        = @status::net.entity_status,
                lock_state    = @lock::net.lock_state,
                lock_reason   = @lreason,
                notes         = @notes,
                tags          = @tags::jsonb,
                external_refs = @refs::jsonb,
                updated_at    = now(),
                updated_by    = @uid,
                version       = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindMlagPoolWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteMlagPoolAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.mlag_domain_pool", id, orgId, userId, ct);

    private static void BindMlagPoolWrite(NpgsqlCommand cmd, MlagDomainPool e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("code", e.PoolCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("first", e.DomainFirst);
        cmd.Parameters.AddWithValue("last", e.DomainLast);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static MlagDomainPool ReadMlagPool(NpgsqlDataReader r)
    {
        var e = new MlagDomainPool
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            PoolCode = r.GetString(2),
            DisplayName = r.GetString(3),
            DomainFirst = r.GetInt32(4),
            DomainLast = r.GetInt32(5),
        };
        PopulateBase(e, r, 6);
        return e;
    }

    public async Task<List<MlagDomain>> ListMlagDomainsAsync(Guid orgId, Guid? poolId, CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, pool_id, domain_id, display_name,
                   scope_level::text, scope_entity_id, " + BaseColumns + @"
            FROM net.mlag_domain
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (poolId.HasValue ? " AND pool_id = @pool" : "") + @"
            ORDER BY domain_id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (poolId.HasValue) cmd.Parameters.AddWithValue("pool", poolId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<MlagDomain>();
        while (await r.ReadAsync(ct)) list.Add(ReadMlagDomain(r));
        return list;
    }

    private static MlagDomain ReadMlagDomain(NpgsqlDataReader r)
    {
        var e = new MlagDomain
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            PoolId = r.GetGuid(2),
            DomainId = r.GetInt32(3),
            DisplayName = r.GetString(4),
            ScopeLevel = ParseScope(r.GetString(5)),
            ScopeEntityId = r.IsDBNull(6) ? null : r.GetGuid(6),
        };
        PopulateBase(e, r, 7);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // MSTP
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<MstpPriorityRule>> ListMstpRulesAsync(Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, rule_code, display_name,
                   scope_level::text, scope_entity_id, " + BaseColumns + @"
            FROM net.mstp_priority_rule
            WHERE organization_id = @org AND deleted_at IS NULL
            ORDER BY rule_code";
        return await ListAsync(sql, orgId, ReadMstpRule, ct);
    }

    public async Task<MstpPriorityRule?> GetMstpRuleAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, rule_code, display_name,
                   scope_level::text, scope_entity_id, " + BaseColumns + @"
            FROM net.mstp_priority_rule
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadMstpRule, ct);
    }

    private static MstpPriorityRule ReadMstpRule(NpgsqlDataReader r)
    {
        var e = new MstpPriorityRule
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            RuleCode = r.GetString(2),
            DisplayName = r.GetString(3),
            ScopeLevel = ParseScope(r.GetString(4)),
            ScopeEntityId = r.IsDBNull(5) ? null : r.GetGuid(5),
        };
        PopulateBase(e, r, 6);
        return e;
    }

    public async Task<List<MstpPriorityRuleStep>> ListMstpStepsAsync(Guid orgId, Guid ruleId,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, rule_id, step_order,
                   match_expression::text, priority, " + BaseColumns + @"
            FROM net.mstp_priority_rule_step
            WHERE organization_id = @org AND rule_id = @rule AND deleted_at IS NULL
            ORDER BY step_order";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        cmd.Parameters.AddWithValue("rule", ruleId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<MstpPriorityRuleStep>();
        while (await r.ReadAsync(ct)) list.Add(ReadMstpStep(r));
        return list;
    }

    private static MstpPriorityRuleStep ReadMstpStep(NpgsqlDataReader r)
    {
        var e = new MstpPriorityRuleStep
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            RuleId = r.GetGuid(2),
            StepOrder = r.GetInt32(3),
            MatchExpression = (JsonNode.Parse(r.GetString(4)) as JsonObject) ?? new(),
            Priority = r.GetInt32(5),
        };
        PopulateBase(e, r, 6);
        return e;
    }

    public async Task<List<MstpPriorityAllocation>> ListMstpAllocationsAsync(Guid orgId, Guid? ruleId,
        CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, rule_id, device_id, bridge_mac::text,
                   priority, allocated_at, " + BaseColumns + @"
            FROM net.mstp_priority_allocation
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (ruleId.HasValue ? " AND rule_id = @rule" : "") + @"
            ORDER BY priority, device_id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (ruleId.HasValue) cmd.Parameters.AddWithValue("rule", ruleId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<MstpPriorityAllocation>();
        while (await r.ReadAsync(ct)) list.Add(ReadMstpAllocation(r));
        return list;
    }

    private static MstpPriorityAllocation ReadMstpAllocation(NpgsqlDataReader r)
    {
        var e = new MstpPriorityAllocation
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            RuleId = r.GetGuid(2),
            DeviceId = r.GetGuid(3),
            BridgeMac = r.IsDBNull(4) ? null : r.GetString(4),
            Priority = r.GetInt32(5),
            AllocatedAt = r.GetDateTime(6),
        };
        PopulateBase(e, r, 7);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Reservation shelf (reads only — shelf writes land with the
    // allocation service in Phase 3c)
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<ReservationShelfEntry>> ListShelfEntriesAsync(Guid orgId,
        ShelfResourceType? resourceType, CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, resource_type, resource_key,
                   pool_id, block_id, retired_at, available_after, retired_reason, " + BaseColumns + @"
            FROM net.reservation_shelf
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (resourceType.HasValue ? " AND resource_type = @rt" : "") + @"
            ORDER BY available_after";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (resourceType.HasValue)
            cmd.Parameters.AddWithValue("rt", resourceType.Value.ToString().ToLowerInvariant());
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<ReservationShelfEntry>();
        while (await r.ReadAsync(ct)) list.Add(ReadShelfEntry(r));
        return list;
    }

    private static ReservationShelfEntry ReadShelfEntry(NpgsqlDataReader r)
    {
        var s = r.GetString(2);
        var e = new ReservationShelfEntry
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            ResourceType = Enum.Parse<ShelfResourceType>(
                char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant()),
            ResourceKey = r.GetString(3),
            PoolId = r.IsDBNull(4) ? null : r.GetGuid(4),
            BlockId = r.IsDBNull(5) ? null : r.GetGuid(5),
            RetiredAt = r.GetDateTime(6),
            AvailableAfter = r.GetDateTime(7),
            RetiredReason = r.IsDBNull(8) ? null : r.GetString(8),
        };
        PopulateBase(e, r, 9);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Generic helpers
    // ═══════════════════════════════════════════════════════════════════

    private async Task<List<T>> ListAsync<T>(string sql, Guid orgId,
        Func<NpgsqlDataReader, T> reader, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<T>();
        while (await r.ReadAsync(ct)) list.Add(reader(r));
        return list;
    }

    private async Task<T?> GetOneAsync<T>(string sql, Guid id, Guid orgId,
        Func<NpgsqlDataReader, T> reader, CancellationToken ct) where T : class
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("org", orgId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? reader(r) : null;
    }

    private async Task<bool> SoftDeleteAsync(string table, Guid id, Guid orgId, int? userId, CancellationToken ct)
    {
        var sql = $@"
            UPDATE {table}
            SET deleted_at = now(), deleted_by = @uid, version = version + 1
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("org", orgId);
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }
}
