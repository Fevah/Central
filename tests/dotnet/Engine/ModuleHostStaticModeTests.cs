using Central.Engine.Modules;

namespace Central.Tests.Engine;

/// <summary>
/// Phase 5b tests for the static-mode <see cref="ModuleHost"/>
/// constructor + <see cref="ModuleHostManager"/>'s routing of
/// static hosts through <c>HandleFullRestartAsync</c> regardless
/// of the notification's <c>changeKind</c>.
/// </summary>
[Collection("RibbonAudit")]
public class ModuleHostStaticModeTests
{
    private sealed class FakeModule : IModule
    {
        public string Name               => "Fake";
        public string PermissionCategory => "fake";
        public int    SortOrder          => 999;
    }

    private sealed class RecordingPolicy : IModuleUpdatePolicy
    {
        public List<ModuleUpdateNotification> HotSwapApplied { get; } = new();
        public List<ModuleUpdateNotification> SoftReload     { get; } = new();
        public List<ModuleUpdateNotification> FullRestart    { get; } = new();
        public List<(ModuleUpdateNotification n, string reason)> Failures { get; } = new();

        public Task HandleHotSwapAppliedAsync(ModuleUpdateNotification n, CancellationToken _) { HotSwapApplied.Add(n); return Task.CompletedTask; }
        public Task HandleSoftReloadAsync(ModuleUpdateNotification n, Func<Task> __, CancellationToken _) { SoftReload.Add(n); return Task.CompletedTask; }
        public Task HandleFullRestartAsync(ModuleUpdateNotification n, CancellationToken _) { FullRestart.Add(n); return Task.CompletedTask; }
        public Task HandleFailureAsync(ModuleUpdateNotification n, string r, Exception? _, CancellationToken __) { Failures.Add((n, r)); return Task.CompletedTask; }
    }

    private sealed class UnusedDownloader : IModuleDllDownloader
    {
        public int CallCount { get; private set; }
        public Task<byte[]?> DownloadAsync(string _, string __, CancellationToken ___ = default)
        {
            CallCount++;
            return Task.FromResult<byte[]?>(null);
        }
    }

    [Fact]
    public void StaticCtor_PopulatesLiveModuleAndVersion()
    {
        var host = new ModuleHost("fake", new FakeModule(), "1.2.3");

        Assert.True(host.IsStatic);
        Assert.True(host.IsLoaded);
        Assert.NotNull(host.LiveModule);
        Assert.Equal("fake", host.ModuleCode);
        Assert.Equal("1.2.3", host.LoadedVersion);
    }

    [Fact]
    public void StaticCtor_RejectsNullsAndBlanks()
    {
        var m = new FakeModule();
        Assert.Throws<ArgumentException>(() => new ModuleHost("",    m, "1.0.0"));
        Assert.Throws<ArgumentNullException>(() => new ModuleHost("fake", null!, "1.0.0"));
        Assert.Throws<ArgumentException>(() => new ModuleHost("fake", m, ""));
    }

    [Fact]
    public void StaticHost_LoadThrows()
    {
        var host = new ModuleHost("fake", new FakeModule(), "1.0.0");
        Assert.Throws<InvalidOperationException>(() =>
            host.Load(new byte[] { 1, 2, 3 }, "2.0.0"));
    }

    [Fact]
    public void StaticHost_UnloadIsNoop()
    {
        var host = new ModuleHost("fake", new FakeModule(), "1.0.0");
        host.Unload();                       // no throw + no state change
        Assert.True(host.IsLoaded);
        Assert.Equal("1.0.0", host.LoadedVersion);
    }

    [Fact]
    public async Task Manager_StaticHost_HotSwap_RoutesToFullRestart()
    {
        var dl     = new UnusedDownloader();
        var policy = new RecordingPolicy();
        var mgr    = new ModuleHostManager(dl, policy);
        try
        {
            mgr.RegisterHost(new ModuleHost("fake", new FakeModule(), "1.0.0"));

            var n = new ModuleUpdateNotification(
                "fake", "1.0.0", "1.1.0", "HotSwap", 1, "ignored", 0);

            await mgr.HandleNotificationAsync(n);

            Assert.Single(policy.FullRestart);       // routed to restart despite HotSwap
            Assert.Empty(policy.HotSwapApplied);
            Assert.Equal(0, dl.CallCount);           // no download attempted
        }
        finally { mgr.Dispose(); }
    }

    [Fact]
    public async Task Manager_StaticHost_SoftReload_RoutesToFullRestart()
    {
        var dl     = new UnusedDownloader();
        var policy = new RecordingPolicy();
        var mgr    = new ModuleHostManager(dl, policy);
        try
        {
            mgr.RegisterHost(new ModuleHost("fake", new FakeModule(), "1.0.0"));

            var n = new ModuleUpdateNotification(
                "fake", "1.0.0", "2.0.0", "SoftReload", 1, "ignored", 0);

            await mgr.HandleNotificationAsync(n);

            Assert.Single(policy.FullRestart);
            Assert.Empty(policy.SoftReload);
        }
        finally { mgr.Dispose(); }
    }

    [Fact]
    public async Task Manager_StaticHost_SameVersion_SkippedBeforeStaticCheck()
    {
        // Same-version no-op MUST run even for static hosts — otherwise
        // a SignalR replay after reconnect would show a bogus "restart
        // to apply" toast for the version you're already running.
        var dl     = new UnusedDownloader();
        var policy = new RecordingPolicy();
        var mgr    = new ModuleHostManager(dl, policy);
        try
        {
            mgr.RegisterHost(new ModuleHost("fake", new FakeModule(), "1.0.0"));

            var n = new ModuleUpdateNotification(
                "fake", "0.9.0", "1.0.0", "HotSwap", 1, "ignored", 0);

            await mgr.HandleNotificationAsync(n);

            Assert.Empty(policy.FullRestart);
            Assert.Empty(policy.HotSwapApplied);
        }
        finally { mgr.Dispose(); }
    }
}
