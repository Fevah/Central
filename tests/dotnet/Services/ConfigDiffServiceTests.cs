using Central.Engine.Services;

namespace Central.Tests.Services;

public class ConfigDiffServiceTests
{
    [Fact]
    public void IdenticalLines_NoChanges()
    {
        var old = new[] { "line1", "line2", "line3" };
        var @new = new[] { "line1", "line2", "line3" };

        ConfigDiffService.BuildAlignedDiff(old, @new,
            out var left, out var leftChanged,
            out var right, out var rightChanged);

        Assert.Equal(3, left.Length);
        Assert.Equal(3, right.Length);
        Assert.All(leftChanged, c => Assert.False(c));
        Assert.All(rightChanged, c => Assert.False(c));
        Assert.Equal(old, left);
        Assert.Equal(@new, right);
    }

    [Fact]
    public void EmptyOld_AllNew()
    {
        var old = Array.Empty<string>();
        var @new = new[] { "line1", "line2" };

        ConfigDiffService.BuildAlignedDiff(old, @new,
            out var left, out var leftChanged,
            out var right, out var rightChanged);

        Assert.Equal(2, right.Length);
        Assert.All(rightChanged, c => Assert.True(c));
        Assert.All(left, l => Assert.Equal("", l));
    }

    [Fact]
    public void EmptyNew_AllRemoved()
    {
        var old = new[] { "line1", "line2" };
        var @new = Array.Empty<string>();

        ConfigDiffService.BuildAlignedDiff(old, @new,
            out var left, out var leftChanged,
            out var right, out var rightChanged);

        Assert.Equal(2, left.Length);
        Assert.All(leftChanged, c => Assert.True(c));
        Assert.All(right, r => Assert.Equal("", r));
    }

    [Fact]
    public void BothEmpty_EmptyResult()
    {
        ConfigDiffService.BuildAlignedDiff(
            Array.Empty<string>(), Array.Empty<string>(),
            out var left, out var leftChanged,
            out var right, out var rightChanged);

        Assert.Empty(left);
        Assert.Empty(right);
    }

    [Fact]
    public void OneLine_Replaced()
    {
        var old = new[] { "set system hostname CORE01" };
        var @new = new[] { "set system hostname CORE02" };

        ConfigDiffService.BuildAlignedDiff(old, @new,
            out var left, out var leftChanged,
            out var right, out var rightChanged);

        // Both are changed (different lines)
        Assert.True(leftChanged[0]);
        Assert.True(rightChanged[0]);
    }

    [Fact]
    public void InsertedLine_AlignsCorrectly()
    {
        var old = new[] { "A", "C" };
        var @new = new[] { "A", "B", "C" };

        ConfigDiffService.BuildAlignedDiff(old, @new,
            out var left, out var leftChanged,
            out var right, out var rightChanged);

        // A matches, B is inserted, C matches
        Assert.Equal(3, left.Length);
        Assert.Equal(3, right.Length);
        Assert.False(leftChanged[0]); // A
        Assert.False(rightChanged[0]); // A
        Assert.False(leftChanged[2]); // C
        Assert.False(rightChanged[2]); // C
    }

    [Fact]
    public void DeletedLine_AlignsCorrectly()
    {
        var old = new[] { "A", "B", "C" };
        var @new = new[] { "A", "C" };

        ConfigDiffService.BuildAlignedDiff(old, @new,
            out var left, out var leftChanged,
            out var right, out var rightChanged);

        Assert.Equal(3, left.Length);
        Assert.Equal("A", left[0]);
        Assert.False(leftChanged[0]);
    }

    [Fact]
    public void OutputArrays_SameLength()
    {
        var old = new[] { "A", "B", "C", "D" };
        var @new = new[] { "X", "B", "Y", "D" };

        ConfigDiffService.BuildAlignedDiff(old, @new,
            out var left, out var leftChanged,
            out var right, out var rightChanged);

        Assert.Equal(left.Length, right.Length);
        Assert.Equal(left.Length, leftChanged.Length);
        Assert.Equal(right.Length, rightChanged.Length);
    }

    [Fact]
    public void UnchangedLines_MatchExactly()
    {
        var old = new[] { "set vlans vlan-id 101", "set vlans vlan-id 102", "set vlans vlan-id 103" };
        var @new = new[] { "set vlans vlan-id 101", "set vlans vlan-id 104", "set vlans vlan-id 103" };

        ConfigDiffService.BuildAlignedDiff(old, @new,
            out var left, out var leftChanged,
            out var right, out var rightChanged);

        // First and last lines should be unchanged
        Assert.False(leftChanged[0]);
        Assert.False(rightChanged[0]);
        Assert.Equal("set vlans vlan-id 101", left[0]);
        Assert.Equal("set vlans vlan-id 101", right[0]);
    }
}
