using System.Net.Http;
using Xunit;

namespace Central.Tests.Integration;

/// <summary>
/// Smoke tests that verify all platform service endpoints are reachable.
/// These hit the running K8s cluster — skip if it's not deployed.
///
/// Run all: dotnet test --filter "FullyQualifiedName~ServiceHealth"
/// Skip via env: SKIP_HEALTH_TESTS=1
/// </summary>
public class ServiceHealthTests
{
    private static readonly bool Skip = Environment.GetEnvironmentVariable("SKIP_HEALTH_TESTS") == "1";

    private static async Task AssertReachable(string url, int timeoutSeconds = 5)
    {
        if (Skip) return;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        try
        {
            var resp = await http.GetAsync(url);
            // Any 2xx, 3xx, 4xx is fine — proves the service is reachable.
            // 5xx or no response means the service is down.
            Assert.True((int)resp.StatusCode < 500,
                $"{url} returned {(int)resp.StatusCode} {resp.StatusCode} — service is unhealthy");
        }
        catch (HttpRequestException ex)
        {
            // Service genuinely unreachable — fail with clear message
            Assert.Fail($"{url} unreachable: {ex.Message} — service may be down");
        }
        catch (TaskCanceledException)
        {
            Assert.Fail($"{url} timed out after {timeoutSeconds}s — service may be hung");
        }
    }

    [Fact]
    public Task CentralApi_IsReachable() =>
        AssertReachable("http://192.168.56.200:5000/api/health");

    [Fact]
    public Task AuthService_IsReachable() =>
        AssertReachable("http://192.168.56.10:30081/health");

    [Fact]
    public async Task ApiGateway_IsReachable()
    {
        if (Skip) return;
        // Gateway /health returns 503 if any backend is unhealthy, but reaching
        // it at all proves the gateway itself is up. We accept any HTTP response.
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            var resp = await http.GetAsync("http://192.168.56.203:8000/health");
            // Accept any status code — the gateway responded
            Assert.True((int)resp.StatusCode > 0, "Gateway returned no status");
        }
        catch (HttpRequestException ex)
        {
            Assert.Fail($"Gateway unreachable: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            Assert.Fail("Gateway timed out");
        }
    }

    [Fact]
    public Task Prometheus_IsReachable() =>
        AssertReachable("http://192.168.56.10:30909/-/healthy");

    [Fact]
    public Task Grafana_IsReachable() =>
        AssertReachable("http://192.168.56.210:3000/api/health");

    [Fact]
    public Task JaegerUi_IsReachable() =>
        AssertReachable("http://192.168.56.10:30686/");

    [Fact]
    public Task MinioConsole_IsReachable() =>
        AssertReachable("http://192.168.56.10:30901/");

    [Fact]
    public Task MinioS3Api_IsReachable() =>
        AssertReachable("http://192.168.56.10:30900/minio/health/live");

    [Fact]
    public Task ContainerRegistry_IsReachable() =>
        AssertReachable("http://192.168.56.10:30500/v2/");
}
