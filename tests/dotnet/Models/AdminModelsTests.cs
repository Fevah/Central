using Central.Engine.Models;

namespace Central.Tests.Models;

public class AdminModelsTests
{
    // ── ReferenceConfig ──

    [Fact]
    public void ReferenceConfig_SampleOutput_FormatsCorrectly()
    {
        var config = new ReferenceConfig { Prefix = "DEV-", Suffix = "", PadLength = 6, NextValue = 42 };
        Assert.Equal("DEV-000042", config.SampleOutput);
    }

    [Fact]
    public void ReferenceConfig_SampleOutput_WithSuffix()
    {
        var config = new ReferenceConfig { Prefix = "TKT-", Suffix = "-UK", PadLength = 4, NextValue = 7 };
        Assert.Equal("TKT-0007-UK", config.SampleOutput);
    }

    [Fact]
    public void ReferenceConfig_SampleOutput_UpdatesOnPropertyChange()
    {
        var config = new ReferenceConfig { Prefix = "A-", PadLength = 3, NextValue = 1 };
        Assert.Equal("A-001", config.SampleOutput);

        bool sampleChanged = false;
        config.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ReferenceConfig.SampleOutput)) sampleChanged = true;
        };

        config.Prefix = "B-";
        Assert.True(sampleChanged);
        Assert.Equal("B-001", config.SampleOutput);
    }

    // ── ContainerInfo ──

    [Fact]
    public void ContainerInfo_StateColor_Running()
    {
        var info = new ContainerInfo { State = "running" };
        Assert.Equal("#22C55E", info.StateColor);
        Assert.True(info.IsRunning);
    }

    [Fact]
    public void ContainerInfo_StateColor_Exited()
    {
        var info = new ContainerInfo { State = "exited" };
        Assert.Equal("#EF4444", info.StateColor);
        Assert.False(info.IsRunning);
    }

    [Fact]
    public void ContainerInfo_StateColor_Unknown()
    {
        var info = new ContainerInfo { State = "unknown" };
        Assert.Equal("#6B7280", info.StateColor);
    }

    // ── MigrationRecord ──

    [Fact]
    public void MigrationRecord_Applied_GreenStatus()
    {
        var rec = new MigrationRecord { IsApplied = true };
        Assert.Equal("#22C55E", rec.StatusColor);
        Assert.Equal("Applied", rec.StatusText);
    }

    [Fact]
    public void MigrationRecord_Pending_AmberStatus()
    {
        var rec = new MigrationRecord { IsApplied = false };
        Assert.Equal("#F59E0B", rec.StatusColor);
        Assert.Equal("Pending", rec.StatusText);
    }

    // ── BackupRecord ──

    [Fact]
    public void BackupRecord_FileSizeDisplay_Bytes()
    {
        var rec = new BackupRecord { FileSizeBytes = 512 };
        Assert.Equal("512 B", rec.FileSizeDisplay);
    }

    [Fact]
    public void BackupRecord_FileSizeDisplay_KB()
    {
        var rec = new BackupRecord { FileSizeBytes = 5120 };
        Assert.Equal("5.0 KB", rec.FileSizeDisplay);
    }

    [Fact]
    public void BackupRecord_FileSizeDisplay_MB()
    {
        var rec = new BackupRecord { FileSizeBytes = 5_242_880 };
        Assert.Equal("5.0 MB", rec.FileSizeDisplay);
    }

    [Fact]
    public void BackupRecord_FileSizeDisplay_GB()
    {
        var rec = new BackupRecord { FileSizeBytes = 2_147_483_648 };
        Assert.Equal("2.00 GB", rec.FileSizeDisplay);
    }

    [Fact]
    public void BackupRecord_FileSizeDisplay_Null()
    {
        var rec = new BackupRecord { FileSizeBytes = null };
        Assert.Equal("", rec.FileSizeDisplay);
    }

    [Fact]
    public void BackupRecord_StatusColor()
    {
        Assert.Equal("#22C55E", new BackupRecord { Status = "success" }.StatusColor);
        Assert.Equal("#3B82F6", new BackupRecord { Status = "running" }.StatusColor);
        Assert.Equal("#EF4444", new BackupRecord { Status = "failed" }.StatusColor);
    }

    // ── PanelCustomization Models ──

    [Fact]
    public void GridSettings_Defaults()
    {
        var gs = new GridSettings();
        Assert.Equal(25, gs.RowHeight);
        Assert.True(gs.UseAlternatingRows);
        Assert.True(gs.ShowSummaryFooter);
        Assert.False(gs.ShowGroupPanel);
        Assert.True(gs.ShowAutoFilterRow);
    }

    [Fact]
    public void LinkRule_Defaults()
    {
        var lr = new LinkRule();
        Assert.Equal("", lr.SourcePanel);
        Assert.Equal("", lr.TargetPanel);
        Assert.True(lr.FilterOnSelect);
    }

    // ── Location Models ──

    [Fact]
    public void Country_PropertyChanged()
    {
        var c = new Country();
        bool fired = false;
        c.PropertyChanged += (_, _) => fired = true;
        c.Name = "United Kingdom";
        Assert.True(fired);
    }

    [Fact]
    public void Region_PropertyChanged()
    {
        var r = new Region();
        bool fired = false;
        r.PropertyChanged += (_, _) => fired = true;
        r.Code = "ENG";
        Assert.True(fired);
    }

    // ── Appointment ──

    [Fact]
    public void Appointment_PropertyChanged()
    {
        var a = new Appointment();
        string? changedProp = null;
        a.PropertyChanged += (_, e) => changedProp = e.PropertyName;
        a.Subject = "Team Meeting";
        Assert.Equal("Subject", changedProp);
    }

    [Fact]
    public void AppointmentResource_Defaults()
    {
        var r = new AppointmentResource();
        Assert.Equal("#3B82F6", r.Color);
        Assert.True(r.IsActive);
    }
}
