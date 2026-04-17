using Central.Api.Services;

namespace Central.Tests.Api;

/// <summary>
/// Tests for the SSH rate limiter service.
/// </summary>
public class RateLimiterTests
{
    [Fact]
    public void TryAcquire_UnderLimit_Allowed()
    {
        var limiter = new RateLimiter(maxRequests: 5, windowSeconds: 60);
        for (int i = 0; i < 5; i++)
            Assert.True(limiter.TryAcquire("user1"));
    }

    [Fact]
    public void TryAcquire_AtLimit_Rejected()
    {
        var limiter = new RateLimiter(maxRequests: 3, windowSeconds: 60);
        Assert.True(limiter.TryAcquire("user1"));
        Assert.True(limiter.TryAcquire("user1"));
        Assert.True(limiter.TryAcquire("user1"));
        Assert.False(limiter.TryAcquire("user1"));
    }

    [Fact]
    public void TryAcquire_DifferentKeys_Independent()
    {
        var limiter = new RateLimiter(maxRequests: 1, windowSeconds: 60);
        Assert.True(limiter.TryAcquire("user1"));
        Assert.True(limiter.TryAcquire("user2"));
        Assert.False(limiter.TryAcquire("user1"));
    }

    [Fact]
    public void TryAcquire_SingleRequest_Allowed()
    {
        var limiter = new RateLimiter(maxRequests: 1, windowSeconds: 60);
        Assert.True(limiter.TryAcquire("key"));
    }
}
