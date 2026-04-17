using Central.Engine.Models;

namespace Central.Tests.Models;

public class SdRequestExtendedTests2
{
    // ── StatusColor for all statuses ──

    [Theory]
    [InlineData("Open", "#3B82F6")]
    [InlineData("In Progress", "#F59E0B")]
    [InlineData("On Hold", "#8B5CF6")]
    [InlineData("Awaiting Response", "#A855F7")]
    [InlineData("Resolved", "#22C55E")]
    [InlineData("Closed", "#22C55E")]
    [InlineData("Canceled", "#EF4444")]
    [InlineData("Cancelled", "#EF4444")]
    [InlineData("Archive", "#4B5563")]
    [InlineData("Unknown", "#9CA3AF")]
    [InlineData("", "#9CA3AF")]
    public void StatusColor_AllStatuses(string status, string expected)
    {
        var r = new SdRequest { Status = status };
        Assert.Equal(expected, r.StatusColor);
    }

    // ── PriorityColor ──

    [Theory]
    [InlineData("High", "#EF4444")]
    [InlineData("Urgent", "#EF4444")]
    [InlineData("Medium", "#F59E0B")]
    [InlineData("Normal", "#F59E0B")]
    [InlineData("Low", "#22C55E")]
    [InlineData("", "#9CA3AF")]
    [InlineData("Unknown", "#9CA3AF")]
    public void PriorityColor_AllPriorities(string priority, string expected)
    {
        var r = new SdRequest { Priority = priority };
        Assert.Equal(expected, r.PriorityColor);
    }

    // ── IsClosed ──

    [Theory]
    [InlineData("Resolved", true)]
    [InlineData("Closed", true)]
    [InlineData("Open", false)]
    [InlineData("In Progress", false)]
    [InlineData("Canceled", false)]
    public void IsClosed_VariousStatuses(string status, bool expected)
    {
        var r = new SdRequest { Status = status };
        Assert.Equal(expected, r.IsClosed);
    }

    // ── IsOverdue ──

    [Fact]
    public void IsOverdue_True_WhenPastDueAndOpen()
    {
        var r = new SdRequest
        {
            Status = "Open",
            DueBy = DateTime.UtcNow.AddDays(-1)
        };
        Assert.True(r.IsOverdue);
        Assert.Equal("\u26a0", r.OverdueIcon);
    }

    [Fact]
    public void IsOverdue_False_WhenNoDueBy()
    {
        var r = new SdRequest { Status = "Open", DueBy = null };
        Assert.False(r.IsOverdue);
        Assert.Equal("", r.OverdueIcon);
    }

    [Fact]
    public void IsOverdue_False_WhenResolved()
    {
        var r = new SdRequest
        {
            Status = "Resolved",
            DueBy = DateTime.UtcNow.AddDays(-1)
        };
        Assert.False(r.IsOverdue);
    }

    [Fact]
    public void IsOverdue_False_WhenCanceled()
    {
        var r = new SdRequest
        {
            Status = "Canceled",
            DueBy = DateTime.UtcNow.AddDays(-1)
        };
        Assert.False(r.IsOverdue);
    }

    [Fact]
    public void IsOverdue_False_WhenArchive()
    {
        var r = new SdRequest
        {
            Status = "Archive",
            DueBy = DateTime.UtcNow.AddDays(-1)
        };
        Assert.False(r.IsOverdue);
    }

    [Fact]
    public void IsOverdue_False_WhenFutureDue()
    {
        var r = new SdRequest
        {
            Status = "Open",
            DueBy = DateTime.UtcNow.AddDays(1)
        };
        Assert.False(r.IsOverdue);
    }

    // ── Dirty tracking ──

    [Fact]
    public void DirtyTracking_NotDirty_BeforeAcceptChanges()
    {
        var r = new SdRequest();
        r.Status = "Open";
        Assert.False(r.IsDirty); // not tracking yet
    }

