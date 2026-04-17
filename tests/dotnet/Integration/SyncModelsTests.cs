using Central.Engine.Integration;

namespace Central.Tests.Integration;

public class SyncModelsTests
{
    [Fact]
    public void SyncConfig_StatusColor_Success()
    {
        Assert.Equal("#22C55E", new SyncConfig { LastSyncStatus = "success" }.StatusColor);
    }

    [Fact]
    public void SyncConfig_StatusColor_Failed()
    {
        Assert.Equal("#EF4444", new SyncConfig { LastSyncStatus = "failed" }.StatusColor);
    }

    [Fact]
    public void SyncConfig_StatusColor_Running()
    {
        Assert.Equal("#3B82F6", new SyncConfig { LastSyncStatus = "running" }.StatusColor);
    }

    [Fact]
    public void SyncConfig_StatusColor_Partial()
    {
        Assert.Equal("#F59E0B", new SyncConfig { LastSyncStatus = "partial" }.StatusColor);
    }

    [Fact]
    public void SyncConfig_StatusColor_Never()
    {
        Assert.Equal("#6B7280", new SyncConfig { LastSyncStatus = "never" }.StatusColor);
    }

    [Fact]
    public void SyncLogEntry_StatusColor()
    {
        Assert.Equal("#22C55E", new SyncLogEntry { Status = "success" }.StatusColor);
        Assert.Equal("#EF4444", new SyncLogEntry { Status = "failed" }.StatusColor);
        Assert.Equal("#3B82F6", new SyncLogEntry { Status = "running" }.StatusColor);
    }

    [Fact]
    public void SyncEntityMap_PropertyChanged()
    {
        var map = new SyncEntityMap();
        bool fired = false;
        map.PropertyChanged += (_, _) => fired = true;
        map.SourceEntity = "requests";
        Assert.True(fired);
    }

    [Fact]
    public void SyncFieldMap_PropertyChanged()
    {
        var map = new SyncFieldMap();
        bool fired = false;
        map.PropertyChanged += (_, _) => fired = true;
        map.ConverterType = "expression";
        Assert.True(fired);
    }

    [Fact]
    public void SyncConfig_PropertyChanged()
    {
        var cfg = new SyncConfig();
        string? changed = null;
        cfg.PropertyChanged += (_, e) => changed = e.PropertyName;
        cfg.Name = "Test Integration";
        Assert.Equal("Name", changed);
    }
}
