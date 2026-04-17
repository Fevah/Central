using Central.Core.Models;

namespace Central.Tests.Models;

public class SdRequestTests
{
    [Fact]
    public void AcceptChanges_SetsTrackingAndClearsIsDirty()
    {
        var req = new SdRequest { Status = "Open", Priority = "Low", GroupName = "IT", TechnicianName = "John", Category = "Hardware" };
        req.AcceptChanges();

        Assert.False(req.IsDirty);
        Assert.Equal("Open", req.OriginalStatus);
        Assert.Equal("Low", req.OriginalPriority);
        Assert.Equal("IT", req.OriginalGroupName);
        Assert.Equal("John", req.OriginalTechnicianName);
        Assert.Equal("Hardware", req.OriginalCategory);
    }

    [Fact]
    public void MarkDirty_StatusChange_SetsIsDirty()
    {
        var req = new SdRequest { Status = "Open" };
        req.AcceptChanges();

        req.Status = "Closed";

        Assert.True(req.IsDirty);
        Assert.Equal("#33F59E0B", req.RowColor); // amber
    }

    [Fact]
    public void MarkDirty_RevertToOriginal_ClearsIsDirty()
    {
        var req = new SdRequest { Status = "Open" };
        req.AcceptChanges();

        req.Status = "Closed";
        Assert.True(req.IsDirty);

        req.Status = "Open";
        Assert.False(req.IsDirty);
        Assert.Equal("#00000000", req.RowColor); // transparent
    }

    [Fact]
    public void MarkDirty_BeforeAcceptChanges_DoesNotTrack()
    {
        var req = new SdRequest { Status = "Open" };
        // No AcceptChanges called
        req.Status = "Closed";

        Assert.False(req.IsDirty);
    }

    [Fact]
    public void IsClosed_ResolvedAndClosed_BothTrue()
    {
        Assert.True(new SdRequest { Status = "Resolved" }.IsClosed);
        Assert.True(new SdRequest { Status = "Closed" }.IsClosed);
    }

    [Fact]
    public void IsClosed_OpenStatuses_False()
    {
        Assert.False(new SdRequest { Status = "Open" }.IsClosed);
        Assert.False(new SdRequest { Status = "In Progress" }.IsClosed);
        Assert.False(new SdRequest { Status = "On Hold" }.IsClosed);
        Assert.False(new SdRequest { Status = "Canceled" }.IsClosed);
    }

    [Fact]
    public void StatusColor_AllStatuses_ReturnCorrectColor()
    {
        Assert.Equal("#3B82F6", new SdRequest { Status = "Open" }.StatusColor);
        Assert.Equal("#F59E0B", new SdRequest { Status = "In Progress" }.StatusColor);
        Assert.Equal("#8B5CF6", new SdRequest { Status = "On Hold" }.StatusColor);
        Assert.Equal("#A855F7", new SdRequest { Status = "Awaiting Response" }.StatusColor);
        Assert.Equal("#22C55E", new SdRequest { Status = "Resolved" }.StatusColor);
        Assert.Equal("#22C55E", new SdRequest { Status = "Closed" }.StatusColor);
        Assert.Equal("#EF4444", new SdRequest { Status = "Canceled" }.StatusColor);
        Assert.Equal("#4B5563", new SdRequest { Status = "Archive" }.StatusColor);
        Assert.Equal("#9CA3AF", new SdRequest { Status = "Unknown" }.StatusColor);
    }

    [Fact]
    public void PriorityColor_AllPriorities_ReturnCorrectColor()
    {
        Assert.Equal("#EF4444", new SdRequest { Priority = "High" }.PriorityColor);
        Assert.Equal("#EF4444", new SdRequest { Priority = "Urgent" }.PriorityColor);
        Assert.Equal("#F59E0B", new SdRequest { Priority = "Medium" }.PriorityColor);
        Assert.Equal("#F59E0B", new SdRequest { Priority = "Normal" }.PriorityColor);
        Assert.Equal("#22C55E", new SdRequest { Priority = "Low" }.PriorityColor);
    }

    [Fact]
    public void IsOverdue_PastDueOpenTicket_True()
    {
        var req = new SdRequest { Status = "Open", DueBy = DateTime.UtcNow.AddDays(-1) };
        Assert.True(req.IsOverdue);
    }

    [Fact]
    public void IsOverdue_ClosedTicket_False()
    {
        var req = new SdRequest { Status = "Closed", DueBy = DateTime.UtcNow.AddDays(-1) };
        Assert.False(req.IsOverdue);
    }

    [Fact]
    public void IsOverdue_NoDueDate_False()
    {
        var req = new SdRequest { Status = "Open", DueBy = null };
        Assert.False(req.IsOverdue);
    }

    [Fact]
    public void IsOverdue_FutureDue_False()
    {
        var req = new SdRequest { Status = "Open", DueBy = DateTime.UtcNow.AddDays(7) };
        Assert.False(req.IsOverdue);
    }

    [Fact]
    public void IsOverdue_CanceledWithPastDue_False()
    {
        var req = new SdRequest { Status = "Canceled", DueBy = DateTime.UtcNow.AddDays(-1) };
        Assert.False(req.IsOverdue);
    }

    [Fact]
    public void PropertyChanged_Fires_OnStatusChange()
    {
        var req = new SdRequest();
        var changed = new List<string>();
        req.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        req.Status = "Open";

        Assert.Contains("Status", changed);
        Assert.Contains("StatusColor", changed);
    }

    [Fact]
    public void AcceptChanges_AfterDirty_Resets()
    {
        var req = new SdRequest { Status = "Open", Priority = "Low" };
        req.AcceptChanges();
        req.Status = "Closed";
        Assert.True(req.IsDirty);

        req.AcceptChanges();
        Assert.False(req.IsDirty);
        Assert.Equal("Closed", req.OriginalStatus);
    }
}
