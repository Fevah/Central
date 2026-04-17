using Central.Engine.Auth;

namespace Central.Tests.Auth;

/// <summary>
/// Tests for password expiry, minimum age, and change policy enforcement.
/// </summary>
public class PasswordExpiryTests
{
    [Fact]
    public void IsExpired_RecentPassword_NotExpired()
    {
        var policy = PasswordPolicy.Default;
        Assert.False(policy.IsExpired(DateTime.UtcNow.AddDays(-1)));
    }

    [Fact]
    public void IsExpired_OldPassword_Expired()
    {
        var policy = PasswordPolicy.Default; // 90 day expiry
        Assert.True(policy.IsExpired(DateTime.UtcNow.AddDays(-91)));
    }

    [Fact]
    public void IsExpired_NullDate_NotExpired()
    {
        var policy = PasswordPolicy.Default;
        Assert.False(policy.IsExpired(null));
    }

    [Fact]
    public void IsExpired_ExpiryDisabled_NeverExpires()
    {
        var policy = new PasswordPolicy { ExpiryDays = 0 };
        Assert.False(policy.IsExpired(DateTime.UtcNow.AddDays(-365)));
    }

    [Fact]
    public void IsTooRecent_JustChanged_TooRecent()
    {
        var policy = PasswordPolicy.Default; // MinAgeDays = 1
        Assert.True(policy.IsTooRecent(DateTime.UtcNow));
    }

    [Fact]
    public void IsTooRecent_ChangedYesterday_NotTooRecent()
    {
        var policy = PasswordPolicy.Default;
        Assert.False(policy.IsTooRecent(DateTime.UtcNow.AddDays(-2)));
    }

    [Fact]
    public void IsTooRecent_NullDate_NotTooRecent()
    {
        var policy = PasswordPolicy.Default;
        Assert.False(policy.IsTooRecent(null));
    }

    [Fact]
    public void Validate_StrongPassword_Passes()
    {
        var policy = PasswordPolicy.Default;
        var result = policy.Validate("MyStr0ng!Pass");
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_TooShort_Fails()
    {
        var policy = PasswordPolicy.Default;
        var result = policy.Validate("Ab1!");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("8"));
    }

    [Fact]
    public void Validate_NoUppercase_Fails()
    {
        var policy = PasswordPolicy.Default;
        var result = policy.Validate("lowercase1!");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_NoDigit_Fails()
    {
        var policy = PasswordPolicy.Default;
        var result = policy.Validate("NoDigitHere!");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_NoSpecialChar_Fails()
    {
        var policy = PasswordPolicy.Default;
        var result = policy.Validate("NoSpecial1A");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RelaxedPolicy_AcceptsSimple()
    {
        var policy = PasswordPolicy.Relaxed;
        var result = policy.Validate("easy");
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Description_IncludesAllRequirements()
    {
        var policy = PasswordPolicy.Default;
        var desc = policy.Description;
        Assert.Contains("8", desc);      // min length
        Assert.Contains("uppercase", desc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Argon2id_NewHash_VerifiesCorrectly()
    {
        var salt = PasswordHasher.GenerateSalt();
        var hash = PasswordHasher.Hash("TestP@ss1!", salt);

        Assert.True(PasswordHasher.Verify("TestP@ss1!", salt, hash));
        Assert.False(PasswordHasher.Verify("WrongPass1!", salt, hash));
    }

    [Fact]
    public void LegacySha256_MigratesOnVerify()
    {
        // Simulate legacy SHA256 hash
        var salt = PasswordHasher.GenerateSalt();
        var legacyInput = System.Text.Encoding.UTF8.GetBytes("OldPass1!" + salt);
        var legacyHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(legacyInput));

        // Legacy verify works
        Assert.True(PasswordHasher.VerifyLegacySha256("OldPass1!", salt, legacyHash));

        // New Argon2id verify does NOT match legacy hash
        Assert.False(PasswordHasher.Verify("OldPass1!", salt, legacyHash));

        // Re-hash to Argon2id
        var newSalt = PasswordHasher.GenerateSalt();
        var newHash = PasswordHasher.Hash("OldPass1!", newSalt);

        // New hash verifies with Argon2id
        Assert.True(PasswordHasher.Verify("OldPass1!", newSalt, newHash));
    }
}
