using Central.Core.Integration;

namespace Central.Tests.Integration;

public class RestApiAgentTests
{
    [Fact]
    public void AgentType_IsRestApi()
    {
        var agent = new RestApiAgent();
        Assert.Equal("rest_api", agent.AgentType);
        Assert.Equal("REST API", agent.DisplayName);
    }

    [Fact]
    public async Task TestConnection_NoBaseUrl_Fails()
    {
        var agent = new RestApiAgent();
        await agent.InitializeAsync(new Dictionary<string, string>());
        var result = await agent.TestConnectionAsync();
        Assert.False(result.Success);
        Assert.Contains("No base_url", result.Message);
    }

    [Fact]
    public async Task TestConnection_InvalidUrl_Fails()
    {
        var agent = new RestApiAgent();
        await agent.InitializeAsync(new Dictionary<string, string>
        {
            ["base_url"] = "http://localhost:99999"
        });
        var result = await agent.TestConnectionAsync();
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Initialize_SetsConfig()
    {
        var agent = new RestApiAgent();
        await agent.InitializeAsync(new Dictionary<string, string>
        {
            ["base_url"] = "https://api.example.com",
            ["auth_type"] = "bearer",
            ["auth_value"] = "test-token",
            ["page_size"] = "50"
        });
        // No exception = config accepted
    }

    [Fact]
    public async Task GetEntityNames_ReturnsConfiguredEndpoint()
    {
        var agent = new RestApiAgent();
        await agent.InitializeAsync(new Dictionary<string, string>
        {
            ["list_endpoint"] = "users"
        });
        var names = await agent.GetEntityNamesAsync();
        Assert.Contains("users", names);
    }

    [Fact]
    public async Task Delete_NotImplemented()
    {
        var agent = new RestApiAgent();
        var result = await agent.DeleteAsync("users", "123");
        Assert.False(result.Success);
    }
}
