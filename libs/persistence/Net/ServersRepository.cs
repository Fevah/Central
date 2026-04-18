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
}
