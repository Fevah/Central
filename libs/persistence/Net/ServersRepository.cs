using System.Text.Json.Nodes;
using Central.Engine.Net;
using Central.Engine.Net.Hierarchy;
using Central.Engine.Net.Servers;
using Npgsql;

namespace Central.Persistence.Net;

/// <summary>
/// Repository for the Phase-6 server catalog (server_profile / server /
/// server_nic). Reads only in 6b; writes + import + NIC fan-out logic
/// land in 6c and 6d.
/// </summary>
public class ServersRepository
{
    private readonly string _dsn;
    public ServersRepository(string dsn) => _dsn = dsn;

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
    // ServerProfile
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<ServerProfile>> ListProfilesAsync(Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, profile_code, display_name, description,
                   nic_count, default_loopback_prefix, naming_template, color_hint, " + BaseColumns + @"
            FROM net.server_profile
            WHERE organization_id = @org AND deleted_at IS NULL
            ORDER BY profile_code";
        return await ListAsync(sql, orgId, ReadProfile, ct);
    }

    public async Task<ServerProfile?> GetProfileAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, profile_code, display_name, description,
                   nic_count, default_loopback_prefix, naming_template, color_hint, " + BaseColumns + @"
            FROM net.server_profile
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadProfile, ct);
    }

    public async Task<Guid> CreateProfileAsync(ServerProfile e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.server_profile (organization_id, profile_code, display_name, description,
                                            nic_count, default_loopback_prefix, naming_template, color_hint,
                                            status, lock_state, notes, tags, external_refs,
                                            created_by, updated_by)
            VALUES (@org, @code, @name, @desc, @nics, @pfx, @tpl, @color,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindProfileWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateProfileAsync(ServerProfile e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.server_profile SET
                profile_code             = @code,
                display_name             = @name,
                description              = @desc,
                nic_count                = @nics,
                default_loopback_prefix  = @pfx,
                naming_template          = @tpl,
                color_hint               = @color,
                status                   = @status::net.entity_status,
                lock_state               = @lock::net.lock_state,
                lock_reason              = @lreason,
                notes                    = @notes,
                tags                     = @tags::jsonb,
                external_refs            = @refs::jsonb,
                updated_at               = now(),
                updated_by               = @uid,
                version                  = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindProfileWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteProfileAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.server_profile", id, orgId, userId, ct);

    private static void BindProfileWrite(NpgsqlCommand cmd, ServerProfile e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("code", e.ProfileCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("desc", (object?)e.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("nics", e.NicCount);
        cmd.Parameters.AddWithValue("pfx", e.DefaultLoopbackPrefix);
        cmd.Parameters.AddWithValue("tpl", e.NamingTemplate);
        cmd.Parameters.AddWithValue("color", (object?)e.ColorHint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static ServerProfile ReadProfile(NpgsqlDataReader r)
    {
        var e = new ServerProfile
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            ProfileCode = r.GetString(2),
            DisplayName = r.GetString(3),
            Description = r.IsDBNull(4) ? null : r.GetString(4),
            NicCount = r.GetInt32(5),
            DefaultLoopbackPrefix = r.GetInt32(6),
            NamingTemplate = r.GetString(7),
            ColorHint = r.IsDBNull(8) ? null : r.GetString(8),
        };
        PopulateBase(e, r, 9);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Server
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<Server>> ListServersAsync(Guid orgId, Guid? buildingId,
        CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, server_profile_id, building_id, room_id, rack_id,
                   asn_allocation_id, loopback_ip_address_id, hostname, server_code, display_name,
                   hardware_model, serial_number, mac_address::text, firmware_version,
                   management_ip::text, ssh_username, ssh_port,
                   last_ping_at, last_ping_ok, last_ping_ms,
                   last_ssh_at, last_ssh_ok,
                   legacy_server_id, " + BaseColumns + @"
            FROM net.server
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (buildingId.HasValue ? " AND building_id = @bld" : "") + @"
            ORDER BY hostname";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (buildingId.HasValue) cmd.Parameters.AddWithValue("bld", buildingId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Server>();
        while (await r.ReadAsync(ct)) list.Add(ReadServer(r));
        return list;
    }

    public async Task<Server?> GetServerAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, server_profile_id, building_id, room_id, rack_id,
                   asn_allocation_id, loopback_ip_address_id, hostname, server_code, display_name,
                   hardware_model, serial_number, mac_address::text, firmware_version,
                   management_ip::text, ssh_username, ssh_port,
                   last_ping_at, last_ping_ok, last_ping_ms,
                   last_ssh_at, last_ssh_ok,
                   legacy_server_id, " + BaseColumns + @"
            FROM net.server
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadServer, ct);
    }

    public async Task<Guid> CreateServerAsync(Server e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.server
                (organization_id, server_profile_id, building_id, room_id, rack_id,
                 asn_allocation_id, loopback_ip_address_id, hostname, server_code, display_name,
                 hardware_model, serial_number, mac_address, firmware_version,
                 management_ip, ssh_username, ssh_port,
                 legacy_server_id,
                 status, lock_state, notes, tags, external_refs, created_by, updated_by)
            VALUES
                (@org, @prof, @bld, @room, @rack, @asn, @loop, @host, @code, @name,
                 @hw, @sn, @mac::macaddr, @fw,
                 @mip::inet, @sshuser, @sshport, @legacy,
                 @status::net.entity_status, @lock::net.lock_state,
                 @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindServerWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateServerAsync(Server e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.server SET
                server_profile_id      = @prof,
                building_id            = @bld,
                room_id                = @room,
                rack_id                = @rack,
                asn_allocation_id      = @asn,
                loopback_ip_address_id = @loop,
                hostname               = @host,
                server_code            = @code,
                display_name           = @name,
                hardware_model         = @hw,
                serial_number          = @sn,
                mac_address            = @mac::macaddr,
                firmware_version       = @fw,
                management_ip          = @mip::inet,
                ssh_username           = @sshuser,
                ssh_port               = @sshport,
                legacy_server_id       = @legacy,
                status                 = @status::net.entity_status,
                lock_state             = @lock::net.lock_state,
                lock_reason            = @lreason,
                notes                  = @notes,
                tags                   = @tags::jsonb,
                external_refs          = @refs::jsonb,
                updated_at             = now(),
                updated_by             = @uid,
                version                = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindServerWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteServerAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.server", id, orgId, userId, ct);

    /// <summary>Fast-path ping probe update; skips version bump for the same reason as Device.RecordPingAsync.</summary>
    public async Task RecordPingAsync(Guid id, Guid orgId, bool ok, decimal? latencyMs,
        CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.server SET
                last_ping_at = now(), last_ping_ok = @ok, last_ping_ms = @ms
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("org", orgId);
        cmd.Parameters.AddWithValue("ok", ok);
        cmd.Parameters.AddWithValue("ms", (object?)latencyMs ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void BindServerWrite(NpgsqlCommand cmd, Server e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("prof", (object?)e.ServerProfileId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("bld", (object?)e.BuildingId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("room", (object?)e.RoomId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("rack", (object?)e.RackId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("asn", (object?)e.AsnAllocationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("loop", (object?)e.LoopbackIpAddressId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("host", e.Hostname);
        cmd.Parameters.AddWithValue("code", (object?)e.ServerCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("name", (object?)e.DisplayName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hw", (object?)e.HardwareModel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sn", (object?)e.SerialNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mac", (object?)e.MacAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fw", (object?)e.FirmwareVersion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mip", (object?)e.ManagementIp ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sshuser", (object?)e.SshUsername ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sshport", (object?)e.SshPort ?? DBNull.Value);
        cmd.Parameters.AddWithValue("legacy", (object?)e.LegacyServerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static Server ReadServer(NpgsqlDataReader r)
    {
        var e = new Server
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            ServerProfileId = r.IsDBNull(2) ? null : r.GetGuid(2),
            BuildingId = r.IsDBNull(3) ? null : r.GetGuid(3),
            RoomId = r.IsDBNull(4) ? null : r.GetGuid(4),
            RackId = r.IsDBNull(5) ? null : r.GetGuid(5),
            AsnAllocationId = r.IsDBNull(6) ? null : r.GetGuid(6),
            LoopbackIpAddressId = r.IsDBNull(7) ? null : r.GetGuid(7),
            Hostname = r.GetString(8),
            ServerCode = r.IsDBNull(9) ? null : r.GetString(9),
            DisplayName = r.IsDBNull(10) ? null : r.GetString(10),
            HardwareModel = r.IsDBNull(11) ? null : r.GetString(11),
            SerialNumber = r.IsDBNull(12) ? null : r.GetString(12),
            MacAddress = r.IsDBNull(13) ? null : r.GetString(13),
            FirmwareVersion = r.IsDBNull(14) ? null : r.GetString(14),
            ManagementIp = r.IsDBNull(15) ? null : StripPrefix(r.GetString(15)),
            SshUsername = r.IsDBNull(16) ? null : r.GetString(16),
            SshPort = r.IsDBNull(17) ? null : r.GetInt32(17),
            LastPingAt = r.IsDBNull(18) ? null : r.GetDateTime(18),
            LastPingOk = r.IsDBNull(19) ? null : r.GetBoolean(19),
            LastPingMs = r.IsDBNull(20) ? null : r.GetDecimal(20),
            LastSshAt = r.IsDBNull(21) ? null : r.GetDateTime(21),
            LastSshOk = r.IsDBNull(22) ? null : r.GetBoolean(22),
            LegacyServerId = r.IsDBNull(23) ? null : r.GetInt32(23),
        };
        PopulateBase(e, r, 24);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // ServerNic
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<ServerNic>> ListNicsAsync(Guid orgId, Guid? serverId,
        CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, server_id, nic_index,
                   target_port_id, target_device_id, ip_address_id, subnet_id, vlan_id,
                   mlag_side, nic_name, mac_address::text, admin_up,
                   last_ping_at, last_ping_ok, " + BaseColumns + @"
            FROM net.server_nic
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (serverId.HasValue ? " AND server_id = @srv" : "") + @"
            ORDER BY server_id, nic_index";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (serverId.HasValue) cmd.Parameters.AddWithValue("srv", serverId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<ServerNic>();
        while (await r.ReadAsync(ct)) list.Add(ReadNic(r));
        return list;
    }

    public async Task<Guid> CreateNicAsync(ServerNic e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.server_nic
                (organization_id, server_id, nic_index, target_port_id, target_device_id,
                 ip_address_id, subnet_id, vlan_id, mlag_side,
                 nic_name, mac_address, admin_up,
                 status, lock_state, notes, tags, external_refs,
                 created_by, updated_by)
            VALUES
                (@org, @srv, @idx, @port, @dev, @ip, @subnet, @vlan, @side,
                 @nname, @mac::macaddr, @up,
                 @status::net.entity_status, @lock::net.lock_state,
                 @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindNicWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateNicAsync(ServerNic e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.server_nic SET
                server_id        = @srv,
                nic_index        = @idx,
                target_port_id   = @port,
                target_device_id = @dev,
                ip_address_id    = @ip,
                subnet_id        = @subnet,
                vlan_id          = @vlan,
                mlag_side        = @side,
                nic_name         = @nname,
                mac_address      = @mac::macaddr,
                admin_up         = @up,
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
        BindNicWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteNicAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.server_nic", id, orgId, userId, ct);

    private static void BindNicWrite(NpgsqlCommand cmd, ServerNic e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("srv", e.ServerId);
        cmd.Parameters.AddWithValue("idx", e.NicIndex);
        cmd.Parameters.AddWithValue("port", (object?)e.TargetPortId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("dev", (object?)e.TargetDeviceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ip", (object?)e.IpAddressId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("subnet", (object?)e.SubnetId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("vlan", (object?)e.VlanId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("side",
            e.MlagSide == MlagSide.None ? (object)DBNull.Value : e.MlagSide.ToString());
        cmd.Parameters.AddWithValue("nname", (object?)e.NicName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mac", (object?)e.MacAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("up", e.AdminUp);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static ServerNic ReadNic(NpgsqlDataReader r)
    {
        var e = new ServerNic
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            ServerId = r.GetGuid(2),
            NicIndex = r.GetInt32(3),
            TargetPortId = r.IsDBNull(4) ? null : r.GetGuid(4),
            TargetDeviceId = r.IsDBNull(5) ? null : r.GetGuid(5),
            IpAddressId = r.IsDBNull(6) ? null : r.GetGuid(6),
            SubnetId = r.IsDBNull(7) ? null : r.GetGuid(7),
            VlanId = r.IsDBNull(8) ? null : r.GetGuid(8),
            MlagSide = ParseMlagSide(r.IsDBNull(9) ? null : r.GetString(9)),
            NicName = r.IsDBNull(10) ? null : r.GetString(10),
            MacAddress = r.IsDBNull(11) ? null : r.GetString(11),
            AdminUp = r.GetBoolean(12),
            LastPingAt = r.IsDBNull(13) ? null : r.GetDateTime(13),
            LastPingOk = r.IsDBNull(14) ? null : r.GetBoolean(14),
        };
        PopulateBase(e, r, 15);
        return e;
    }

    private static MlagSide ParseMlagSide(string? raw) => raw switch
    {
        "A" => MlagSide.A,
        "B" => MlagSide.B,
        _   => MlagSide.None,
    };

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static string StripPrefix(string s)
    {
        var slash = s.IndexOf('/');
        return slash > 0 ? s[..slash] : s;
    }

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
