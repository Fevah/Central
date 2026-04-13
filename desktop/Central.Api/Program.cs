using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Central.Api.Auth;
using Central.Api.Endpoints;
using Central.Api.Hubs;
using Central.Api.Services;
using Central.Workflows;
using Elsa.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Database DSN
var dsn = builder.Configuration.GetConnectionString("Default")
    ?? Environment.GetEnvironmentVariable("CENTRAL_DSN")
    ?? "Host=192.168.56.10;Port=30432;Database=central;Username=central;Password=central";
builder.Services.AddSingleton(new Central.Data.DbConnectionFactory(dsn));

// JWT settings — supports both Central tokens and auth-service tokens
var jwtSecret = Environment.GetEnvironmentVariable("CENTRAL_JWT_SECRET")
    ?? "Central-InsecureDev-" + Environment.MachineName;
var authServiceSecret = Environment.GetEnvironmentVariable("AUTH_SERVICE_JWT_SECRET")
    ?? "Central-Auth-Shared-JWT-Key-Override-This-In-Production-32bytes!";
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
            ValidateAudience = false,  // auth-service uses "secure", Central uses own audience
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuers = new[] { "auth-service", jwtSettings.Issuer, "central-auth" },
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
Central.Core.Auth.CredentialEncryptor.Initialize(
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

// CORS (allow WPF + web clients)
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.SetIsOriginAllowed(_ => true)
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

// Elsa Workflows engine (PostgreSQL persistence, custom activities)
builder.Services.AddCentralWorkflows(dsn);

var app = builder.Build();

// Elsa workflow middleware (HTTP triggers, API endpoints)
app.UseWorkflows();

// Request logging middleware — logs method, path, status, duration
app.Use(async (context, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await next();
    sw.Stop();
    var path = context.Request.Path;
    if (!path.StartsWithSegments("/health") && !path.StartsWithSegments("/swagger"))
    {
        var method = context.Request.Method;
        var status = context.Response.StatusCode;
        var user = context.User?.Identity?.Name ?? "anonymous";
        app.Logger.LogInformation("{Method} {Path} → {Status} ({Duration}ms) [{User}]",
            method, path, status, sw.ElapsedMilliseconds, user);
    }
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

// Enterprise V2
app.MapGroup("/api/presence").WithTags("Collaboration").MapPresenceEndpoints().RequireAuthorization();
app.MapGroup("/api/security/policies").WithTags("Security").MapSecurityPolicyEndpoints().RequireAuthorization();

// Global Admin (platform-level — requires global_admin claim)
app.MapGroup("/api/global-admin").WithTags("Global Admin").MapGlobalAdminEndpoints().RequireAuthorization("GlobalAdmin");

// Platform
app.MapGroup("/api/dashboard").WithTags("Platform").MapDashboardEndpoints().RequireAuthorization();
app.MapGroup("/api/status").WithTags("Platform").MapStatusEndpoints().RequireAuthorization();
app.MapGroup("/api/search").WithTags("Platform").MapSearchEndpoints().RequireAuthorization();
app.MapGroup("/api/activity").WithTags("Platform").MapActivityEndpoints().RequireAuthorization();
app.MapGroup("/api/validation").WithTags("Platform").MapValidationEndpoints().RequireAuthorization();
app.MapGroup("/api/settings").WithTags("Platform").MapSettingsEndpoints().RequireAuthorization();

// Register default validation rules
Central.Core.Services.DataValidationService.Instance.RegisterDefaults();

// SignalR hub (auth required)
app.MapHub<NotificationHub>("/hubs/notify").RequireAuthorization();

// Health check (anonymous)
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();
