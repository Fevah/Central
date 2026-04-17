using Central.Engine.Integration;

namespace Central.Tests.Integration;

public class SyncEngineTests
{
    [Fact]
    public void RegisterAgent_AddsToAgentTypes()
    {
        var engine = new SyncEngine();
        engine.RegisterAgent(new TestAgent("test_agent"));
        Assert.Contains("test_agent", engine.GetAgentTypes());
    }

    [Fact]
    public void RegisterConverter_AddsToConverterTypes()
    {
        var engine = new SyncEngine();
        engine.RegisterConverter(new DirectConverter());
        engine.RegisterConverter(new ConstantConverter());
        Assert.Contains("direct", engine.GetConverterTypes());
        Assert.Contains("constant", engine.GetConverterTypes());
    }

    [Fact]
    public async Task ExecuteSync_NoAgent_ReturnsFailed()
    {
        var engine = new SyncEngine();
        var config = new SyncConfig { Id = 1, Name = "Test", AgentType = "nonexistent" };
        var result = await engine.ExecuteSyncAsync(config, new(), new(), (_, _, _) => Task.CompletedTask);

        Assert.Equal("failed", result.Status);
        Assert.Contains("No agent registered", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteSync_WithAgent_ReadsAndMaps()
    {
        var engine = new SyncEngine();
        var agent = new TestAgent("test_agent");
        agent.TestRecords.Add(new Dictionary<string, object?> { ["name"] = "John", ["email"] = "john@example.com" });
        agent.TestRecords.Add(new Dictionary<string, object?> { ["name"] = "Jane", ["email"] = "jane@example.com" });
        engine.RegisterAgent(agent);
        engine.RegisterConverter(new DirectConverter());

        var config = new SyncConfig { Id = 1, Name = "Test", AgentType = "test_agent", ConfigJson = "{}" };
        var entityMaps = new List<SyncEntityMap>
        {
            new() { Id = 1, SyncConfigId = 1, SourceEntity = "users", TargetTable = "test_users", IsEnabled = true, UpsertKey = "email" }
        };
        var fieldMaps = new List<SyncFieldMap>
        {
            new() { Id = 1, EntityMapId = 1, SourceField = "name", TargetColumn = "display_name", ConverterType = "direct" },
            new() { Id = 2, EntityMapId = 1, SourceField = "email", TargetColumn = "email", ConverterType = "direct" }
        };

        var upserted = new List<(string table, Dictionary<string, object?> fields)>();
        var result = await engine.ExecuteSyncAsync(config, entityMaps, fieldMaps,
            (table, fields, key) => { upserted.Add((table, fields)); return Task.CompletedTask; });

        Assert.Equal("success", result.Status);
        Assert.Equal(2, result.RecordsRead);
        Assert.Equal(2, upserted.Count);
        Assert.Equal("John", upserted[0].fields["display_name"]);
        Assert.Equal("jane@example.com", upserted[1].fields["email"]);
    }

    [Fact]
    public async Task ExecuteSync_DisabledEntity_Skipped()
    {
        var engine = new SyncEngine();
        engine.RegisterAgent(new TestAgent("test_agent"));
        engine.RegisterConverter(new DirectConverter());

        var config = new SyncConfig { Id = 1, AgentType = "test_agent", ConfigJson = "{}" };
        var entityMaps = new List<SyncEntityMap>
        {
            new() { Id = 1, SyncConfigId = 1, SourceEntity = "users", TargetTable = "test", IsEnabled = false }
        };

        var result = await engine.ExecuteSyncAsync(config, entityMaps, new(), (_, _, _) => Task.CompletedTask);
        Assert.Equal("success", result.Status);
        Assert.Equal(0, result.RecordsRead);
    }

    [Fact]
    public async Task ExecuteSync_ConverterApplied()
    {
        var engine = new SyncEngine();
        var agent = new TestAgent("test_agent");
        agent.TestRecords.Add(new Dictionary<string, object?> { ["first"] = "John", ["last"] = "Smith" });
        engine.RegisterAgent(agent);
        engine.RegisterConverter(new DirectConverter());
        engine.RegisterConverter(new CombineConverter());

        var config = new SyncConfig { Id = 1, AgentType = "test_agent", ConfigJson = "{}" };
        var entityMaps = new List<SyncEntityMap>
        {
            new() { Id = 1, SyncConfigId = 1, SourceEntity = "users", TargetTable = "test", IsEnabled = true, UpsertKey = "name" }
        };
        var fieldMaps = new List<SyncFieldMap>
        {
            new() { Id = 1, EntityMapId = 1, SourceField = "first", TargetColumn = "name", ConverterType = "combine", ConverterExpr = "{first} {last}" }
        };

        var upserted = new List<Dictionary<string, object?>>();
        await engine.ExecuteSyncAsync(config, entityMaps, fieldMaps,
            (_, fields, _) => { upserted.Add(fields); return Task.CompletedTask; });

        Assert.Single(upserted);
        Assert.Equal("John Smith", upserted[0]["name"]);
    }

    // ── Test Agent ──

    private class TestAgent : IIntegrationAgent
    {
        public string AgentType { get; }
        public string DisplayName => "Test Agent";
        public List<Dictionary<string, object?>> TestRecords { get; } = new();

        public TestAgent(string type) => AgentType = type;

        public Task InitializeAsync(Dictionary<string, string> config) => Task.CompletedTask;
        public Task<AgentTestResult> TestConnectionAsync() => Task.FromResult(AgentTestResult.Ok());
        public Task<AgentReadResult> ReadAsync(ReadRequest request) =>
            Task.FromResult(new AgentReadResult { Success = true, Records = TestRecords, TotalAvailable = TestRecords.Count });
        public Task<AgentWriteResult> WriteAsync(WriteRequest request) => Task.FromResult(AgentWriteResult.Ok());
        public Task<AgentWriteResult> DeleteAsync(string entityName, string externalId) => Task.FromResult(AgentWriteResult.Ok());
        public Task<List<string>> GetEntityNamesAsync() => Task.FromResult(new List<string> { "users" });
        public Task<List<AgentFieldInfo>> GetFieldsAsync(string entityName) => Task.FromResult(new List<AgentFieldInfo>());
    }
}
