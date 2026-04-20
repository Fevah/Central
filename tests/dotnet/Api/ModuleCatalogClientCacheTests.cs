using System.Net;
using System.Text;
using Central.ApiClient;

namespace Central.Tests.Api;

/// <summary>
/// Unit tests for the in-memory catalog cache added to
/// <see cref="ModuleCatalogClient"/> in Roll D
/// (<c>ListCatalogAsync(bypassCache?)</c> + <c>InvalidateCatalogCache</c>).
/// Verifies cache hit/miss counting against a recording
/// <see cref="HttpMessageHandler"/>.
/// </summary>
public class ModuleCatalogClientCacheTests
{
    private sealed class CountingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(resp);
        }
    }

    private static (ModuleCatalogClient client, CountingHandler handler) BuildHarness()
    {
        var handler = new CountingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
        var client = new ModuleCatalogClient(http);
        return (client, handler);
    }

    [Fact]
    public async Task ListCatalog_RepeatedCalls_HitCacheAfterFirst()
    {
        var (client, handler) = BuildHarness();

        await client.ListCatalogAsync();
        await client.ListCatalogAsync();
        await client.ListCatalogAsync();

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ListCatalog_BypassCache_ForcesRefetch()
    {
        var (client, handler) = BuildHarness();

        await client.ListCatalogAsync();                    // populates cache
        await client.ListCatalogAsync(bypassCache: true);   // forces refetch

        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task InvalidateCache_NextCallRefetches()
    {
        var (client, handler) = BuildHarness();

        await client.ListCatalogAsync();
        client.InvalidateCatalogCache();
        await client.ListCatalogAsync();

        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task ListCatalog_ZeroTtl_NeverCaches()
    {
        var (client, handler) = BuildHarness();
        client.CatalogCacheTtl = TimeSpan.Zero;

        await client.ListCatalogAsync();
        await client.ListCatalogAsync();
        await client.ListCatalogAsync();

        Assert.Equal(3, handler.CallCount);
    }
}
