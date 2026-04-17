using System.Text.Json;
using System.Text.Json.Nodes;
using Central.Engine.Net;
using Central.Engine.Net.Hierarchy;
using Npgsql;
using NpgsqlTypes;

namespace Central.Persistence.Net;

/// <summary>
/// Repository for the networking engine's geographic hierarchy — regions,
/// sites, buildings, floors, rooms, racks, and their profiles. Every read
/// is scoped by organisation; every write enforces optimistic concurrency
/// via the <see cref="EntityBase.Version"/> column.
///
/// Enum columns use text casts (<c>@status::net.entity_status</c>) rather than
/// Npgsql enum registration so no data-source setup is required.
/// </summary>
public class HierarchyRepository
{
    private readonly string _dsn;
    public HierarchyRepository(string dsn) => _dsn = dsn;

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

    // ── shared mappers ───────────────────────────────────────────────────

    private static EntityStatus ParseStatus(string s) => Enum.Parse<EntityStatus>(s);
    private static LockState ParseLock(string s) => Enum.Parse<LockState>(s);
    private static JsonObject ReadJsonObject(NpgsqlDataReader r, int idx)
        => r.IsDBNull(idx) ? new() : (JsonNode.Parse(r.GetString(idx)) as JsonObject) ?? new();
    private static JsonArray ReadJsonArray(NpgsqlDataReader r, int idx)
        => r.IsDBNull(idx) ? new() : (JsonNode.Parse(r.GetString(idx)) as JsonArray) ?? new();

