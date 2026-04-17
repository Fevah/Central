using Central.Core.Models;

namespace Central.Tests.Models;

public class BackupRecordTests
{
    [Fact]
    public void FileSizeDisplay_AllRanges()
    {
        Assert.Equal("", new BackupRecord { FileSizeBytes = null }.FileSizeDisplay);
        Assert.Equal("100 B", new BackupRecord { FileSizeBytes = 100 }.FileSizeDisplay);
        Assert.Equal("1.0 KB", new BackupRecord { FileSizeBytes = 1024 }.FileSizeDisplay);
        Assert.Equal("1.0 MB", new BackupRecord { FileSizeBytes = 1024 * 1024 }.FileSizeDisplay);
        Assert.Equal("1.00 GB", new BackupRecord { FileSizeBytes = 1024L * 1024 * 1024 }.FileSizeDisplay);
    }

    [Fact]
    public void StatusColor_AllStatuses()
    {
        Assert.Equal("#22C55E", new BackupRecord { Status = "success" }.StatusColor);
        Assert.Equal("#3B82F6", new BackupRecord { Status = "running" }.StatusColor);
        Assert.Equal("#EF4444", new BackupRecord { Status = "failed" }.StatusColor);
        Assert.Equal("#6B7280", new BackupRecord { Status = "unknown" }.StatusColor);
    }
}
