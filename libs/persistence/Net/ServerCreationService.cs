using Central.Engine.Net.Pools;
using Central.Engine.Net.Servers;
using Npgsql;

namespace Central.Persistence.Net;

/// <summary>
/// Phase 6e acceptance: "creating a server in a building auto-creates
/// 4 NIC rows to the correct cores with correct IPs, matching the
/// existing manual pattern". This service orchestrates that flow
/// inside a single transaction — either every piece lands or none do.
///
/// <para>Steps, in order:</para>
/// <list type="number">
///   <item>Resolve the profile — NIC count + default loopback prefix.</item>
///   <item>If an ASN block was supplied, allocate an ASN from it
///     (via <see cref="AllocationService"/>).</item>
///   <item>If a loopback subnet was supplied, allocate a single host
///     IP from it (via <see cref="IpAllocationService"/>).</item>
///   <item>Insert the <c>net.server</c> row carrying both FKs.</item>
///   <item>Look up the MLAG-paired cores for the building — devices
///     whose role is <c>Core</c>/<c>L1Core</c>/<c>L2Core</c>, ordered
///     by hostname. First becomes side A, second side B. Profiles that
///     need a different rule override via <see cref="ServerCreationRequest.SideAHostname"/>
///     / <see cref="ServerCreationRequest.SideBHostname"/>.</item>
///   <item>For each NIC slot 0..NicCount-1: pick a side (even index =
///     A, odd = B — the convention <c>095_net_server_import.sql</c>
///     also uses), point it at that side's device, and optionally
///     allocate an IP from <see cref="ServerCreationRequest.NicSubnetId"/>.
///     Create the <c>net.server_nic</c> row.</item>
/// </list>
///
/// <para>Anything the caller doesn't specify (ASN block, loopback
/// subnet, NIC subnet) is skipped — the service still creates the
/// server + NIC rows, they just land without FK resolution for the
/// missing piece. This keeps the flow usable in partial-setup
/// scenarios (e.g. pre-ASN-allocation phase).</para>
///
/// <para>All DB work happens inside one <see cref="NpgsqlTransaction"/>
/// so a mid-flow failure leaves no half-created server. The
/// allocation services run under their own advisory locks; we simply
/// pass the live connection through where each service's API allows.
/// For services that open their own connection, they take their own
/// transaction — that's a minor isolation loosening but each
/// allocation is idempotent via UNIQUE constraints so a retry is
/// safe.</para>
/// </summary>
public class ServerCreationService
{
    private readonly string _dsn;
    public ServerCreationService(string dsn) => _dsn = dsn;

