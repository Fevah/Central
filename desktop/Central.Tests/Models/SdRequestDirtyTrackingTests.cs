using Central.Core.Models;

namespace Central.Tests.Models;

/// <summary>
/// Comprehensive dirty tracking tests — this is the core write-back mechanism.
/// Every editable field must be tracked correctly.
/// </summary>
public class SdRequestDirtyTrackingTests
{
    private static SdRequest CreateTracked(string status = "Open", string priority = "Low",
        string group = "IT", string tech = "John", string category = "Hardware")
    {
        var req = new SdRequest
        {
            Id = 1, DisplayId = "SR-001", Subject = "Test",
            Status = status, Priority = priority, GroupName = group,
            TechnicianName = tech, Category = category
        };
        req.AcceptChanges();
        return req;
    }

    [Fact]
    public void AllEditableFields_TrackIndependently()
    {
        var req = CreateTracked();

        req.Status = "Closed";
        Assert.True(req.IsDirty);
        req.Status = "Open"; // revert
        Assert.False(req.IsDirty);

        req.Priority = "High";
        Assert.True(req.IsDirty);
        req.Priority = "Low"; // revert
        Assert.False(req.IsDirty);

        req.GroupName = "Security";
        Assert.True(req.IsDirty);
        req.GroupName = "IT"; // revert
        Assert.False(req.IsDirty);

        req.TechnicianName = "Jane";
        Assert.True(req.IsDirty);
        req.TechnicianName = "John"; // revert
        Assert.False(req.IsDirty);

        req.Category = "Software";
        Assert.True(req.IsDirty);
        req.Category = "Hardware"; // revert
        Assert.False(req.IsDirty);
    }

    [Fact]
    public void MultipleFields_AllDirty_StillDirtyWhenOneReverted()
    {
        var req = CreateTracked();

        req.Status = "Closed";
        req.Priority = "High";
        Assert.True(req.IsDirty);

        req.Status = "Open"; // revert one
        Assert.True(req.IsDirty); // still dirty because Priority changed
    }

    [Fact]
    public void MultipleFields_AllReverted_Clean()
    {
        var req = CreateTracked();

        req.Status = "Closed";
        req.Priority = "High";
        req.GroupName = "Security";

        req.Status = "Open";
        req.Priority = "Low";
        req.GroupName = "IT";

        Assert.False(req.IsDirty);
    }

    [Fact]
    public void NonEditableFields_DoNotAffectDirty()
    {
        var req = CreateTracked();

        req.RequesterName = "Changed";
        req.Site = "Changed";
        req.Department = "Changed";

        Assert.False(req.IsDirty);
    }

    [Fact]
    public void RowColor_ReflectsDirtyState()
    {
        var req = CreateTracked();
        Assert.Equal("#00000000", req.RowColor);

        req.Status = "Closed";
        Assert.Equal("#33F59E0B", req.RowColor);

        req.Status = "Open";
        Assert.Equal("#00000000", req.RowColor);
    }

    [Fact]
    public void AcceptChanges_UpdatesOriginals()
    {
        var req = CreateTracked();
        req.Status = "Closed";
        req.Priority = "High";
        Assert.True(req.IsDirty);

        req.AcceptChanges();

        Assert.False(req.IsDirty);
        Assert.Equal("Closed", req.OriginalStatus);
        Assert.Equal("High", req.OriginalPriority);

        // Now changing back to the OLD values should be dirty
        req.Status = "Open";
        Assert.True(req.IsDirty);
    }

    [Fact]
    public void PropertyChanged_FiresForIsDirtyAndRowColor()
    {
        var req = CreateTracked();
        var changed = new List<string>();
        req.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        req.Status = "Closed";

        Assert.Contains("IsDirty", changed);
        Assert.Contains("RowColor", changed);
    }
}
