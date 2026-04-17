using System.Text.Json.Nodes;
using Central.Engine.Net;
using Central.Engine.Net.Devices;
using Central.Engine.Net.Hierarchy;
using Npgsql;

namespace Central.Persistence.Net;

/// <summary>
/// Repository for the Phase-4 device catalog (device_role / device /
/// module / port / aggregate_ethernet / loopback /
/// building_profile_role_count).
///
/// <para>Reads land here in Phase 4b. Writes ship in 4c — device
/// creation from the migration path (4e) and the UI (4f) both route
/// through the soon-to-exist Create / Update / SoftDelete methods on
/// this class.</para>
///
/// <para>Same idioms as <see cref="HierarchyRepository"/> and
/// <see cref="PoolsRepository"/>: text-cast enums, the 17 universal
/// base columns via <see cref="PopulateBase"/>, per-family Read
/// helpers.</para>
/// </summary>
public class DevicesRepository
{
    private readonly string _dsn;
    public DevicesRepository(string dsn) => _dsn = dsn;

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

    // ── Shared mappers ──────────────────────────────────────────────────

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
    // DeviceRole
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<DeviceRole>> ListRolesAsync(Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, role_code, display_name, description,
                   default_asn_kind, default_loopback_prefix, color_hint, " + BaseColumns + @"
            FROM net.device_role
            WHERE organization_id = @org AND deleted_at IS NULL
            ORDER BY role_code";
        return await ListAsync(sql, orgId, ReadDeviceRole, ct);
    }

