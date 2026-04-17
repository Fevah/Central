using Central.Engine.Integration;

namespace Central.Tests.Integration;

/// <summary>Tests for the complete sync engine agent and converter registry.</summary>
public class AgentRegistrationTests
{
    [Fact]
    public void AllBuiltInAgents_Registered()
    {
        var engine = new SyncEngine();
        engine.RegisterAgent(new CsvImportAgent());
        engine.RegisterAgent(new RestApiAgent());

        var types = engine.GetAgentTypes();
        Assert.Contains("csv_import", types);
        Assert.Contains("rest_api", types);
    }

    [Fact]
    public void AllBuiltInConverters_Registered()
    {
        var engine = new SyncEngine();
        engine.RegisterConverter(new DirectConverter());
        engine.RegisterConverter(new ConstantConverter());
        engine.RegisterConverter(new CombineConverter());
        engine.RegisterConverter(new SplitConverter());
        engine.RegisterConverter(new LookupConverter());
        engine.RegisterConverter(new DateFormatConverter());
        engine.RegisterConverter(new ExpressionConverter());

        var types = engine.GetConverterTypes();
        Assert.Equal(7, types.Count);
        Assert.Contains("direct", types);
        Assert.Contains("constant", types);
        Assert.Contains("combine", types);
        Assert.Contains("split", types);
        Assert.Contains("lookup", types);
        Assert.Contains("date_format", types);
        Assert.Contains("expression", types);
    }

    [Fact]
    public void DuplicateAgent_Overwrites()
    {
        var engine = new SyncEngine();
        engine.RegisterAgent(new CsvImportAgent());
        engine.RegisterAgent(new CsvImportAgent()); // same type again
        Assert.Single(engine.GetAgentTypes().Where(t => t == "csv_import"));
    }
}