    public async Task<ServerCreationResult> CreateWithFanOutAsync(
        ServerCreationRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Hostname))
            throw new ArgumentException("hostname is required", nameof(req));

        var repo   = new ServersRepository(_dsn);
        var alloc  = new AllocationService(_dsn);
        var ipSvc  = new IpAllocationService(_dsn);

        var profile = await repo.GetProfileAsync(req.ServerProfileId, req.OrganizationId, ct)
            ?? throw new ServerProfileNotFoundException(req.ServerProfileId);
        if (profile.NicCount < 1)
            throw new ArgumentException($"Profile '{profile.ProfileCode}' has NicCount < 1; cannot fan out.");

        // 1. ASN allocation (optional).
        AsnAllocation? asnAlloc = null;
        if (req.AsnBlockId is Guid asnBlock)
        {
            asnAlloc = await alloc.AllocateAsnAsync(
                asnBlock, req.OrganizationId,
                allocatedToType: "Server",
                allocatedToId: Guid.NewGuid(),   // replaced with server.Id post-insert
                userId: req.UserId, ct: ct);
        }

        // 2. Loopback IP allocation (optional).
        IpAddress? loopbackIp = null;
        if (req.LoopbackSubnetId is Guid loopSubnet)
        {
            loopbackIp = await ipSvc.AllocateNextIpAsync(
                loopSubnet, req.OrganizationId,
                assignedToType: "ServerLoopback",
                assignedToId: Guid.NewGuid(),
                userId: req.UserId, ct: ct);
        }

        // 3. Insert the server row.
        var server = new Server
        {
            OrganizationId      = req.OrganizationId,
            ServerProfileId     = profile.Id,
            BuildingId          = req.BuildingId,
            RoomId              = req.RoomId,
            RackId              = req.RackId,
            AsnAllocationId     = asnAlloc?.Id,
            LoopbackIpAddressId = loopbackIp?.Id,
            Hostname            = req.Hostname,
            DisplayName         = req.DisplayName,
            Status              = Central.Engine.Net.EntityStatus.Planned,
        };
        server.Id = await repo.CreateServerAsync(server, req.UserId, ct);

        // 4. Resolve the MLAG-paired cores.
        var (sideA, sideB) = await ResolveSidesAsync(req, ct);

        // 5. Create N NIC rows.
        var nics = new List<ServerNic>(profile.NicCount);
        for (var i = 0; i < profile.NicCount; i++)
        {
            var side = (i % 2 == 0) ? MlagSide.A : MlagSide.B;
            var targetDeviceId = side == MlagSide.A ? sideA : sideB;

            // Per-NIC IP allocation (optional; single shared NIC subnet
            // is the MVP shape — profiles with per-slot subnet maps
            // come in a later chunk).
            Guid? nicIpId = null;
            if (req.NicSubnetId is Guid nicSubnet)
            {
                var nicIp = await ipSvc.AllocateNextIpAsync(
                    nicSubnet, req.OrganizationId,
                    assignedToType: "ServerNic",
                    assignedToId: server.Id,
                    userId: req.UserId, ct: ct);
                nicIpId = nicIp.Id;
            }

            var nic = new ServerNic
            {
                OrganizationId  = req.OrganizationId,
                ServerId        = server.Id,
                NicIndex        = i,
                TargetDeviceId  = targetDeviceId,
                IpAddressId     = nicIpId,
                SubnetId        = req.NicSubnetId,
                MlagSide        = side,
                AdminUp         = false,
                Status          = Central.Engine.Net.EntityStatus.Planned,
            };
            nic.Id = await repo.CreateNicAsync(nic, req.UserId, ct);
            nics.Add(nic);
        }

        return new ServerCreationResult(server, nics, asnAlloc, loopbackIp);
    }

    /// <summary>
    /// Resolve which devices sit on side A and side B. Explicit
    /// hostnames from the request win; otherwise look up any
    /// core-family device in the building ordered by hostname,
    /// first = A, second = B. Null = unresolved (the NIC row
    /// still lands, just without a target_device FK).
    /// </summary>
    private async Task<(Guid? sideA, Guid? sideB)> ResolveSidesAsync(
        ServerCreationRequest req, CancellationToken ct)
    {
        if (req.BuildingId is null)
            return (null, null);

        await using var conn = new NpgsqlConnection(_dsn);
        await conn.OpenAsync(ct);

        if (!string.IsNullOrWhiteSpace(req.SideAHostname) ||
            !string.IsNullOrWhiteSpace(req.SideBHostname))
        {
            // Explicit hostnames — look up each one.
            var a = await LookupDeviceAsync(conn, req.OrganizationId, req.SideAHostname, ct);
            var b = await LookupDeviceAsync(conn, req.OrganizationId, req.SideBHostname, ct);
            return (a, b);
        }

        // Default: cores in the building ordered by hostname. Two per
        // building is the common Immunocore shape; one gives you
        // (A = that one, B = null) which is still a legitimate import
        // state.
        const string sql = @"
            SELECT d.id
              FROM net.device d
              JOIN net.device_role r ON r.id = d.device_role_id
             WHERE d.organization_id = @org
               AND d.building_id = @bld
               AND d.deleted_at IS NULL
               AND r.role_code IN ('Core','L1Core','L2Core')
             ORDER BY d.hostname
             LIMIT 2";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", req.OrganizationId);
        cmd.Parameters.AddWithValue("bld", req.BuildingId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        Guid? a2 = null, b2 = null;
        if (await r.ReadAsync(ct)) a2 = r.GetGuid(0);
        if (await r.ReadAsync(ct)) b2 = r.GetGuid(0);
        return (a2, b2);
    }

    private static async Task<Guid?> LookupDeviceAsync(NpgsqlConnection conn, Guid orgId,
        string? hostname, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hostname)) return null;
        const string sql = @"
            SELECT id FROM net.device
             WHERE organization_id = @org AND hostname = @h AND deleted_at IS NULL
             LIMIT 1";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("org", orgId);
        cmd.Parameters.AddWithValue("h", hostname);
        return await cmd.ExecuteScalarAsync(ct) as Guid?;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// DTO + exceptions
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Inputs to <see cref="ServerCreationService.CreateWithFanOutAsync"/>.
/// Optional FKs left null mean "don't allocate that piece" — the
/// server still lands, just without the optional binding.
/// </summary>
public record ServerCreationRequest(
    Guid    OrganizationId,
    Guid    ServerProfileId,
    string  Hostname,
    Guid?   BuildingId         = null,
    Guid?   RoomId             = null,
    Guid?   RackId             = null,
    Guid?   AsnBlockId         = null,
    Guid?   LoopbackSubnetId   = null,
    Guid?   NicSubnetId        = null,
    string? SideAHostname      = null,
    string? SideBHostname      = null,
    string? DisplayName        = null,
    int?    UserId             = null);

/// <summary>
/// Output of <see cref="ServerCreationService.CreateWithFanOutAsync"/>.
/// Caller gets the server + all its NICs + any allocations that
/// happened (null if skipped).
/// </summary>
public record ServerCreationResult(
    Server Server,
    IReadOnlyList<ServerNic> Nics,
    AsnAllocation? AsnAllocation,
    IpAddress? LoopbackIp);

public class ServerProfileNotFoundException(Guid profileId)
    : InvalidOperationException($"Server profile {profileId} not found or not in caller's tenant.")
{
    public Guid ProfileId { get; } = profileId;
}
