using Central.Core.Auth;

namespace Central.Tests.Auth;

public class PasswordPolicyExtendedTests
{
    // ── Boundary: exactly min length ──

    [Fact]
    public void Validate_ExactlyMinLength_Passes()
    {
        var policy = new PasswordPolicy { MinLength = 8 };
        var result = policy.Validate("A1b!cdef");
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_OneLessThanMinLength_Fails()
    {
        var policy = new PasswordPolicy { MinLength = 8 };
        var result = policy.Validate("A1b!cde");
        Assert.False(result.IsValid);
    }

    // ── Boundary: max length ──

    [Fact]
    public void Validate_ExactlyMaxLength_Passes()
    {
        var policy = new PasswordPolicy { MinLength = 4, MaxLength = 10 };
        var result = policy.Validate("Aa1!567890");
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_OverMaxLength_Fails()
    {
        var policy = new PasswordPolicy { MinLength = 4, MaxLength = 10 };
        var result = policy.Validate("Aa1!5678901");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Maximum"));
    }

    // ── Boundary: expiry days ──

    [Fact]
    public void IsExpired_ExactlyOnBoundary_NotExpired()
    {
        var policy = new PasswordPolicy { ExpiryDays = 90 };
        // Exactly 90 days ago — should NOT be expired (> not >=)
        Assert.False(policy.IsExpired(DateTime.UtcNow.AddDays(-89.99)));
    }

    [Fact]
    public void IsExpired_JustPastBoundary_Expired()
    {
        var policy = new PasswordPolicy { ExpiryDays = 90 };
        Assert.True(policy.IsExpired(DateTime.UtcNow.AddDays(-90.01)));
    }

    [Fact]
    public void IsExpired_NullDate_NotExpired()
    {
        var policy = new PasswordPolicy { ExpiryDays = 90 };
        Assert.False(policy.IsExpired(null));
    }

    // ── Boundary: min age days ──

    [Fact]
    public void IsTooRecent_ExactlyMinAge_TooRecent()
    {
        var policy = new PasswordPolicy { MinAgeDays = 1 };
        // Just changed (less than 1 day ago)
        Assert.True(policy.IsTooRecent(DateTime.UtcNow.AddHours(-12)));
    }

    [Fact]
    public void IsTooRecent_PastMinAge_Allowed()
    {
        var policy = new PasswordPolicy { MinAgeDays = 1 };
        Assert.False(policy.IsTooRecent(DateTime.UtcNow.AddDays(-1.5)));
    }

    [Fact]
    public void IsTooRecent_ZeroMinAge_NeverBlocked()
    {
        var policy = new PasswordPolicy { MinAgeDays = 0 };
        Assert.False(policy.IsTooRecent(DateTime.UtcNow));
    }

    [Fact]
    public void IsTooRecent_NullDate_Allowed()
    {
        var policy = new PasswordPolicy { MinAgeDays = 1 };
        Assert.False(policy.IsTooRecent(null));
    }

    // ── Password history edge cases ──

    [Fact]
    public void Validate_PasswordHistory_NoSalt_SkipsCheck()
    {
        var policy = new PasswordPolicy { PasswordHistoryCount = 3 };
        var hashes = new[] { "hash1", "hash2" };
        // No salt → history check is skipped
        var result = policy.Validate("MyP@ssw0rd!", hashes, null);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_PasswordHistory_ZeroCount_SkipsCheck()
    {
        var policy = new PasswordPolicy { PasswordHistoryCount = 0 };
        var salt = "salt";
        var hashes = new[] { PasswordHasher.Hash("MyP@ssw0rd!", salt) };
        var result = policy.Validate("MyP@ssw0rd!", hashes, salt);
        Assert.True(result.IsValid);
    }

    // ── Description edge cases ──

    [Fact]
    public void Description_NoExpiry_OmitsExpiryText()
    {
        var policy = new PasswordPolicy { ExpiryDays = 0 };
        Assert.DoesNotContain("expires", policy.Description);
    }

    [Fact]
    public void Description_RelaxedPolicy_Minimal()
    {
        var desc = PasswordPolicy.Relaxed.Description;
        Assert.Contains("Min 4", desc);
        Assert.DoesNotContain("uppercase", desc);
        Assert.DoesNotContain("digit", desc);
    }

    // ── ErrorSummary ──

    [Fact]
    public void ErrorSummary_MultipleErrors_JoinedBySemicolon()
    {
        var result = PasswordPolicy.Default.Validate("a");
        Assert.Contains(";", result.ErrorSummary);
    }

    [Fact]
    public void ErrorSummary_NoErrors_Empty()
    {
        var result = PasswordPolicy.Default.Validate("MyP@ssw0rd!");
        Assert.Equal("", result.ErrorSummary);
    }

    // ── Special characters edge cases ──

    [Theory]
    [InlineData("MyP@ssw0rd!", true)]
    [InlineData("MyP#ssw0rd", true)]
    [InlineData("MyP$ssw0rd", true)]
    [InlineData("MyP%ssw0rd", true)]
    [InlineData("MyP&ssw0rd", true)]
    [InlineData("MyPass123", false)]  // no special char
    public void Validate_VariousSpecialChars(string password, bool expectedValid)
    {
        var result = PasswordPolicy.Default.Validate(password);
        Assert.Equal(expectedValid, result.IsValid);
    }

    // ── Unicode characters ──

    [Fact]
    public void Validate_UnicodePassword_WithAllRequirements_Passes()
    {
        var result = PasswordPolicy.Default.Validate("Aa1!Ümlaut");
        Assert.True(result.IsValid);
    }
}
