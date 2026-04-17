using Central.Core.Services;

namespace Central.Tests.Services;

public class AuditServiceTests
{
    [Fact]
    public async Task LogAsync_NoPersistFunc_DoesNotThrow()
    {
        var svc = new AuditService();
        await svc.LogAsync("Create", "Device", "1", "TestDevice");
        // No exception = success
    }

    [Fact]
    public async Task LogAsync_PersistFunc_Called()
    {
        var svc = new AuditService();
        AuditEntry? captured = null;
        svc.SetPersistFunc(entry => { captured = entry; return Task.CompletedTask; });

        await svc.LogAsync("Update", "Switch", "42", "MEP-91-CORE02");

        Assert.NotNull(captured);
        Assert.Equal("Update", captured!.Action);
        Assert.Equal("Switch", captured.EntityType);
        Assert.Equal("42", captured.EntityId);
        Assert.Equal("MEP-91-CORE02", captured.EntityName);
    }

    [Fact]
    public async Task LogAsync_BroadcastFunc_Called()
    {
        var svc = new AuditService();
        string? broadcastAction = null;
        svc.SetBroadcastFunc((action, _, _, _) => broadcastAction = action);
        svc.SetPersistFunc(_ => Task.CompletedTask);

        await svc.LogAsync("Delete", "User", "5");

        Assert.Equal("Delete", broadcastAction);
    }

    [Fact]
    public async Task LogCreateAsync_SetsAction()
    {
        var svc = new AuditService();
        AuditEntry? captured = null;
        svc.SetPersistFunc(entry => { captured = entry; return Task.CompletedTask; });

        await svc.LogCreateAsync("Device", "10", "NewDevice");

        Assert.Equal("Create", captured!.Action);
        Assert.Equal("NewDevice", captured.EntityName);
    }

    [Fact]
    public async Task LogDeleteAsync_SetsAction()
    {
        var svc = new AuditService();
        AuditEntry? captured = null;
        svc.SetPersistFunc(entry => { captured = entry; return Task.CompletedTask; });

        await svc.LogDeleteAsync("User", "3", "john.smith");

        Assert.Equal("Delete", captured!.Action);
        Assert.Equal("User", captured.EntityType);
    }

    [Fact]
    public async Task LogAsync_WithBeforeAfter_Serializes()
    {
        var svc = new AuditService();
        AuditEntry? captured = null;
        svc.SetPersistFunc(entry => { captured = entry; return Task.CompletedTask; });

        await svc.LogUpdateAsync("Device", "1", "Dev1",
            before: new() { ["status"] = "Active" },
            after: new() { ["status"] = "Inactive" });

        Assert.Contains("Active", captured!.BeforeJson!);
        Assert.Contains("Inactive", captured.AfterJson!);
    }

    [Fact]
    public async Task LogAsync_PersistThrows_DoesNotCrash()
    {
        var svc = new AuditService();
        svc.SetPersistFunc(_ => throw new Exception("DB down"));

        await svc.LogAsync("Create", "Test"); // Should not throw
    }
}
