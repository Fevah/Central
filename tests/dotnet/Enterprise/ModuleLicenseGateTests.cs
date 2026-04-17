using Central.Engine.Modules;

namespace Central.Tests.Enterprise;

public class ModuleLicenseGateTests
{
    [Fact]
    public void AllowAllGate_EnablesEveryModule()
    {
        var gate = new AllowAllModuleGate();
        Assert.True(gate.IsEnabled("devices"));
        Assert.True(gate.IsEnabled("networking"));
        Assert.True(gate.IsEnabled("crm"));
        Assert.True(gate.IsEnabled("module-that-doesnt-exist-yet"));
    }

    [Fact]
    public void AllowListGate_OnlyAllowsListedModules()
    {
        var gate = new AllowListModuleGate(new[] { "devices", "networking" });
        Assert.True(gate.IsEnabled("devices"));
        Assert.True(gate.IsEnabled("networking"));
        Assert.False(gate.IsEnabled("crm"));
        Assert.False(gate.IsEnabled("projects"));
    }

    [Fact]
    public void AllowListGate_IsCaseInsensitive()
    {
        var gate = new AllowListModuleGate(new[] { "Devices", "NETWORKING" });
        Assert.True(gate.IsEnabled("devices"));
        Assert.True(gate.IsEnabled("networking"));
        Assert.True(gate.IsEnabled("DEVICES"));
    }

    [Fact]
    public void AllowListGate_HandlesEmptyList()
    {
        var gate = new AllowListModuleGate(Array.Empty<string>());
        Assert.False(gate.IsEnabled("devices"));
        Assert.False(gate.IsEnabled(""));
    }
}
