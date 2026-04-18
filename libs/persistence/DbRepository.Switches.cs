using Npgsql;
using Central.Engine.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Central.Persistence;

public partial class DbRepository
{
    // ── SSH Override IP ─────────────────────────────────────────────────

    public async Task UpdateSshOverrideIpAsync(Guid switchId, string ip)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("UPDATE switches SET ssh_override_ip=@ip WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", switchId);
        cmd.Parameters.AddWithValue("@ip", ip);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Audit Log ────────────────────────────────────────────────────────

    public async Task AddAuditLogAsync(Guid switchId, string op, string action, string? field, string? oldVal, string? newVal, string desc = "")
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO switch_audit_log (switch_id, operator, action, field_name, old_value, new_value, description)
            VALUES (@sid, @op, @act, @f, @old, @new, @desc)
            """, conn);
        cmd.Parameters.AddWithValue("@sid", switchId);
        cmd.Parameters.AddWithValue("@op", op);
        cmd.Parameters.AddWithValue("@act", action);
        cmd.Parameters.AddWithValue("@f", (object?)field ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@old", (object?)oldVal ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@new", (object?)newVal ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@desc", desc);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<(int Id, DateTime Timestamp, string Operator, string Action, string Field, string OldValue, string NewValue, string Description)>> GetAuditLogAsync(Guid switchId)
    {
        var list = new List<(int, DateTime, string, string, string, string, string, string)>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, timestamp, operator, action, COALESCE(field_name,''), COALESCE(old_value,''), COALESCE(new_value,''), COALESCE(description,'') FROM switch_audit_log WHERE switch_id=@id ORDER BY timestamp DESC", conn);
        cmd.Parameters.AddWithValue("@id", switchId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((r.GetInt32(0), r.GetDateTime(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7)));
        return list;
    }

    // ── Config Backups ───────────────────────────────────────────────────

    public async Task SaveConfigBackupAsync(Guid switchId, string configText, string sourceIp, string op, string desc)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO config_backups (switch_id, operator, description, config_text, line_count, source_ip)
            VALUES (@id, @op, @desc, @cfg, @lines, @ip)
            """, conn);
        cmd.Parameters.AddWithValue("@id", switchId);
        cmd.Parameters.AddWithValue("@op", op);
        cmd.Parameters.AddWithValue("@desc", desc);
        cmd.Parameters.AddWithValue("@cfg", configText);
        cmd.Parameters.AddWithValue("@lines", configText.Split('\n').Length);
        cmd.Parameters.AddWithValue("@ip", sourceIp);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<(int Id, DateTime CreatedAt, string Operator, string Description, int LineCount)>> GetConfigBackupsAsync(Guid switchId)
    {
        var list = new List<(int, DateTime, string, string, int)>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, created_at, operator, COALESCE(description,''), line_count FROM config_backups WHERE switch_id=@id ORDER BY created_at DESC", conn);
        cmd.Parameters.AddWithValue("@id", switchId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((r.GetInt32(0), r.GetDateTime(1), r.GetString(2), r.GetString(3), r.GetInt32(4)));
        return list;
    }

    public async Task<string?> GetConfigBackupTextAsync(int backupId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT config_text FROM config_backups WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", backupId);
        return (await cmd.ExecuteScalarAsync()) as string;
    }

    public async Task UpdateConfigBackupDescriptionAsync(int backupId, string description)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("UPDATE config_backups SET description=@d WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", backupId);
        cmd.Parameters.AddWithValue("@d", description);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RestoreConfigBackupAsync(Guid switchId, int backupId, string op)
    {
        var configText = await GetConfigBackupTextAsync(backupId);
        if (configText == null) return;
        // Save as new running config
        await SaveRunningConfigAsync(switchId, configText, "restore");
        await AddAuditLogAsync(switchId, op, "Config Restore", "config", null, $"Restored backup #{backupId}", "");
    }

    public async Task UpdateServerNicStatusAsync(Server s)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = "UPDATE servers SET nic1_status=@n1, nic2_status=@n2, nic3_status=@n3, nic4_status=@n4 WHERE id=@id";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", s.Id);
        cmd.Parameters.AddWithValue("@n1", s.Nic1Status);
        cmd.Parameters.AddWithValue("@n2", s.Nic2Status);
        cmd.Parameters.AddWithValue("@n3", s.Nic3Status);
        cmd.Parameters.AddWithValue("@n4", s.Nic4Status);
        await cmd.ExecuteNonQueryAsync();
    }

    // S() helper is in DbRepository.cs

    // ── Configured Switches ────────────────────────────────────────────────

    public async Task<List<SwitchRecord>> GetSwitchesAsync(List<string>? allowedSites = null)
    {
        // Phase 4f transition flag. When set, read from net.device joined
        // with net.building + net.loopback + net.ip_address rather than
        // public.switches. The dual-write trigger (migration 090) keeps
        // both tables identical, so the caller sees the same data either
        // way. Off by default until every UI path is validated.
        if (Environment.GetEnvironmentVariable("CENTRAL_USE_NET_DEVICE") == "1")
            return await GetSwitchesFromNetDeviceAsync(allowedSites);

        var list = new List<SwitchRecord>();
        await using var conn = await OpenConnectionAsync();

        var where = "";
        if (allowedSites != null && allowedSites.Count > 0)
            where = "WHERE site = ANY(@sites)";
        else if (allowedSites != null && allowedSites.Count == 0)
            where = "WHERE FALSE";

        var sql = $"""
            SELECT
                id, hostname, site, role,
                split_part(cast(loopback_ip as text), '/', 1),
                loopback_prefix,
                split_part(cast(management_ip as text), '/', 1),
                ssh_username, ssh_port,
                COALESCE(ssh_password, ''),
                COALESCE(ssh_override_ip, ''),
                last_ping_ok, last_ping_ms,
                last_ssh_ok,
                last_ping_at::text,
                last_ssh_at::text,
                picos_version,
                COALESCE(hardware_model, ''),
                COALESCE(mac_address, ''),
                COALESCE(serial_number, ''),
                COALESCE(uptime, '')
            FROM switches
            {where}
            ORDER BY site, hostname
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        if (allowedSites != null && allowedSites.Count > 0)
            cmd.Parameters.AddWithValue("@sites", allowedSites.ToArray());
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
        {
            list.Add(new SwitchRecord
            {
                Id             = rdr.GetGuid(0),
                Hostname       = rdr.IsDBNull(1)  ? "" : rdr.GetString(1),
                Site           = rdr.IsDBNull(2)  ? "" : rdr.GetString(2),
                Role           = rdr.IsDBNull(3)  ? "" : rdr.GetString(3),
                LoopbackIp     = rdr.IsDBNull(4)  ? "" : rdr.GetString(4),
                LoopbackPrefix = rdr.IsDBNull(5)  ? 0  : rdr.GetInt32(5),
                ManagementIp   = rdr.IsDBNull(6)  ? "" : rdr.GetString(6),
                SshUsername    = rdr.IsDBNull(7)  ? "" : rdr.GetString(7),
                SshPort        = rdr.IsDBNull(8)  ? 22 : rdr.GetInt32(8),
                SshPassword    = rdr.GetString(9),
                SshOverrideIp  = rdr.GetString(10),
                LastPingOk     = rdr.IsDBNull(11) ? null : (bool?)rdr.GetBoolean(11),
                LastPingMs     = rdr.IsDBNull(12) ? null : (double?)Convert.ToDouble(rdr.GetValue(12)),
                LastSshOk      = rdr.IsDBNull(13) ? null : (bool?)rdr.GetBoolean(13),
                LastPingAt     = rdr.IsDBNull(14) ? "" : rdr.GetString(14),
                LastSshAt      = rdr.IsDBNull(15) ? "" : rdr.GetString(15),
                PicosVersion   = rdr.IsDBNull(16) ? "" : rdr.GetString(16),
                HardwareModel  = rdr.GetString(17),
                MacAddress     = rdr.GetString(18),
                SerialNumber   = rdr.GetString(19),
                Uptime         = rdr.GetString(20),
            });
        }
        return list;
    }

    /// <summary>
    /// Phase 4f reader — pulls SwitchRecord rows from net.device joined
    /// with building / loopback / ip_address / device_role, plus a
    /// LEFT JOIN to public.switches by legacy_switch_id for the handful
    /// of fields that haven't been lifted into the net.* schema yet
    /// (ssh_password, ssh_override_ip, uptime).
    ///
    /// The projected SwitchRecord shape is identical to what
    /// <see cref="GetSwitchesAsync"/> returns from the legacy path, so
    /// the UI doesn't notice the swap. Controlled by the
    /// CENTRAL_USE_NET_DEVICE env var until we're satisfied; the
    /// dual-write trigger guarantees the data matches either way.
    /// </summary>
    public async Task<List<SwitchRecord>> GetSwitchesFromNetDeviceAsync(List<string>? allowedSites = null)
    {
        var list = new List<SwitchRecord>();
        await using var conn = await OpenConnectionAsync();

        var where = "d.deleted_at IS NULL";
        if (allowedSites != null && allowedSites.Count == 0)
            where += " AND FALSE";
        else if (allowedSites != null && allowedSites.Count > 0)
            where += " AND b.building_code = ANY(@sites)";

        var sql = $"""
            SELECT
                d.id,
                d.hostname,
                COALESCE(b.building_code, '')       AS site,
                COALESCE(lower(r.role_code), '')    AS role,
                COALESCE(host(ia.address), '')      AS loopback_ip,
                COALESCE(masklen(ia.address), 0)    AS loopback_prefix,
                COALESCE(host(d.management_ip), '') AS management_ip,
                COALESCE(d.ssh_username, '')        AS ssh_username,
                COALESCE(d.ssh_port, 22)            AS ssh_port,
                COALESCE(s.ssh_password, '')        AS ssh_password,
                COALESCE(s.ssh_override_ip, '')     AS ssh_override_ip,
                d.last_ping_ok, d.last_ping_ms,
                d.last_ssh_ok,
                d.last_ping_at::text,
                d.last_ssh_at::text,
                d.firmware_version                  AS picos_version,
                COALESCE(d.hardware_model, '')      AS hardware_model,
                COALESCE(d.mac_address::text, '')   AS mac_address,
                COALESCE(d.serial_number, '')       AS serial_number,
                COALESCE(s.uptime, '')              AS uptime
              FROM net.device d
              LEFT JOIN net.building    b  ON b.id = d.building_id
              LEFT JOIN net.device_role r  ON r.id = d.device_role_id
              LEFT JOIN net.loopback    lo ON lo.device_id = d.id AND lo.loopback_number = 0
                                           AND lo.deleted_at IS NULL
              LEFT JOIN net.ip_address  ia ON ia.id = lo.ip_address_id AND ia.deleted_at IS NULL
              LEFT JOIN public.switches s  ON s.id = d.legacy_switch_id
             WHERE {where}
             ORDER BY site, d.hostname
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        if (allowedSites != null && allowedSites.Count > 0)
            cmd.Parameters.AddWithValue("@sites", allowedSites.ToArray());
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
        {
            list.Add(new SwitchRecord
            {
                Id             = rdr.GetGuid(0),
                Hostname       = rdr.IsDBNull(1)  ? "" : rdr.GetString(1),
                Site           = rdr.IsDBNull(2)  ? "" : rdr.GetString(2),
                Role           = rdr.IsDBNull(3)  ? "" : rdr.GetString(3),
                LoopbackIp     = rdr.IsDBNull(4)  ? "" : rdr.GetString(4),
                LoopbackPrefix = rdr.IsDBNull(5)  ? 0  : rdr.GetInt32(5),
                ManagementIp   = rdr.IsDBNull(6)  ? "" : rdr.GetString(6),
                SshUsername    = rdr.IsDBNull(7)  ? "" : rdr.GetString(7),
                SshPort        = rdr.IsDBNull(8)  ? 22 : rdr.GetInt32(8),
                SshPassword    = rdr.GetString(9),
                SshOverrideIp  = rdr.GetString(10),
                LastPingOk     = rdr.IsDBNull(11) ? null : (bool?)rdr.GetBoolean(11),
                LastPingMs     = rdr.IsDBNull(12) ? null : (double?)Convert.ToDouble(rdr.GetValue(12)),
                LastSshOk      = rdr.IsDBNull(13) ? null : (bool?)rdr.GetBoolean(13),
                LastPingAt     = rdr.IsDBNull(14) ? "" : rdr.GetString(14),
                LastSshAt      = rdr.IsDBNull(15) ? "" : rdr.GetString(15),
                PicosVersion   = rdr.IsDBNull(16) ? "" : rdr.GetString(16),
                HardwareModel  = rdr.GetString(17),
                MacAddress     = rdr.GetString(18),
                SerialNumber   = rdr.GetString(19),
                Uptime         = rdr.GetString(20),
            });
        }
        return list;
    }

    public async Task<SwitchRecord?> GetSwitchByHostnameAsync(string hostname)
    {
        await using var conn = await OpenConnectionAsync();
        var sql = """
            SELECT
                id, hostname, site, role,
                split_part(cast(loopback_ip as text), '/', 1),
                loopback_prefix,
                split_part(cast(management_ip as text), '/', 1),
                ssh_username, ssh_port,
                COALESCE(ssh_password, ''),
                COALESCE(ssh_override_ip, ''),
                last_ping_ok, last_ping_ms,
                last_ssh_ok,
                last_ping_at::text,
                last_ssh_at::text,
                picos_version,
                COALESCE(hardware_model, ''),
                COALESCE(mac_address, ''),
                COALESCE(serial_number, ''),
                COALESCE(uptime, '')
            FROM switches WHERE hostname = @h LIMIT 1
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@h", hostname);
        await using var rdr = await cmd.ExecuteReaderAsync();
        if (!await rdr.ReadAsync()) return null;
        return new SwitchRecord
        {
            Id             = rdr.GetGuid(0),
            Hostname       = rdr.IsDBNull(1)  ? "" : rdr.GetString(1),
            Site           = rdr.IsDBNull(2)  ? "" : rdr.GetString(2),
            Role           = rdr.IsDBNull(3)  ? "" : rdr.GetString(3),
            LoopbackIp     = rdr.IsDBNull(4)  ? "" : rdr.GetString(4),
            LoopbackPrefix = rdr.IsDBNull(5)  ? 0  : rdr.GetInt32(5),
            ManagementIp   = rdr.IsDBNull(6)  ? "" : rdr.GetString(6),
            SshUsername    = rdr.IsDBNull(7)  ? "" : rdr.GetString(7),
            SshPort        = rdr.IsDBNull(8)  ? 22 : rdr.GetInt32(8),
            SshPassword    = rdr.GetString(9),
            SshOverrideIp  = rdr.GetString(10),
            LastPingOk     = rdr.IsDBNull(11) ? null : (bool?)rdr.GetBoolean(11),
            LastPingMs     = rdr.IsDBNull(12) ? null : (double?)Convert.ToDouble(rdr.GetValue(12)),
            LastSshOk      = rdr.IsDBNull(13) ? null : (bool?)rdr.GetBoolean(13),
            LastPingAt     = rdr.IsDBNull(14) ? "" : rdr.GetString(14),
            LastSshAt      = rdr.IsDBNull(15) ? "" : rdr.GetString(15),
            PicosVersion   = rdr.IsDBNull(16) ? "" : rdr.GetString(16),
            HardwareModel  = rdr.GetString(17),
            MacAddress     = rdr.GetString(18),
        };
    }

    public async Task UpdatePingResultAsync(string hostname, bool ok, double? ms)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            UPDATE switches SET last_ping_ok = @ok, last_ping_ms = @ms, last_ping_at = NOW()
            WHERE hostname = @h
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@h",  hostname);
        cmd.Parameters.AddWithValue("@ok", ok);
        cmd.Parameters.AddWithValue("@ms", (object?)ms ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateSshResultAsync(string hostname, bool ok)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            UPDATE switches SET last_ssh_ok = @ok, last_ssh_at = NOW()
            WHERE hostname = @h
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@h",  hostname);
        cmd.Parameters.AddWithValue("@ok", ok);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Switch Version ──────────────────────────────────────────────────

    public async Task SaveSwitchVersionAsync(SwitchVersion v)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            INSERT INTO switch_versions (switch_id, captured_at, mac_address, hardware_model, serial_number, uptime,
                linux_version, linux_date, l2l3_version, l2l3_date, ovs_version, ovs_date, raw_output)
            VALUES (@sid, NOW(), @mac, @hw, @sn, @up, @lv, @ld, @l2v, @l2d, @ov, @od, @raw)
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sid", v.SwitchId);
        cmd.Parameters.AddWithValue("@mac", v.MacAddress);
        cmd.Parameters.AddWithValue("@hw",  v.HardwareModel);
        cmd.Parameters.AddWithValue("@sn",  v.SerialNumber);
        cmd.Parameters.AddWithValue("@up",  v.Uptime);
        cmd.Parameters.AddWithValue("@lv",  v.LinuxVersion);
        cmd.Parameters.AddWithValue("@ld",  v.LinuxDate);
        cmd.Parameters.AddWithValue("@l2v", v.L2L3Version);
        cmd.Parameters.AddWithValue("@l2d", v.L2L3Date);
        cmd.Parameters.AddWithValue("@ov",  v.OvsVersion);
        cmd.Parameters.AddWithValue("@od",  v.OvsDate);
        cmd.Parameters.AddWithValue("@raw", v.RawOutput);
        await cmd.ExecuteNonQueryAsync();

        // Update switches table with latest version/model/serial/uptime
        const string upd = """
            UPDATE switches SET picos_version = @ver, hardware_model = @hw, mac_address = @mac,
                serial_number = @sn, uptime = @up
            WHERE id = @sid
            """;
        await using var cmd2 = new NpgsqlCommand(upd, conn);
        cmd2.Parameters.AddWithValue("@sid", v.SwitchId);
        cmd2.Parameters.AddWithValue("@ver", v.L2L3Version);
        cmd2.Parameters.AddWithValue("@hw",  v.HardwareModel);
        cmd2.Parameters.AddWithValue("@mac", v.MacAddress);
        cmd2.Parameters.AddWithValue("@sn",  v.SerialNumber);
        cmd2.Parameters.AddWithValue("@up",  v.Uptime);
        await cmd2.ExecuteNonQueryAsync();
    }

    public async Task<SwitchVersion?> GetLatestSwitchVersionAsync(Guid switchId)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            SELECT id, switch_id, captured_at, mac_address, hardware_model,
                   linux_version, linux_date, l2l3_version, l2l3_date, ovs_version, ovs_date, raw_output
            FROM switch_versions WHERE switch_id = @sid ORDER BY captured_at DESC LIMIT 1
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sid", switchId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        if (!await rdr.ReadAsync()) return null;
        return new SwitchVersion
        {
            Id = rdr.GetInt32(0),
            SwitchId = rdr.GetGuid(1),
            CapturedAt = rdr.GetDateTime(2),
            MacAddress = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
            HardwareModel = rdr.IsDBNull(4) ? "" : rdr.GetString(4),
            LinuxVersion = rdr.IsDBNull(5) ? "" : rdr.GetString(5),
            LinuxDate = rdr.IsDBNull(6) ? "" : rdr.GetString(6),
            L2L3Version = rdr.IsDBNull(7) ? "" : rdr.GetString(7),
            L2L3Date = rdr.IsDBNull(8) ? "" : rdr.GetString(8),
            OvsVersion = rdr.IsDBNull(9) ? "" : rdr.GetString(9),
            OvsDate = rdr.IsDBNull(10) ? "" : rdr.GetString(10),
            RawOutput = rdr.IsDBNull(11) ? "" : rdr.GetString(11),
        };
    }

    // ── Switch Interfaces ────────────────────────────────────────────────

    public async Task SaveSwitchInterfacesAsync(Guid switchId, List<SwitchInterface> interfaces)
    {
        await using var conn = await OpenConnectionAsync();
        // Clear previous
        await using (var del = new NpgsqlCommand("DELETE FROM switch_interfaces WHERE switch_id = @sid", conn))
        {
            del.Parameters.AddWithValue("@sid", switchId);
            await del.ExecuteNonQueryAsync();
        }
        const string sql = """
            INSERT INTO switch_interfaces (switch_id, captured_at, interface_name, admin_status, link_status, speed, mtu, description, lldp_host, lldp_port)
            VALUES (@sid, NOW(), @name, @admin, @link, @speed, @mtu, @desc, @lhost, @lport)
            """;
        foreach (var iface in interfaces)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@sid",   switchId);
            cmd.Parameters.AddWithValue("@name",  iface.InterfaceName);
            cmd.Parameters.AddWithValue("@admin", iface.AdminStatus);
            cmd.Parameters.AddWithValue("@link",  iface.LinkStatus);
            cmd.Parameters.AddWithValue("@speed", iface.Speed);
            cmd.Parameters.AddWithValue("@mtu",   iface.Mtu);
            cmd.Parameters.AddWithValue("@desc",  iface.Description);
            cmd.Parameters.AddWithValue("@lhost", iface.LldpHost);
            cmd.Parameters.AddWithValue("@lport", iface.LldpPort);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<List<SwitchInterface>> GetSwitchInterfacesAsync(Guid switchId)
    {
        var list = new List<SwitchInterface>();
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            SELECT id, switch_id, captured_at, interface_name, admin_status, link_status, speed, mtu, description, lldp_host, lldp_port
            FROM switch_interfaces WHERE switch_id = @sid
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sid", switchId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
        {
            list.Add(new SwitchInterface
            {
                Id = rdr.GetInt32(0),
                SwitchId = rdr.GetGuid(1),
                CapturedAt = rdr.GetDateTime(2),
                InterfaceName = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                AdminStatus = rdr.IsDBNull(4) ? "" : rdr.GetString(4),
                LinkStatus = rdr.IsDBNull(5) ? "" : rdr.GetString(5),
                Speed = rdr.IsDBNull(6) ? "" : rdr.GetString(6),
                Mtu = rdr.IsDBNull(7) ? "" : rdr.GetString(7),
                Description = rdr.IsDBNull(8) ? "" : rdr.GetString(8),
                LldpHost = rdr.IsDBNull(9) ? "" : rdr.GetString(9),
                LldpPort = rdr.IsDBNull(10) ? "" : rdr.GetString(10),
            });
        }
        list.Sort((a, b) => NaturalCompareInterface(a.InterfaceName, b.InterfaceName));
        return list;
    }

    /// <summary>Natural sort for interface names: xe-1/1/2 before xe-1/1/10.</summary>
    internal static int NaturalCompareInterface(string a, string b)
    {
        var pa = System.Text.RegularExpressions.Regex.Split(a ?? "", @"(\d+)");
        var pb = System.Text.RegularExpressions.Regex.Split(b ?? "", @"(\d+)");
        for (int i = 0; i < Math.Min(pa.Length, pb.Length); i++)
        {
            if (int.TryParse(pa[i], out var na) && int.TryParse(pb[i], out var nb))
            { if (na != nb) return na.CompareTo(nb); }
            else
            { var c = string.Compare(pa[i], pb[i], StringComparison.OrdinalIgnoreCase); if (c != 0) return c; }
        }
        return pa.Length.CompareTo(pb.Length);
    }

    // ── Interface Descriptions (for live toggle) ──────────────────────

    /// <summary>Get interface descriptions for a switch by hostname (from latest sync)</summary>
    public async Task<Dictionary<string, string>> GetInterfaceDescriptionsAsync(string hostname)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            SELECT si.interface_name, si.description
            FROM switch_interfaces si
            JOIN switches s ON s.id = si.switch_id
            WHERE UPPER(s.hostname) = UPPER(@h) AND si.description <> ''
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@h", hostname);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var name = r.GetString(0);
            var desc = r.GetString(1);
            dict[name] = desc;
        }
        return dict;
    }

    /// <summary>Batch version: get interface descriptions for multiple hostnames in one query.</summary>
    public async Task<Dictionary<string, Dictionary<string, string>>> GetInterfaceDescriptionsBatchAsync(List<string> hostnames)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (hostnames.Count == 0) return result;
        foreach (var h in hostnames) result[h] = new(StringComparer.OrdinalIgnoreCase);

        await using var conn = await OpenConnectionAsync();
        const string sql = """
            SELECT s.hostname, si.interface_name, si.description
            FROM switch_interfaces si
            JOIN switches s ON s.id = si.switch_id
            WHERE UPPER(s.hostname) = ANY(SELECT UPPER(x) FROM unnest(@hosts) x)
              AND si.description <> ''
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@hosts", hostnames.ToArray());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var host = r.GetString(0);
            var name = r.GetString(1);
            var desc = r.GetString(2);
            if (!result.ContainsKey(host)) result[host] = new(StringComparer.OrdinalIgnoreCase);
            result[host][name] = desc;
        }
        return result;
    }

    // ── Switch Model Interfaces ─────────────────────────────────────────

    public async Task<List<string>> GetModelInterfacesAsync(string model)
    {
        var list = new List<string>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT interface_name FROM switch_model_interfaces WHERE model = @m ORDER BY sort_order, interface_name", conn);
        cmd.Parameters.AddWithValue("@m", model);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list;
    }

    // ── Interface Optics (historical) ───────────────────────────────────

    public async Task SaveInterfaceOpticsAsync(Guid switchId, List<InterfaceOptics> optics)
    {
        if (optics.Count == 0) return;
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            INSERT INTO interface_optics (switch_id, captured_at, interface_name, channel, temp_c, temp_f, voltage, bias_ma, tx_power_dbm, rx_power_dbm, module_type)
            VALUES (@sid, NOW(), @name, @ch, @tc, @tf, @v, @bias, @tx, @rx, @mod)
            """;
        foreach (var o in optics)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@sid", switchId);
            cmd.Parameters.AddWithValue("@name", o.InterfaceName);
            cmd.Parameters.AddWithValue("@ch", o.Channel);
            cmd.Parameters.AddWithValue("@tc", (object?)o.TempC ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tf", (object?)o.TempF ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@v", (object?)o.Voltage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@bias", (object?)o.BiasMa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tx", (object?)o.TxPowerDbm ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rx", (object?)o.RxPowerDbm ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mod", o.ModuleType);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>Get latest optics reading per interface (channel C1 or single-channel only for grid merge)</summary>
    public async Task<List<InterfaceOptics>> GetLatestOpticsAsync(Guid switchId)
    {
        var list = new List<InterfaceOptics>();
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            SELECT DISTINCT ON (interface_name) id, switch_id, captured_at, interface_name, channel,
                   temp_c, temp_f, voltage, bias_ma, tx_power_dbm, rx_power_dbm, module_type
            FROM interface_optics
            WHERE switch_id = @sid AND (channel = '' OR channel = 'C1')
            ORDER BY interface_name, captured_at DESC
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sid", switchId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new InterfaceOptics
            {
                Id = r.GetInt32(0),
                SwitchId = r.GetGuid(1),
                CapturedAt = r.GetDateTime(2),
                InterfaceName = r.IsDBNull(3) ? "" : r.GetString(3),
                Channel = r.IsDBNull(4) ? "" : r.GetString(4),
                TempC = r.IsDBNull(5) ? null : r.GetDecimal(5),
                TempF = r.IsDBNull(6) ? null : r.GetDecimal(6),
                Voltage = r.IsDBNull(7) ? null : r.GetDecimal(7),
                BiasMa = r.IsDBNull(8) ? null : r.GetDecimal(8),
                TxPowerDbm = r.IsDBNull(9) ? null : r.GetDecimal(9),
                RxPowerDbm = r.IsDBNull(10) ? null : r.GetDecimal(10),
                ModuleType = r.IsDBNull(11) ? "" : r.GetString(11),
            });
        }
        return list;
    }

    /// <summary>Get optics history for a specific interface (all channels, all readings)</summary>
    public async Task<List<InterfaceOptics>> GetOpticsHistoryAsync(Guid switchId, string interfaceName)
    {
        var list = new List<InterfaceOptics>();
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            SELECT id, switch_id, captured_at, interface_name, channel,
                   temp_c, temp_f, voltage, bias_ma, tx_power_dbm, rx_power_dbm, module_type
            FROM interface_optics
            WHERE switch_id = @sid AND interface_name = @name
            ORDER BY captured_at DESC, channel
            LIMIT 500
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sid", switchId);
        cmd.Parameters.AddWithValue("@name", interfaceName);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new InterfaceOptics
            {
                Id = r.GetInt32(0),
                SwitchId = r.GetGuid(1),
                CapturedAt = r.GetDateTime(2),
                InterfaceName = r.IsDBNull(3) ? "" : r.GetString(3),
                Channel = r.IsDBNull(4) ? "" : r.GetString(4),
                TempC = r.IsDBNull(5) ? null : r.GetDecimal(5),
                TempF = r.IsDBNull(6) ? null : r.GetDecimal(6),
                Voltage = r.IsDBNull(7) ? null : r.GetDecimal(7),
                BiasMa = r.IsDBNull(8) ? null : r.GetDecimal(8),
                TxPowerDbm = r.IsDBNull(9) ? null : r.GetDecimal(9),
                RxPowerDbm = r.IsDBNull(10) ? null : r.GetDecimal(10),
                ModuleType = r.IsDBNull(11) ? "" : r.GetString(11),
            });
        }
        return list;
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync();
            return true;
        }
        catch { return false; }
    }

    // ── Users ────────────────────────────────────────────────────────────

    public async Task<AppUser?> GetUserByUsernameAsync(string username)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = "SELECT id, username, display_name, role, is_active, auto_login FROM app_users WHERE username = @u AND is_active = TRUE";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@u", username);
        await using var rdr = await cmd.ExecuteReaderAsync();
        if (!await rdr.ReadAsync()) return null;
        return new AppUser
        {
            Id          = rdr.GetInt32(0),
            Username    = rdr.GetString(1),
            DisplayName = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
            Role        = rdr.GetString(3),
            IsActive    = rdr.GetBoolean(4),
            AutoLogin   = rdr.GetBoolean(5),
        };
    }

    public async Task<List<AppUser>> GetAllUsersAsync()
    {
        var list = new List<AppUser>();
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            SELECT id, username, display_name, role, is_active, auto_login,
                   COALESCE(user_type, 'Standard'), COALESCE(email, ''),
                   last_login_at, COALESCE(login_count, 0), created_at,
                   COALESCE(department, ''), COALESCE(title, ''),
                   COALESCE(phone, ''), COALESCE(mobile, ''),
                   COALESCE(company, ''), COALESCE(ad_guid, ''),
                   last_ad_sync
            FROM app_users ORDER BY username
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new AppUser
            {
                Id          = rdr.GetInt32(0),
                Username    = rdr.GetString(1),
                DisplayName = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                Role        = rdr.GetString(3),
                IsActive    = rdr.GetBoolean(4),
                AutoLogin   = rdr.GetBoolean(5),
                UserType    = rdr.GetString(6),
                Email       = rdr.GetString(7),
                LastLoginAt = rdr.IsDBNull(8) ? null : rdr.GetDateTime(8),
                LoginCount  = rdr.GetInt32(9),
                CreatedAt   = rdr.IsDBNull(10) ? null : rdr.GetDateTime(10),
                Department  = rdr.GetString(11),
                Title       = rdr.GetString(12),
                Phone       = rdr.GetString(13),
                Mobile      = rdr.GetString(14),
                Company     = rdr.GetString(15),
                AdGuid      = rdr.GetString(16),
                LastAdSync  = rdr.IsDBNull(17) ? null : rdr.GetDateTime(17),
            });
        return list;
    }

    public async Task UpsertUserAsync(AppUser u)
    {
        await using var conn = await OpenConnectionAsync();
        if (u.Id == 0)
        {
            const string sql = """
                INSERT INTO app_users (username, display_name, role, is_active, auto_login,
                    user_type, email, department, title, phone, mobile, company, ad_guid)
                VALUES (@user, @name, @role, @active, @auto, @type, @email,
                    @dept, @title, @phone, @mobile, @company, @adguid)
                RETURNING id
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@user",    u.Username);
            cmd.Parameters.AddWithValue("@name",    (object?)u.DisplayName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@role",    u.Role);
            cmd.Parameters.AddWithValue("@active",  u.IsActive);
            cmd.Parameters.AddWithValue("@auto",    u.AutoLogin);
            cmd.Parameters.AddWithValue("@type",    (object?)u.UserType ?? "Standard");
            cmd.Parameters.AddWithValue("@email",   (object?)u.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dept",    (object?)u.Department ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@title",   (object?)u.Title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@phone",   (object?)u.Phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mobile",  (object?)u.Mobile ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@company", (object?)u.Company ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@adguid",  (object?)u.AdGuid ?? DBNull.Value);
            u.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            const string sql = """
                UPDATE app_users SET username=@user, display_name=@name, role=@role,
                    is_active=@active, auto_login=@auto, user_type=@type, email=@email,
                    department=@dept, title=@title, phone=@phone, mobile=@mobile,
                    company=@company, ad_guid=@adguid
                WHERE id=@id
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id",      u.Id);
            cmd.Parameters.AddWithValue("@user",    u.Username);
            cmd.Parameters.AddWithValue("@name",    (object?)u.DisplayName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@role",    u.Role);
            cmd.Parameters.AddWithValue("@active",  u.IsActive);
            cmd.Parameters.AddWithValue("@auto",    u.AutoLogin);
            cmd.Parameters.AddWithValue("@type",    (object?)u.UserType ?? "Standard");
            cmd.Parameters.AddWithValue("@email",   (object?)u.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dept",    (object?)u.Department ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@title",   (object?)u.Title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@phone",   (object?)u.Phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mobile",  (object?)u.Mobile ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@company", (object?)u.Company ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@adguid",  (object?)u.AdGuid ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>Delete a user. Protected users (System, Service) cannot be deleted.</summary>
    public async Task<bool> DeleteUserAsync(int id)
    {
        await using var conn = await OpenConnectionAsync();

        // Guard: check if user is protected
        await using var chk = new NpgsqlCommand(
            "SELECT COALESCE(user_type, 'Standard') FROM app_users WHERE id=@id", conn);
        chk.Parameters.AddWithValue("@id", id);
        var userType = await chk.ExecuteScalarAsync() as string;
        if (Central.Engine.Auth.UserTypes.IsProtected(userType))
            return false; // Cannot delete protected users

        await using var cmd = new NpgsqlCommand("DELETE FROM app_users WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    // ── Role Permissions ─────────────────────────────────────────────────

    public async Task<Dictionary<string, RolePermission>> GetRolePermissionsAsync(string role)
    {
        var dict = new Dictionary<string, RolePermission>();
        await using var conn = await OpenConnectionAsync();
        const string sql = "SELECT module, can_view, can_edit, can_delete, COALESCE(can_view_reserved, true) FROM role_permissions WHERE role = @r";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@r", role);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
        {
            var p = new RolePermission
            {
                Module          = rdr.GetString(0),
                CanView         = rdr.GetBoolean(1),
                CanEdit         = rdr.GetBoolean(2),
                CanDelete       = rdr.GetBoolean(3),
                CanViewReserved = rdr.GetBoolean(4),
            };
            dict[p.Module] = p;
        }
        return dict;
    }

    /// <summary>Get all permission grants for a role by name (from v2 permissions system).</summary>
    public async Task<List<(string Code, string Name, string Category)>> GetPermissionGrantsForRoleAsync(string roleName)
    {
        var list = new List<(string, string, string)>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT p.code, p.name, p.category
            FROM role_permission_grants rpg
            JOIN roles r ON rpg.role_id = r.id
            JOIN permissions p ON rpg.permission_id = p.id
            WHERE r.name = @role
            ORDER BY p.category, p.sort_order", conn);
        cmd.Parameters.AddWithValue("role", roleName);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add((rdr.GetString(0), rdr.GetString(1), rdr.GetString(2)));
        return list;
    }

    // ── Roles CRUD ──────────────────────────────────────────────────────

    public async Task<List<RoleRecord>> GetAllRolesAsync()
    {
        var list = new List<RoleRecord>();
        await using var conn = await OpenConnectionAsync();

        // Load roles
        const string rolesSql = "SELECT id, name, description, COALESCE(priority, 0), COALESCE(is_system, false) FROM roles ORDER BY priority DESC, id";
        await using var cmd = new NpgsqlCommand(rolesSql, conn);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new RoleRecord
            {
                Id          = rdr.GetInt32(0),
                Name        = rdr.GetString(1),
                Description = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                Priority    = rdr.GetInt32(3),
                IsSystem    = rdr.GetBoolean(4),
            });
        await rdr.CloseAsync();

        // Load permissions for each role
        foreach (var role in list)
        {
            const string permSql = "SELECT module, can_view, can_edit, can_delete, COALESCE(can_view_reserved, true) FROM role_permissions WHERE role = @r";
            await using var pcmd = new NpgsqlCommand(permSql, conn);
            pcmd.Parameters.AddWithValue("@r", role.Name);
            await using var prdr = await pcmd.ExecuteReaderAsync();
            while (await prdr.ReadAsync())
            {
                var module   = prdr.GetString(0);
                var view     = prdr.GetBoolean(1);
                var edit     = prdr.GetBoolean(2);
                var del      = prdr.GetBoolean(3);
                var viewRes  = prdr.GetBoolean(4);
                switch (module)
                {
                    case "devices":     role.DevicesView = view;     role.DevicesEdit = edit;     role.DevicesDelete = del; role.DevicesViewReserved = viewRes; break;
                    case "switches": role.SwitchesView = view;  role.SwitchesEdit = edit;  role.SwitchesDelete = del; break;
                    case "admin":    role.AdminView = view;     role.AdminEdit = edit;     role.AdminDelete = del;    break;
                }
            }
            await prdr.CloseAsync();
        }
        return list;
    }

    public async Task<List<string>> GetRoleNamesAsync()
    {
        var list = new List<string>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT name FROM roles ORDER BY id", conn);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync()) list.Add(rdr.GetString(0));
        return list;
    }

    public async Task UpsertRoleAsync(RoleRecord r)
    {
        await using var conn = await OpenConnectionAsync();

        // Upsert role record
        if (r.Id == 0)
        {
            const string sql = "INSERT INTO roles (name, description, priority) VALUES (@name, @desc, @pri) RETURNING id";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", r.Name);
            cmd.Parameters.AddWithValue("@desc", (object?)r.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pri",  r.Priority);
            r.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            const string sql = "UPDATE roles SET name=@name, description=@desc, priority=@pri WHERE id=@id";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id",   r.Id);
            cmd.Parameters.AddWithValue("@name", r.Name);
            cmd.Parameters.AddWithValue("@desc", (object?)r.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pri",  r.Priority);
            await cmd.ExecuteNonQueryAsync();
        }

        // Upsert permissions for all 3 modules
        var modules = new[]
        {
            ("devices",  r.DevicesView,     r.DevicesEdit,     r.DevicesDelete,     r.DevicesViewReserved),
            ("switches", r.SwitchesView, r.SwitchesEdit, r.SwitchesDelete, true),
            ("admin",    r.AdminView,    r.AdminEdit,    r.AdminDelete,    true),
        };
        foreach (var (mod, view, edit, del, viewRes) in modules)
        {
            const string permSql = """
                INSERT INTO role_permissions (role, module, can_view, can_edit, can_delete, can_view_reserved)
                VALUES (@role, @mod, @v, @e, @d, @vr)
                ON CONFLICT (role, module)
                DO UPDATE SET can_view=EXCLUDED.can_view, can_edit=EXCLUDED.can_edit, can_delete=EXCLUDED.can_delete, can_view_reserved=EXCLUDED.can_view_reserved
                """;
            await using var cmd = new NpgsqlCommand(permSql, conn);
            cmd.Parameters.AddWithValue("@role", r.Name);
            cmd.Parameters.AddWithValue("@mod",  mod);
            cmd.Parameters.AddWithValue("@v",    view);
            cmd.Parameters.AddWithValue("@e",    edit);
            cmd.Parameters.AddWithValue("@d",    del);
            cmd.Parameters.AddWithValue("@vr",   viewRes);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteRoleAsync(int id, string roleName)
    {
        await using var conn = await OpenConnectionAsync();
        // Remove permissions first
        await using var pcmd = new NpgsqlCommand("DELETE FROM role_permissions WHERE role=@r", conn);
        pcmd.Parameters.AddWithValue("@r", roleName);
        await pcmd.ExecuteNonQueryAsync();
        // Remove role
        await using var cmd = new NpgsqlCommand("DELETE FROM roles WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Role Site Access ─────────────────────────────────────────────────

    public async Task<List<RoleSiteAccess>> GetRoleSitesAsync(string role)
    {
        var list = new List<RoleSiteAccess>();
        await using var conn = await OpenConnectionAsync();
        const string sql = "SELECT building, allowed FROM role_sites WHERE role = @r ORDER BY building";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@r", role);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new RoleSiteAccess
            {
                Building = rdr.GetString(0),
                Allowed  = rdr.GetBoolean(1),
            });
        return list;
    }

    public async Task<List<string>> GetAllowedSitesAsync(string role)
    {
        var list = new List<string>();
        await using var conn = await OpenConnectionAsync();
        const string sql = "SELECT building FROM role_sites WHERE role = @r AND allowed = TRUE ORDER BY building";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@r", role);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync()) list.Add(rdr.GetString(0));
        return list;
    }

    public async Task UpsertRoleSiteAsync(string role, string building, bool allowed)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            INSERT INTO role_sites (role, building, allowed) VALUES (@r, @b, @a)
            ON CONFLICT (role, building) DO UPDATE SET allowed = EXCLUDED.allowed
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@r", role);
        cmd.Parameters.AddWithValue("@b", building);
        cmd.Parameters.AddWithValue("@a", allowed);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SeedRoleSitesAsync(string role)
    {
        // Ensure all buildings from switch_guide exist for this role
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            INSERT INTO role_sites (role, building, allowed)
            SELECT @r, building, TRUE FROM (SELECT DISTINCT building FROM switch_guide) b
            ON CONFLICT (role, building) DO NOTHING
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@r", role);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── User Settings (layouts, preferences) ─────────────────────────────

    public async Task<string?> GetUserSettingAsync(int userId, string key)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = "SELECT setting_value FROM user_settings WHERE user_id = @uid AND setting_key = @key";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@key", key);
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    public async Task SaveUserSettingAsync(int userId, string key, string value)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            INSERT INTO user_settings (user_id, setting_key, setting_value)
            VALUES (@uid, @key, @val)
            ON CONFLICT (user_id, setting_key) DO UPDATE SET setting_value = EXCLUDED.setting_value
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@val", value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteUserSettingAsync(int userId, string key)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = "DELETE FROM user_settings WHERE user_id = @uid AND setting_key = @key";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@key", key);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── SSH Logs ──────────────────────────────────────────────────────────

    /// <summary>Insert a new SSH log entry and return its ID.</summary>
    public async Task<int> InsertSshLogAsync(Guid? switchId, string hostname, string hostIp, string username, int port)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            INSERT INTO ssh_logs (switch_id, hostname, host_ip, username, port, started_at)
            VALUES (@sid, @host, @ip, @user, @port, NOW())
            RETURNING id
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sid", (object?)switchId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@host", hostname);
        cmd.Parameters.AddWithValue("@ip", hostIp);
        cmd.Parameters.AddWithValue("@user", username);
        cmd.Parameters.AddWithValue("@port", port);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>Update an SSH log entry with results.</summary>
    public async Task UpdateSshLogAsync(int logId, bool success, string error, string rawOutput, int configLines, string logEntries)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            UPDATE ssh_logs SET finished_at = NOW(), success = @ok, error = @err,
                raw_output = @raw, config_lines = @lines, log_entries = @log
            WHERE id = @id
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", logId);
        cmd.Parameters.AddWithValue("@ok", success);
        cmd.Parameters.AddWithValue("@err", error ?? "");
        cmd.Parameters.AddWithValue("@raw", rawOutput ?? "");
        cmd.Parameters.AddWithValue("@lines", configLines);
        cmd.Parameters.AddWithValue("@log", logEntries ?? "");
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Load SSH logs (most recent first, limit 500).</summary>
    public async Task<List<SshLogEntry>> GetSshLogsAsync()
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            SELECT id, switch_id, hostname, host_ip, started_at, finished_at,
                   success, username, port, error, raw_output, config_lines, log_entries
            FROM ssh_logs ORDER BY started_at DESC LIMIT 500
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var rdr = await cmd.ExecuteReaderAsync();
        var list = new List<SshLogEntry>();
        while (await rdr.ReadAsync())
        {
            list.Add(new SshLogEntry
            {
                Id          = rdr.GetInt32(0),
                SwitchId    = rdr.IsDBNull(1) ? null : rdr.GetGuid(1),
                Hostname    = rdr.GetString(2),
                HostIp      = rdr.GetString(3),
                StartedAt   = rdr.GetDateTime(4),
                FinishedAt  = rdr.IsDBNull(5) ? null : rdr.GetDateTime(5),
                Success     = rdr.GetBoolean(6),
                Username    = rdr.GetString(7),
                Port        = rdr.GetInt32(8),
                Error       = rdr.IsDBNull(9) ? "" : rdr.GetString(9),
                RawOutput   = rdr.IsDBNull(10) ? "" : rdr.GetString(10),
                ConfigLines = rdr.GetInt32(11),
                LogEntries  = rdr.IsDBNull(12) ? "" : rdr.GetString(12),
            });
        }
        return list;
    }

    /// <summary>Delete SSH logs older than N days.</summary>
    public async Task PurgeSshLogsAsync(int daysOld = 30)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = "DELETE FROM ssh_logs WHERE started_at < NOW() - INTERVAL '1 day' * @days";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@days", daysOld);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── App Log ──────────────────────────────────────────────────────────

    public async Task InsertAppLogAsync(AppLogEntry entry)
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            const string sql = """
                INSERT INTO app_log (level, tag, source, message, detail, username)
                VALUES (@level, @tag, @source, @msg, @detail, @user)
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@level",  entry.Level);
            cmd.Parameters.AddWithValue("@tag",    entry.Tag);
            cmd.Parameters.AddWithValue("@source", entry.Source);
            cmd.Parameters.AddWithValue("@msg",    entry.Message);
            cmd.Parameters.AddWithValue("@detail", entry.Detail);
            cmd.Parameters.AddWithValue("@user",   entry.Username);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { /* logging must never throw */ }
    }

    public async Task<List<AppLogEntry>> GetAppLogsAsync(int limit = 1000)
    {
        var list = new List<AppLogEntry>();
        await using var conn = await OpenConnectionAsync();
        var sql = $"SELECT id,timestamp,level,tag,source,message,detail,username FROM app_log ORDER BY timestamp DESC LIMIT {limit}";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AppLogEntry
            {
                Id = r.GetInt32(0),
                Timestamp = r.GetDateTime(1),
                Level = S(r, 2),
                Tag = S(r, 3),
                Source = S(r, 4),
                Message = S(r, 5),
                Detail = S(r, 6),
                Username = S(r, 7)
            });
        return list;
    }

    public async Task DeleteAppLogAsync(int id)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM app_log WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ClearAppLogsAsync()
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM app_log", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Builder Selections ─────────────────────────────────────────────

    public async Task<List<(string SectionKey, string ItemKey, bool Enabled)>> GetBuilderSelectionsAsync(string deviceName)
    {
        var list = new List<(string, string, bool)>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT section_key, item_key, enabled FROM builder_selections WHERE device_name = @d ORDER BY id", conn);
        cmd.Parameters.AddWithValue("@d", deviceName);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((r.GetString(0), r.GetString(1), r.GetBoolean(2)));
        return list;
    }

    public async Task UpsertBuilderSelectionAsync(string deviceName, string sectionKey, string itemKey, bool enabled)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            INSERT INTO builder_selections (device_name, section_key, item_key, enabled)
            VALUES (@d, @s, @i, @e)
            ON CONFLICT (device_name, section_key, item_key)
            DO UPDATE SET enabled = @e, updated_at = NOW()
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@d", deviceName);
        cmd.Parameters.AddWithValue("@s", sectionKey);
        cmd.Parameters.AddWithValue("@i", itemKey);
        cmd.Parameters.AddWithValue("@e", enabled);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveBuilderSelectionsAsync(string deviceName, List<(string SectionKey, string ItemKey, bool Enabled)> selections)
    {
        await using var conn = await OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await using (var del = new NpgsqlCommand("DELETE FROM builder_selections WHERE device_name = @d", conn, tx))
        {
            del.Parameters.AddWithValue("@d", deviceName);
            await del.ExecuteNonQueryAsync();
        }
        foreach (var (sk, ik, en) in selections)
        {
            const string sql = "INSERT INTO builder_selections (device_name, section_key, item_key, enabled) VALUES (@d, @s, @i, @e)";
            await using var ins = new NpgsqlCommand(sql, conn, tx);
            ins.Parameters.AddWithValue("@d", deviceName);
            ins.Parameters.AddWithValue("@s", sk);
            ins.Parameters.AddWithValue("@i", ik);
            ins.Parameters.AddWithValue("@e", en);
            await ins.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    // ── Config Versions (running_configs history) ──────────────────────

    public async Task<List<ConfigVersionEntry>> GetConfigVersionsAsync(Guid switchId)
    {
        var list = new List<ConfigVersionEntry>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT id, downloaded_at, version_num, line_count, diff_from_prev, COALESCE(operator,''), COALESCE(source_ip::text,'')
            FROM running_configs WHERE switch_id=@id ORDER BY downloaded_at DESC
            """, conn);
        cmd.Parameters.AddWithValue("@id", switchId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new ConfigVersionEntry
            {
                Id = r.GetGuid(0),
                DownloadedAt = r.GetDateTime(1),
                VersionNum = r.IsDBNull(2) ? 0 : r.GetInt32(2),
                LineCount = r.IsDBNull(3) ? 0 : r.GetInt32(3),
                DiffStatus = r.IsDBNull(4) ? "" : r.GetString(4),
                Operator = r.GetString(5),
                SourceIp = r.GetString(6)
            });
        }
        return list;
    }

    public async Task<string?> GetConfigVersionTextAsync(Guid versionId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT config_text FROM running_configs WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", versionId);
        return (await cmd.ExecuteScalarAsync()) as string;
    }

    public async Task DeleteConfigVersionAsync(Guid versionId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM running_configs WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", versionId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── BGP ─────────────────────────────────────────────────────────────────

    public async Task<List<BgpRecord>> GetBgpRecordsAsync(List<string>? sites = null)
    {
        var list = new List<BgpRecord>();
        await using var conn = await OpenConnectionAsync();

        var where = sites != null && sites.Count > 0 ? "WHERE s.site = ANY(@sites)" : "";
        var sql = $@"
            SELECT b.id, b.switch_id, s.site, s.hostname, b.local_as::text, COALESCE(b.router_id::text,''),
                   COALESCE(b.fast_external_failover, false),
                   COALESCE(b.ebgp_requires_policy, false),
                   COALESCE(b.bestpath_multipath_relax, false),
                   COALESCE(b.redistribute_connected, true),
                   COALESCE(b.max_paths, 4),
                   b.last_synced,
                   (SELECT COUNT(*) FROM bgp_neighbors n WHERE n.bgp_id = b.id),
                   (SELECT COUNT(*) FROM bgp_networks  n WHERE n.bgp_id = b.id)
            FROM bgp_config b
            JOIN switches s ON s.id = b.switch_id
            {where}
            ORDER BY s.site, s.hostname";

        await using var cmd = new NpgsqlCommand(sql, conn);
        if (sites != null && sites.Count > 0)
            cmd.Parameters.AddWithValue("@sites", sites.ToArray());

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new BgpRecord
            {
                Id = r.GetGuid(0).GetHashCode(), // int proxy for grid
                SwitchId = r.GetGuid(1),
                Building = r.IsDBNull(2) ? "" : r.GetString(2),
                Hostname = r.GetString(3),
                LocalAs = r.GetString(4),
                RouterId = r.GetString(5),
                FastExternalFailover = r.GetBoolean(6),
                EbgpRequiresPolicy = r.GetBoolean(7),
                BestpathMultipathRelax = r.GetBoolean(8),
                RedistributeConnected = r.GetBoolean(9),
                MaxPaths = r.GetInt32(10),
                LastSynced = r.IsDBNull(11) ? null : r.GetDateTime(11),
                NeighborCount = (int)r.GetInt64(12),
                NetworkCount = (int)r.GetInt64(13),
            });
        }
        return list;
    }

    public async Task<List<BgpNeighborRecord>> GetBgpNeighborsAsync(Guid switchId)
    {
        var list = new List<BgpNeighborRecord>();
        await using var conn = await OpenConnectionAsync();
        var sql = @"SELECT n.id, n.bgp_id, n.neighbor_ip::text, COALESCE(n.remote_as::text,''), COALESCE(n.description,''),
                           COALESCE(n.bfd_enabled,false), COALESCE(n.ipv4_unicast,false)
                    FROM bgp_neighbors n
                    JOIN bgp_config b ON b.id = n.bgp_id
                    WHERE b.switch_id = @sid
                    ORDER BY n.neighbor_ip";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sid", switchId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new BgpNeighborRecord
            {
                Id = r.GetGuid(0).GetHashCode(),
                BgpId = r.GetGuid(1).GetHashCode(),
                NeighborIp = r.GetString(2),
                RemoteAs = r.GetString(3),
                Description = r.GetString(4),
                BfdEnabled = r.GetBoolean(5),
                Ipv4Unicast = r.GetBoolean(6),
            });
        }
        return list;
    }

    public async Task<List<BgpNetworkRecord>> GetBgpNetworksAsync(Guid switchId)
    {
        var list = new List<BgpNetworkRecord>();
        await using var conn = await OpenConnectionAsync();
        var sql = @"SELECT n.id, n.bgp_id, n.network_prefix::text
                    FROM bgp_networks n
                    JOIN bgp_config b ON b.id = n.bgp_id
                    WHERE b.switch_id = @sid
                    ORDER BY n.network_prefix";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sid", switchId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new BgpNetworkRecord
            {
                Id = r.GetGuid(0).GetHashCode(),
                BgpId = r.GetGuid(1).GetHashCode(),
                NetworkPrefix = r.GetString(2),
            });
        }
        return list;
    }

    /// <summary>Sync BGP config from parsed PicOS running-config lines into bgp_config/neighbors/networks.</summary>
    public async Task SyncBgpFromConfigAsync(Guid switchId, string localAs, string routerId,
        bool fastFailover, bool ebgpRequiresPolicy, bool bestpathRelax, bool redistributeConnected, int maxPaths,
        List<(string ip, string remoteAs, bool bfd, string desc)> neighbors,
        List<string> networks)
    {
        await using var conn = await OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        // Upsert bgp_config
        var upsertCfg = @"
            INSERT INTO bgp_config (switch_id, local_as, router_id, fast_external_failover, ebgp_requires_policy,
                                    bestpath_multipath_relax, redistribute_connected, max_paths, last_synced)
            VALUES (@sid, @las::bigint, @rid::inet, @fef, @erp, @bmr, @rc, @mp, NOW())
            ON CONFLICT (switch_id) DO UPDATE SET
                local_as = EXCLUDED.local_as,
                router_id = EXCLUDED.router_id,
                fast_external_failover = EXCLUDED.fast_external_failover,
                ebgp_requires_policy = EXCLUDED.ebgp_requires_policy,
                bestpath_multipath_relax = EXCLUDED.bestpath_multipath_relax,
                redistribute_connected = EXCLUDED.redistribute_connected,
                max_paths = EXCLUDED.max_paths,
                last_synced = EXCLUDED.last_synced
            RETURNING id";
        await using var cmd = new NpgsqlCommand(upsertCfg, conn);
        cmd.Parameters.AddWithValue("@sid", switchId);
        cmd.Parameters.AddWithValue("@las", localAs);
        cmd.Parameters.AddWithValue("@rid", string.IsNullOrEmpty(routerId) ? DBNull.Value : routerId);
        cmd.Parameters.AddWithValue("@fef", fastFailover);
        cmd.Parameters.AddWithValue("@erp", ebgpRequiresPolicy);
        cmd.Parameters.AddWithValue("@bmr", bestpathRelax);
        cmd.Parameters.AddWithValue("@rc", redistributeConnected);
        cmd.Parameters.AddWithValue("@mp", maxPaths);
        var bgpId = (Guid)(await cmd.ExecuteScalarAsync())!;

        // Replace neighbors
        await using (var del = new NpgsqlCommand("DELETE FROM bgp_neighbors WHERE bgp_id=@bid", conn))
        { del.Parameters.AddWithValue("@bid", bgpId); await del.ExecuteNonQueryAsync(); }

        foreach (var (ip, remoteAs, bfd, desc) in neighbors)
        {
            var ins = @"INSERT INTO bgp_neighbors (bgp_id, neighbor_ip, remote_as, bfd_enabled, description, ipv4_unicast)
                        VALUES (@bid, @ip::inet, @ras::bigint, @bfd, @desc, true)
                        ON CONFLICT (bgp_id, neighbor_ip) DO NOTHING";
            await using var nc = new NpgsqlCommand(ins, conn);
            nc.Parameters.AddWithValue("@bid", bgpId);
            nc.Parameters.AddWithValue("@ip", ip);
            nc.Parameters.AddWithValue("@ras", remoteAs);
            nc.Parameters.AddWithValue("@bfd", bfd);
            nc.Parameters.AddWithValue("@desc", desc);
            await nc.ExecuteNonQueryAsync();
        }

        // Replace networks
        await using (var del = new NpgsqlCommand("DELETE FROM bgp_networks WHERE bgp_id=@bid", conn))
        { del.Parameters.AddWithValue("@bid", bgpId); await del.ExecuteNonQueryAsync(); }

        foreach (var net in networks)
        {
            var ins = @"INSERT INTO bgp_networks (bgp_id, network_prefix) VALUES (@bid, @net::cidr)
                        ON CONFLICT (bgp_id, network_prefix) DO NOTHING";
            await using var nc = new NpgsqlCommand(ins, conn);
            nc.Parameters.AddWithValue("@bid", bgpId);
            nc.Parameters.AddWithValue("@net", net);
            await nc.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }

}
