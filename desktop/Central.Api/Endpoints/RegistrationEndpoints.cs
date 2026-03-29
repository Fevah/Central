using System.Text.Json;
using Central.Data;
using Central.Licensing;

namespace Central.Api.Endpoints;

/// <summary>
/// Self-service registration, subscription management, and module licensing API.
/// Registration endpoints are unauthenticated; subscription/license endpoints require auth.
/// </summary>
public static class RegistrationEndpoints
{
    public static RouteGroupBuilder MapRegistrationEndpoints(this RouteGroupBuilder group)
    {
        // ── Registration (no auth) ──

        group.MapPost("/register", async (RegistrationService svc, JsonElement body) =>
        {
            var result = await svc.RegisterAsync(new RegistrationRequest
            {
                Email = body.GetProperty("email").GetString() ?? "",
                Password = body.GetProperty("password").GetString() ?? "",
                CompanyName = body.GetProperty("company_name").GetString() ?? "",
                DisplayName = body.TryGetProperty("display_name", out var dn) ? dn.GetString() : null
            });

            if (!result.Success)
                return Results.BadRequest(new { error = result.ErrorMessage });

            // Provision tenant schema
            var migrationsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migrations");
            if (Directory.Exists(migrationsDir))
                await svc.ProvisionTenantSchemaAsync(result.TenantSlug!, migrationsDir);

            return Results.Ok(new
            {
                result.UserId,
                result.TenantId,
                result.TenantSlug,
                message = "Registration successful. Verify your email to activate.",
                verify_token = result.VerifyToken // In production, send via email instead
            });
        });

        group.MapPost("/verify-email", async (RegistrationService svc, JsonElement body) =>
        {
            var token = body.GetProperty("token").GetString() ?? "";
            var verified = await svc.VerifyEmailAsync(token);
            return verified ? Results.Ok(new { verified = true }) : Results.BadRequest(new { error = "Invalid or expired token" });
        });

        group.MapGet("/check-slug/{slug}", async (string slug, RegistrationService svc) =>
        {
            var available = await svc.IsSlugAvailableAsync(slug);
            return Results.Ok(new { slug, available });
        });

        // ── Subscription (auth required) ──

        group.MapGet("/subscription/plans", async (SubscriptionService svc) =>
        {
            return Results.Ok(await svc.GetPlansAsync());
        });

        // ── Module Licensing (auth required) ──

        group.MapGet("/modules", async (ModuleLicenseService svc, HttpContext ctx) =>
        {
            var tenantId = GetTenantId(ctx);
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            return Results.Ok(await svc.GetModulesAsync(tenantId));
        });

        group.MapPost("/modules/{code}/activate", async (string code, ModuleLicenseService svc, HttpContext ctx) =>
        {
            var tenantId = GetTenantId(ctx);
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            await svc.GrantModuleAsync(tenantId, code);
            return Results.Ok(new { module = code, status = "activated" });
        });

        // ── License Key (auth required) ──

        group.MapPost("/license/issue", async (LicenseKeyService svc, ModuleLicenseService moduleSvc,
            JsonElement body, HttpContext ctx) =>
        {
            var tenantId = GetTenantId(ctx);
            if (tenantId == Guid.Empty) return Results.Unauthorized();

            var hardwareId = body.GetProperty("hardware_id").GetString() ?? "";
            var modules = (await moduleSvc.GetModulesAsync(tenantId))
                .Where(m => m.IsLicensed).Select(m => m.Code).ToArray();

            var key = await svc.IssueLicenseAsync(tenantId, hardwareId, modules,
                DateTime.UtcNow.AddYears(1));

            return Results.Ok(new { license_key = key, modules, expires_at = DateTime.UtcNow.AddYears(1) });
        });

        return group;
    }

    private static Guid GetTenantId(HttpContext ctx)
    {
        var claim = ctx.User.FindFirst("tenant_id");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }
}
