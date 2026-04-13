using Central.Core.Models;

namespace Central.Tests.Models;

public class RoleRecordExtendedTests
{
    // ── PropertyChanged on all permission booleans ──

    [Fact]
    public void PropertyChanged_DevicesView_Fires()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.DevicesView = true;
        Assert.Equal("DevicesView", changed);
    }

    [Fact]
    public void PropertyChanged_DevicesEdit_Fires()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.DevicesEdit = true;
        Assert.Equal("DevicesEdit", changed);
    }

    [Fact]
    public void PropertyChanged_DevicesDelete_Fires()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.DevicesDelete = true;
        Assert.Equal("DevicesDelete", changed);
    }

    [Fact]
    public void PropertyChanged_SwitchesView_Fires()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.SwitchesView = true;
        Assert.Equal("SwitchesView", changed);
    }

    [Fact]
    public void PropertyChanged_LinksView_Fires()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.LinksView = true;
        Assert.Equal("LinksView", changed);
    }

    [Fact]
    public void PropertyChanged_BgpView_Fires()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.BgpView = true;
        Assert.Equal("BgpView", changed);
    }

    [Fact]
    public void PropertyChanged_BgpSync_Fires()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.BgpSync = true;
        Assert.Equal("BgpSync", changed);
    }

    [Fact]
    public void PropertyChanged_VlansView_Fires()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.VlansView = true;
        Assert.Equal("VlansView", changed);
    }

    [Fact]
    public void PropertyChanged_TasksView_Fires()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.TasksView = true;
        Assert.Equal("TasksView", changed);
    }

    [Fact]
    public void PropertyChanged_ServiceDeskView_Fires()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.ServiceDeskView = true;
        Assert.Equal("ServiceDeskView", changed);
    }

    [Fact]
    public void PropertyChanged_ServiceDeskSync_Fires()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.ServiceDeskSync = true;
        Assert.Equal("ServiceDeskSync", changed);
    }

    [Fact]
    public void PropertyChanged_Description_Fires()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.Description = "Full admin";
        Assert.Equal("Description", changed);
    }

    [Fact]
    public void PropertyChanged_Priority_Fires()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.Priority = 1000;
        Assert.Equal("Priority", changed);
    }

    [Fact]
    public void PropertyChanged_IsSystem_Fires()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.IsSystem = true;
        Assert.Equal("IsSystem", changed);
    }

    [Fact]
    public void PropertyChanged_Id_Fires()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.Id = 99;
        Assert.Equal("Id", changed);
    }

    // ── PermissionSummary counts ──

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    public void PermissionSummary_VariousCounts(int targetCount)
    {
        var r = new RoleRecord();
        var props = new[]
        {
            nameof(RoleRecord.DevicesView), nameof(RoleRecord.DevicesEdit), nameof(RoleRecord.DevicesDelete),
            nameof(RoleRecord.SwitchesView), nameof(RoleRecord.SwitchesEdit), nameof(RoleRecord.SwitchesDelete),
            nameof(RoleRecord.AdminView), nameof(RoleRecord.AdminEdit), nameof(RoleRecord.AdminDelete),
            nameof(RoleRecord.LinksView), nameof(RoleRecord.LinksEdit), nameof(RoleRecord.LinksDelete),
            nameof(RoleRecord.BgpView), nameof(RoleRecord.BgpEdit), nameof(RoleRecord.BgpSync),
            nameof(RoleRecord.VlansView), nameof(RoleRecord.VlansEdit),
            nameof(RoleRecord.TasksView), nameof(RoleRecord.TasksEdit), nameof(RoleRecord.TasksDelete),
            nameof(RoleRecord.ServiceDeskView), nameof(RoleRecord.ServiceDeskEdit), nameof(RoleRecord.ServiceDeskSync),
        };

        for (int i = 0; i < targetCount && i < props.Length; i++)
        {
            var prop = typeof(RoleRecord).GetProperty(props[i]);
            prop?.SetValue(r, true);
        }

        Assert.StartsWith($"{targetCount}/22", r.PermissionSummary);
    }

    // ── RoleUserDetail ──

    [Fact]
    public void RoleUserDetail_CanSetProperties()
    {
        var d = new RoleUserDetail
        {
            Username = "jsmith",
            DisplayName = "John Smith",
            IsActive = true,
            LastLogin = "2026-03-30"
        };
        Assert.Equal("jsmith", d.Username);
        Assert.Equal("John Smith", d.DisplayName);
        Assert.True(d.IsActive);
        Assert.Equal("2026-03-30", d.LastLogin);
    }

    // ── DevicesViewReserved default ──

    [Fact]
    public void DevicesViewReserved_DefaultTrue()
    {
        var r = new RoleRecord();
        Assert.True(r.DevicesViewReserved);
    }

    [Fact]
    public void DevicesViewReserved_PropertyChanged()
    {
        var r = new RoleRecord();
        string? changed = null;
        r.PropertyChanged += (_, e) => changed = e.PropertyName;
        r.DevicesViewReserved = false;
        Assert.Equal("DevicesViewReserved", changed);
    }
}
