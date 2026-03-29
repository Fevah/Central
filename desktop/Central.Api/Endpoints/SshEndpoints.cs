using Central.Api.Services;

namespace Central.Api.Endpoints;

public static class SshEndpoints
{
    public static RouteGroupBuilder MapSshEndpoints(this RouteGroupBuilder group)
    {
        // Rate limit check helper
        static IResult CheckRate(RateLimiter limiter, HttpContext ctx)
        {
            var user = ctx.User?.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!limiter.TryAcquire(user))
                return Results.StatusCode(429); // Too Many Requests
            return null!;
        }

        // POST /api/ssh/{switchId}/ping
        group.MapPost("/{switchId}/ping", async (Guid switchId, SshOperationsService ssh, RateLimiter limiter, HttpContext ctx) =>
        {
            var limited = CheckRate(limiter, ctx);
            if (limited is not null) return limited;
            var result = await ssh.PingSwitchAsync(switchId);
            return Results.Ok(result);
        });

        // POST /api/ssh/{switchId}/download-config
        group.MapPost("/{switchId}/download-config", async (Guid switchId, SshOperationsService ssh, RateLimiter limiter, HttpContext ctx) =>
        {
            var limited = CheckRate(limiter, ctx);
            if (limited is not null) return limited;
            var operatorName = ctx.User?.Identity?.Name ?? "api";
            var result = await ssh.DownloadConfigAsync(switchId, operatorName);
            return result.Ok ? Results.Ok(result) : Results.BadRequest(result);
        });

        // POST /api/ssh/{switchId}/sync-bgp
        group.MapPost("/{switchId}/sync-bgp", async (Guid switchId, SshOperationsService ssh, RateLimiter limiter, HttpContext ctx) =>
        {
            var limited = CheckRate(limiter, ctx);
            if (limited is not null) return limited;
            var operatorName = ctx.User?.Identity?.Name ?? "api";
            var result = await ssh.SyncBgpAsync(switchId, operatorName);
            return result.Ok ? Results.Ok(result) : Results.BadRequest(result);
        });

        // POST /api/ssh/ping-all — batch ping all switches
        group.MapPost("/ping-all", async (SshOperationsService ssh, RateLimiter limiter, HttpContext ctx) =>
        {
            var limited = CheckRate(limiter, ctx);
            if (limited is not null) return limited;
            var reachable = await ssh.PingAllSwitchesAsync();
            return Results.Ok(new { reachable, message = $"{reachable} switches reachable" });
        });

        return group;
    }
}
