using Central.Core.Models;

namespace Central.Tests.Models;

public class ContainerInfoExtendedTests
{
    // ── PropertyChanged all properties ──

    [Fact]
    public void PropertyChanged_Status_Fires()
    {
        var c = new ContainerInfo();
        string? changed = null;
        c.PropertyChanged += (_, e) => changed = e.PropertyName;
        c.Status = "Up 5 minutes";
        Assert.Equal("Status", changed);
    }

    [Fact]
    public void PropertyChanged_Created_Fires()
    {
        var c = new ContainerInfo();
        string? changed = null;
        c.PropertyChanged += (_, e) => changed = e.PropertyName;
        c.Created = "2026-03-30";
        Assert.Equal("Created", changed);
    }

    [Fact]
    public void PropertyChanged_Ports_Fires()
    {
        var c = new ContainerInfo();
        string? changed = null;
        c.PropertyChanged += (_, e) => changed = e.PropertyName;
        c.Ports = "0.0.0.0:5432->5432/tcp";
        Assert.Equal("Ports", changed);
    }

    [Fact]
    public void PropertyChanged_CpuPercent_Fires()
    {
        var c = new ContainerInfo();
        string? changed = null;
        c.PropertyChanged += (_, e) => changed = e.PropertyName;
        c.CpuPercent = "1.5%";
        Assert.Equal("CpuPercent", changed);
    }

    [Fact]
    public void PropertyChanged_MemUsage_Fires()
    {
        var c = new ContainerInfo();
        string? changed = null;
        c.PropertyChanged += (_, e) => changed = e.PropertyName;
        c.MemUsage = "128MB / 4GB";
        Assert.Equal("MemUsage", changed);
    }

    // ── StateColor ──

    [Theory]
    [InlineData("running", "#22C55E")]
    [InlineData("exited", "#EF4444")]
    [InlineData("paused", "#F59E0B")]
    [InlineData("created", "#6B7280")]
    [InlineData("removing", "#6B7280")]
    [InlineData("", "#6B7280")]
    [InlineData("unknown", "#6B7280")]
    public void StateColor_AllCases(string state, string expected)
    {
        var c = new ContainerInfo { State = state };
        Assert.Equal(expected, c.StateColor);
    }

    // ── IsRunning ──

    [Theory]
    [InlineData("running", true)]
    [InlineData("exited", false)]
    [InlineData("paused", false)]
    [InlineData("created", false)]
    [InlineData("", false)]
    public void IsRunning_AllCases(string state, bool expected)
    {
        var c = new ContainerInfo { State = state };
        Assert.Equal(expected, c.IsRunning);
    }

    // ── Defaults ──

    [Fact]
    public void Defaults_AllEmpty()
    {
        var c = new ContainerInfo();
        Assert.Equal("", c.Name);
        Assert.Equal("", c.Image);
        Assert.Equal("", c.Status);
        Assert.Equal("", c.State);
        Assert.Equal("", c.Created);
        Assert.Equal("", c.Ports);
        Assert.Equal("", c.CpuPercent);
        Assert.Equal("", c.MemUsage);
    }

    // ── Full scenario ──

    [Fact]
    public void FullScenario_PostgresContainer()
    {
        var c = new ContainerInfo
        {
            Name = "central-db",
            Image = "postgres:18.3",
            Status = "Up 5 hours",
            State = "running",
            Created = "2026-03-30T08:00:00Z",
            Ports = "0.0.0.0:5432->5432/tcp",
            CpuPercent = "0.5%",
            MemUsage = "64MB / 4GB"
        };
        Assert.Equal("#22C55E", c.StateColor);
        Assert.True(c.IsRunning);
        Assert.Equal("central-db", c.Name);
        Assert.Contains("postgres", c.Image);
    }
}
