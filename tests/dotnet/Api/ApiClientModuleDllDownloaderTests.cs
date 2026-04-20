using System.Net;
using System.Text;
using Central.ApiClient;

namespace Central.Tests.Api;

/// <summary>
/// Phase 5 unit tests for <see cref="ApiClientModuleDllDownloader"/>,
/// the adapter that turns <see cref="ModuleCatalogClient.DownloadDllAsync"/>
/// into an <c>IModuleDllDownloader</c> the <c>ModuleHostManager</c>
/// can consume. Uses a stub <see cref="HttpMessageHandler"/> so
/// tests don't hit a live API.
/// </summary>
public class ApiClientModuleDllDownloaderTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public byte[]? ResponseBody { get; set; }
        public List<string> CalledPaths { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CalledPaths.Add(request.RequestUri!.AbsolutePath);
            var resp = new HttpResponseMessage(StatusCode);
            if (ResponseBody is not null)
                resp.Content = new ByteArrayContent(ResponseBody);
            return Task.FromResult(resp);
        }
    }

    private static (ApiClientModuleDllDownloader downloader, StubHandler stub) BuildHarness()
    {
        var stub = new StubHandler();
        var http = new HttpClient(stub) { BaseAddress = new Uri("https://example.invalid/") };
        var catalog = new ModuleCatalogClient(http);
        var downloader = new ApiClientModuleDllDownloader(catalog);
        return (downloader, stub);
    }

    [Fact]
    public async Task Download_Success_ReturnsBytes()
    {
        var (downloader, stub) = BuildHarness();
        stub.ResponseBody = Encoding.UTF8.GetBytes("module bytes");

        var bytes = await downloader.DownloadAsync("crm", "1.2.3");

        Assert.NotNull(bytes);
        Assert.Equal("module bytes", Encoding.UTF8.GetString(bytes!));
        Assert.Contains("/api/modules/crm/1.2.3/dll", stub.CalledPaths);
    }

    [Fact]
    public async Task Download_404_ReturnsNull()
    {
        var (downloader, stub) = BuildHarness();
        stub.StatusCode = HttpStatusCode.NotFound;

        var bytes = await downloader.DownloadAsync("crm", "99.99.99");

        Assert.Null(bytes);
    }

    [Fact]
    public async Task Download_410Yanked_ReturnsNull()
    {
        var (downloader, stub) = BuildHarness();
        stub.StatusCode = HttpStatusCode.Gone;

        var bytes = await downloader.DownloadAsync("crm", "1.0.0");

        Assert.Null(bytes);
    }

    [Fact]
    public async Task Download_5xx_Throws()
    {
        var (downloader, stub) = BuildHarness();
        stub.StatusCode = HttpStatusCode.InternalServerError;

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            downloader.DownloadAsync("crm", "1.0.0"));
    }

    [Fact]
    public void Ctor_RejectsNullCatalog()
    {
        Assert.Throws<ArgumentNullException>(() => new ApiClientModuleDllDownloader(null!));
    }
}
