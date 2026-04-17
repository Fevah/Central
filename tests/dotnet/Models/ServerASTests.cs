using Central.Core.Models;

namespace Central.Tests.Models;

public class ServerASTests
{
    // ── Defaults ──

    [Fact]
    public void Defaults_AreCorrect()
    {
        var s = new ServerAS();
        Assert.Equal(0, s.Id);
        Assert.Equal("", s.Building);
        Assert.Equal("", s.ServerAsn);
        Assert.Equal("Active", s.Status); // default = "Active"
    }

    // ── PropertyChanged ──

    [Fact]
    public void PropertyChanged_Id_Fires()
    {
        var s = new ServerAS();
        string? changed = null;
        s.PropertyChanged += (_, e) => changed = e.PropertyName;
        s.Id = 42;
        Assert.Equal("Id", changed);
    }

    [Fact]
    public void PropertyChanged_Building_Fires()
    {
        var s = new ServerAS();
        string? changed = null;
        s.PropertyChanged += (_, e) => changed = e.PropertyName;
        s.Building = "MEP-91";
        Assert.Equal("Building", changed);
    }

    [Fact]
    public void PropertyChanged_ServerAsn_Fires()
    {
        var s = new ServerAS();
        string? changed = null;
        s.PropertyChanged += (_, e) => changed = e.PropertyName;
        s.ServerAsn = "65112";
        Assert.Equal("ServerAsn", changed);
    }

    [Fact]
    public void PropertyChanged_Status_Fires()
    {
        var s = new ServerAS();
        string? changed = null;
        s.PropertyChanged += (_, e) => changed = e.PropertyName;
        s.Status = "Inactive";
        Assert.Equal("Status", changed);
    }

    [Fact]
    public void AllProperties_FirePropertyChanged()
    {
        var s = new ServerAS();
        var changed = new List<string>();
        s.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        s.Id = 1;
        s.Building = "MEP-92";
        s.ServerAsn = "65121";
        s.Status = "Decommissioned";

        Assert.Equal(4, changed.Count);
        Assert.Contains("Id", changed);
        Assert.Contains("Building", changed);
        Assert.Contains("ServerAsn", changed);
        Assert.Contains("Status", changed);
    }

    [Fact]
    public void Status_CanBeChanged()
    {
        var s = new ServerAS { Status = "Active" };
        s.Status = "Inactive";
        Assert.Equal("Inactive", s.Status);
    }
}
