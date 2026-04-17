using Central.Engine.Models;

namespace Central.Tests.Models;

public class AppointmentExtendedTests
{
    // ── Appointment PropertyChanged on all properties ──

    [Fact]
    public void PropertyChanged_Id_Fires()
    {
        var a = new Appointment();
        string? changed = null;
        a.PropertyChanged += (_, e) => changed = e.PropertyName;
        a.Id = 42;
        Assert.Equal("Id", changed);
    }

    [Fact]
    public void PropertyChanged_Description_Fires()
    {
        var a = new Appointment();
        string? changed = null;
        a.PropertyChanged += (_, e) => changed = e.PropertyName;
        a.Description = "Review sprint";
        Assert.Equal("Description", changed);
    }

    [Fact]
    public void PropertyChanged_StartTime_Fires()
    {
        var a = new Appointment();
        string? changed = null;
        a.PropertyChanged += (_, e) => changed = e.PropertyName;
        a.StartTime = new DateTime(2026, 4, 1, 9, 0, 0);
        Assert.Equal("StartTime", changed);
    }

    [Fact]
    public void PropertyChanged_EndTime_Fires()
    {
        var a = new Appointment();
        string? changed = null;
        a.PropertyChanged += (_, e) => changed = e.PropertyName;
        a.EndTime = new DateTime(2026, 4, 1, 10, 0, 0);
        Assert.Equal("EndTime", changed);
    }

    [Fact]
    public void PropertyChanged_AllDay_Fires()
    {
        var a = new Appointment();
        string? changed = null;
        a.PropertyChanged += (_, e) => changed = e.PropertyName;
        a.AllDay = true;
        Assert.Equal("AllDay", changed);
    }

    [Fact]
    public void PropertyChanged_Location_Fires()
    {
        var a = new Appointment();
        string? changed = null;
        a.PropertyChanged += (_, e) => changed = e.PropertyName;
        a.Location = "Meeting Room 1";
        Assert.Equal("Location", changed);
    }

    [Fact]
    public void PropertyChanged_ResourceId_Fires()
    {
        var a = new Appointment();
        string? changed = null;
        a.PropertyChanged += (_, e) => changed = e.PropertyName;
        a.ResourceId = 5;
        Assert.Equal("ResourceId", changed);
    }

    [Fact]
    public void PropertyChanged_Status_Fires()
    {
        var a = new Appointment();
        string? changed = null;
        a.PropertyChanged += (_, e) => changed = e.PropertyName;
        a.Status = 1;
        Assert.Equal("Status", changed);
    }

    [Fact]
    public void PropertyChanged_Label_Fires()
    {
        var a = new Appointment();
        string? changed = null;
        a.PropertyChanged += (_, e) => changed = e.PropertyName;
        a.Label = 2;
        Assert.Equal("Label", changed);
    }

    [Fact]
    public void PropertyChanged_RecurrenceInfo_Fires()
    {
        var a = new Appointment();
        string? changed = null;
        a.PropertyChanged += (_, e) => changed = e.PropertyName;
        a.RecurrenceInfo = "<xml>recurrence</xml>";
        Assert.Equal("RecurrenceInfo", changed);
    }

    [Fact]
    public void PropertyChanged_TaskId_Fires()
    {
        var a = new Appointment();
        string? changed = null;
        a.PropertyChanged += (_, e) => changed = e.PropertyName;
        a.TaskId = 100;
        Assert.Equal("TaskId", changed);
    }

    [Fact]
    public void PropertyChanged_TicketId_Fires()
    {
        var a = new Appointment();
        string? changed = null;
        a.PropertyChanged += (_, e) => changed = e.PropertyName;
        a.TicketId = 12345L;
        Assert.Equal("TicketId", changed);
    }

    [Fact]
    public void PropertyChanged_CreatedBy_Fires()
    {
        var a = new Appointment();
        string? changed = null;
        a.PropertyChanged += (_, e) => changed = e.PropertyName;
        a.CreatedBy = 1;
        Assert.Equal("CreatedBy", changed);
    }

    [Fact]
    public void PropertyChanged_CreatedAt_Fires()
    {
        var a = new Appointment();
        string? changed = null;
        a.PropertyChanged += (_, e) => changed = e.PropertyName;
        a.CreatedAt = DateTime.UtcNow;
        Assert.Equal("CreatedAt", changed);
    }

    // ── Nullable fields ──

    [Fact]
    public void ResourceId_CanBeNull()
    {
        var a = new Appointment { ResourceId = 5 };
        a.ResourceId = null;
        Assert.Null(a.ResourceId);
    }

    [Fact]
    public void TaskId_CanBeNull()
    {
        var a = new Appointment { TaskId = 10 };
        a.TaskId = null;
        Assert.Null(a.TaskId);
    }

    [Fact]
    public void TicketId_CanBeNull()
    {
        var a = new Appointment { TicketId = 100 };
        a.TicketId = null;
        Assert.Null(a.TicketId);
    }

    [Fact]
    public void CreatedBy_CanBeNull()
    {
        var a = new Appointment { CreatedBy = 1 };
        a.CreatedBy = null;
        Assert.Null(a.CreatedBy);
    }

    // ── AppointmentResource extended ──

    [Fact]
    public void AppointmentResource_PropertyChanged_AllProperties()
    {
        var r = new AppointmentResource();
        var changed = new List<string>();
        r.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        r.Id = 1;
        r.UserId = 5;
        r.DisplayName = "John";
        r.Color = "#FF0000";
        r.IsActive = false;

        Assert.Equal(5, changed.Count);
        Assert.Contains("Id", changed);
        Assert.Contains("UserId", changed);
        Assert.Contains("DisplayName", changed);
        Assert.Contains("Color", changed);
        Assert.Contains("IsActive", changed);
    }
}
