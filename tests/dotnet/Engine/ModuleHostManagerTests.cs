using System.IO;
using Central.Engine.Modules;

namespace Central.Tests.Engine;

/// <summary>
/// Phase 4 dispatch tests for <see cref="ModuleHostManager"/>. Exercises
/// the per-<c>changeKind</c> routing logic headlessly with a recording
/// policy + a deterministic fake downloader. No SignalR, no WPF.
/// </summary>
[Collection("RibbonAudit")]
public class ModuleHostManagerTests
{
    private static byte[] LoadAuditDll() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Central.Module.Audit.dll"));

    private sealed class RecordingPolicy : IModuleUpdatePolicy
    {
        public List<ModuleUpdateNotification> HotSwapApplied { get; } = new();
        public List<ModuleUpdateNotification> SoftReload    { get; } = new();
        public List<ModuleUpdateNotification> FullRestart   { get; } = new();
        public List<(ModuleUpdateNotification n, string reason)> Failures { get; } = new();

        /// <summary>If non-null, invoked during SoftReload to simulate user clicking "Reload now".</summary>
        public Func<Func<Task>, Task>? SoftReloadHandler { get; set; }

        public Task HandleHotSwapAppliedAsync(ModuleUpdateNotification n, CancellationToken ct)
        {
            HotSwapApplied.Add(n);
            return Task.CompletedTask;
        }

        public async Task HandleSoftReloadAsync(ModuleUpdateNotification n, Func<Task> applyAction, CancellationToken ct)
        {
            SoftReload.Add(n);
            if (SoftReloadHandler is not null) await SoftReloadHandler(applyAction);
        }

        public Task HandleFullRestartAsync(ModuleUpdateNotification n, CancellationToken ct)
        {
            FullRestart.Add(n);
            return Task.CompletedTask;
        }