    [Fact]
    public void DirtyTracking_Dirty_AfterStatusChange()
    {
        var r = new SdRequest { Status = "Open" };
        r.AcceptChanges();
        r.Status = "In Progress";
        Assert.True(r.IsDirty);
    }

    [Fact]
    public void DirtyTracking_Clean_AfterRevert()
    {
        var r = new SdRequest { Status = "Open" };
        r.AcceptChanges();
        r.Status = "In Progress";
        Assert.True(r.IsDirty);
        r.Status = "Open"; // revert
        Assert.False(r.IsDirty);
    }

    [Fact]
    public void DirtyTracking_Dirty_AfterPriorityChange()
    {
        var r = new SdRequest { Priority = "Low" };
        r.AcceptChanges();
        r.Priority = "High";
        Assert.True(r.IsDirty);
    }

    [Fact]
    public void DirtyTracking_Dirty_AfterGroupChange()
    {
        var r = new SdRequest { GroupName = "IT" };
        r.AcceptChanges();
        r.GroupName = "HR";
        Assert.True(r.IsDirty);
    }

    [Fact]
    public void DirtyTracking_Dirty_AfterTechnicianChange()
    {
        var r = new SdRequest { TechnicianName = "John" };
        r.AcceptChanges();
        r.TechnicianName = "Jane";
        Assert.True(r.IsDirty);
    }

    [Fact]
    public void DirtyTracking_Dirty_AfterCategoryChange()
    {
        var r = new SdRequest { Category = "Hardware" };
        r.AcceptChanges();
        r.Category = "Software";
        Assert.True(r.IsDirty);
    }

    // ── RowColor ──

    [Fact]
    public void RowColor_Amber_WhenDirty()
    {
        var r = new SdRequest { Status = "Open" };
        r.AcceptChanges();
        r.Status = "Closed";
        Assert.Equal("#33F59E0B", r.RowColor);
    }

    [Fact]
    public void RowColor_Transparent_WhenClean()
    {
        var r = new SdRequest();
        Assert.Equal("#00000000", r.RowColor);
    }

    // ── Defaults ──

    [Fact]
    public void Defaults_AreCorrect()
    {
        var r = new SdRequest();
        Assert.Equal(0, r.Id);
        Assert.Equal("", r.DisplayId);
        Assert.Equal("", r.Subject);
        Assert.Equal("", r.Status);
        Assert.Equal("", r.Priority);
        Assert.Equal("", r.GroupName);
        Assert.Equal("", r.TechnicianName);
        Assert.Equal("", r.RequesterName);
        Assert.Equal("", r.RequesterEmail);
        Assert.Equal("", r.Category);
        Assert.Equal("", r.Site);
        Assert.Equal("", r.Department);
        Assert.Equal("", r.Template);
        Assert.Equal("", r.TicketUrl);
        Assert.Null(r.CreatedAt);
        Assert.Null(r.DueBy);
        Assert.Null(r.ResolvedAt);
        Assert.False(r.IsServiceRequest);
    }

    // ── PropertyChanged ──

    [Fact]
    public void PropertyChanged_Subject_Fires()
    {
        var r = new SdRequest();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.Subject = "Laptop broken";
        Assert.Equal("Subject", changed);
    }

    [Fact]
    public void PropertyChanged_Status_FiresStatusColor()
    {
        var r = new SdRequest();
        var changed = new List<string>();
        r.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        r.Status = "Open";
        Assert.Contains("Status", changed);
        Assert.Contains("StatusColor", changed);
    }

    [Fact]
    public void PropertyChanged_Priority_FiresPriorityColor()
    {
        var r = new SdRequest();
        var changed = new List<string>();
        r.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        r.Priority = "High";
        Assert.Contains("Priority", changed);
        Assert.Contains("PriorityColor", changed);
    }

    [Fact]
    public void PropertyChanged_DueBy_FiresIsOverdue()
    {
        var r = new SdRequest();
        var changed = new List<string>();
        r.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        r.DueBy = DateTime.UtcNow;
        Assert.Contains("DueBy", changed);
        Assert.Contains("IsOverdue", changed);
    }
}
