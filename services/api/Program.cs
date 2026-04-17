using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Central.Api.Auth;
using Central.Api.Endpoints;
using Central.Api.Hubs;
using Central.Api.Services;
using Central.Workflows;
using Elsa.Extensions;
using Serilog;
using Serilog.Events;

// Configure Serilog structured logging (JSON to console for K8s log aggregation)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "central-api")
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Database DSN — MUST be set via config or environment variable
var dsn = builder.Configuration.GetConnectionString("Default")
    ?? Environment.GetEnvironmentVariable("CENTRAL_DSN")
    ?? throw new InvalidOperationException(
        "Database connection string not configured. Set ConnectionStrings:Default in appsettings.json or CENTRAL_DSN environment variable.");
builder.Services.AddSingleton(new Central.Persistence.DbConnectionFactory(dsn));

// JWT settings — MUST be set via environment variables (no hardcoded fallbacks)
var jwtSecret = Environment.GetEnvironmentVariable("CENTRAL_JWT_SECRET")
    ?? throw new InvalidOperationException(
        "CENTRAL_JWT_SECRET environment variable is required. Set a random 32+ byte secret.");
var authServiceSecret = Environment.GetEnvironmentVariable("AUTH_SERVICE_JWT_SECRET")
    ?? throw new InvalidOperationException(
        "AUTH_SERVICE_JWT_SECRET environment variable is required. Must match the auth-service configuration.");
var jwtSettings = new JwtSettings { Secret = jwtSecret };
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<TokenService>();

// Authentication — accepts JWTs from auth-service OR Central's own tokens
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        // Accept tokens from multiple issuers (auth-service + Central)
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuers = new[] { "auth-service", jwtSettings.Issuer, "central-auth" },
            ValidAudiences = new[] { "central-api", "secure", jwtSettings.Audience, "Central.Desktop" },
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
            {
                // Try auth-service secret first, then Central's own
                return new[]
                {
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authServiceSecret)),
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
                };
            }
        };

        // Allow SignalR to use token from query string
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("GlobalAdmin", policy =>
        policy.RequireClaim("global_admin", "true"));
});

// SignalR + real-time change notifier
builder.Services.AddSignalR();
builder.Services.AddHostedService<ChangeNotifierService>();

// SSH operations (server-side only — credentials never leave the server)
builder.Services.AddSingleton<SshOperationsService>();

// Rate limiter for SSH endpoints (10 requests per 60s per user)
builder.Services.AddSingleton(new RateLimiter(maxRequests: 10, windowSeconds: 60));

// Background job scheduler
builder.Services.AddHostedService<JobSchedulerService>();

// Credential encryption key (from env or default)
Central.Engine.Auth.CredentialEncryptor.Initialize(
    Environment.GetEnvironmentVariable("CENTRAL_CREDENTIAL_KEY"));

// Swagger/OpenAPI (Swashbuckle 10.x + Microsoft.OpenApi 2.x)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Central API",
        Version = "v1",
        Description = "Central — enterprise infrastructure platform. REST API for: devices, switches, links, VLANs, BGP, SSH, service desk, admin, identity providers, sync engine, audit trail, search, scheduling, notifications, and background jobs."
    });
    o.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Description = "JWT token from /api/auth/login",
        Name = "Authorization",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    // Swashbuckle 10.x: OpenApiSecurityRequirement uses OpenApiSecuritySchemeReference
    o.AddSecurityRequirement(doc =>
    {
        var schemeRef = new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer");
        return new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            { schemeRef, new List<string>() }
        };
    });
});

// CORS — restrict to known origins (WPF + web clients)
var allowedOrigins = (Environment.GetEnvironmentVariable("CENTRAL_CORS_ORIGINS") ?? "http://localhost:4200,http://localhost:7472,http://localhost:5000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins)
     .AllowAnyMethod()
     .AllowAnyHeader()
     .AllowCredentials()));

// Multi-tenancy DI registration
builder.Services.AddScoped<Central.Tenancy.TenantContext>();
builder.Services.AddScoped<Central.Tenancy.ITenantContext>(sp => sp.GetRequiredService<Central.Tenancy.TenantContext>());
builder.Services.AddScoped<Central.Tenancy.ITenantConnectionFactory>(sp =>
{
    var tenantCtx = sp.GetRequiredService<Central.Tenancy.ITenantContext>();
    return new Central.Tenancy.TenantConnectionFactory(dsn, tenantCtx);
});

