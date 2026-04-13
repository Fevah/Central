using Central.Core.Models;

namespace Central.Tests.Models;

public class MigrationRecordTests
{
    [Fact]
    public void StatusColor_Applied_Green()
    {
        var m = new MigrationRecord { IsApplied = true };
        Assert.Equal("#22C55E", m.StatusColor);
    }

    [Fact]
    public void StatusColor_Pending_Amber()
    {
        var m = new MigrationRecord { IsApplied = false };
        Assert.Equal("#F59E0B", m.StatusColor);
    }

    [Fact]
    public void StatusText_Applied()
    {
        var m = new MigrationRecord { IsApplied = true };
        Assert.Equal("Applied", m.StatusText);
    }

    [Fact]
    public void StatusText_Pending()
    {
        var m = new MigrationRecord { IsApplied = false };
        Assert.Equal("Pending", m.StatusText);
    }

    [Fact]
    public void Defaults()
    {
        var m = new MigrationRecord();
        Assert.Equal(0, m.Id);
        Assert.Equal("", m.MigrationName);
        Assert.Null(m.AppliedAt);
        Assert.Null(m.DurationMs);
        Assert.Equal("system", m.AppliedBy);
        Assert.Equal("", m.Checksum);
        Assert.False(m.IsApplied);
    }
}