    public async Task<DeviceRole?> GetRoleAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, role_code, display_name, description,
                   default_asn_kind, default_loopback_prefix, color_hint, " + BaseColumns + @"
            FROM net.device_role
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadDeviceRole, ct);
    }

    public async Task<Guid> CreateRoleAsync(DeviceRole e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.device_role (organization_id, role_code, display_name, description,
                                         default_asn_kind, default_loopback_prefix, color_hint,
                                         status, lock_state, notes, tags, external_refs,
                                         created_by, updated_by)
            VALUES (@org, @code, @name, @desc, @kind, @pfx, @color,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindRoleWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateRoleAsync(DeviceRole e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.device_role SET
                role_code               = @code,
                display_name            = @name,
                description             = @desc,
                default_asn_kind        = @kind,
                default_loopback_prefix = @pfx,
                color_hint              = @color,
                status                  = @status::net.entity_status,
                lock_state              = @lock::net.lock_state,
                lock_reason             = @lreason,
                notes                   = @notes,
                tags                    = @tags::jsonb,
                external_refs           = @refs::jsonb,
                updated_at              = now(),
                updated_by              = @uid,
                version                 = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindRoleWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteRoleAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.device_role", id, orgId, userId, ct);

    private static void BindRoleWrite(NpgsqlCommand cmd, DeviceRole e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("code", e.RoleCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("desc", (object?)e.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("kind", (object?)e.DefaultAsnKind ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pfx", (object?)e.DefaultLoopbackPrefix ?? DBNull.Value);
        cmd.Parameters.AddWithValue("color", (object?)e.ColorHint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static DeviceRole ReadDeviceRole(NpgsqlDataReader r)
    {
        var e = new DeviceRole
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            RoleCode = r.GetString(2),
            DisplayName = r.GetString(3),
            Description = r.IsDBNull(4) ? null : r.GetString(4),
            DefaultAsnKind = r.IsDBNull(5) ? null : r.GetString(5),
            DefaultLoopbackPrefix = r.IsDBNull(6) ? null : r.GetInt32(6),
            ColorHint = r.IsDBNull(7) ? null : r.GetString(7),
        };
        PopulateBase(e, r, 8);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Device
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// List devices for a tenant, optionally narrowed to a building. A
    /// null <paramref name="buildingId"/> returns all devices in the
    /// tenant (used by the IPAM-style cross-site grid).
    /// </summary>
    public async Task<List<Device>> ListDevicesAsync(Guid orgId, Guid? buildingId, CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, device_role_id, building_id, room_id, rack_id,
                   asn_allocation_id, hostname, device_code, display_name,
                   hardware_model, serial_number, mac_address::text, firmware_version,
                   management_ip::text, ssh_username, ssh_port,
                   management_vrf, inband_enabled,
                   last_ping_at, last_ping_ok, last_ping_ms,
                   last_ssh_at, last_ssh_ok,
                   legacy_switch_id, " + BaseColumns + @"
            FROM net.device
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (buildingId.HasValue ? " AND building_id = @bld" : "") + @"
            ORDER BY hostname";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (buildingId.HasValue) cmd.Parameters.AddWithValue("bld", buildingId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Device>();
        while (await r.ReadAsync(ct)) list.Add(ReadDevice(r));
        return list;
    }

    public async Task<Device?> GetDeviceAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, device_role_id, building_id, room_id, rack_id,
                   asn_allocation_id, hostname, device_code, display_name,
                   hardware_model, serial_number, mac_address::text, firmware_version,
                   management_ip::text, ssh_username, ssh_port,
                   management_vrf, inband_enabled,
                   last_ping_at, last_ping_ok, last_ping_ms,
                   last_ssh_at, last_ssh_ok,
                   legacy_switch_id, " + BaseColumns + @"
            FROM net.device
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadDevice, ct);
    }

    public async Task<Guid> CreateDeviceAsync(Device e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.device
                (organization_id, device_role_id, building_id, room_id, rack_id,
                 asn_allocation_id, hostname, device_code, display_name,
                 hardware_model, serial_number, mac_address, firmware_version,
                 management_ip, ssh_username, ssh_port,
                 management_vrf, inband_enabled,
                 legacy_switch_id,
                 status, lock_state, notes, tags, external_refs, created_by, updated_by)
            VALUES
                (@org, @role, @bld, @room, @rack, @asn, @host, @code, @name,
                 @hw, @sn, @mac::macaddr, @fw,
                 @mip::inet, @sshuser, @sshport, @vrf, @inband, @legacy,
                 @status::net.entity_status, @lock::net.lock_state,
                 @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindDeviceWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateDeviceAsync(Device e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.device SET
                device_role_id    = @role,
                building_id       = @bld,
                room_id           = @room,
                rack_id           = @rack,
                asn_allocation_id = @asn,
                hostname          = @host,
                device_code       = @code,
                display_name      = @name,
                hardware_model    = @hw,
                serial_number     = @sn,
                mac_address       = @mac::macaddr,
                firmware_version  = @fw,
                management_ip     = @mip::inet,
                ssh_username      = @sshuser,
                ssh_port          = @sshport,
                management_vrf    = @vrf,
                inband_enabled    = @inband,
                legacy_switch_id  = @legacy,
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
        BindDeviceWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteDeviceAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.device", id, orgId, userId, ct);

    /// <summary>
    /// Record a ping probe result against a device. Fast-path write that
    /// skips the version bump — monitoring churn shouldn't create
    /// concurrency conflicts against human edits.
    /// </summary>
    public async Task RecordPingAsync(Guid id, Guid orgId, bool ok, decimal? latencyMs,
        CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.device SET
                last_ping_at = now(),
                last_ping_ok = @ok,
                last_ping_ms = @ms
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("org", orgId);
        cmd.Parameters.AddWithValue("ok", ok);
        cmd.Parameters.AddWithValue("ms", (object?)latencyMs ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void BindDeviceWrite(NpgsqlCommand cmd, Device e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("role", (object?)e.DeviceRoleId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("bld", (object?)e.BuildingId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("room", (object?)e.RoomId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("rack", (object?)e.RackId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("asn", (object?)e.AsnAllocationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("host", e.Hostname);
        cmd.Parameters.AddWithValue("code", (object?)e.DeviceCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("name", (object?)e.DisplayName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hw", (object?)e.HardwareModel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sn", (object?)e.SerialNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mac", (object?)e.MacAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fw", (object?)e.FirmwareVersion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mip", (object?)e.ManagementIp ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sshuser", (object?)e.SshUsername ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sshport", (object?)e.SshPort ?? DBNull.Value);
        cmd.Parameters.AddWithValue("vrf", e.ManagementVrf);
        cmd.Parameters.AddWithValue("inband", e.InbandEnabled);
        cmd.Parameters.AddWithValue("legacy", (object?)e.LegacySwitchId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static Device ReadDevice(NpgsqlDataReader r)
    {
        var e = new Device
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            DeviceRoleId = r.IsDBNull(2) ? null : r.GetGuid(2),
            BuildingId = r.IsDBNull(3) ? null : r.GetGuid(3),
            RoomId = r.IsDBNull(4) ? null : r.GetGuid(4),
            RackId = r.IsDBNull(5) ? null : r.GetGuid(5),
            AsnAllocationId = r.IsDBNull(6) ? null : r.GetGuid(6),
            Hostname = r.GetString(7),
            DeviceCode = r.IsDBNull(8) ? null : r.GetString(8),
            DisplayName = r.IsDBNull(9) ? null : r.GetString(9),
            HardwareModel = r.IsDBNull(10) ? null : r.GetString(10),
            SerialNumber = r.IsDBNull(11) ? null : r.GetString(11),
            MacAddress = r.IsDBNull(12) ? null : r.GetString(12),
            FirmwareVersion = r.IsDBNull(13) ? null : r.GetString(13),
            ManagementIp = r.IsDBNull(14) ? null : StripPrefix(r.GetString(14)),
            SshUsername = r.IsDBNull(15) ? null : r.GetString(15),
            SshPort = r.IsDBNull(16) ? null : r.GetInt32(16),
            ManagementVrf = r.GetBoolean(17),
            InbandEnabled = r.GetBoolean(18),
            LastPingAt = r.IsDBNull(19) ? null : r.GetDateTime(19),
            LastPingOk = r.IsDBNull(20) ? null : r.GetBoolean(20),
            LastPingMs = r.IsDBNull(21) ? null : r.GetDecimal(21),
            LastSshAt = r.IsDBNull(22) ? null : r.GetDateTime(22),
            LastSshOk = r.IsDBNull(23) ? null : r.GetBoolean(23),
            LegacySwitchId = r.IsDBNull(24) ? null : r.GetGuid(24),
        };
        PopulateBase(e, r, 25);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Module
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<Module>> ListModulesAsync(Guid orgId, Guid? deviceId, CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, device_id, slot, module_type,
                   model, serial_number, part_number, " + BaseColumns + @"
            FROM net.module
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (deviceId.HasValue ? " AND device_id = @dev" : "") + @"
            ORDER BY slot";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (deviceId.HasValue) cmd.Parameters.AddWithValue("dev", deviceId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Module>();
        while (await r.ReadAsync(ct)) list.Add(ReadModule(r));
        return list;
    }

    public async Task<Guid> CreateModuleAsync(Module e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.module (organization_id, device_id, slot, module_type,
                                    model, serial_number, part_number,
                                    status, lock_state, notes, tags, external_refs,
                                    created_by, updated_by)
            VALUES (@org, @dev, @slot, @type, @model, @sn, @pn,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindModuleWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateModuleAsync(Module e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.module SET
                device_id     = @dev,
                slot          = @slot,
                module_type   = @type,
                model         = @model,
                serial_number = @sn,
                part_number   = @pn,
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
        BindModuleWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteModuleAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.module", id, orgId, userId, ct);

    private static void BindModuleWrite(NpgsqlCommand cmd, Module e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("dev", e.DeviceId);
        cmd.Parameters.AddWithValue("slot", e.Slot);
        cmd.Parameters.AddWithValue("type", e.ModuleType.ToString());
        cmd.Parameters.AddWithValue("model", (object?)e.Model ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sn", (object?)e.SerialNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pn", (object?)e.PartNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static Module ReadModule(NpgsqlDataReader r)
    {
        var e = new Module
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            DeviceId = r.GetGuid(2),
            Slot = r.GetString(3),
            ModuleType = Enum.Parse<ModuleType>(r.GetString(4)),
            Model = r.IsDBNull(5) ? null : r.GetString(5),
            SerialNumber = r.IsDBNull(6) ? null : r.GetString(6),
            PartNumber = r.IsDBNull(7) ? null : r.GetString(7),
        };
        PopulateBase(e, r, 8);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Port
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<Port>> ListPortsAsync(Guid orgId, Guid? deviceId, CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, device_id, module_id, breakout_parent_id,
                   aggregate_ethernet_id, interface_name, interface_prefix, speed_mbps,
                   admin_up, description, port_mode, native_vlan_id, config_json::text, " +
                   BaseColumns + @"
            FROM net.port
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (deviceId.HasValue ? " AND device_id = @dev" : "") + @"
            ORDER BY interface_name";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (deviceId.HasValue) cmd.Parameters.AddWithValue("dev", deviceId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Port>();
        while (await r.ReadAsync(ct)) list.Add(ReadPort(r));
        return list;
    }

    public async Task<Guid> CreatePortAsync(Port e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.port (organization_id, device_id, module_id, breakout_parent_id,
                                  aggregate_ethernet_id, interface_name, interface_prefix,
                                  speed_mbps, admin_up, description, port_mode, native_vlan_id,
                                  config_json,
                                  status, lock_state, notes, tags, external_refs,
                                  created_by, updated_by)
            VALUES (@org, @dev, @mod, @parent, @ae, @name, @prefix, @speed, @up, @desc,
                    @mode, @nvid, @cfg::jsonb,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindPortWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdatePortAsync(Port e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.port SET
                device_id             = @dev,
                module_id             = @mod,
                breakout_parent_id    = @parent,
                aggregate_ethernet_id = @ae,
                interface_name        = @name,
                interface_prefix      = @prefix,
                speed_mbps            = @speed,
                admin_up              = @up,
                description           = @desc,
                port_mode             = @mode,
                native_vlan_id        = @nvid,
                config_json           = @cfg::jsonb,
                status                = @status::net.entity_status,
                lock_state            = @lock::net.lock_state,
                lock_reason           = @lreason,
                notes                 = @notes,
                tags                  = @tags::jsonb,
                external_refs         = @refs::jsonb,
                updated_at            = now(),
                updated_by            = @uid,
                version               = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindPortWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeletePortAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.port", id, orgId, userId, ct);

    private static void BindPortWrite(NpgsqlCommand cmd, Port e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("dev", e.DeviceId);
        cmd.Parameters.AddWithValue("mod", (object?)e.ModuleId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("parent", (object?)e.BreakoutParentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ae", (object?)e.AggregateEthernetId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("name", e.InterfaceName);
        cmd.Parameters.AddWithValue("prefix", e.InterfacePrefix);
        cmd.Parameters.AddWithValue("speed", (object?)e.SpeedMbps ?? DBNull.Value);
        cmd.Parameters.AddWithValue("up", e.AdminUp);
        cmd.Parameters.AddWithValue("desc", (object?)e.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mode", e.PortMode.ToString().ToLowerInvariant());
        cmd.Parameters.AddWithValue("nvid", (object?)e.NativeVlanId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cfg", e.ConfigJson.ToJsonString());
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static Port ReadPort(NpgsqlDataReader r)
    {
        var e = new Port
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            DeviceId = r.GetGuid(2),
            ModuleId = r.IsDBNull(3) ? null : r.GetGuid(3),
            BreakoutParentId = r.IsDBNull(4) ? null : r.GetGuid(4),
            AggregateEthernetId = r.IsDBNull(5) ? null : r.GetGuid(5),
            InterfaceName = r.GetString(6),
            InterfacePrefix = r.GetString(7),
            SpeedMbps = r.IsDBNull(8) ? null : r.GetInt64(8),
            AdminUp = r.GetBoolean(9),
            Description = r.IsDBNull(10) ? null : r.GetString(10),
            PortMode = ParsePortMode(r.GetString(11)),
            NativeVlanId = r.IsDBNull(12) ? null : r.GetInt32(12),
            ConfigJson = (JsonNode.Parse(r.GetString(13)) as JsonObject) ?? new(),
        };
        PopulateBase(e, r, 14);
        return e;
    }

    private static PortMode ParsePortMode(string s) => s switch
    {
        "access"  => PortMode.Access,
        "trunk"   => PortMode.Trunk,
        "routed"  => PortMode.Routed,
        _         => PortMode.Unset,
    };

    // ═══════════════════════════════════════════════════════════════════
    // AggregateEthernet
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<AggregateEthernet>> ListAggregatesAsync(Guid orgId, Guid? deviceId,
        CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, device_id, ae_name, lacp_mode, min_links,
                   description, " + BaseColumns + @"
            FROM net.aggregate_ethernet
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (deviceId.HasValue ? " AND device_id = @dev" : "") + @"
            ORDER BY ae_name";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (deviceId.HasValue) cmd.Parameters.AddWithValue("dev", deviceId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<AggregateEthernet>();
        while (await r.ReadAsync(ct)) list.Add(ReadAggregate(r));
        return list;
    }

    public async Task<Guid> CreateAggregateAsync(AggregateEthernet e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.aggregate_ethernet (organization_id, device_id, ae_name,
                                                lacp_mode, min_links, description,
                                                status, lock_state, notes, tags, external_refs,
                                                created_by, updated_by)
            VALUES (@org, @dev, @name, @mode, @min, @desc,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindAggregateWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateAggregateAsync(AggregateEthernet e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.aggregate_ethernet SET
                device_id     = @dev,
                ae_name       = @name,
                lacp_mode     = @mode,
                min_links     = @min,
                description   = @desc,
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
        BindAggregateWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteAggregateAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.aggregate_ethernet", id, orgId, userId, ct);

    private static void BindAggregateWrite(NpgsqlCommand cmd, AggregateEthernet e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("dev", e.DeviceId);
        cmd.Parameters.AddWithValue("name", e.AeName);
        cmd.Parameters.AddWithValue("mode", e.LacpMode.ToString().ToLowerInvariant());
        cmd.Parameters.AddWithValue("min", e.MinLinks);
        cmd.Parameters.AddWithValue("desc", (object?)e.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static AggregateEthernet ReadAggregate(NpgsqlDataReader r)
    {
        var e = new AggregateEthernet
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            DeviceId = r.GetGuid(2),
            AeName = r.GetString(3),
            LacpMode = ParseLacpMode(r.GetString(4)),
            MinLinks = r.GetInt32(5),
            Description = r.IsDBNull(6) ? null : r.GetString(6),
        };
        PopulateBase(e, r, 7);
        return e;
    }

    private static LacpMode ParseLacpMode(string s) => s switch
    {
        "active"  => LacpMode.Active,
        "passive" => LacpMode.Passive,
        "static"  => LacpMode.Static,
        _         => LacpMode.Active,
    };

    // ═══════════════════════════════════════════════════════════════════
    // Loopback
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<Loopback>> ListLoopbacksAsync(Guid orgId, Guid? deviceId,
        CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, device_id, loopback_number, ip_address_id,
                   description, " + BaseColumns + @"
            FROM net.loopback
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (deviceId.HasValue ? " AND device_id = @dev" : "") + @"
            ORDER BY device_id, loopback_number";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (deviceId.HasValue) cmd.Parameters.AddWithValue("dev", deviceId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Loopback>();
        while (await r.ReadAsync(ct)) list.Add(ReadLoopback(r));
        return list;
    }

    public async Task<Guid> CreateLoopbackAsync(Loopback e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.loopback (organization_id, device_id, loopback_number, ip_address_id,
                                      description,
                                      status, lock_state, notes, tags, external_refs,
                                      created_by, updated_by)
            VALUES (@org, @dev, @num, @ip, @desc,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindLoopbackWrite(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateLoopbackAsync(Loopback e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.loopback SET
                device_id       = @dev,
                loopback_number = @num,
                ip_address_id   = @ip,
                description     = @desc,
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
        BindLoopbackWrite(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteLoopbackAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.loopback", id, orgId, userId, ct);

    private static void BindLoopbackWrite(NpgsqlCommand cmd, Loopback e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("dev", e.DeviceId);
        cmd.Parameters.AddWithValue("num", e.LoopbackNumber);
        cmd.Parameters.AddWithValue("ip", (object?)e.IpAddressId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("desc", (object?)e.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static Loopback ReadLoopback(NpgsqlDataReader r)
    {
        var e = new Loopback
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            DeviceId = r.GetGuid(2),
            LoopbackNumber = r.GetInt32(3),
            IpAddressId = r.IsDBNull(4) ? null : r.GetGuid(4),
            Description = r.IsDBNull(5) ? null : r.GetString(5),
        };
        PopulateBase(e, r, 6);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // BuildingProfileRoleCount
    // ═══════════════════════════════════════════════════════════════════

    public async Task<List<BuildingProfileRoleCount>> ListProfileRoleCountsAsync(
        Guid orgId, Guid? buildingProfileId, CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, building_profile_id, device_role_id,
                   expected_count, " + BaseColumns + @"
            FROM net.building_profile_role_count
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (buildingProfileId.HasValue ? " AND building_profile_id = @prof" : "") + @"
            ORDER BY building_profile_id, device_role_id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (buildingProfileId.HasValue) cmd.Parameters.AddWithValue("prof", buildingProfileId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<BuildingProfileRoleCount>();
        while (await r.ReadAsync(ct)) list.Add(ReadProfileRoleCount(r));
        return list;
    }

    private static BuildingProfileRoleCount ReadProfileRoleCount(NpgsqlDataReader r)
    {
        var e = new BuildingProfileRoleCount
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            BuildingProfileId = r.GetGuid(2),
            DeviceRoleId = r.GetGuid(3),
            ExpectedCount = r.GetInt32(4),
        };
        PopulateBase(e, r, 5);
        return e;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Shared helpers
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