// Licensing services (scoped — platform schema queries)
builder.Services.AddScoped(_ => new Central.Licensing.RegistrationService(dsn));
builder.Services.AddScoped(_ => new Central.Licensing.SubscriptionService(dsn));
builder.Services.AddScoped(_ => new Central.Licensing.ModuleLicenseService(dsn));
builder.Services.AddScoped<Central.Licensing.LicenseKeyService>(sp =>
{
    var svc = new Central.Licensing.LicenseKeyService(dsn);
    var privKey = Environment.GetEnvironmentVariable("CENTRAL_LICENSE_PRIVATE_KEY");
    var pubKey = Environment.GetEnvironmentVariable("CENTRAL_LICENSE_PUBLIC_KEY");
    if (!string.IsNullOrEmpty(privKey) && !string.IsNullOrEmpty(pubKey))
        svc.LoadKeys(privKey, pubKey);
    return svc;
});

// Security: ABAC engine (singleton with tenant-level caching)
builder.Services.AddSingleton(Central.Security.SecurityPolicyEngine.Instance);

// Collaboration: Presence tracking (singleton, in-memory)
builder.Services.AddSingleton(Central.Collaboration.PresenceService.Instance);

// AI: Tenant provider resolver (dual-tier: tenant BYOK → platform key fallback)
builder.Services.AddSingleton<Central.Engine.Services.ITenantAiProviderResolver>(
    _ => new Central.Persistence.TenantAiProviderResolver(dsn));

// Elsa Workflows engine (PostgreSQL persistence, custom activities)
builder.Services.AddCentralWorkflows(dsn);

var app = builder.Build();

// Elsa workflow middleware (HTTP triggers, API endpoints)
app.UseWorkflows();

// Structured request logging via Serilog (JSON output for Loki/ELK ingestion)
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("user", httpContext.User?.Identity?.Name ?? "anonymous");
        diagnosticContext.Set("client_ip", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    };
    opts.GetLevel = (ctx, _, _) =>
        ctx.Request.Path.StartsWithSegments("/api/health") || ctx.Request.Path.StartsWithSegments("/swagger")
            ? LogEventLevel.Debug
            : LogEventLevel.Information;
});

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "Central API v1"));
app.UseAuthentication();
app.UseAuthorization();

// Middleware pipeline (order: Correlation → SecurityHeaders → Logging → RateLimit → ApiKey → Auth → Tenant → ModuleLicense)
Central.Api.Middleware.CorrelationIdExtensions.UseCorrelationId(app);
Central.Api.Middleware.SecurityHeadersExtensions.UseSecurityHeaders(app);
Central.Api.Middleware.RequestLoggingExtensions.UseRequestLogging(app);
Central.Api.Middleware.RateLimitingExtensions.UseRateLimiting(app, maxRequests: 200, windowSeconds: 60);
Central.Api.Middleware.ApiKeyAuthExtensions.UseApiKeyAuth(app);

// Tenant resolution + module license enforcement (after auth)
Central.Api.Middleware.TenantResolutionExtensions.UseTenantResolution(app);
Central.Api.Middleware.ModuleLicenseExtensions.UseModuleLicenseCheck(app);

// Public endpoints (no auth)
app.MapGroup("/api/health").MapHealthEndpoints();
app.MapGroup("/api/version").MapVersionEndpoints();
app.MapGroup("/api/updates").WithTags("Updates").MapUpdateEndpoints();

// Inbound client-side error logging (browsers/mobile push errors here so
// they land in app_log alongside server logs). Anonymous on purpose — auth
// failures themselves should still be loggable.
app.MapGroup("/api/log/client").WithTags("Logging").MapClientLogEndpoints();

// Files
app.MapGroup("/api/files").WithTags("Files").MapFileEndpoints().RequireAuthorization();

// Auth + Registration (anonymous endpoints)
app.MapGroup("/api/auth").WithTags("Auth").MapAuthEndpoints();
app.MapGroup("/api/register").WithTags("Registration").MapRegistrationEndpoints();

// Core data
app.MapGroup("/api/devices").WithTags("Devices").MapDeviceEndpoints().RequireAuthorization();
app.MapGroup("/api/switches").WithTags("Switches").MapSwitchEndpoints().RequireAuthorization();
app.MapGroup("/api/links").WithTags("Links").MapLinkEndpoints().RequireAuthorization();
app.MapGroup("/api/vlans").WithTags("VLANs").MapVlanEndpoints().RequireAuthorization();
app.MapGroup("/api/bgp").WithTags("BGP").MapBgpEndpoints().RequireAuthorization();
app.MapGroup("/api/tasks").WithTags("Tasks").MapTaskEndpoints().RequireAuthorization();
app.MapGroup("/api/projects").WithTags("Projects").MapProjectEndpoints().RequireAuthorization();

