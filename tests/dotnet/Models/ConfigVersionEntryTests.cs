using Central.Engine.Models;

namespace Central.Tests.Models;

public class ConfigVersionEntryTests
{
    // ── Defaults ──

    [Fact]
    public void Defaults_AreCorrect()
    {
        var e = new ConfigVersionEntry();
        Assert.Equal(Guid.Empty, e.Id);
        Assert.Equal(0, e.VersionNum);
        Assert.Equal(0, e.LineCount);
        Assert.Equal("", e.DiffStatus);
        Assert.Equal("", e.Operator);
        Assert.Equal("", e.SourceIp);
        Assert.False(e.IsSelected);
    }

    // ── Computed properties ──

    [Fact]
    public void DisplayDate_FormatsCorrectly()
    {
        var e = new ConfigVersionEntry { DownloadedAt = new DateTime(2026, 3, 15, 14, 30, 45) };
        Assert.Equal("2026-03-15 14:30:45", e.DisplayDate);
    }

    [Fact]
    public void DisplayVersion_FormatsCorrectly()
    {
        var e = new ConfigVersionEntry { VersionNum = 5 };
        Assert.Equal("v5", e.DisplayVersion);
    }

    [Fact]
    public void DisplaySummary_FormatsCorrectly()
    {
        var e = new ConfigVersionEntry { VersionNum = 3, LineCount = 150, DiffStatus = "Modified" };
        Assert.Equal("v3  \u00b7  150 lines  \u00b7  Modified", e.DisplaySummary);
    }

    [Fact]
    public void DisplaySummary_EmptyDiffStatus()
    {
        var e = new ConfigVersionEntry { VersionNum = 1, LineCount = 42, DiffStatus = "" };
        Assert.Equal("v1  \u00b7  42 lines  \u00b7  ", e.DisplaySummary);
    }

    [Theory]
    [InlineData(0, "v0")]
    [InlineData(1, "v1")]
    [InlineData(999, "v999")]
    public void DisplayVersion_VariousNumbers(int version, string expected)
    {
        var e = new ConfigVersionEntry { VersionNum = version };
        Assert.Equal(expected, e.DisplayVersion);
    }

    // ── PropertyChanged ──

    [Fact]
    public void IsSelected_PropertyChanged_Fires()
    {
        var e = new ConfigVersionEntry();
        var changed = new List<string>();
        e.PropertyChanged += (_, args) => changed.Add(args.PropertyName!);

        e.IsSelected = true;

        Assert.Contains("IsSelected", changed);
        Assert.True(e.IsSelected);
    }

    [Fact]
    public void IsSelected_PropertyChanged_FiresOnToggle()
    {
        var e = new ConfigVersionEntry();
        var count = 0;
        e.PropertyChanged += (_, _) => count++;

        e.IsSelected = true;
        e.IsSelected = false;

        Assert.Equal(2, count);
        Assert.False(e.IsSelected);
    }

    // ── Id assignment ──

    [Fact]
    public void Id_CanBeAssigned()
    {
        var id = Guid.NewGuid();
        var e = new ConfigVersionEntry { Id = id };
        Assert.Equal(id, e.Id);
    }

    // ── DownloadedAt edge cases ──

    [Fact]
    public void DisplayDate_MinValue()
    {
        var e = new ConfigVersionEntry { DownloadedAt = DateTime.MinValue };
        Assert.Contains("0001", e.DisplayDate);
    }
}
