namespace Central.Core.Auth;

/// <summary>
/// Permission code constants using module:action format.
/// Used with IAuthContext.HasPermission().
/// </summary>
public static class P
{
    // ── Devices / IPAM ──
    public const string DevicesRead     = "devices:read";
    public const string DevicesWrite    = "devices:write";
    public const string DevicesDelete   = "devices:delete";
    public const string DevicesExport   = "devices:export";
    public const string DevicesReserved = "devices:reserved";

    // ── Switches ──
    public const string SwitchesRead    = "switches:read";
    public const string SwitchesWrite   = "switches:write";
    public const string SwitchesDelete  = "switches:delete";
    public const string SwitchesPing    = "switches:ping";
    public const string SwitchesSsh     = "switches:ssh";
    public const string SwitchesSync    = "switches:sync";
    public const string SwitchesDeploy  = "switches:deploy";

    // ── Links ──
    public const string LinksRead       = "links:read";
    public const string LinksWrite      = "links:write";
    public const string LinksDelete     = "links:delete";

    // ── Routing / BGP ──
    public const string BgpRead         = "bgp:read";
    public const string BgpWrite        = "bgp:write";
    public const string BgpSync         = "bgp:sync";

    // ── VLANs ──
    public const string VlansRead       = "vlans:read";
    public const string VlansWrite      = "vlans:write";

    // ── Admin ──
    public const string AdminUsers      = "admin:users";
    public const string AdminRoles      = "admin:roles";
    public const string AdminLookups    = "admin:lookups";
    public const string AdminSettings   = "admin:settings";
    public const string AdminAudit      = "admin:audit";
    public const string AdminAd         = "admin:ad";
    public const string AdminMigrations = "admin:migrations";
    public const string AdminPurge      = "admin:purge";
    public const string AdminBackup     = "admin:backup";
    public const string AdminLocations  = "admin:locations";
    public const string AdminReferences = "admin:references";
    public const string AdminContainers = "admin:containers";

    // ── Tasks ──
    public const string TasksRead       = "tasks:read";
    public const string TasksWrite      = "tasks:write";
    public const string TasksDelete     = "tasks:delete";

    // ── Projects (task hierarchy) ──
    public const string ProjectsRead    = "projects:read";
    public const string ProjectsWrite   = "projects:write";
    public const string ProjectsDelete  = "projects:delete";
    public const string SprintsRead     = "sprints:read";
    public const string SprintsWrite    = "sprints:write";
    public const string SprintsDelete   = "sprints:delete";

    // ── Scheduler ──
    public const string SchedulerRead   = "scheduler:read";
    public const string SchedulerWrite  = "scheduler:write";
}
