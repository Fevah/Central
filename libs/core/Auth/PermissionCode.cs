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

    // ── Companies ──
    public const string CompaniesRead   = "companies:read";
    public const string CompaniesWrite  = "companies:write";
    public const string CompaniesDelete = "companies:delete";

    // ── Contacts ──
    public const string ContactsRead    = "contacts:read";
    public const string ContactsWrite   = "contacts:write";
    public const string ContactsDelete  = "contacts:delete";
    public const string ContactsExport  = "contacts:export";

    // ── Teams / Departments ──
    public const string AdminTeams      = "admin:teams";
    public const string AdminDepartments = "admin:departments";

    // ── Profiles ──
    public const string ProfilesRead    = "profiles:read";
    public const string ProfilesWrite   = "profiles:write";

    // ── CRM (future phases 15+) ──
    public const string CrmRead         = "crm:read";
    public const string CrmWrite        = "crm:write";
    public const string CrmDelete       = "crm:delete";
    public const string CrmAdmin        = "crm:admin";

    // ── Global Admin ──
    public const string GlobalAdminRead = "global_admin:read";
    public const string GlobalAdminWrite = "global_admin:write";

    // ── Groups ──
    public const string GroupsRead     = "groups:read";
    public const string GroupsWrite    = "groups:write";
    public const string GroupsDelete   = "groups:delete";
    public const string GroupsAssign   = "groups:assign";

    // ── Feature Flags ──
    public const string FeaturesRead   = "features:read";
    public const string FeaturesWrite  = "features:write";

    // ── Security ──
    public const string SecurityIpRules     = "security:ip_rules";
    public const string SecurityKeys        = "security:keys";
    public const string SecurityDeprovision = "security:deprovision";
    public const string SecurityDomains     = "security:domains";

    // ── Billing ──
    public const string BillingRead     = "billing:read";
    public const string BillingWrite    = "billing:write";
    public const string BillingDiscount = "billing:discount";
    public const string BillingInvoice  = "billing:invoice";

    // ── Stage 1: Marketing Automation ──
    public const string MarketingRead       = "marketing:read";
    public const string MarketingWrite      = "marketing:write";
    public const string MarketingCampaigns  = "marketing:campaigns";
    public const string MarketingSegments   = "marketing:segments";
    public const string MarketingSequences  = "marketing:sequences";
    public const string MarketingForms      = "marketing:forms";
    public const string MarketingAttribution = "marketing:attribution";

    // ── Stage 2: Sales Operations ──
    public const string SalesOpsRead        = "salesops:read";
    public const string SalesOpsWrite       = "salesops:write";
    public const string SalesOpsTerritories = "salesops:territories";
    public const string SalesOpsQuotas      = "salesops:quotas";
    public const string SalesOpsCommissions = "salesops:commissions";
    public const string SalesOpsSplits      = "salesops:splits";
    public const string SalesOpsAccountTeams = "salesops:account_teams";
    public const string SalesOpsForecast    = "salesops:forecast";

    // ── Stage 3: CPQ + Contracts + Revenue ──
    public const string CpqRead             = "cpq:read";
    public const string CpqWrite            = "cpq:write";
    public const string CpqBundles          = "cpq:bundles";
    public const string CpqPricing          = "cpq:pricing";
    public const string CpqDiscountApproval = "cpq:discount_approval";
    public const string ContractsRead       = "contracts:read";
    public const string ContractsWrite      = "contracts:write";
    public const string ContractsClauses    = "contracts:clauses";
    public const string SubscriptionsRead   = "subscriptions:read";
    public const string SubscriptionsWrite  = "subscriptions:write";
    public const string RevenueRead         = "revenue:read";
    public const string RevenueWrite        = "revenue:write";
    public const string OrdersRead          = "orders:read";
    public const string OrdersWrite         = "orders:write";
    public const string ApprovalsRead       = "approvals:read";
    public const string ApprovalsAct        = "approvals:act";

    // ── Stage 5: Portals + Platform + Commerce ──
    public const string PortalRead          = "portal:read";
    public const string PortalAdmin         = "portal:admin";
    public const string PartnerPortal       = "portal:partner";
    public const string CommunityRead       = "community:read";
    public const string CommunityWrite      = "community:write";
    public const string CommunityModerate   = "community:moderate";
    public const string KbRead              = "kb:read";
    public const string KbWrite             = "kb:write";
    public const string RulesValidation     = "rules:validation";
    public const string RulesWorkflow       = "rules:workflow";
    public const string CustomObjectsRead   = "custom_objects:read";
    public const string CustomObjectsWrite  = "custom_objects:write";
    public const string FieldPermissions    = "admin:field_permissions";
    public const string ImportRead          = "import:read";
    public const string ImportWrite         = "import:write";
    public const string CommerceRead        = "commerce:read";
    public const string CommerceWrite       = "commerce:write";
    public const string PaymentsRead        = "payments:read";
    public const string PaymentsRefund      = "payments:refund";

    // ── Stage 4: AI & Intelligence ──
    public const string AiProvidersRead     = "ai:providers:read";
    public const string AiProvidersAdmin    = "ai:providers:admin";      // platform-level management
    public const string AiTenantConfig      = "ai:tenant_config";        // tenant admin: BYOK, feature mapping
    public const string AiUse               = "ai:use";                  // any user calling AI
    public const string AiAssistantUse      = "ai:assistant";
    public const string AiAssistantAdmin    = "ai:assistant:admin";
    public const string AiScoringRead       = "ai:scoring:read";
    public const string AiScoringTrain      = "ai:scoring:train";
    public const string AiDedupRead         = "ai:dedup:read";
    public const string AiDedupMerge        = "ai:dedup:merge";
    public const string AiEnrichmentRead    = "ai:enrichment:read";
    public const string AiEnrichmentRun     = "ai:enrichment:run";
    public const string AiChurnRead         = "ai:churn:read";
    public const string AiCallsRead         = "ai:calls:read";
    public const string AiCallsAdmin        = "ai:calls:admin";
    public const string AiUsageRead         = "ai:usage:read";
}
