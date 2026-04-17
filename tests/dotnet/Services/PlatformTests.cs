using Central.Engine.Auth;
using Central.Engine.Integration;
using Central.Engine.Models;
using Central.Engine.Services;
using Central.Engine.Shell;

namespace Central.Tests.Services;

/// <summary>
/// Platform-level integration tests — verify all singletons and services work together.
/// </summary>
public class PlatformTests
{
    [Fact]
    public void Mediator_Singleton_Exists()
    {
        Assert.NotNull(Mediator.Instance);
    }

    [Fact]
    public void SyncEngine_Singleton_Exists()
    {
        Assert.NotNull(SyncEngine.Instance);
    }

    [Fact]
    public void LinkEngine_Singleton_Exists()
    {
        Assert.NotNull(LinkEngine.Instance);
    }

    [Fact]
    public void AuditService_Singleton_Exists()
    {
        Assert.NotNull(AuditService.Instance);
    }

    [Fact]
    public void DataValidationService_Singleton_Exists()
    {
        Assert.NotNull(DataValidationService.Instance);
    }

    [Fact]
    public void NotificationService_Singleton_Exists()
    {
        Assert.NotNull(NotificationService.Instance);
    }

    [Fact]
    public void PermissionCodes_AllDefined()
    {
        // Verify key permission codes exist
        Assert.Equal("devices:read", P.DevicesRead);
        Assert.Equal("admin:users", P.AdminUsers);
        Assert.Equal("admin:ad", P.AdminAd);
        Assert.Equal("admin:backup", P.AdminBackup);
        Assert.Equal("scheduler:read", P.SchedulerRead);
    }

    [Fact]
    public void AuthStates_9Values()
    {
        Assert.Equal(9, Enum.GetValues<AuthStates>().Length);
    }

    [Fact]
    public void NotificationEventTypes_8Events()
    {
        Assert.Equal(8, NotificationEventTypes.All.Length);
        Assert.Contains("sync_failure", NotificationEventTypes.All);
        Assert.Contains("auth_lockout", NotificationEventTypes.All);
        Assert.Contains("backup_complete", NotificationEventTypes.All);
    }

    [Fact]
    public void PasswordPolicy_Default_IsReasonable()
    {
        var policy = PasswordPolicy.Default;
        Assert.Equal(8, policy.MinLength);
        Assert.True(policy.RequireUppercase);
        Assert.True(policy.RequireDigit);
        Assert.Equal(90, policy.ExpiryDays);
    }

    [Fact]
    public void PasswordPolicy_Relaxed_IsPermissive()
    {
        var policy = PasswordPolicy.Relaxed;
        Assert.Equal(4, policy.MinLength);
        Assert.False(policy.RequireUppercase);
        Assert.Equal(0, policy.ExpiryDays);
    }
}