    private static void PopulateBase(EntityBase e, NpgsqlDataReader r, int startCol)
    {
        // Layout: status, lock_state, lock_reason, locked_by, locked_at,
        //         created_at, created_by, updated_at, updated_by,
        //         deleted_at, deleted_by, notes, tags, external_refs, version
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

    /// <summary>
    /// Column list shared by every SELECT to hydrate the base fields. The
    /// entity-specific columns come before this block in each query.
    /// </summary>
    private const string BaseColumns =
        "status::text, lock_state::text, lock_reason, locked_by, locked_at, " +
        "created_at, created_by, updated_at, updated_by, deleted_at, deleted_by, " +
        "notes, tags::text, external_refs::text, version";

    // ── Region ───────────────────────────────────────────────────────────

    public async Task<List<Region>> ListRegionsAsync(Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, region_code, display_name,
                   default_ip_pool_id, default_asn_pool_id, b2b_mesh_policy, " + BaseColumns + @"
            FROM net.region
            WHERE organization_id = @org AND deleted_at IS NULL
            ORDER BY region_code";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Region>();
        while (await r.ReadAsync(ct)) list.Add(ReadRegion(r));
        return list;
    }

    public async Task<Region?> GetRegionAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, region_code, display_name,
                   default_ip_pool_id, default_asn_pool_id, b2b_mesh_policy, " + BaseColumns + @"
            FROM net.region
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("org", orgId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? ReadRegion(r) : null;
    }

    public async Task<Guid> CreateRegionAsync(Region e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.region (organization_id, region_code, display_name,
                                    b2b_mesh_policy, status, lock_state, notes,
                                    tags, external_refs, created_by, updated_by)
            VALUES (@org, @code, @name, @policy,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("code", e.RegionCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("policy", e.B2bMeshPolicy);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateRegionAsync(Region e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.region SET
                region_code       = @code,
                display_name      = @name,
                b2b_mesh_policy   = @policy,
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
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("code", e.RegionCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("policy", e.B2bMeshPolicy);
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

    public Task<bool> SoftDeleteRegionAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.region", id, orgId, userId, ct);

    private static Region ReadRegion(NpgsqlDataReader r)
    {
        var e = new Region
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            RegionCode = r.GetString(2),
            DisplayName = r.GetString(3),
            DefaultIpPoolId = r.IsDBNull(4) ? null : r.GetGuid(4),
            DefaultAsnPoolId = r.IsDBNull(5) ? null : r.GetGuid(5),
            B2bMeshPolicy = r.GetString(6),
        };
        PopulateBase(e, r, 7);
        return e;
    }

    // ── SiteProfile ─────────────────────────────────────────────────────

    public async Task<List<SiteProfile>> ListSiteProfilesAsync(Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, profile_code, display_name,
                   default_max_buildings, default_building_profile_id,
                   default_floors_per_building, allow_mixed_building_profiles, " + BaseColumns + @"
            FROM net.site_profile
            WHERE organization_id = @org AND deleted_at IS NULL
            ORDER BY profile_code";
        return await ListAsync(sql, orgId, ReadSiteProfile, ct);
    }

    public async Task<SiteProfile?> GetSiteProfileAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, profile_code, display_name,
                   default_max_buildings, default_building_profile_id,
                   default_floors_per_building, allow_mixed_building_profiles, " + BaseColumns + @"
            FROM net.site_profile
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadSiteProfile, ct);
    }

    private static SiteProfile ReadSiteProfile(NpgsqlDataReader r)
    {
        var e = new SiteProfile
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            ProfileCode = r.GetString(2),
            DisplayName = r.GetString(3),
            DefaultMaxBuildings = r.GetInt32(4),
            DefaultBuildingProfileId = r.IsDBNull(5) ? null : r.GetGuid(5),
            DefaultFloorsPerBuilding = r.GetInt32(6),
            AllowMixedBuildingProfiles = r.GetBoolean(7),
        };
        PopulateBase(e, r, 8);
        return e;
    }

    // ── Site ─────────────────────────────────────────────────────────────

    public async Task<List<Site>> ListSitesAsync(Guid orgId, Guid? regionId, CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, region_id, site_profile_id, site_code, display_name,
                   address_line1, address_line2, address_line3, city, state_or_county,
                   postcode, country, latitude, longitude, timezone,
                   primary_contact_user_id, max_buildings, " + BaseColumns + @"
            FROM net.site
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (regionId.HasValue ? " AND region_id = @region" : "") + @"
            ORDER BY site_code";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (regionId.HasValue) cmd.Parameters.AddWithValue("region", regionId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Site>();
        while (await r.ReadAsync(ct)) list.Add(ReadSite(r));
        return list;
    }

    public async Task<Site?> GetSiteAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, region_id, site_profile_id, site_code, display_name,
                   address_line1, address_line2, address_line3, city, state_or_county,
                   postcode, country, latitude, longitude, timezone,
                   primary_contact_user_id, max_buildings, " + BaseColumns + @"
            FROM net.site
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadSite, ct);
    }

