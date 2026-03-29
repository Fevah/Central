using Central.Core.Models;

namespace Central.Tests.Models;

public class AppointmentModelTests
{
    [Fact]
    public void Appointment_PropertyChanged()
    {
        var a = new Appointment();
        string? prop = null;
        a.PropertyChanged += (_, e) => prop = e.PropertyName;
        a.Subject = "Meeting";
        Assert.Equal("Subject", prop);
    }

    [Fact]
    public void Appointment_Defaults()
    {
        var a = new Appointment();
        Assert.Equal("", a.Subject);
        Assert.Equal("", a.Description);
        Assert.Equal("", a.Location);
        Assert.False(a.AllDay);
        Assert.Equal(0, a.Status);
        Assert.Null(a.ResourceId);
        Assert.Null(a.TaskId);
    }

    [Fact]
    public void AppointmentResource_Defaults()
    {
        var r = new AppointmentResource();
        Assert.Equal("#3B82F6", r.Color);
        Assert.True(r.IsActive);
        Assert.Equal("", r.DisplayName);
    }

    [Fact]
    public void AppointmentResource_PropertyChanged()
    {
        var r = new AppointmentResource();
        bool fired = false;
        r.PropertyChanged += (_, _) => fired = true;
        r.DisplayName = "John Smith";
        Assert.True(fired);
    }
}
