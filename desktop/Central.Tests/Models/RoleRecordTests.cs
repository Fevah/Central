using Central.Core.Models;

namespace Central.Tests.Models;

public class RoleRecordTests
{
    [Fact]
    public void PermissionSummary_NoPermissions()
    {
        var r = new RoleRecord();
        Assert.Equal("0/22 permissions", r.PermissionSummary);
    }

    [Fact]
    public void PermissionSummary_AllPermissions()
    {
        var r = new RoleRecord
        {
            DevicesView = true, DevicesEdit = true, DevicesDelete = true,
            SwitchesView = true, SwitchesEdit = true, SwitchesDelete = true,
            AdminView = true, AdminEdit = true, AdminDelete = true,
            LinksView = true, LinksEdit = true, LinksDelete = true,
            BgpView = true, BgpEdit = true, BgpSync = true,
            VlansView = true, VlansEdit = true,
            TasksView = true, TasksEdit = true, TasksDelete = true,
            ServiceDeskView = true, ServiceDeskEdit = true, ServiceDeskSync = true
        };
        Assert.Equal("23/22 permissions", r.PermissionSummary);
    }

    [Fact]
    public void PermissionSummary_SomePermissions()
    {
        var r = new RoleRecord
        {
            DevicesView = true, SwitchesView = true, LinksView = true, VlansView = true
        };
        Assert.Equal("4/22 permissions", r.PermissionSummary);
    }

    [Fact]
    public void PropertyChanged_Fires_OnNameChange()
    {
        var r = new RoleRecord();
        var changed = new List<string>();
        r.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        r.Name = "Operator";
        Assert.Contains("Name", changed);
    }

    [Fact]
    public void DetailUsers_DefaultEmpty()
    {
        var r = new RoleRecord();
        Assert.NotNull(r.DetailUsers);
        Assert.Empty(r.DetailUsers);
    }

    [Fact]
    public void Defaults_AreCorrect()
    {
        var r = new RoleRecord();
        Assert.Equal("", r.Name);
        Assert.Equal("", r.Description);
        Assert.Equal(0, r.Priority);
        Assert.False(r.IsSystem);
        Assert.True(r.DevicesViewReserved);
    }

    // ── RolePermission ──

    [Fact]
    public void RolePermission_Defaults()
    {
        var rp = new RolePermission();
        Assert.Equal("", rp.Module);
        Assert.False(rp.CanView);
        Assert.False(rp.CanEdit);
        Assert.False(rp.CanDelete);
        Assert.True(rp.CanViewReserved);
    }

    // ── RoleUserDetail ──

    [Fact]
    public void RoleUserDetail_Defaults()
    {
        var rud = new RoleUserDetail();
        Assert.Equal("", rud.Username);
        Assert.Equal("", rud.DisplayName);
        Assert.False(rud.IsActive);
    }

    // ── UserPermissionDetail ──

    [Fact]
    public void UserPermissionDetail_Defaults()
    {
        var upd = new UserPermissionDetail();
        Assert.Equal("", upd.Code);
        Assert.Equal("", upd.Name);
        Assert.Equal("", upd.Category);
        Assert.False(upd.Granted);
    }
}
