using Central.Core.Auth;

namespace Central.Tests.Auth;

public class PasswordPolicyTests
{
    [Fact]
    public void Default_RequiresAllComplexity()
    {
        var policy = PasswordPolicy.Default;
        Assert.Equal(8, policy.MinLength);
        Assert.True(policy.RequireUppercase);
        Assert.True(policy.RequireLowercase);
        Assert.True(policy.RequireDigit);
        Assert.True(policy.RequireSpecialChar);
    }

    [Fact]
    public void Validate_StrongPassword_Passes()
    {
        var result = PasswordPolicy.Default.Validate("MyP@ssw0rd!");
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_TooShort_Fails()
    {
        var result = PasswordPolicy.Default.Validate("Ab1!");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Minimum"));
    }

    [Fact]
    public void Validate_NoUppercase_Fails()
    {
        var result = PasswordPolicy.Default.Validate("mypassw0rd!");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("uppercase"));
    }

    [Fact]
    public void Validate_NoLowercase_Fails()
    {
        var result = PasswordPolicy.Default.Validate("MYPASSW0RD!");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("lowercase"));
    }

    [Fact]
    public void Validate_NoDigit_Fails()
    {
        var result = PasswordPolicy.Default.Validate("MyPassword!");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("digit"));
    }

    [Fact]
    public void Validate_NoSpecialChar_Fails()
    {
        var result = PasswordPolicy.Default.Validate("MyPassw0rd");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("special"));
    }

    [Fact]
    public void Validate_Empty_Fails()
    {
        var result = PasswordPolicy.Default.Validate("");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_Null_Fails()
    {
        var result = PasswordPolicy.Default.Validate(null!);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Relaxed_AcceptsSimplePassword()
    {
        var result = PasswordPolicy.Relaxed.Validate("test");
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_PasswordHistory_BlocksReuse()
    {
        var policy = new PasswordPolicy { PasswordHistoryCount = 3 };
        var salt = "testsalt";
        var previousHashes = new[]
        {
            PasswordHasher.Hash("OldPass1!", salt),
            PasswordHasher.Hash("OldPass2!", salt),
            PasswordHasher.Hash("OldPass3!", salt),
        };

        // Reusing old password should fail
        var result = policy.Validate("OldPass1!", previousHashes, salt);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("reuse"));

        // New password should pass
        var result2 = policy.Validate("NewPass4!", previousHashes, salt);
        Assert.True(result2.IsValid);
    }

    [Fact]
    public void IsExpired_WithinWindow_NotExpired()
    {
        var policy = new PasswordPolicy { ExpiryDays = 90 };
        Assert.False(policy.IsExpired(DateTime.UtcNow.AddDays(-30)));
    }

    [Fact]
    public void IsExpired_PastWindow_Expired()
    {
        var policy = new PasswordPolicy { ExpiryDays = 90 };
        Assert.True(policy.IsExpired(DateTime.UtcNow.AddDays(-91)));
    }

    [Fact]
    public void IsExpired_NoExpiry_NeverExpires()
    {
        var policy = new PasswordPolicy { ExpiryDays = 0 };
        Assert.False(policy.IsExpired(DateTime.UtcNow.AddDays(-365)));
    }

    [Fact]
    public void IsTooRecent_JustChanged_Blocked()
    {
        var policy = new PasswordPolicy { MinAgeDays = 1 };
        Assert.True(policy.IsTooRecent(DateTime.UtcNow));
    }

    [Fact]
    public void IsTooRecent_OldEnough_Allowed()
    {
        var policy = new PasswordPolicy { MinAgeDays = 1 };
        Assert.False(policy.IsTooRecent(DateTime.UtcNow.AddDays(-2)));
    }

    [Fact]
    public void Description_ReflectsPolicy()
    {
        var desc = PasswordPolicy.Default.Description;
        Assert.Contains("Min 8", desc);
        Assert.Contains("uppercase", desc);
        Assert.Contains("digit", desc);
        Assert.Contains("expires after 90 days", desc);
    }

    [Fact]
    public void MultipleErrors_AllReported()
    {
        var result = PasswordPolicy.Default.Validate("ab");
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 3); // too short + no uppercase + no digit + no special
    }
}
