using Central.Engine.Services;

namespace Central.Tests.Services;

/// <summary>Extended CommandGuard tests for concurrency and edge cases.</summary>
public class CommandGuardEdgeCaseTests
{
    [Fact]
    public void Exit_NonExistent_NoException()
    {
        // Should not throw when exiting a command that was never entered
        CommandGuard.Exit("never_entered_" + Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public void Run_Sync_ExitsClearly_AfterException()
    {
        var name = "test_sync_ex_" + Guid.NewGuid().ToString("N");
        Assert.Throws<InvalidOperationException>(() =>
            CommandGuard.Run(name, () => throw new InvalidOperationException("boom")));
        // After exception, command should be released
        Assert.False(CommandGuard.IsRunning(name));
    }

    [Fact]
    public async Task RunAsync_ExitsCleanly_AfterException()
    {
        var name = "test_async_ex_" + Guid.NewGuid().ToString("N");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CommandGuard.RunAsync(name, () => throw new InvalidOperationException("boom")));
        Assert.False(CommandGuard.IsRunning(name));
    }

    [Fact]
    public void Run_Sync_Skips_WhenAlreadyRunning()
    {
        var name = "test_skip_" + Guid.NewGuid().ToString("N");
        CommandGuard.TryEnter(name);
        int count = 0;
        CommandGuard.Run(name, () => count++);
        Assert.Equal(0, count); // skipped because already running
        CommandGuard.Exit(name);
    }

    [Fact]
    public async Task RunAsync_Skips_WhenAlreadyRunning()
    {
        var name = "test_skip_async_" + Guid.NewGuid().ToString("N");
        CommandGuard.TryEnter(name);
        int count = 0;
        await CommandGuard.RunAsync(name, async () => { count++; await Task.CompletedTask; });
        Assert.Equal(0, count);
        CommandGuard.Exit(name);
    }

    [Fact]
    public async Task ConcurrentAttempts_OnlyOneSucceeds()
    {
        var name = "test_concurrent_" + Guid.NewGuid().ToString("N");
        int executionCount = 0;
        var barrier = new TaskCompletionSource();

        var task1 = Task.Run(async () =>
        {
            await CommandGuard.RunAsync(name, async () =>
            {
                Interlocked.Increment(ref executionCount);
                await barrier.Task;
            });
        });

        // Give task1 time to enter
        await Task.Delay(50);

        // task2 should be blocked
        await CommandGuard.RunAsync(name, async () =>
        {
            Interlocked.Increment(ref executionCount);
            await Task.CompletedTask;
        });

        barrier.SetResult();
        await task1;

        // Only task1 should have executed since it held the guard
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public void MultipleEnterExit_Cycles()
    {
        var name = "test_cycle_" + Guid.NewGuid().ToString("N");
        for (int i = 0; i < 100; i++)
        {
            Assert.True(CommandGuard.TryEnter(name));
            Assert.True(CommandGuard.IsRunning(name));
            CommandGuard.Exit(name);
            Assert.False(CommandGuard.IsRunning(name));
        }
    }
}
