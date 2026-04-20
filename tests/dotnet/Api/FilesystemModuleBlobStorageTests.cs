using System.Text;
using Central.Api.Services;

namespace Central.Tests.Api;

/// <summary>
/// Unit tests for <see cref="FilesystemModuleBlobStorage"/> — the
/// Phase 2 storage adapter behind <c>POST /api/modules/publish</c> +
/// <c>GET /api/modules/{code}/{version}/dll</c>. Each test uses a
/// fresh temp directory so they're parallel-safe and don't leak state.
/// </summary>
public class FilesystemModuleBlobStorageTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FilesystemModuleBlobStorage _storage;

    public FilesystemModuleBlobStorageTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "Central.ModuleBlobStorageTests", Guid.NewGuid().ToString("N"));
        _storage = new FilesystemModuleBlobStorage(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch { /* best effort in teardown */ }
    }

    [Fact]
    public async Task WriteThenOpenRead_RoundTripsContent()
    {
        var payload = Encoding.UTF8.GetBytes("fake dll content");
        await _storage.WriteAsync("crm", "1.2.3", new MemoryStream(payload));

        await using var read = await _storage.OpenReadAsync("crm", "1.2.3");
        Assert.NotNull(read);
        using var ms = new MemoryStream();
        await read!.CopyToAsync(ms);
        Assert.Equal(payload, ms.ToArray());
    }

    [Fact]
    public async Task OpenRead_ReturnsNullWhenMissing()
    {
        var read = await _storage.OpenReadAsync("crm", "does-not-exist");
        Assert.Null(read);
    }

    [Fact]
    public async Task Exists_TracksWriteAndDelete()
    {
        Assert.False(await _storage.ExistsAsync("audit", "0.1.0"));
        await _storage.WriteAsync("audit", "0.1.0", new MemoryStream(new byte[] { 1, 2, 3 }));
        Assert.True(await _storage.ExistsAsync("audit", "0.1.0"));

        Assert.True(await _storage.DeleteAsync("audit", "0.1.0"));
        Assert.False(await _storage.ExistsAsync("audit", "0.1.0"));

        // Deleting again returns false, not throws.
        Assert.False(await _storage.DeleteAsync("audit", "0.1.0"));
    }

    [Fact]
    public async Task Write_OverwritesExistingBlob()
    {
        await _storage.WriteAsync("projects", "2.0.0", new MemoryStream(new byte[] { 0xAA }));
        await _storage.WriteAsync("projects", "2.0.0", new MemoryStream(new byte[] { 0xBB, 0xCC }));

        await using var read = await _storage.OpenReadAsync("projects", "2.0.0");
        using var ms = new MemoryStream();
        await read!.CopyToAsync(ms);
        Assert.Equal(new byte[] { 0xBB, 0xCC }, ms.ToArray());
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("crm/../etc")]
    [InlineData("crm\\..\\etc")]
    [InlineData("has\0null")]
    public void Write_RejectsPathTraversalSegments(string badCode)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            _storage.WriteAsync(badCode, "1.0.0", new MemoryStream(new byte[] { 0 })).GetAwaiter().GetResult());
    }

    [Fact]
    public void Ctor_RejectsEmptyRoot()
    {
        Assert.Throws<ArgumentException>(() => new FilesystemModuleBlobStorage(""));
    }

    [Fact]
    public void Ctor_CreatesRootIfMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), "Central.ModuleBlobStorageTests.Ctor", Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(path));
        try
        {
            _ = new FilesystemModuleBlobStorage(path);
            Assert.True(Directory.Exists(path));
        }
        finally
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public async Task Delete_CleansUpEmptyVersionAndModuleDirs()
    {
        await _storage.WriteAsync("servicedesk", "3.1.4", new MemoryStream(new byte[] { 1 }));
        var versionDir = Path.Combine(_tempRoot, "servicedesk", "3.1.4");
        var moduleDir  = Path.Combine(_tempRoot, "servicedesk");
        Assert.True(Directory.Exists(versionDir));

        await _storage.DeleteAsync("servicedesk", "3.1.4");

        Assert.False(Directory.Exists(versionDir));
        Assert.False(Directory.Exists(moduleDir));
        // Root persists so the next write doesn't race a recreate.
        Assert.True(Directory.Exists(_tempRoot));
    }
}
