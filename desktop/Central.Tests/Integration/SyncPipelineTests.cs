using Central.Core.Integration;

namespace Central.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the sync engine pipeline.
/// Tests the full flow: Agent → Read → Field Converters → Mapped Record → Upsert.
/// </summary>
public class SyncPipelineTests
{
    private SyncEngine CreateEngine()
    {
        var engine = new SyncEngine();
        engine.RegisterAgent(new InMemoryAgent());
        engine.RegisterConverter(new DirectConverter());
        engine.RegisterConverter(new ConstantConverter());
        engine.RegisterConverter(new CombineConverter());
        engine.RegisterConverter(new SplitConverter());
        engine.RegisterConverter(new DateFormatConverter());
        engine.RegisterConverter(new ExpressionConverter());
        return engine;
    }

    [Fact]
    public async Task FullPipeline_DirectMapping()
    {
        var engine = CreateEngine();
        var agent = new InMemoryAgent();
        engine.RegisterAgent(agent);
        agent.AddRecord(new() { ["id"] = 1, ["name"] = "Device A", ["building"] = "MEP-91" });
        agent.AddRecord(new() { ["id"] = 2, ["name"] = "Device B", ["building"] = "MEP-92" });

        var config = new SyncConfig { Id = 1, AgentType = "in_memory", ConfigJson = "{}" };
        var entityMaps = new List<SyncEntityMap>
        {
            new() { Id = 1, SyncConfigId = 1, SourceEntity = "devices", TargetTable = "test_devices", IsEnabled = true, UpsertKey = "device_id" }
        };
        var fieldMaps = new List<SyncFieldMap>
        {
            new() { EntityMapId = 1, SourceField = "id", TargetColumn = "device_id", ConverterType = "direct" },
            new() { EntityMapId = 1, SourceField = "name", TargetColumn = "device_name", ConverterType = "direct" },
            new() { EntityMapId = 1, SourceField = "building", TargetColumn = "site", ConverterType = "direct" }
        };

        var upserted = new List<(string table, Dictionary<string, object?> fields, string key)>();
        var result = await engine.ExecuteSyncAsync(config, entityMaps, fieldMaps,
            (table, fields, key) => { upserted.Add((table, fields, key)); return Task.CompletedTask; });

        Assert.Equal("success", result.Status);
        Assert.Equal(2, result.RecordsRead);
        Assert.Equal(2, upserted.Count);
        Assert.Equal("Device A", upserted[0].fields["device_name"]);
        Assert.Equal("MEP-92", upserted[1].fields["site"]);
        Assert.Equal("test_devices", upserted[0].table);
    }

    [Fact]
    public async Task FullPipeline_WithConverters()
    {
        var engine = CreateEngine();
        var agent = new InMemoryAgent();
        engine.RegisterAgent(agent);
        agent.AddRecord(new() { ["first_name"] = "John", ["last_name"] = "Smith", ["role"] = "admin" });

        var config = new SyncConfig { Id = 1, AgentType = "in_memory", ConfigJson = "{}" };
        var entityMaps = new List<SyncEntityMap>
        {
            new() { Id = 1, SyncConfigId = 1, SourceEntity = "users", TargetTable = "test_users", IsEnabled = true, UpsertKey = "username" }
        };
        var fieldMaps = new List<SyncFieldMap>
        {
            new() { EntityMapId = 1, SourceField = "first_name", TargetColumn = "display_name", ConverterType = "combine", ConverterExpr = "{first_name} {last_name}" },
            new() { EntityMapId = 1, SourceField = "first_name", TargetColumn = "username", ConverterType = "expression", ConverterExpr = "lower:$first_name" },
            new() { EntityMapId = 1, SourceField = "role", TargetColumn = "user_type", ConverterType = "constant", ConverterExpr = "Standard" }
        };

        var upserted = new List<Dictionary<string, object?>>();
        var result = await engine.ExecuteSyncAsync(config, entityMaps, fieldMaps,
            (_, fields, _) => { upserted.Add(fields); return Task.CompletedTask; });

        Assert.Equal("success", result.Status);
        Assert.Single(upserted);
        Assert.Equal("John Smith", upserted[0]["display_name"]);
        Assert.Equal("john", upserted[0]["username"]);
        Assert.Equal("Standard", upserted[0]["user_type"]);
    }

