using System.Reflection;

namespace Central.Api.Endpoints;

/// <summary>
/// Version endpoint — returns build info for the running API instance.
/// No auth required — used by desktop client to check compatibility.
/// </summary>
public static class VersionEndpoints
{
    public static RouteGroupBuilder MapVersionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", () =>
        {
            var asm = Assembly.GetExecutingAssembly();
            var version = asm.GetName().Version?.ToString() ?? "1.0.0.0";
            var buildDate = File.GetLastWriteTimeUtc(asm.Location);

            return Results.Ok(new
            {
                product = "Central API",
                version,
                build_date = buildDate.ToString("O"),
                runtime = $".NET {Environment.Version}",
                os = Environment.OSVersion.ToString(),
                architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
                endpoints = new
                {
                    auth = "/api/auth",
                    devices = "/api/devices",
                    switches = "/api/switches",
                    links = "/api/links",
                    vlans = "/api/vlans",
                    bgp = "/api/bgp",
                    admin = "/api/admin",
                    tasks = "/api/tasks",
                    identity = "/api/identity",
                    sync = "/api/sync",
                    dashboard = "/api/dashboard",
                    search = "/api/search",
                    health = "/api/health",
                    swagger = "/swagger"
                }
            });
        });

        return group;
    }
}
