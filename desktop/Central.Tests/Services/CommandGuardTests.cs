using Central.Core.Services;

namespace Central.Tests.Services;

public class CommandGuardTests
{
    [Fact]
    public void TryEnter_FirstCall_ReturnsTrue()
    {
        Assert.True(CommandGuard.TryEnter("test_enter"));
        CommandGuard.Exit("test_enter");
    }

    [Fact]
    public void TryEnter_SecondCall_ReturnsFalse()
    {
        CommandGuard.TryEnter("test_reentrant");
        Assert.False(CommandGuard.TryEnter("test_reentrant"));
        CommandGuard.Exit("test_reentrant");
    }

    [Fact]
    public void Exit_AllowsReEntry()
    {
        CommandGuard.TryEnter("test_exit");
        CommandGuard.Exit("test_exit");
        Assert.True(CommandGuard.TryEnter("test_exit"));
        CommandGuard.Exit("test_exit");
    }

    [Fact]
    public void IsRunning_TracksState()
    {
        Assert.False(CommandGuard.IsRunning("test_running"));
        CommandGuard.TryEnter("test_running");
        Assert.True(CommandGuard.IsRunning("test_running"));
        CommandGuard.Exit("test_running");
        Assert.False(CommandGuard.IsRunning("test_running"));
    }

    [Fact]
    public void Run_Sync_PreventsReEntry()
    {
        int count = 0;
        CommandGuard.Run("test_sync", () => count++);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RunAsync_PreventsReEntry()
    {
        int count = 0;
        await CommandGuard.RunAsync("test_async", async () =>
        {
            count++;
            await Task.Delay(1);
        });
        Assert.Equal(1, count);
    }

    [Fact]
    public void DifferentCommands_Independent()
    {
        CommandGuard.TryEnter("cmd_a");
        Assert.True(CommandGuard.TryEnter("cmd_b")); // different command = allowed
        CommandGuard.Exit("cmd_a");
        CommandGuard.Exit("cmd_b");
    }
}
