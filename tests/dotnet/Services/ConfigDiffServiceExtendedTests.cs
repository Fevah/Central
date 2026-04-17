using Central.Core.Services;

namespace Central.Tests.Services;

public class ConfigDiffServiceExtendedTests
{
    [Fact]
    public void BuildAlignedDiff_IdenticalLines_NoChanges()
    {
        var lines = new[] { "set a 1", "set b 2", "set c 3" };
        ConfigDiffService.BuildAlignedDiff(lines, lines,
            out var left, out var lc, out var right, out var rc);
        Assert.Equal(3, left.Length);
        Assert.All(lc, changed => Assert.False(changed));
        Assert.All(rc, changed => Assert.False(changed));
    }

    [Fact]
    public void BuildAlignedDiff_FullyDifferent_AllChanged()
    {
        var oldLines = new[] { "set a 1" };
        var newLines = new[] { "set b 2" };
        ConfigDiffService.BuildAlignedDiff(oldLines, newLines,
            out var left, out var lc, out var right, out var rc);
        Assert.True(lc.All(c => c));
        Assert.True(rc.All(c => c));
    }

    [Fact]
    public void BuildAlignedDiff_AddedLines()
    {
        var oldLines = new[] { "set a 1", "set c 3" };
        var newLines = new[] { "set a 1", "set b 2", "set c 3" };
        ConfigDiffService.BuildAlignedDiff(oldLines, newLines,
            out var left, out var lc, out var right, out var rc);
        // Should have 3 output rows
        Assert.Equal(3, left.Length);
        // First and last should be unchanged
        Assert.False(lc[0]);
        // Middle line added on right
        Assert.Contains("set b 2", right);
    }

    [Fact]
    public void BuildAlignedDiff_RemovedLines()
    {
        var oldLines = new[] { "set a 1", "set b 2", "set c 3" };
        var newLines = new[] { "set a 1", "set c 3" };
        ConfigDiffService.BuildAlignedDiff(oldLines, newLines,
            out var left, out var lc, out var right, out var rc);
        Assert.Equal(3, left.Length);
        Assert.Contains("set b 2", left);
    }

    [Fact]
    public void BuildAlignedDiff_EmptyOld()
    {
        var oldLines = Array.Empty<string>();
        var newLines = new[] { "set a 1", "set b 2" };
        ConfigDiffService.BuildAlignedDiff(oldLines, newLines,
            out var left, out var lc, out var right, out var rc);
        Assert.Equal(2, right.Length);
        Assert.All(rc, c => Assert.True(c));
    }

    [Fact]
    public void BuildAlignedDiff_EmptyNew()
    {
        var oldLines = new[] { "set a 1", "set b 2" };
        var newLines = Array.Empty<string>();
        ConfigDiffService.BuildAlignedDiff(oldLines, newLines,
            out var left, out var lc, out var right, out var rc);
        Assert.Equal(2, left.Length);
        Assert.All(lc, c => Assert.True(c));
    }

    [Fact]
    public void BuildAlignedDiff_BothEmpty()
    {
        ConfigDiffService.BuildAlignedDiff(Array.Empty<string>(), Array.Empty<string>(),
            out var left, out var lc, out var right, out var rc);
        Assert.Empty(left);
        Assert.Empty(right);
    }

    [Fact]
    public void BuildAlignedDiff_LargeConfig_HandlesCorrectly()
    {
        var oldLines = Enumerable.Range(1, 100).Select(i => $"set line {i}").ToArray();
        var newLines = Enumerable.Range(1, 100).Select(i => i == 50 ? "set modified-line 50" : $"set line {i}").ToArray();
        ConfigDiffService.BuildAlignedDiff(oldLines, newLines,
            out var left, out var lc, out var right, out var rc);
        Assert.True(left.Length >= 100);
        // At least one changed line
        Assert.Contains(true, lc);
    }

    [Fact]
    public void BuildAlignedDiff_DuplicateLines_HandledCorrectly()
    {
        var oldLines = new[] { "set a 1", "set a 1", "set b 2" };
        var newLines = new[] { "set a 1", "set b 2" };
        ConfigDiffService.BuildAlignedDiff(oldLines, newLines,
            out var left, out var lc, out var right, out var rc);
        Assert.Equal(left.Length, right.Length);
    }
}
