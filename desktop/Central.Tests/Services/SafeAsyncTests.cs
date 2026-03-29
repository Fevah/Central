using Central.Core.Services;

namespace Central.Tests.Services;

public class SafeAsyncTests
{
    [Fact]
    public void Run_Success_Completes()
    {
        bool completed = false;
        SafeAsync.Run(async () => { await Task.Delay(1); completed = true; });
        Thread.Sleep(100); // Give async void time to complete
        Assert.True(completed);
    }

    [Fact]
    public void Run_Exception_DoesNotCrash()
    {
        // This should NOT throw — exceptions are caught internally
        SafeAsync.Run(async () =>
        {
            await Task.Delay(1);
            throw new InvalidOperationException("test error");
        }, "TestContext");

        Thread.Sleep(100);
        // If we get here, the exception was caught (not re-thrown)
    }

    [Fact]
    public void RunGuarded_PreventsReEntry()
    {
        int count = 0;
        SafeAsync.RunGuarded("test_guard", async () =>
        {
            Interlocked.Increment(ref count);
            await Task.Delay(200);
        });

        // Second call while first is running — should be blocked
        SafeAsync.RunGuarded("test_guard", async () =>
        {
            Interlocked.Increment(ref count);
            await Task.Delay(1);
        });

        Thread.Sleep(300);
        Assert.Equal(1, count); // Only first executed
    }

    [Fact]
    public void RunGuarded_Exception_ReleasesGuard()
    {
        SafeAsync.RunGuarded("test_release", async () =>
        {
            await Task.Delay(1);
            throw new Exception("boom");
        });

        Thread.Sleep(100);
        // Guard should be released after exception
        Assert.False(CommandGuard.IsRunning("test_release"));
    }
}
