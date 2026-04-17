using Central.Engine.Services;

namespace Central.Tests.Services;

public class FileManagementServiceTests
{
    [Fact]
    public void ComputeMd5_ByteArray_ReturnsDeterministicHash()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("hello world");
        var hash1 = FileManagementService.ComputeMd5(data);
        var hash2 = FileManagementService.ComputeMd5(data);
        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
    }

    [Fact]
    public void ComputeMd5_DifferentData_DifferentHash()
    {
        var hash1 = FileManagementService.ComputeMd5(System.Text.Encoding.UTF8.GetBytes("hello"));
        var hash2 = FileManagementService.ComputeMd5(System.Text.Encoding.UTF8.GetBytes("world"));
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeMd5_Stream_ReturnsSameAsBytes()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("test data");
        var hashBytes = FileManagementService.ComputeMd5(data);
        using var stream = new MemoryStream(data);
        var hashStream = FileManagementService.ComputeMd5(stream);
        Assert.Equal(hashBytes, hashStream);
    }

    [Fact]
    public void ComputeMd5_Stream_ResetsPosition()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("test");
        using var stream = new MemoryStream(data);
        FileManagementService.ComputeMd5(stream);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void ComputeMd5_EmptyData_ReturnsHash()
    {
        var hash = FileManagementService.ComputeMd5(Array.Empty<byte>());
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void ComputeMd5_LowercaseHex()
    {
        var hash = FileManagementService.ComputeMd5(new byte[] { 1, 2, 3 });
        Assert.Equal(hash, hash.ToLowerInvariant());
    }

    [Fact]
    public void ShouldStoreInline_SmallFile_True()
    {
        var svc = new FileManagementService();
        Assert.True(svc.ShouldStoreInline(1024)); // 1KB
    }

    [Fact]
    public void ShouldStoreInline_ExactLimit_True()
    {
        var svc = new FileManagementService();
        Assert.True(svc.ShouldStoreInline(10 * 1024 * 1024)); // exactly 10MB
    }

    [Fact]
    public void ShouldStoreInline_LargeFile_False()
    {
        var svc = new FileManagementService();
        Assert.False(svc.ShouldStoreInline(10 * 1024 * 1024 + 1)); // 10MB + 1 byte
    }

    [Fact]
    public void ShouldStoreInline_Zero_True()
    {
        var svc = new FileManagementService();
        Assert.True(svc.ShouldStoreInline(0));
    }

    [Fact]
    public void UseStorageService_DefaultFalse()
    {
        var svc = new FileManagementService();
        Assert.False(svc.UseStorageService);
        Assert.Null(svc.StorageServiceUrl);
    }

    [Fact]
    public void ConfigureStorageService_EnablesIt()
    {
        var svc = new FileManagementService();
        svc.ConfigureStorageService("http://storage:9000");
        Assert.True(svc.UseStorageService);
        Assert.Equal("http://storage:9000", svc.StorageServiceUrl);
    }

    // ── FileRecord display ──

    [Theory]
    [InlineData(null, "")]
    [InlineData(500L, "500 B")]
    [InlineData(2048L, "2.0 KB")]
    [InlineData(5242880L, "5.0 MB")]
    [InlineData(2147483648L, "2.00 GB")]
    public void FileRecord_FileSizeDisplay(long? size, string expected)
    {
        var r = new FileRecord { FileSize = size };
        Assert.Equal(expected, r.FileSizeDisplay);
    }
}
