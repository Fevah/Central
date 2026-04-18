using System.Text.Json.Nodes;
using Central.Engine.Net;
using Central.Engine.Net.Hierarchy;
using Central.Engine.Net.Links;
using Npgsql;

namespace Central.Persistence.Net;

/// <summary>
/// Repository for the Phase-5 unified link model (link_type / link /
/// link_endpoint). Reads land here in Phase 5b; writes come in 5c
/// alongside the legacy-import service.
///
/// <para>Same idioms as the sibling repositories — text-cast enum
/// handling, 17-column <c>PopulateBase</c> tail, per-family Read
/// helpers.</para>
/// </summary>
public class LinksRepository
{
    private readonly string _dsn;
    public LinksRepository(string dsn) => _dsn = dsn;

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

    // ═══════════════════════════════════════════════════════════════════
    // LinkType
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<LinkType>> ListTypesAsync(Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, type_code, display_name, description,
                   naming_template, required_endpoints, color_hint, " + BaseColumns + @"
            FROM net.link_type
            WHERE organization_id = @org AND deleted_at IS NULL
            ORDER BY type_code";
        return await ListAsync(sql, orgId, ReadLinkType, ct);
    }

    public async Task<LinkType?> GetTypeAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, type_code, display_name, description,
                   naming_template, required_endpoints, color_hint, " + BaseColumns + @"
            FROM net.link_type
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadLinkType, ct);
    }

    public async Task<Guid> CreateTypeAsync(LinkType e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.link_type (organization_id, type_code, display_name, description,
                                       naming_template, required_endpoints, color_hint,
                                       status, lock_state, notes, tags, external_refs,
                                       created_by, updated_by)
            VALUES (@org, @code, @name, @desc, @tpl, @req, @color,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindTypeWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateTypeAsync(LinkType e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.link_type SET
                type_code          = @code,
                display_name       = @name,
                description        = @desc,
                naming_template    = @tpl,
                required_endpoints = @req,
                color_hint         = @color,
                status             = @status::net.entity_status,
                lock_state         = @lock::net.lock_state,
                lock_reason        = @lreason,
                notes              = @notes,
                tags               = @tags::jsonb,
                external_refs      = @refs::jsonb,
                updated_at         = now(),
                updated_by         = @uid,
                version            = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindTypeWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteTypeAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.link_type", id, orgId, userId, ct);

    private static void BindTypeWrite(NpgsqlCommand cmd, LinkType e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("code", e.TypeCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("desc", (object?)e.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tpl", e.NamingTemplate);
        cmd.Parameters.AddWithValue("req", e.RequiredEndpoints);
        cmd.Parameters.AddWithValue("color", (object?)e.ColorHint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static LinkType ReadLinkType(NpgsqlDataReader r)
    {
        var e = new LinkType
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            TypeCode = r.GetString(2),
            DisplayName = r.GetString(3),
            Description = r.IsDBNull(4) ? null : r.GetString(4),
            NamingTemplate = r.GetString(5),
            RequiredEndpoints = r.GetInt32(6),
            ColorHint = r.IsDBNull(7) ? null : r.GetString(7),
        };
        PopulateBase(e, r, 8);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Link
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// List links for a tenant, optionally narrowed by link type or
    /// building. Useful filter pairs: <c>(null, buildingId)</c> for
    /// "everything in this building", <c>(typeId, null)</c> for
    /// "every B2B in the tenant".
    /// </summary>
    public async Task<List<Link>> ListLinksAsync(Guid orgId, Guid? linkTypeId, Guid? buildingId,
        CancellationToken ct = default)
    {
        var sql = @"
            SELECT id, organization_id, link_type_id, building_id, link_code,
                   display_name, description, vlan_id, subnet_id, config_json::text,
                   legacy_link_kind, legacy_link_id, " + BaseColumns + @"
            FROM net.link
            WHERE organization_id = @org AND deleted_at IS NULL";
        if (linkTypeId.HasValue) sql += " AND link_type_id = @type";
        if (buildingId.HasValue) sql += " AND building_id = @bld";
        sql += " ORDER BY link_code";

        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (linkTypeId.HasValue) cmd.Parameters.AddWithValue("type", linkTypeId.Value);
        if (buildingId.HasValue) cmd.Parameters.AddWithValue("bld", buildingId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Link>();
        while (await r.ReadAsync(ct)) list.Add(ReadLink(r));
        return list;
    }

    public async Task<Link?> GetLinkAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, link_type_id, building_id, link_code,
                   display_name, description, vlan_id, subnet_id, config_json::text,
                   legacy_link_kind, legacy_link_id, " + BaseColumns + @"
            FROM net.link
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadLink, ct);
    }

    public async Task<Guid> CreateLinkAsync(Link e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.link (organization_id, link_type_id, building_id, link_code,
                                  display_name, description, vlan_id, subnet_id, config_json,
                                  legacy_link_kind, legacy_link_id,
                                  status, lock_state, notes, tags, external_refs,
                                  created_by, updated_by)
            VALUES (@org, @type, @bld, @code, @name, @desc, @vlan, @subnet, @cfg::jsonb,
                    @legkind, @legid,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindLinkWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateLinkAsync(Link e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.link SET
                link_type_id     = @type,
                building_id      = @bld,
                link_code        = @code,
                display_name     = @name,
                description      = @desc,
                vlan_id          = @vlan,
                subnet_id        = @subnet,
                config_json      = @cfg::jsonb,
                legacy_link_kind = @legkind,
                legacy_link_id   = @legid,
                status           = @status::net.entity_status,
                lock_state       = @lock::net.lock_state,
                lock_reason      = @lreason,
                notes            = @notes,
                tags             = @tags::jsonb,
                external_refs    = @refs::jsonb,
                updated_at       = now(),
                updated_by       = @uid,
                version          = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindLinkWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteLinkAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.link", id, orgId, userId, ct);

    private static void BindLinkWrite(NpgsqlCommand cmd, Link e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("type", e.LinkTypeId);
        cmd.Parameters.AddWithValue("bld", (object?)e.BuildingId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("code", e.LinkCode);
        cmd.Parameters.AddWithValue("name", (object?)e.DisplayName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("desc", (object?)e.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("vlan", (object?)e.VlanId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("subnet", (object?)e.SubnetId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cfg", e.ConfigJson.ToJsonString());
        cmd.Parameters.AddWithValue("legkind", (object?)e.LegacyLinkKind ?? DBNull.Value);
        cmd.Parameters.AddWithValue("legid", (object?)e.LegacyLinkId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static Link ReadLink(NpgsqlDataReader r)
    {
        var e = new Link
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            LinkTypeId = r.GetGuid(2),
            BuildingId = r.IsDBNull(3) ? null : r.GetGuid(3),
            LinkCode = r.GetString(4),
            DisplayName = r.IsDBNull(5) ? null : r.GetString(5),
            Description = r.IsDBNull(6) ? null : r.GetString(6),
            VlanId = r.IsDBNull(7) ? null : r.GetGuid(7),
            SubnetId = r.IsDBNull(8) ? null : r.GetGuid(8),
            ConfigJson = (JsonNode.Parse(r.GetString(9)) as JsonObject) ?? new(),
            LegacyLinkKind = r.IsDBNull(10) ? null : r.GetString(10),
            LegacyLinkId = r.IsDBNull(11) ? null : r.GetInt32(11),
        };
        PopulateBase(e, r, 12);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // LinkEndpoint
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// List the endpoint rows for a link in order (A side first, then
    /// B). Callers usually want both rows together — the config
    /// builder iterates them to substitute <c>{device_a}</c>/<c>_b</c>
    /// tokens.
    /// </summary>
    public async Task<List<LinkEndpoint>> ListEndpointsAsync(Guid orgId, Guid linkId,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, link_id, endpoint_order, device_id, port_id,
                   ip_address_id, vlan_id, interface_name, description, " + BaseColumns + @"
            FROM net.link_endpoint
            WHERE organization_id = @org AND link_id = @link AND deleted_at IS NULL
            ORDER BY endpoint_order";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        cmd.Parameters.AddWithValue("link", linkId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<LinkEndpoint>();
        while (await r.ReadAsync(ct)) list.Add(ReadEndpoint(r));
        return list;
    }

    public async Task<Guid> CreateEndpointAsync(LinkEndpoint e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.link_endpoint (organization_id, link_id, endpoint_order,
                                           device_id, port_id, ip_address_id, vlan_id,
                                           interface_name, description,
                                           status, lock_state, notes, tags, external_refs,
                                           created_by, updated_by)
            VALUES (@org, @link, @ord, @dev, @port, @ip, @vlan, @iface, @desc,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindEndpointWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateEndpointAsync(LinkEndpoint e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.link_endpoint SET
                link_id        = @link,
                endpoint_order = @ord,
                device_id      = @dev,
                port_id        = @port,
                ip_address_id  = @ip,
                vlan_id        = @vlan,
                interface_name = @iface,
                description    = @desc,
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
        BindEndpointWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteEndpointAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.link_endpoint", id, orgId, userId, ct);

    private static void BindEndpointWrite(NpgsqlCommand cmd, LinkEndpoint e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("link", e.LinkId);
        cmd.Parameters.AddWithValue("ord", e.EndpointOrder);
        cmd.Parameters.AddWithValue("dev", (object?)e.DeviceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("port", (object?)e.PortId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ip", (object?)e.IpAddressId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("vlan", (object?)e.VlanId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("iface", (object?)e.InterfaceName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("desc", (object?)e.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static LinkEndpoint ReadEndpoint(NpgsqlDataReader r)
    {
        var e = new LinkEndpoint
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            LinkId = r.GetGuid(2),
            EndpointOrder = r.GetInt32(3),
            DeviceId = r.IsDBNull(4) ? null : r.GetGuid(4),
            PortId = r.IsDBNull(5) ? null : r.GetGuid(5),
            IpAddressId = r.IsDBNull(6) ? null : r.GetGuid(6),
            VlanId = r.IsDBNull(7) ? null : r.GetGuid(7),
            InterfaceName = r.IsDBNull(8) ? null : r.GetString(8),
            Description = r.IsDBNull(9) ? null : r.GetString(9),
        };
        PopulateBase(e, r, 10);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Shared helpers
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
