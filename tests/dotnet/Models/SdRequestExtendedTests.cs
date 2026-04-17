using Central.Engine.Models;

namespace Central.Tests.Models;

public class SdRequestExtendedTests
{
    // ── StatusColor edge cases ──

    [Fact]
    public void StatusColor_Cancelled_BritishSpelling()
    {
        var req = new SdRequest { Status = "Cancelled" };
        Assert.Equal("#EF4444", req.StatusColor);
    }

    [Fact]
    public void StatusColor_Resolved_Green()
    {
        Assert.Equal("#22C55E", new SdRequest { Status = "Resolved" }.StatusColor);
    }

    [Fact]
    public void StatusColor_EmptyString_Default()
    {
        Assert.Equal("#9CA3AF", new SdRequest { Status = "" }.StatusColor);
    }

    // ── PriorityColor edge cases ──

    [Fact]
    public void PriorityColor_Unknown_Default()
    {
        Assert.Equal("#9CA3AF", new SdRequest { Priority = "Unknown" }.PriorityColor);
    }

    [Fact]
    public void PriorityColor_Empty_Default()
    {
        Assert.Equal("#9CA3AF", new SdRequest { Priority = "" }.PriorityColor);
    }

    // ── IsOverdue edge cases ──

    [Fact]
    public void IsOverdue_CancelledBritish_False()
    {
        var req = new SdRequest { Status = "Cancelled", DueBy = DateTime.UtcNow.AddDays(-5) };
        Assert.False(req.IsOverdue);
    }

    [Fact]
    public void IsOverdue_Resolved_False()
    {
        var req = new SdRequest { Status = "Resolved", DueBy = DateTime.UtcNow.AddDays(-5) };
        Assert.False(req.IsOverdue);
    }

    [Fact]
    public void IsOverdue_Archive_False()
    {
        var req = new SdRequest { Status = "Archive", DueBy = DateTime.UtcNow.AddDays(-5) };
        Assert.False(req.IsOverdue);
    }

    [Fact]
    public void IsOverdue_InProgress_PastDue_True()
    {
        var req = new SdRequest { Status = "In Progress", DueBy = DateTime.UtcNow.AddDays(-1) };
        Assert.True(req.IsOverdue);
    }

    [Fact]
    public void IsOverdue_OnHold_PastDue_True()
    {
        var req = new SdRequest { Status = "On Hold", DueBy = DateTime.UtcNow.AddDays(-1) };
        Assert.True(req.IsOverdue);
    }

    // ── OverdueIcon ──

    [Fact]
    public void OverdueIcon_WhenOverdue_Warning()
    {
        var req = new SdRequest { Status = "Open", DueBy = DateTime.UtcNow.AddDays(-1) };
        Assert.Equal("⚠", req.OverdueIcon);
    }

    [Fact]
    public void OverdueIcon_NotOverdue_Empty()
    {
        var req = new SdRequest { Status = "Open", DueBy = DateTime.UtcNow.AddDays(1) };
        Assert.Equal("", req.OverdueIcon);
    }

    // ── Dirty tracking: all tracked fields ──

    [Fact]
    public void DirtyTracking_GroupNameChange_MarksDirty()
    {
        var req = new SdRequest { GroupName = "IT" };
        req.AcceptChanges();
        req.GroupName = "HR";
        Assert.True(req.IsDirty);
    }

    [Fact]
    public void DirtyTracking_TechnicianChange_MarksDirty()
    {
        var req = new SdRequest { TechnicianName = "John" };
        req.AcceptChanges();
        req.TechnicianName = "Jane";
        Assert.True(req.IsDirty);
    }

    [Fact]
    public void DirtyTracking_CategoryChange_MarksDirty()
    {
        var req = new SdRequest { Category = "Network" };
        req.AcceptChanges();
        req.Category = "Software";
        Assert.True(req.IsDirty);
    }

    [Fact]
    public void DirtyTracking_PriorityChange_MarksDirty()
    {
        var req = new SdRequest { Priority = "Low" };
        req.AcceptChanges();
        req.Priority = "High";
        Assert.True(req.IsDirty);
    }

    [Fact]
    public void DirtyTracking_RevertAll_ClearsDirty()
    {
        var req = new SdRequest { Status = "Open", Priority = "Low", GroupName = "IT", TechnicianName = "J", Category = "HW" };
        req.AcceptChanges();
        req.Status = "Closed";
        req.Priority = "High";
        Assert.True(req.IsDirty);
        req.Status = "Open";
        req.Priority = "Low";
        Assert.False(req.IsDirty);
    }

    // ── IsClosed ──

    [Theory]
    [InlineData("Resolved", true)]
    [InlineData("Closed", true)]
    [InlineData("Open", false)]
    [InlineData("In Progress", false)]
    [InlineData("On Hold", false)]
    [InlineData("Awaiting Response", false)]
    [InlineData("Canceled", false)]
    [InlineData("Cancelled", false)]
    [InlineData("Archive", false)]
    public void IsClosed_AllStatuses(string status, bool expected)
    {
        Assert.Equal(expected, new SdRequest { Status = status }.IsClosed);
    }

    // ── PropertyChanged cascades ──

    [Fact]
    public void PropertyChanged_Priority_AlsoNotifiesPriorityColor()
    {
        var req = new SdRequest();
        var changed = new List<string>();
        req.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        req.Priority = "High";
        Assert.Contains("Priority", changed);
        Assert.Contains("PriorityColor", changed);
    }

    [Fact]
    public void PropertyChanged_DueBy_AlsoNotifiesIsOverdue()
    {
        var req = new SdRequest();
        var changed = new List<string>();
        req.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        req.DueBy = DateTime.UtcNow;
        Assert.Contains("DueBy", changed);
        Assert.Contains("IsOverdue", changed);
    }

    [Fact]
    public void PropertyChanged_IsDirty_AlsoNotifiesRowColor()
    {
        var req = new SdRequest();
        var changed = new List<string>();
        req.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        req.IsDirty = true;
        Assert.Contains("IsDirty", changed);
        Assert.Contains("RowColor", changed);
    }
}
