using Npgsql;
using Central.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Central.Data;

public partial class DbRepository
{
    // ── Excel Sheet Data (read-only grids) ──────────────────────────────────

    public async Task<List<P2PLink>> GetP2PLinksAsync(List<string>? sites = null, bool excludeReserved = false)
    {
        var list = new List<P2PLink>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = "SELECT id,region,building,link_id,vlan,device_a,port_a,device_a_ip,device_b,port_b,device_b_ip,subnet,status,desc_a,desc_b FROM p2p_links";
        var conds = new System.Collections.Generic.List<string>();
        if (sites?.Count > 0) conds.Add("building = ANY(@sites)");
        if (excludeReserved) conds.Add("status <> 'RESERVED'");
        if (conds.Count > 0) sql += " WHERE " + string.Join(" AND ", conds);
        sql += " ORDER BY building,link_id";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (sites?.Count > 0) cmd.Parameters.AddWithValue("@sites", sites.ToArray());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new P2PLink { Id = r.GetInt32(0), Region = r.IsDBNull(1)?"":r.GetString(1), Building = r.GetString(2), LinkId = r.IsDBNull(3)?"":r.GetString(3), Vlan = r.IsDBNull(4)?"":r.GetString(4), DeviceA = r.GetString(5), PortA = r.IsDBNull(6)?"":r.GetString(6), DeviceAIp = r.IsDBNull(7)?"":r.GetString(7), DeviceB = r.GetString(8), PortB = r.IsDBNull(9)?"":r.GetString(9), DeviceBIp = r.IsDBNull(10)?"":r.GetString(10), Subnet = r.IsDBNull(11)?"":r.GetString(11), Status = r.IsDBNull(12)?"":r.GetString(12), DescA = r.IsDBNull(13)?"":r.GetString(13), DescB = r.IsDBNull(14)?"":r.GetString(14) });
        return list;
    }

    public async Task UpsertP2PLinkAsync(P2PLink l)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (l.Id > 0)
        {
            const string sql = """
                UPDATE p2p_links SET region=@reg, building=@bld, link_id=@lid, vlan=@vl,
                    device_a=@da, port_a=@pa, device_a_ip=@daip, desc_a=@desca,
                    device_b=@db, port_b=@pb, device_b_ip=@dbip, desc_b=@descb,
                    subnet=@sub, status=@st
                WHERE id=@id
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", l.Id);
            cmd.Parameters.AddWithValue("@reg", l.Region);
            cmd.Parameters.AddWithValue("@bld", l.Building);
            cmd.Parameters.AddWithValue("@lid", l.LinkId);
            cmd.Parameters.AddWithValue("@vl", l.Vlan);
            cmd.Parameters.AddWithValue("@da", l.DeviceA);
            cmd.Parameters.AddWithValue("@pa", l.PortA);
            cmd.Parameters.AddWithValue("@daip", l.DeviceAIp);
            cmd.Parameters.AddWithValue("@desca", l.DescA);
            cmd.Parameters.AddWithValue("@db", l.DeviceB);
            cmd.Parameters.AddWithValue("@pb", l.PortB);
            cmd.Parameters.AddWithValue("@dbip", l.DeviceBIp);
            cmd.Parameters.AddWithValue("@descb", l.DescB);
            cmd.Parameters.AddWithValue("@sub", l.Subnet);
            cmd.Parameters.AddWithValue("@st", l.Status);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            const string sql = """
                INSERT INTO p2p_links (region,building,link_id,vlan,device_a,port_a,device_a_ip,desc_a,device_b,port_b,device_b_ip,desc_b,subnet,status)
                VALUES (@reg,@bld,@lid,@vl,@da,@pa,@daip,@desca,@db,@pb,@dbip,@descb,@sub,@st)
                RETURNING id
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@reg", l.Region);
            cmd.Parameters.AddWithValue("@bld", l.Building);
            cmd.Parameters.AddWithValue("@lid", l.LinkId);
            cmd.Parameters.AddWithValue("@vl", l.Vlan);
            cmd.Parameters.AddWithValue("@da", l.DeviceA);
            cmd.Parameters.AddWithValue("@pa", l.PortA);
            cmd.Parameters.AddWithValue("@daip", l.DeviceAIp);
            cmd.Parameters.AddWithValue("@desca", l.DescA);
            cmd.Parameters.AddWithValue("@db", l.DeviceB);
            cmd.Parameters.AddWithValue("@pb", l.PortB);
            cmd.Parameters.AddWithValue("@dbip", l.DeviceBIp);
            cmd.Parameters.AddWithValue("@descb", l.DescB);
            cmd.Parameters.AddWithValue("@sub", l.Subnet);
            cmd.Parameters.AddWithValue("@st", l.Status);
            var newId = await cmd.ExecuteScalarAsync();
            if (newId != null) l.Id = Convert.ToInt32(newId);
        }
    }

    public async Task DeleteP2PLinkAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM p2p_links WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpsertB2BLinkAsync(B2BLink l)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = l.Id > 0
            ? @"UPDATE b2b_links SET link_id=@lid, vlan=@v, building_a=@ba, device_a=@da, port_a=@pa,
                module_a=@ma, device_a_ip=@aip, building_b=@bb, device_b=@db, port_b=@pb,
                module_b=@mb, device_b_ip=@bip, tx=@tx, rx=@rx, media=@med, speed=@spd,
                subnet=@sub, status=@st WHERE id=@id"
            : @"INSERT INTO b2b_links (link_id, vlan, building_a, device_a, port_a, module_a, device_a_ip,
                building_b, device_b, port_b, module_b, device_b_ip, tx, rx, media, speed, subnet, status)
                VALUES (@lid, @v, @ba, @da, @pa, @ma, @aip, @bb, @db, @pb, @mb, @bip, @tx, @rx, @med, @spd, @sub, @st)
                RETURNING id";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (l.Id > 0) cmd.Parameters.AddWithValue("@id", l.Id);
        cmd.Parameters.AddWithValue("@lid", l.LinkId ?? "");
        cmd.Parameters.AddWithValue("@v", l.Vlan ?? "");
        cmd.Parameters.AddWithValue("@ba", l.BuildingA ?? "");
        cmd.Parameters.AddWithValue("@da", l.DeviceA ?? "");
        cmd.Parameters.AddWithValue("@pa", l.PortA ?? "");
        cmd.Parameters.AddWithValue("@ma", l.ModuleA ?? "");
        cmd.Parameters.AddWithValue("@aip", l.DeviceAIp ?? "");
        cmd.Parameters.AddWithValue("@bb", l.BuildingB ?? "");
        cmd.Parameters.AddWithValue("@db", l.DeviceB ?? "");
        cmd.Parameters.AddWithValue("@pb", l.PortB ?? "");
        cmd.Parameters.AddWithValue("@mb", l.ModuleB ?? "");
        cmd.Parameters.AddWithValue("@bip", l.DeviceBIp ?? "");
        cmd.Parameters.AddWithValue("@tx", l.Tx ?? "");
        cmd.Parameters.AddWithValue("@rx", l.Rx ?? "");
        cmd.Parameters.AddWithValue("@med", l.Media ?? "");
        cmd.Parameters.AddWithValue("@spd", l.Speed ?? "");
        cmd.Parameters.AddWithValue("@sub", l.Subnet ?? "");
        cmd.Parameters.AddWithValue("@st", l.Status ?? "Active");
        if (l.Id > 0) await cmd.ExecuteNonQueryAsync();
        else l.Id = (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task DeleteB2BLinkAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM b2b_links WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpsertFWLinkAsync(FWLink l)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = l.Id > 0
            ? @"UPDATE fw_links SET building=@bld, link_id=@lid, vlan=@v, switch=@sw, switch_port=@sp,
                switch_ip=@sip, firewall=@fw, firewall_port=@fp, firewall_ip=@fip, subnet=@sub, status=@st
                WHERE id=@id"
            : @"INSERT INTO fw_links (building, link_id, vlan, switch, switch_port, switch_ip,
                firewall, firewall_port, firewall_ip, subnet, status)
                VALUES (@bld, @lid, @v, @sw, @sp, @sip, @fw, @fp, @fip, @sub, @st) RETURNING id";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (l.Id > 0) cmd.Parameters.AddWithValue("@id", l.Id);
        cmd.Parameters.AddWithValue("@bld", l.Building ?? "");
        cmd.Parameters.AddWithValue("@lid", l.LinkId ?? "");
        cmd.Parameters.AddWithValue("@v", l.Vlan ?? "");
        cmd.Parameters.AddWithValue("@sw", l.Switch ?? "");
        cmd.Parameters.AddWithValue("@sp", l.SwitchPort ?? "");
        cmd.Parameters.AddWithValue("@sip", l.SwitchIp ?? "");
        cmd.Parameters.AddWithValue("@fw", l.Firewall ?? "");
        cmd.Parameters.AddWithValue("@fp", l.FirewallPort ?? "");
        cmd.Parameters.AddWithValue("@fip", l.FirewallIp ?? "");
        cmd.Parameters.AddWithValue("@sub", l.Subnet ?? "");
        cmd.Parameters.AddWithValue("@st", l.Status ?? "Active");
        if (l.Id > 0) await cmd.ExecuteNonQueryAsync();
        else l.Id = (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task DeleteFWLinkAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM fw_links WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteMlagConfigAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM mlag_config WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteMstpConfigAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM mstp_config WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteServerAsAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM server_as WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteIpRangeAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM ip_ranges WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsnDefinitionAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM asn_definitions WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteServerAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM servers WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteMasterDeviceAsync(string id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM switch_guide WHERE id=@id::uuid", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<B2BLink>> GetB2BLinksAsync(List<string>? sites = null, bool excludeReserved = false)
    {
        var list = new List<B2BLink>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = "SELECT id,link_id,vlan,building_a,device_a,port_a,module_a,device_a_ip,building_b,device_b,port_b,module_b,device_b_ip,tx,rx,media,speed,subnet,status FROM b2b_links";
        var conds = new System.Collections.Generic.List<string>();
        if (sites?.Count > 0) conds.Add("(building_a = ANY(@sites) OR building_b = ANY(@sites))");
        if (excludeReserved) conds.Add("status <> 'RESERVED'");
        if (conds.Count > 0) sql += " WHERE " + string.Join(" AND ", conds);
        sql += " ORDER BY link_id";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (sites?.Count > 0) cmd.Parameters.AddWithValue("@sites", sites.ToArray());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new B2BLink { Id = r.GetInt32(0), LinkId = S(r,1), Vlan = S(r,2), BuildingA = S(r,3), DeviceA = S(r,4), PortA = S(r,5), ModuleA = S(r,6), DeviceAIp = S(r,7), BuildingB = S(r,8), DeviceB = S(r,9), PortB = S(r,10), ModuleB = S(r,11), DeviceBIp = S(r,12), Tx = S(r,13), Rx = S(r,14), Media = S(r,15), Speed = S(r,16), Subnet = S(r,17), Status = S(r,18) });
        return list;
    }

    public async Task<List<FWLink>> GetFWLinksAsync(List<string>? sites = null, bool excludeReserved = false)
    {
        var list = new List<FWLink>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = "SELECT id,building,link_id,vlan,switch,switch_port,switch_ip,firewall,firewall_port,firewall_ip,subnet,status FROM fw_links";
        var conds = new System.Collections.Generic.List<string>();
        if (sites?.Count > 0) conds.Add("building = ANY(@sites)");
        if (excludeReserved) conds.Add("status <> 'RESERVED'");
        if (conds.Count > 0) sql += " WHERE " + string.Join(" AND ", conds);
        sql += " ORDER BY building,link_id";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (sites?.Count > 0) cmd.Parameters.AddWithValue("@sites", sites.ToArray());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new FWLink { Id = r.GetInt32(0), Building = S(r,1), LinkId = S(r,2), Vlan = S(r,3), Switch = S(r,4), SwitchPort = S(r,5), SwitchIp = S(r,6), Firewall = S(r,7), FirewallPort = S(r,8), FirewallIp = S(r,9), Subnet = S(r,10), Status = S(r,11) });
        return list;
    }

    public async Task<List<ServerAS>> GetServerASAsync(List<string>? sites = null, bool excludeReserved = false)
    {
        var list = new List<ServerAS>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = "SELECT id,building,server_as,status FROM server_as";
        var conds = new System.Collections.Generic.List<string>();
        if (sites?.Count > 0) conds.Add("building = ANY(@sites)");
        if (excludeReserved) conds.Add("status <> 'Active'");
        if (conds.Count > 0) sql += " WHERE " + string.Join(" AND ", conds);
        sql += " ORDER BY building";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (sites?.Count > 0) cmd.Parameters.AddWithValue("@sites", sites.ToArray());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ServerAS { Id = r.GetInt32(0), Building = S(r,1), ServerAsn = S(r,2), Status = S(r,3) });
        return list;
    }

    // ── ASN Definitions ──────────────────────────────────────────────────
    public async Task<List<AsnDefinition>> GetAsnDefinitionsAsync()
    {
        var list = new List<AsnDefinition>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        const string sql = """
            SELECT a.id, a.asn, a.description, a.asn_type, a.sort_order,
                   COUNT(sg.id)::int AS device_count,
                   STRING_AGG(sg.switch_name, ', ' ORDER BY sg.switch_name) AS devices
            FROM asn_definitions a
            LEFT JOIN switch_guide sg ON sg.asn = a.asn
            GROUP BY a.id, a.asn, a.description, a.asn_type, a.sort_order
            ORDER BY a.asn
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AsnDefinition
            {
                Id = r.GetInt32(0),
                Asn = S(r, 1),
                Description = S(r, 2),
                AsnType = S(r, 3),
                SortOrder = r.GetInt32(4),
                DeviceCount = r.GetInt32(5),
                Devices = S(r, 6)
            });
        return list;
    }

    public async Task UpsertAsnDefinitionAsync(AsnDefinition a)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        const string sql = """
            INSERT INTO asn_definitions (asn, description, asn_type, sort_order)
            VALUES (@asn, @desc, @type, @sort)
            ON CONFLICT (asn) DO UPDATE SET description = @desc, asn_type = @type, sort_order = @sort
            RETURNING id
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@asn", a.Asn);
        cmd.Parameters.AddWithValue("@desc", a.Description);
        cmd.Parameters.AddWithValue("@type", a.AsnType);
        cmd.Parameters.AddWithValue("@sort", a.SortOrder);
        a.Id = (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<List<IpRange>> GetIpRangesAsync(bool excludeReserved = false)
    {
        var list = new List<IpRange>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = "SELECT id,region,pool_name,block,purpose,notes,status FROM ip_ranges";
        if (excludeReserved) sql += " WHERE status <> 'RESERVED'";
        sql += " ORDER BY region,pool_name";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new IpRange { Id = r.GetInt32(0), Region = S(r,1), PoolName = S(r,2), Block = S(r,3), Purpose = S(r,4), Notes = S(r,5), Status = S(r,6) });
        return list;
    }

    public async Task<List<MlagConfig>> GetMlagConfigAsync(List<string>? sites = null, bool excludeReserved = false)
    {
        var list = new List<MlagConfig>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = "SELECT id,building,domain_type,mlag_domain,switch_a,switch_b,b2b_partner,status,peer_link_ae,physical_members,peer_vlan,trunk_vlans,shared_domain_mac,peer_link_subnet,node0_ip,node1_ip,node0_ip_link2,node1_ip_link2,notes FROM mlag_config";
        var conds = new System.Collections.Generic.List<string>();
        if (sites?.Count > 0) conds.Add("building = ANY(@sites)");
        if (excludeReserved) conds.Add("status <> 'RESERVED'");
        if (conds.Count > 0) sql += " WHERE " + string.Join(" AND ", conds);
        sql += " ORDER BY building,mlag_domain";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (sites?.Count > 0) cmd.Parameters.AddWithValue("@sites", sites.ToArray());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new MlagConfig { Id = r.GetInt32(0), Building = S(r,1), DomainType = S(r,2), MlagDomain = S(r,3), SwitchA = S(r,4), SwitchB = S(r,5), B2BPartner = S(r,6), Status = S(r,7), PeerLinkAe = S(r,8), PhysicalMembers = S(r,9), PeerVlan = S(r,10), TrunkVlans = S(r,11), SharedDomainMac = S(r,12), PeerLinkSubnet = S(r,13), Node0Ip = S(r,14), Node1Ip = S(r,15), Node0IpLink2 = S(r,16), Node1IpLink2 = S(r,17), Notes = S(r,18) });
        return list;
    }

    public async Task<List<MstpConfig>> GetMstpConfigAsync(List<string>? sites = null, bool excludeReserved = false)
    {
        var list = new List<MstpConfig>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = "SELECT id,building,device_name,device_role,mstp_priority,notes,status FROM mstp_config";
        var conds = new System.Collections.Generic.List<string>();
        if (sites?.Count > 0) conds.Add("building = ANY(@sites)");
        if (excludeReserved) conds.Add("status <> 'RESERVED'");
        if (conds.Count > 0) sql += " WHERE " + string.Join(" AND ", conds);
        sql += " ORDER BY building,device_name";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (sites?.Count > 0) cmd.Parameters.AddWithValue("@sites", sites.ToArray());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new MstpConfig { Id = r.GetInt32(0), Building = S(r,1), DeviceName = S(r,2), DeviceRole = S(r,3), MstpPriority = S(r,4), Notes = S(r,5), Status = S(r,6) });
        return list;
    }

    public async Task<List<VlanEntry>> GetVlanInventoryAsync(bool excludeReserved = false, List<string>? sites = null, bool includeDefault = true)
    {
        var list = new List<VlanEntry>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = "SELECT id,block,vlan_id,name,network_address,subnet,gateway,usable_range,status,sort_order,block_locked,is_default,site FROM vlan_inventory";
        var clauses = new List<string>();
        if (excludeReserved) clauses.Add("status <> 'RESERVED'");

        // Build site filter: include selected sites + optionally Default
        var siteNames = new List<string>();
        if (includeDefault) siteNames.Add("Default");
        if (sites != null) siteNames.AddRange(sites);
        if (siteNames.Count > 0)
            clauses.Add($"site = ANY(@sites)");
        else
            clauses.Add("1 = 0"); // nothing selected

        if (clauses.Count > 0) sql += " WHERE " + string.Join(" AND ", clauses);
        sql += " ORDER BY sort_order, vlan_id, site";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (siteNames.Count > 0)
            cmd.Parameters.AddWithValue("@sites", siteNames.ToArray());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new VlanEntry
            {
                Id = r.GetInt32(0), Block = S(r,1), VlanId = S(r,2), Name = S(r,3),
                NetworkAddress = S(r,4), Subnet = S(r,5), Gateway = S(r,6),
                UsableRange = S(r,7), Status = S(r,8),
                VlanIdSort = r.IsDBNull(9) ? 99999 : r.GetInt32(9),
                BlockLocked = !r.IsDBNull(10) && r.GetBoolean(10),
                IsDefault = !r.IsDBNull(11) && r.GetBoolean(11),
                Site = S(r,12)
            });
        return list;
    }

    /// <summary>Generate per-site VLAN rows by copying Default templates and resolving 10.x addresses.</summary>
    public async Task<int> GenerateSiteVlansAsync(string siteName)
    {
        var octet = VlanEntry.BuildingNumberMap.GetValueOrDefault(siteName, "");
        if (string.IsNullOrEmpty(octet)) return 0;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Check if site rows already exist
        await using (var chk = new NpgsqlCommand("SELECT COUNT(*) FROM vlan_inventory WHERE site = @site", conn))
        {
            chk.Parameters.AddWithValue("@site", siteName);
            var count = (long)(await chk.ExecuteScalarAsync())!;
            if (count > 0) return (int)count; // already provisioned
        }

        // Copy Default rows with resolved addresses
        const string sql = """
            INSERT INTO vlan_inventory (block, vlan_id, name, network_address, subnet, gateway, usable_range, status, sort_order, block_locked, is_default, site)
            SELECT
                REPLACE(block, '10.x.', '10.' || @octet || '.'),
                vlan_id, name,
                REPLACE(network_address, '10.x.', '10.' || @octet || '.'),
                subnet,
                REPLACE(gateway, '10.x.', '10.' || @octet || '.'),
                REPLACE(usable_range, '10.x.', '10.' || @octet || '.'),
                status, sort_order, block_locked, is_default, @site
            FROM vlan_inventory
            WHERE site = 'Default'
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@octet", octet);
        cmd.Parameters.AddWithValue("@site", siteName);
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpsertVlanEntryAsync(VlanEntry v)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        const string sql = """
            UPDATE vlan_inventory
            SET block = @block, vlan_id = @vid, name = @name, network_address = @net, subnet = @sub,
                gateway = @gw, usable_range = @range, status = @status, sort_order = @sort,
                block_locked = @locked, is_default = @def, site = @site
            WHERE id = @id
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", v.Id);
        cmd.Parameters.AddWithValue("@block", v.Block);
        cmd.Parameters.AddWithValue("@vid", v.VlanId);
        cmd.Parameters.AddWithValue("@name", v.Name);
        cmd.Parameters.AddWithValue("@net", v.NetworkAddress);
        cmd.Parameters.AddWithValue("@sub", v.Subnet);
        cmd.Parameters.AddWithValue("@gw", v.Gateway);
        cmd.Parameters.AddWithValue("@range", v.UsableRange);
        cmd.Parameters.AddWithValue("@status", v.Status);
        cmd.Parameters.AddWithValue("@sort", v.VlanIdSort);
        cmd.Parameters.AddWithValue("@locked", v.BlockLocked);
        cmd.Parameters.AddWithValue("@def", v.IsDefault);
        cmd.Parameters.AddWithValue("@site", v.Site ?? "Default");
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Sync shared fields (name, is_default) across all sites for the same vlan_id.</summary>
    public async Task SyncVlanAcrossSitesAsync(string vlanId, string name, bool isDefault)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        const string sql = "UPDATE vlan_inventory SET name = @name, is_default = @def WHERE vlan_id = @vid";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@vid", vlanId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@def", isDefault);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteVlanEntryAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM vlan_inventory WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<MasterDevice>> GetMasterDevicesAsync(List<string>? sites = null, bool excludeReserved = false)
    {
        var list = new List<MasterDevice>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = """
            SELECT id, device_name, device_type, region, building, status,
                   primary_ip, management_ip, loopback_ip, loopback_subnet, mgmt_l3_ip,
                   asn, mlag_domain, ae_range, model, serial_number,
                   uplink_switch, uplink_port, notes,
                   p2p_link_count, b2b_link_count, fw_link_count,
                   mstp_priority, mlag_peer, has_config
            FROM v_master_devices
            """;
        var conds = new System.Collections.Generic.List<string>();
        if (sites?.Count > 0) conds.Add("building = ANY(@sites)");
        if (excludeReserved) conds.Add("status <> 'RESERVED'");
        if (conds.Count > 0) sql += " WHERE " + string.Join(" AND ", conds);
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (sites?.Count > 0) cmd.Parameters.AddWithValue("@sites", sites.ToArray());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new MasterDevice
            {
                Id = r.GetGuid(0), DeviceName = S(r,1), DeviceType = S(r,2),
                Region = S(r,3), Building = S(r,4), Status = S(r,5),
                PrimaryIp = S(r,6), ManagementIp = S(r,7), LoopbackIp = S(r,8),
                LoopbackSubnet = S(r,9), MgmtL3Ip = S(r,10),
                Asn = S(r,11), MlagDomain = S(r,12), AeRange = S(r,13),
                Model = S(r,14), SerialNumber = S(r,15),
                UplinkSwitch = S(r,16), UplinkPort = S(r,17), Notes = S(r,18),
                P2PLinkCount = r.IsDBNull(19) ? 0 : Convert.ToInt32(r.GetValue(19)),
                B2BLinkCount = r.IsDBNull(20) ? 0 : Convert.ToInt32(r.GetValue(20)),
                FWLinkCount = r.IsDBNull(21) ? 0 : Convert.ToInt32(r.GetValue(21)),
                MstpPriority = S(r,22), MlagPeer = S(r,23),
                HasConfig = !r.IsDBNull(24) && r.GetBoolean(24)
            });
        }
        return list;
    }

    public async Task<List<Server>> GetServersAsync(List<string>? sites = null, bool excludeReserved = false)
    {
        var list = new List<Server>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = "SELECT id,building,server_name,server_as,loopback_ip,nic1_ip,nic1_router,nic1_subnet,nic1_status,nic2_ip,nic2_router,nic2_subnet,nic2_status,nic3_ip,nic3_router,nic3_subnet,nic3_status,nic4_ip,nic4_router,nic4_subnet,nic4_status,status FROM servers";
        var conds = new System.Collections.Generic.List<string>();
        if (sites?.Count > 0) conds.Add("building = ANY(@sites)");
        if (excludeReserved) conds.Add("status <> 'RESERVED'");
        if (conds.Count > 0) sql += " WHERE " + string.Join(" AND ", conds);
        sql += " ORDER BY building,server_name";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (sites?.Count > 0) cmd.Parameters.AddWithValue("@sites", sites.ToArray());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new Server { Id = r.GetInt32(0), Building = S(r,1), ServerName = S(r,2), ServerAs = S(r,3), LoopbackIp = S(r,4),
                Nic1Ip = S(r,5), Nic1Router = S(r,6), Nic1Subnet = S(r,7), Nic1Status = S(r,8),
                Nic2Ip = S(r,9), Nic2Router = S(r,10), Nic2Subnet = S(r,11), Nic2Status = S(r,12),
                Nic3Ip = S(r,13), Nic3Router = S(r,14), Nic3Subnet = S(r,15), Nic3Status = S(r,16),
                Nic4Ip = S(r,17), Nic4Router = S(r,18), Nic4Subnet = S(r,19), Nic4Status = S(r,20),
                Status = S(r,21) });
        return list;
    }

    public async Task<string?> GetLatestRunningConfigAsync(Guid switchId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT config_text FROM running_configs WHERE switch_id=@id ORDER BY downloaded_at DESC LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@id", switchId);
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    public async Task SaveRunningConfigAsync(Guid switchId, string configText, string sourceIp, string operatorName = "")
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var lineCount = configText.Split('\n').Length;
        // Get previous config for diff
        string? prevConfig = null;
        int nextVersion = 1;
        await using (var pcmd = new NpgsqlCommand(
            "SELECT config_text, version_num FROM running_configs WHERE switch_id=@id ORDER BY downloaded_at DESC LIMIT 1", conn))
        {
            pcmd.Parameters.AddWithValue("@id", switchId);
            await using var r = await pcmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                prevConfig = r.IsDBNull(0) ? null : r.GetString(0);
                nextVersion = (r.IsDBNull(1) ? 0 : r.GetInt32(1)) + 1;
            }
        }
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO running_configs (switch_id, downloaded_at, source_ip, config_text, line_count, diff_from_prev, version_num, operator)
            VALUES (@id, NOW(), @ip::inet, @cfg, @lines, @diff, @ver, @op)
            """, conn);
        cmd.Parameters.AddWithValue("@id", switchId);
        cmd.Parameters.AddWithValue("@ip", (object)(string.IsNullOrWhiteSpace(sourceIp) ? DBNull.Value : sourceIp));
        cmd.Parameters.AddWithValue("@cfg", configText);
        cmd.Parameters.AddWithValue("@lines", lineCount);
        cmd.Parameters.AddWithValue("@diff", prevConfig != null && prevConfig != configText ? "Changed" : "No change");
        cmd.Parameters.AddWithValue("@ver", nextVersion);
        cmd.Parameters.AddWithValue("@op", operatorName);
        await cmd.ExecuteNonQueryAsync();
    }
}