// Admin
app.MapGroup("/api/admin").WithTags("Admin").MapAdminEndpoints().RequireAuthorization();
app.MapGroup("/api/locations").WithTags("Admin").MapLocationEndpoints().RequireAuthorization();
app.MapGroup("/api/backup").WithTags("Admin").MapBackupEndpoints().RequireAuthorization();
app.MapGroup("/api/import").WithTags("Admin").MapImportEndpoints().RequireAuthorization();

// SSH & Jobs
app.MapGroup("/api/ssh").WithTags("SSH").MapSshEndpoints().RequireAuthorization();
app.MapGroup("/api/jobs").WithTags("Jobs").MapJobEndpoints().RequireAuthorization();

// Service Desk & Scheduler
app.MapGroup("/api/appointments").WithTags("Scheduler").MapAppointmentEndpoints().RequireAuthorization();

// Identity & Security
app.MapGroup("/api/identity").WithTags("Identity").MapIdentityProviderEndpoints().RequireAuthorization();
app.MapGroup("/api/keys").WithTags("Identity").MapApiKeyEndpoints().RequireAuthorization();
app.MapGroup("/api/audit").WithTags("Identity").MapAuditEndpoints().RequireAuthorization();
app.MapGroup("/api/notifications").WithTags("Notifications").MapNotificationEndpoints().RequireAuthorization();

// Sync Engine
app.MapGroup("/api/sync").WithTags("Sync").MapSyncEndpoints().RequireAuthorization();
app.MapGroup("/api/webhooks").WithTags("Sync").MapWebhookEndpoints(); // No auth — external systems POST here

// Enterprise V2 — Foundation entities (Phases 1-5)
app.MapGroup("/api/companies").WithTags("Companies").MapCompanyEndpoints().RequireAuthorization();
app.MapGroup("/api/contacts").WithTags("Contacts").MapContactEndpoints().RequireAuthorization();
app.MapGroup("/api/teams").WithTags("Teams").MapTeamEndpoints().RequireAuthorization();
app.MapGroup("/api/addresses").WithTags("Addresses").MapAddressEndpoints().RequireAuthorization();
app.MapGroup("/api/profile").WithTags("Profile").MapProfileEndpoints().RequireAuthorization();
app.MapGroup("/api/invitations").WithTags("Admin").MapInvitationEndpoints().RequireAuthorization();
app.MapGroup("/api/role-templates").WithTags("Admin").MapRoleTemplateEndpoints().RequireAuthorization();

// Enterprise V3 — Groups, feature flags, security, billing extensions
app.MapGroup("/api/groups").WithTags("Groups").MapGroupEndpoints().RequireAuthorization();
app.MapGroup("/api/features").WithTags("Features").MapFeatureFlagEndpoints().RequireAuthorization();
app.MapGroup("/api/security/ip-rules").WithTags("Security").MapIpRulesEndpoints().RequireAuthorization();
app.MapGroup("/api/security/social-providers").WithTags("Security").MapSocialProviderEndpoints().RequireAuthorization();
app.MapGroup("/api/user-keys").WithTags("Security").MapUserKeyEndpoints().RequireAuthorization();
app.MapGroup("/api/account").WithTags("Account").MapAccountRecoveryEndpoints();
app.MapGroup("/api/billing").WithTags("Billing").MapBillingEndpoints().RequireAuthorization();

// CRM Module (Phases 15-19 + 22-23 of the 29-phase buildout)
app.MapGroup("/api/crm/accounts").WithTags("CRM").MapCrmAccountEndpoints().RequireAuthorization();
app.MapGroup("/api/crm/deals").WithTags("CRM").MapCrmDealEndpoints().RequireAuthorization();
app.MapGroup("/api/crm/leads").WithTags("CRM").MapCrmLeadEndpoints().RequireAuthorization();
app.MapGroup("/api/crm/activities").WithTags("CRM").MapCrmActivityEndpoints().RequireAuthorization();
app.MapGroup("/api/crm").WithTags("CRM").MapCrmProductQuoteEndpoints().RequireAuthorization();
app.MapGroup("/api/crm/dashboard").WithTags("CRM").MapCrmDashboardEndpoints().RequireAuthorization();
app.MapGroup("/api/crm/reports").WithTags("CRM").MapCrmReportEndpoints().RequireAuthorization();
app.MapGroup("/api/crm/documents").WithTags("CRM").MapCrmDocumentEndpoints().RequireAuthorization();

