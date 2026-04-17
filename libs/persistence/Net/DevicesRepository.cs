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
}