        public Task HandleFailureAsync(ModuleUpdateNotification n, string reason, Exception? ex, CancellationToken ct)
        {
            Failures.Add((n, reason));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDownloader : IModuleDllDownloader
    {
        public int CallCount { get; private set; }
        public byte[]? NextResponse { get; set; }
        public Exception? NextException { get; set; }

        public Task<byte[]?> DownloadAsync(string moduleCode, string version, CancellationToken ct = default)
        {
            CallCount++;
            if (NextException is not null) throw NextException;
            return Task.FromResult(NextResponse);
        }
    }

    private static string Sha256Hex(byte[] bytes)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    private static (ModuleHostManager mgr, ModuleHost host, RecordingPolicy policy, FakeDownloader dl) BuildHarness()
    {
        var dl     = new FakeDownloader();
        var policy = new RecordingPolicy();
        var mgr    = new ModuleHostManager(dl, policy);
        var host   = new ModuleHost("audit");
        host.Load(LoadAuditDll(), version: "1.0.0");
        mgr.RegisterHost(host);
        return (mgr, host, policy, dl);
    }

    [Fact]
    public async Task HotSwap_DownloadsAndReloadsAndNotifiesPolicy()
    {
        var (mgr, host, policy, dl) = BuildHarness();
        try
        {
            var bytes = LoadAuditDll();
            dl.NextResponse = bytes;

            var n = new ModuleUpdateNotification(
                ModuleCode: "audit", FromVersion: "1.0.0", ToVersion: "1.0.1",
                ChangeKind: "HotSwap", MinEngineContract: 1,
                Sha256: Sha256Hex(bytes), SizeBytes: bytes.Length);

            await mgr.HandleNotificationAsync(n);

            Assert.Equal(1, dl.CallCount);
            Assert.Equal("1.0.1", host.LoadedVersion);
            Assert.Single(policy.HotSwapApplied);
            Assert.Empty(policy.Failures);
        }
        finally { mgr.Dispose(); }
    }

    [Fact]
    public async Task SoftReload_DoesNotDownloadUntilPolicyApplies()
    {
        var (mgr, host, policy, dl) = BuildHarness();
        try
        {
            var n = new ModuleUpdateNotification(
                "audit", "1.0.0", "1.1.0", "SoftReload", 1,
                Sha256: "ignored", SizeBytes: 0);

            await mgr.HandleNotificationAsync(n);

            Assert.Single(policy.SoftReload);
            Assert.Equal(0, dl.CallCount);                 // Policy hasn't called applyAction
            Assert.Equal("1.0.0", host.LoadedVersion);     // Host not touched
        }
        finally { mgr.Dispose(); }
    }

    [Fact]
    public async Task SoftReload_PolicyInvokingApply_DownloadsAndReloads()
    {
        var (mgr, host, policy, dl) = BuildHarness();
        try
        {
            var bytes = LoadAuditDll();
            dl.NextResponse = bytes;
            policy.SoftReloadHandler = apply => apply();   // simulate user clicking "Reload now"

            var n = new ModuleUpdateNotification(
                "audit", "1.0.0", "1.1.0", "SoftReload", 1,
                Sha256: Sha256Hex(bytes), SizeBytes: bytes.Length);

            await mgr.HandleNotificationAsync(n);

            Assert.Equal(1, dl.CallCount);
            Assert.Equal("1.1.0", host.LoadedVersion);
            Assert.Single(policy.SoftReload);
        }
        finally { mgr.Dispose(); }
    }

    [Fact]
    public async Task FullRestart_DoesNotTouchHost()
    {
        var (mgr, host, policy, dl) = BuildHarness();
        try
        {
            var n = new ModuleUpdateNotification(
                "audit", "1.0.0", "2.0.0", "FullRestart", 2,
                Sha256: "ignored", SizeBytes: 0);

            await mgr.HandleNotificationAsync(n);

            Assert.Single(policy.FullRestart);
            Assert.Equal(0, dl.CallCount);                 // Full restart handles its own download
            Assert.Equal("1.0.0", host.LoadedVersion);
        }
        finally { mgr.Dispose(); }
    }

    [Fact]
    public async Task UnknownChangeKind_ReportsFailure()
    {
        var (mgr, _, policy, _) = BuildHarness();
        try
        {
            var n = new ModuleUpdateNotification(
                "audit", "1.0.0", "1.0.1", "Teleport", 1, "ignored", 0);

            await mgr.HandleNotificationAsync(n);

            Assert.Single(policy.Failures);
            Assert.Contains("Unknown changeKind", policy.Failures[0].reason);
        }
        finally { mgr.Dispose(); }
    }

    [Fact]
    public async Task UnknownModule_SilentlyDropped()
    {
        var dl     = new FakeDownloader();
        var policy = new RecordingPolicy();
        var mgr    = new ModuleHostManager(dl, policy);   // no hosts registered
        try
        {
            var n = new ModuleUpdateNotification(
                "networking", "1.0.0", "1.0.1", "HotSwap", 1, "x", 0);

            await mgr.HandleNotificationAsync(n);         // should not throw

            Assert.Equal(0, dl.CallCount);
            Assert.Empty(policy.HotSwapApplied);
            Assert.Empty(policy.Failures);
        }
        finally { mgr.Dispose(); }
    }

    [Fact]
    public async Task SameVersionNotification_SkippedAsNoOp()
    {
        var (mgr, _, policy, dl) = BuildHarness();
        try
        {
            var n = new ModuleUpdateNotification(
                "audit", "0.9.0", "1.0.0", "HotSwap", 1, "ignored", 0);

            await mgr.HandleNotificationAsync(n);

            Assert.Equal(0, dl.CallCount);
            Assert.Empty(policy.HotSwapApplied);
        }
        finally { mgr.Dispose(); }
    }

    [Fact]
    public async Task HotSwap_DownloadReturnsNull_ReportsFailure()
    {
        var (mgr, host, policy, dl) = BuildHarness();
        try
        {
            dl.NextResponse = null;   // simulate 404 / 410

            var n = new ModuleUpdateNotification(
                "audit", "1.0.0", "1.0.1", "HotSwap", 1, "x", 0);

            await mgr.HandleNotificationAsync(n);

            Assert.Single(policy.Failures);
            Assert.Contains("Downloader returned null", policy.Failures[0].reason);
            Assert.Equal("1.0.0", host.LoadedVersion);   // Untouched.
        }
        finally { mgr.Dispose(); }
    }

    [Fact]
    public async Task HotSwap_ShaMismatch_ReportsFailure()
    {
        var (mgr, host, policy, dl) = BuildHarness();
        try
        {
            dl.NextResponse = LoadAuditDll();

            var n = new ModuleUpdateNotification(
                "audit", "1.0.0", "1.0.1", "HotSwap", 1,
                Sha256: new string('0', 64),              // doesn't match real bytes
                SizeBytes: 0);

            await mgr.HandleNotificationAsync(n);

            Assert.Single(policy.Failures);
            Assert.Contains("SHA-256 mismatch", policy.Failures[0].reason);
            // Host ends unloaded after a failed Reload — caller's
            // responsibility to roll back to prior bytes if desired.
            Assert.False(host.IsLoaded);
        }
        finally { mgr.Dispose(); }
    }

    [Fact]
    public void RegisterHost_RejectsNull()
    {
        var mgr = new ModuleHostManager(new FakeDownloader(), new RecordingPolicy());
        try { Assert.Throws<ArgumentNullException>(() => mgr.RegisterHost(null!)); }
        finally { mgr.Dispose(); }
    }

    [Fact]
    public void Ctor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ModuleHostManager(null!, new RecordingPolicy()));
        Assert.Throws<ArgumentNullException>(() => new ModuleHostManager(new FakeDownloader(), null!));
    }
}