    public async Task<Guid> CreateSiteAsync(Site e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.site (organization_id, region_id, site_profile_id, site_code,
                                  display_name, address_line1, address_line2, address_line3,
                                  city, state_or_county, postcode, country, latitude, longitude,
                                  timezone, primary_contact_user_id, max_buildings,
                                  status, lock_state, notes, tags, external_refs,
                                  created_by, updated_by)
            VALUES (@org, @region, @profile, @code, @name,
                    @a1, @a2, @a3, @city, @state, @post, @country, @lat, @lng,
                    @tz, @contact, @maxb,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindSiteWriteParams(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateSiteAsync(Site e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.site SET
                region_id            = @region,
                site_profile_id      = @profile,
                site_code            = @code,
                display_name         = @name,
                address_line1        = @a1,
                address_line2        = @a2,
                address_line3        = @a3,
                city                 = @city,
                state_or_county      = @state,
                postcode             = @post,
                country              = @country,
                latitude             = @lat,
                longitude            = @lng,
                timezone             = @tz,
                primary_contact_user_id = @contact,
                max_buildings        = @maxb,
                status               = @status::net.entity_status,
                lock_state           = @lock::net.lock_state,
                lock_reason          = @lreason,
                notes                = @notes,
                tags                 = @tags::jsonb,
                external_refs        = @refs::jsonb,
                updated_at           = now(),
                updated_by           = @uid,
                version              = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindSiteWriteParams(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteSiteAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.site", id, orgId, userId, ct);

    private static void BindSiteWriteParams(NpgsqlCommand cmd, Site e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("region", e.RegionId);
        cmd.Parameters.AddWithValue("profile", (object?)e.SiteProfileId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("code", e.SiteCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("a1", (object?)e.AddressLine1 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("a2", (object?)e.AddressLine2 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("a3", (object?)e.AddressLine3 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("city", (object?)e.City ?? DBNull.Value);
        cmd.Parameters.AddWithValue("state", (object?)e.StateOrCounty ?? DBNull.Value);
        cmd.Parameters.AddWithValue("post", (object?)e.Postcode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("country", (object?)e.Country ?? DBNull.Value);
        cmd.Parameters.AddWithValue("lat", (object?)e.Latitude ?? DBNull.Value);
        cmd.Parameters.AddWithValue("lng", (object?)e.Longitude ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tz", (object?)e.Timezone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("contact", (object?)e.PrimaryContactUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("maxb", (object?)e.MaxBuildings ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static Site ReadSite(NpgsqlDataReader r)
    {
        var e = new Site
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            RegionId = r.GetGuid(2),
            SiteProfileId = r.IsDBNull(3) ? null : r.GetGuid(3),
            SiteCode = r.GetString(4),
            DisplayName = r.GetString(5),
            AddressLine1 = r.IsDBNull(6) ? null : r.GetString(6),
            AddressLine2 = r.IsDBNull(7) ? null : r.GetString(7),
            AddressLine3 = r.IsDBNull(8) ? null : r.GetString(8),
            City = r.IsDBNull(9) ? null : r.GetString(9),
            StateOrCounty = r.IsDBNull(10) ? null : r.GetString(10),
            Postcode = r.IsDBNull(11) ? null : r.GetString(11),
            Country = r.IsDBNull(12) ? null : r.GetString(12),
            Latitude = r.IsDBNull(13) ? null : r.GetDecimal(13),
            Longitude = r.IsDBNull(14) ? null : r.GetDecimal(14),
            Timezone = r.IsDBNull(15) ? null : r.GetString(15),
            PrimaryContactUserId = r.IsDBNull(16) ? null : r.GetInt32(16),
            MaxBuildings = r.IsDBNull(17) ? null : r.GetInt32(17),
        };
        PopulateBase(e, r, 18);
        return e;
    }

    // ── BuildingProfile ─────────────────────────────────────────────────

    public async Task<List<BuildingProfile>> ListBuildingProfilesAsync(Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, profile_code, display_name, default_floor_count, " + BaseColumns + @"
            FROM net.building_profile
            WHERE organization_id = @org AND deleted_at IS NULL
            ORDER BY profile_code";
        return await ListAsync(sql, orgId, ReadBuildingProfile, ct);
    }

    public async Task<BuildingProfile?> GetBuildingProfileAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, profile_code, display_name, default_floor_count, " + BaseColumns + @"
            FROM net.building_profile
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadBuildingProfile, ct);
    }

    private static BuildingProfile ReadBuildingProfile(NpgsqlDataReader r)
    {
        var e = new BuildingProfile
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            ProfileCode = r.GetString(2),
            DisplayName = r.GetString(3),
            DefaultFloorCount = r.GetInt32(4),
        };
        PopulateBase(e, r, 5);
        return e;
    }

    // ── Building ────────────────────────────────────────────────────────

    public async Task<List<Building>> ListBuildingsAsync(Guid orgId, Guid? siteId, CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, site_id, building_profile_id, building_code,
                   display_name, building_number, is_reserved,
                   assigned_slash16_subnet_id, assigned_asn_block_id,
                   assigned_loopback_switch_block_id, assigned_loopback_server_block_id,
                   server_asn_allocation_id, max_floors, max_devices_total,
                   b2b_partners, " + BaseColumns + @"
            FROM net.building
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (siteId.HasValue ? " AND site_id = @site" : "") + @"
            ORDER BY building_code";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (siteId.HasValue) cmd.Parameters.AddWithValue("site", siteId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Building>();
        while (await r.ReadAsync(ct)) list.Add(ReadBuilding(r));
        return list;
    }

    public async Task<Building?> GetBuildingAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, site_id, building_profile_id, building_code,
                   display_name, building_number, is_reserved,
                   assigned_slash16_subnet_id, assigned_asn_block_id,
                   assigned_loopback_switch_block_id, assigned_loopback_server_block_id,
                   server_asn_allocation_id, max_floors, max_devices_total,
                   b2b_partners, " + BaseColumns + @"
            FROM net.building
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadBuilding, ct);
    }

    public async Task<Guid> CreateBuildingAsync(Building e, int? userId = null, CancellationToken ct = default)
    {
        // Phase-3 pool FKs (subnet, ASN block, loopback blocks, server ASN)
        // are not writable yet — they're assigned by the allocation service
        // when Phase 3 lands. We only persist the hierarchy + cardinality
        // fields that the operator can set today.
        const string sql = @"
            INSERT INTO net.building (organization_id, site_id, building_profile_id,
                                      building_code, display_name, building_number,
                                      is_reserved, max_floors, max_devices_total,
                                      b2b_partners, status, lock_state, notes,
                                      tags, external_refs, created_by, updated_by)
            VALUES (@org, @site, @profile, @code, @name, @num, @reserved,
                    @maxf, @maxd, @partners,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindBuildingWriteParams(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateBuildingAsync(Building e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.building SET
                site_id             = @site,
                building_profile_id = @profile,
                building_code       = @code,
                display_name        = @name,
                building_number     = @num,
                is_reserved         = @reserved,
                max_floors          = @maxf,
                max_devices_total   = @maxd,
                b2b_partners        = @partners,
                status              = @status::net.entity_status,
                lock_state          = @lock::net.lock_state,
                lock_reason         = @lreason,
                notes               = @notes,
                tags                = @tags::jsonb,
                external_refs       = @refs::jsonb,
                updated_at          = now(),
                updated_by          = @uid,
                version             = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindBuildingWriteParams(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteBuildingAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.building", id, orgId, userId, ct);

    private static void BindBuildingWriteParams(NpgsqlCommand cmd, Building e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("site", e.SiteId);
        cmd.Parameters.AddWithValue("profile", (object?)e.BuildingProfileId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("code", e.BuildingCode);
        cmd.Parameters.AddWithValue("name", e.DisplayName);
        cmd.Parameters.AddWithValue("num", (object?)e.BuildingNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("reserved", e.IsReserved);
        cmd.Parameters.AddWithValue("maxf", (object?)e.MaxFloors ?? DBNull.Value);
        cmd.Parameters.AddWithValue("maxd", (object?)e.MaxDevicesTotal ?? DBNull.Value);
        cmd.Parameters.Add(new NpgsqlParameter("partners", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = e.B2bPartners ?? Array.Empty<Guid>()
        });
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static Building ReadBuilding(NpgsqlDataReader r)
    {
        var e = new Building
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            SiteId = r.GetGuid(2),
            BuildingProfileId = r.IsDBNull(3) ? null : r.GetGuid(3),
            BuildingCode = r.GetString(4),
            DisplayName = r.GetString(5),
            BuildingNumber = r.IsDBNull(6) ? null : r.GetInt32(6),
            IsReserved = r.GetBoolean(7),
            AssignedSlash16SubnetId = r.IsDBNull(8) ? null : r.GetGuid(8),
            AssignedAsnBlockId = r.IsDBNull(9) ? null : r.GetGuid(9),
            AssignedLoopbackSwitchBlockId = r.IsDBNull(10) ? null : r.GetGuid(10),
            AssignedLoopbackServerBlockId = r.IsDBNull(11) ? null : r.GetGuid(11),
            ServerAsnAllocationId = r.IsDBNull(12) ? null : r.GetGuid(12),
            MaxFloors = r.IsDBNull(13) ? null : r.GetInt32(13),
            MaxDevicesTotal = r.IsDBNull(14) ? null : r.GetInt32(14),
            B2bPartners = r.IsDBNull(15) ? Array.Empty<Guid>() : (Guid[])r.GetValue(15),
        };
        PopulateBase(e, r, 16);
        return e;
    }

    // ── Floor ────────────────────────────────────────────────────────────

    public async Task<List<Floor>> ListFloorsAsync(Guid orgId, Guid? buildingId, CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, building_id, floor_profile_id, floor_code,
                   floor_number, display_name, max_rooms, " + BaseColumns + @"
            FROM net.floor
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (buildingId.HasValue ? " AND building_id = @bld" : "") + @"
            ORDER BY floor_number NULLS LAST, floor_code";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (buildingId.HasValue) cmd.Parameters.AddWithValue("bld", buildingId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Floor>();
        while (await r.ReadAsync(ct)) list.Add(ReadFloor(r));
        return list;
    }

    public async Task<Floor?> GetFloorAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, building_id, floor_profile_id, floor_code,
                   floor_number, display_name, max_rooms, " + BaseColumns + @"
            FROM net.floor
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadFloor, ct);
    }

    public async Task<Guid> CreateFloorAsync(Floor e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO net.floor (organization_id, building_id, floor_profile_id,
                                   floor_code, floor_number, display_name, max_rooms,
                                   status, lock_state, notes, tags, external_refs,
                                   created_by, updated_by)
            VALUES (@org, @bld, @profile, @code, @num, @name, @maxr,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindFloorWriteParams(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateFloorAsync(Floor e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.floor SET
                building_id       = @bld,
                floor_profile_id  = @profile,
                floor_code        = @code,
                floor_number      = @num,
                display_name      = @name,
                max_rooms         = @maxr,
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
        BindFloorWriteParams(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteFloorAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.floor", id, orgId, userId, ct);

    private static void BindFloorWriteParams(NpgsqlCommand cmd, Floor e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("bld", e.BuildingId);
        cmd.Parameters.AddWithValue("profile", (object?)e.FloorProfileId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("code", e.FloorCode);
        cmd.Parameters.AddWithValue("num", (object?)e.FloorNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("name", (object?)e.DisplayName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("maxr", (object?)e.MaxRooms ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static Floor ReadFloor(NpgsqlDataReader r)
    {
        var e = new Floor
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            BuildingId = r.GetGuid(2),
            FloorProfileId = r.IsDBNull(3) ? null : r.GetGuid(3),
            FloorCode = r.GetString(4),
            FloorNumber = r.IsDBNull(5) ? null : r.GetInt32(5),
            DisplayName = r.IsDBNull(6) ? null : r.GetString(6),
            MaxRooms = r.IsDBNull(7) ? null : r.GetInt32(7),
        };
        PopulateBase(e, r, 8);
        return e;
    }

    // ── FloorProfile ────────────────────────────────────────────────────

    public async Task<List<FloorProfile>> ListFloorProfilesAsync(Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, profile_code, display_name,
                   default_room_count, default_rack_count_per_room, " + BaseColumns + @"
            FROM net.floor_profile
            WHERE organization_id = @org AND deleted_at IS NULL
            ORDER BY profile_code";
        return await ListAsync(sql, orgId, ReadFloorProfile, ct);
    }

    public async Task<FloorProfile?> GetFloorProfileAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, profile_code, display_name,
                   default_room_count, default_rack_count_per_room, " + BaseColumns + @"
            FROM net.floor_profile
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadFloorProfile, ct);
    }

    private static FloorProfile ReadFloorProfile(NpgsqlDataReader r)
    {
        var e = new FloorProfile
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            ProfileCode = r.GetString(2),
            DisplayName = r.GetString(3),
            DefaultRoomCount = r.GetInt32(4),
            DefaultRackCountPerRoom = r.GetInt32(5),
        };
        PopulateBase(e, r, 6);
        return e;
    }

    // ── Room ─────────────────────────────────────────────────────────────

    public async Task<List<Room>> ListRoomsAsync(Guid orgId, Guid? floorId, CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, floor_id, room_code, room_type, max_racks,
                   environmental_notes, power_feed_a_id, power_feed_b_id, " + BaseColumns + @"
            FROM net.room
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (floorId.HasValue ? " AND floor_id = @floor" : "") + @"
            ORDER BY room_code";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (floorId.HasValue) cmd.Parameters.AddWithValue("floor", floorId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Room>();
        while (await r.ReadAsync(ct)) list.Add(ReadRoom(r));
        return list;
    }

    public async Task<Room?> GetRoomAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, floor_id, room_code, room_type, max_racks,
                   environmental_notes, power_feed_a_id, power_feed_b_id, " + BaseColumns + @"
            FROM net.room
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadRoom, ct);
    }

    public async Task<Guid> CreateRoomAsync(Room e, int? userId = null, CancellationToken ct = default)
    {
        // power_feed_a/b_id land in Phase 13 (environmental / PDU); skip them here.
        const string sql = @"
            INSERT INTO net.room (organization_id, floor_id, room_code, room_type,
                                  max_racks, environmental_notes,
                                  status, lock_state, notes, tags, external_refs,
                                  created_by, updated_by)
            VALUES (@org, @floor, @code, @type, @maxr, @envnotes,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindRoomWriteParams(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateRoomAsync(Room e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.room SET
                floor_id            = @floor,
                room_code           = @code,
                room_type           = @type,
                max_racks           = @maxr,
                environmental_notes = @envnotes,
                status              = @status::net.entity_status,
                lock_state          = @lock::net.lock_state,
                lock_reason         = @lreason,
                notes               = @notes,
                tags                = @tags::jsonb,
                external_refs       = @refs::jsonb,
                updated_at          = now(),
                updated_by          = @uid,
                version             = version + 1
            WHERE id = @id AND organization_id = @org AND version = @ver AND deleted_at IS NULL
            RETURNING version";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", e.Id);
        cmd.Parameters.AddWithValue("ver", e.Version);
        cmd.Parameters.AddWithValue("lreason", (object?)e.LockReason ?? DBNull.Value);
        BindRoomWriteParams(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteRoomAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.room", id, orgId, userId, ct);

    private static void BindRoomWriteParams(NpgsqlCommand cmd, Room e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("floor", e.FloorId);
        cmd.Parameters.AddWithValue("code", e.RoomCode);
        cmd.Parameters.AddWithValue("type", e.RoomType);
        cmd.Parameters.AddWithValue("maxr", (object?)e.MaxRacks ?? DBNull.Value);
        cmd.Parameters.AddWithValue("envnotes", (object?)e.EnvironmentalNotes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static Room ReadRoom(NpgsqlDataReader r)
    {
        var e = new Room
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            FloorId = r.GetGuid(2),
            RoomCode = r.GetString(3),
            RoomType = r.GetString(4),
            MaxRacks = r.IsDBNull(5) ? null : r.GetInt32(5),
            EnvironmentalNotes = r.IsDBNull(6) ? null : r.GetString(6),
            PowerFeedAId = r.IsDBNull(7) ? null : r.GetGuid(7),
            PowerFeedBId = r.IsDBNull(8) ? null : r.GetGuid(8),
        };
        PopulateBase(e, r, 9);
        return e;
    }

    // ── Rack ─────────────────────────────────────────────────────────────

    public async Task<List<Rack>> ListRacksAsync(Guid orgId, Guid? roomId, CancellationToken ct = default)
    {
        string sql = @"
            SELECT id, organization_id, room_id, rack_code, u_height, row, position,
                   pdu_a_id, pdu_b_id, max_devices, " + BaseColumns + @"
            FROM net.rack
            WHERE organization_id = @org AND deleted_at IS NULL" +
            (roomId.HasValue ? " AND room_id = @room" : "") + @"
            ORDER BY rack_code";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        if (roomId.HasValue) cmd.Parameters.AddWithValue("room", roomId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Rack>();
        while (await r.ReadAsync(ct)) list.Add(ReadRack(r));
        return list;
    }

    public async Task<Rack?> GetRackAsync(Guid id, Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, organization_id, room_id, rack_code, u_height, row, position,
                   pdu_a_id, pdu_b_id, max_devices, " + BaseColumns + @"
            FROM net.rack
            WHERE id = @id AND organization_id = @org AND deleted_at IS NULL";
        return await GetOneAsync(sql, id, orgId, ReadRack, ct);
    }

    public async Task<Guid> CreateRackAsync(Rack e, int? userId = null, CancellationToken ct = default)
    {
        // PDU FKs (pdu_a_id / pdu_b_id) land with the power model in Phase 13.
        const string sql = @"
            INSERT INTO net.rack (organization_id, room_id, rack_code, u_height,
                                  row, position, max_devices,
                                  status, lock_state, notes, tags, external_refs,
                                  created_by, updated_by)
            VALUES (@org, @room, @code, @uheight, @row, @pos, @maxd,
                    @status::net.entity_status, @lock::net.lock_state,
                    @notes, @tags::jsonb, @refs::jsonb, @uid, @uid)
            RETURNING id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        BindRackWriteParams(cmd, e, userId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> UpdateRackAsync(Rack e, int? userId = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE net.rack SET
                room_id           = @room,
                rack_code         = @code,
                u_height          = @uheight,
                row               = @row,
                position          = @pos,
                max_devices       = @maxd,
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
        BindRackWriteParams(cmd, e, userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int v ? v : throw new ConcurrencyException(e.Id, e.Version);
    }

    public Task<bool> SoftDeleteRackAsync(Guid id, Guid orgId, int? userId, CancellationToken ct = default)
        => SoftDeleteAsync("net.rack", id, orgId, userId, ct);

    private static void BindRackWriteParams(NpgsqlCommand cmd, Rack e, int? userId)
    {
        cmd.Parameters.AddWithValue("org", e.OrganizationId);
        cmd.Parameters.AddWithValue("room", e.RoomId);
        cmd.Parameters.AddWithValue("code", e.RackCode);
        cmd.Parameters.AddWithValue("uheight", e.UHeight);
        cmd.Parameters.AddWithValue("row", (object?)e.Row ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pos", (object?)e.Position ?? DBNull.Value);
        cmd.Parameters.AddWithValue("maxd", (object?)e.MaxDevices ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", e.Status.ToString());
        cmd.Parameters.AddWithValue("lock", e.LockState.ToString());
        cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", e.Tags.ToJsonString());
        cmd.Parameters.AddWithValue("refs", e.ExternalRefs.ToJsonString());
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    }

    private static Rack ReadRack(NpgsqlDataReader r)
    {
        var e = new Rack
        {
            Id = r.GetGuid(0),
            OrganizationId = r.GetGuid(1),
            RoomId = r.GetGuid(2),
            RackCode = r.GetString(3),
            UHeight = r.GetInt32(4),
            Row = r.IsDBNull(5) ? null : r.GetString(5),
            Position = r.IsDBNull(6) ? null : r.GetInt32(6),
            PduAId = r.IsDBNull(7) ? null : r.GetGuid(7),
            PduBId = r.IsDBNull(8) ? null : r.GetGuid(8),
            MaxDevices = r.IsDBNull(9) ? null : r.GetInt32(9),
        };
        PopulateBase(e, r, 10);
        return e;
    }

    // ── Generic helpers (used by profile / leaf entities) ──────────────

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

/// <summary>
/// Thrown when an optimistic-concurrency update's WHERE clause (id, org,
/// version) matched zero rows — meaning the entity was updated or deleted
/// by someone else since the caller read it.
/// </summary>
public class ConcurrencyException(Guid entityId, int expectedVersion)
    : InvalidOperationException(
        $"Optimistic concurrency conflict on entity {entityId}: expected version {expectedVersion} but row no longer matches.")
{
    public Guid EntityId { get; } = entityId;
    public int ExpectedVersion { get; } = expectedVersion;
}