// CRM Expansion Stage 1: Marketing Automation
app.MapGroup("/api/crm/campaigns").WithTags("CRM Marketing").MapCampaignEndpoints().RequireAuthorization();
app.MapGroup("/api/crm/marketing").WithTags("CRM Marketing").MapSegmentSequenceEndpoints();  // Public landing + form endpoints inside

// CRM Expansion Stage 2: Sales Operations
app.MapGroup("/api/crm/salesops").WithTags("CRM Sales Ops").MapSalesOpsEndpoints().RequireAuthorization();

// CRM Expansion Stage 3: CPQ + Contracts + Revenue
app.MapGroup("/api/crm/cpq").WithTags("CRM CPQ").MapCpqEndpoints().RequireAuthorization();
app.MapGroup("/api/approvals").WithTags("Approvals").MapApprovalEndpoints().RequireAuthorization();
app.MapGroup("/api/crm/contracts").WithTags("CRM Contracts").MapContractEndpoints().RequireAuthorization();
app.MapGroup("/api/crm/subscriptions").WithTags("CRM Revenue").MapSubscriptionEndpoints().RequireAuthorization();
app.MapGroup("/api/crm/revenue").WithTags("CRM Revenue").MapRevenueEndpoints().RequireAuthorization();
app.MapGroup("/api/crm/orders").WithTags("CRM Revenue").MapOrderEndpoints().RequireAuthorization();

// CRM Expansion Stage 5: Portals + Platform + Commerce
app.MapGroup("/api/portal").WithTags("Portal").MapPortalEndpoints();                 // Magic-link endpoints anonymous
app.MapGroup("/api/portal").WithTags("Community").MapKbCommunityEndpoints();          // KB + community (public GETs)
app.MapGroup("/api/rules").WithTags("Rules").MapRuleEngineEndpoints().RequireAuthorization();
app.MapGroup("/api/custom-objects").WithTags("Custom Objects").MapCustomObjectEndpoints().RequireAuthorization();
app.MapGroup("/api/commerce").WithTags("Commerce").MapImportCommerceEndpoints().RequireAuthorization();

// CRM Expansion Stage 4: AI & Intelligence (dual-tier: platform + tenant BYOK)
app.MapGroup("/api/global-admin/ai").WithTags("AI Admin").MapAiProviderAdminEndpoints().RequireAuthorization("GlobalAdmin");
app.MapGroup("/api/ai/tenant").WithTags("AI Tenant").MapTenantAiConfigEndpoints().RequireAuthorization();
app.MapGroup("/api/ai/assistant").WithTags("AI Assistant").MapAiAssistantEndpoints().RequireAuthorization();
app.MapGroup("/api/ai").WithTags("AI Insights").MapAiInsightsEndpoints().RequireAuthorization();

// Email integration (Phase 20)
app.MapGroup("/api/email").WithTags("Email").MapEmailEndpoints();  // Tracking endpoints are anonymous

// Webhook subscriptions (Phase 29)
app.MapGroup("/api/webhooks").WithTags("Webhooks").MapWebhookSubscriptionEndpoints().RequireAuthorization();

// Collaboration & Security
app.MapGroup("/api/presence").WithTags("Collaboration").MapPresenceEndpoints().RequireAuthorization();
app.MapGroup("/api/security/policies").WithTags("Security").MapSecurityPolicyEndpoints().RequireAuthorization();

// Global Admin (platform-level — requires global_admin claim)
app.MapGroup("/api/global-admin").WithTags("Global Admin").MapGlobalAdminEndpoints().RequireAuthorization("GlobalAdmin");
app.MapGroup("/api/global-admin").WithTags("Tenant Provisioning").MapTenantProvisioningEndpoints().RequireAuthorization("GlobalAdmin");

// Platform
app.MapGroup("/api/dashboard").WithTags("Platform").MapDashboardEndpoints().RequireAuthorization();
app.MapGroup("/api/status").WithTags("Platform").MapStatusEndpoints().RequireAuthorization();
app.MapGroup("/api/search").WithTags("Platform").MapSearchEndpoints().RequireAuthorization();
app.MapGroup("/api/activity").WithTags("Platform").MapActivityEndpoints().RequireAuthorization();
app.MapGroup("/api/validation").WithTags("Platform").MapValidationEndpoints().RequireAuthorization();
app.MapGroup("/api/settings").WithTags("Platform").MapSettingsEndpoints().RequireAuthorization();

// Register default validation rules
Central.Engine.Services.DataValidationService.Instance.RegisterDefaults();

// SignalR hub (auth required)
app.MapHub<NotificationHub>("/hubs/notify").RequireAuthorization();

// Health check (anonymous)
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();