    [Fact]
    public async Task FullPipeline_UpsertFailure_CountsAsFailed()
    {
        var engine = CreateEngine();
        var agent = new InMemoryAgent();
        engine.RegisterAgent(agent);
        agent.AddRecord(new() { ["id"] = 1, ["name"] = "Good" });
        agent.AddRecord(new() { ["id"] = 2, ["name"] = "Bad" });

        var config = new SyncConfig { Id = 1, AgentType = "in_memory", ConfigJson = "{}" };
        var entityMaps = new List<SyncEntityMap>
        {
            new() { Id = 1, SyncConfigId = 1, SourceEntity = "items", TargetTable = "test", IsEnabled = true, UpsertKey = "id" }
        };
        var fieldMaps = new List<SyncFieldMap>
        {
            new() { EntityMapId = 1, SourceField = "id", TargetColumn = "id", ConverterType = "direct" },
            new() { EntityMapId = 1, SourceField = "name", TargetColumn = "name", ConverterType = "direct" }
        };

        int failCount = 0;
        var result = await engine.ExecuteSyncAsync(config, entityMaps, fieldMaps,
            (_, _, _) =>
            {
                failCount++;
                if (failCount <= 3) throw new Exception("DB error"); // fail first 3 calls (record 1 retries exhaust)
                return Task.CompletedTask;
            });

        Assert.Equal(2, result.RecordsRead);
        Assert.True(result.RecordsFailed >= 1); // at least one record failed after retries
    }

    [Fact]
    public async Task FullPipeline_EmptySource_SuccessZeroRecords()
    {
        var engine = CreateEngine();
        engine.RegisterAgent(new InMemoryAgent()); // empty

        var config = new SyncConfig { Id = 1, AgentType = "in_memory", ConfigJson = "{}" };
        var entityMaps = new List<SyncEntityMap>
        {
            new() { Id = 1, SyncConfigId = 1, SourceEntity = "items", TargetTable = "test", IsEnabled = true, UpsertKey = "id" }
        };

        var result = await engine.ExecuteSyncAsync(config, entityMaps, new List<SyncFieldMap>(),
            (_, _, _) => Task.CompletedTask);

        Assert.Equal("success", result.Status);
        Assert.Equal(0, result.RecordsRead);
    }

    [Fact]
    public async Task FullPipeline_MultipleEntities()
    {
        var engine = CreateEngine();
        var agent = new InMemoryAgent();
        engine.RegisterAgent(agent);
        agent.AddRecord(new() { ["id"] = 1, ["name"] = "Item1" });

        var config = new SyncConfig { Id = 1, AgentType = "in_memory", ConfigJson = "{}", MaxConcurrent = 2 };
        var entityMaps = new List<SyncEntityMap>
        {
            new() { Id = 1, SyncConfigId = 1, SourceEntity = "items", TargetTable = "table_a", IsEnabled = true, UpsertKey = "id" },
            new() { Id = 2, SyncConfigId = 1, SourceEntity = "items", TargetTable = "table_b", IsEnabled = true, UpsertKey = "id" }
        };
        var fieldMaps = new List<SyncFieldMap>
        {
            new() { EntityMapId = 1, SourceField = "id", TargetColumn = "id", ConverterType = "direct" },
            new() { EntityMapId = 1, SourceField = "name", TargetColumn = "name", ConverterType = "direct" },
            new() { EntityMapId = 2, SourceField = "id", TargetColumn = "id", ConverterType = "direct" },
            new() { EntityMapId = 2, SourceField = "name", TargetColumn = "name", ConverterType = "direct" }
        };

        var tables = new List<string>();
        var result = await engine.ExecuteSyncAsync(config, entityMaps, fieldMaps,
            (table, _, _) => { tables.Add(table); return Task.CompletedTask; });

        Assert.Equal("success", result.Status);
        Assert.Contains("table_a", tables);
        Assert.Contains("table_b", tables);
    }

    /// <summary>Test agent that stores records in memory.</summary>
    private class InMemoryAgent : IIntegrationAgent
    {
        private readonly List<Dictionary<string, object?>> _records = new();
        public string AgentType => "in_memory";
        public string DisplayName => "In-Memory Test Agent";
        public void AddRecord(Dictionary<string, object?> record) => _records.Add(record);
        public Task InitializeAsync(Dictionary<string, string> config) => Task.CompletedTask;
        public Task<AgentTestResult> TestConnectionAsync() => Task.FromResult(AgentTestResult.Ok());
        public Task<AgentReadResult> ReadAsync(ReadRequest request) =>
            Task.FromResult(new AgentReadResult { Success = true, Records = new(_records), TotalAvailable = _records.Count });
        public Task<AgentWriteResult> WriteAsync(WriteRequest request) => Task.FromResult(AgentWriteResult.Ok());
        public Task<AgentWriteResult> DeleteAsync(string entityName, string externalId) => Task.FromResult(AgentWriteResult.Ok());
        public Task<List<string>> GetEntityNamesAsync() => Task.FromResult(new List<string> { "items" });
        public Task<List<AgentFieldInfo>> GetFieldsAsync(string entityName) => Task.FromResult(new List<AgentFieldInfo>());
    }
}
