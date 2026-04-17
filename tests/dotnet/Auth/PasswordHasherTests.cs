using Central.Engine.Auth;

namespace Central.Tests.Auth;

public class PasswordHasherTests
{
    [Fact]
    public void GenerateSalt_ReturnsNonEmpty()
    {
        var salt = PasswordHasher.GenerateSalt();
        Assert.NotNull(salt);
        Assert.True(salt.Length >= 16);
    }

    [Fact]
    public void GenerateSalt_UniqueEachTime()
    {
        var salt1 = PasswordHasher.GenerateSalt();
        var salt2 = PasswordHasher.GenerateSalt();
        Assert.NotEqual(salt1, salt2);
    }

    [Fact]
    public void Hash_SameInput_SameOutput()
    {
        var salt = PasswordHasher.GenerateSalt();
        var hash1 = PasswordHasher.Hash("password", salt);
        var hash2 = PasswordHasher.Hash("password", salt);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Hash_DifferentSalt_DifferentOutput()
    {
        var salt1 = PasswordHasher.GenerateSalt();
        var salt2 = PasswordHasher.GenerateSalt();
        var hash1 = PasswordHasher.Hash("password", salt1);
        var hash2 = PasswordHasher.Hash("password", salt2);
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var salt = PasswordHasher.GenerateSalt();
        var hash = PasswordHasher.Hash("mypassword", salt);
        Assert.True(PasswordHasher.Verify("mypassword", salt, hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var salt = PasswordHasher.GenerateSalt();
        var hash = PasswordHasher.Hash("correct", salt);
        Assert.False(PasswordHasher.Verify("wrong", salt, hash));
    }

    [Fact]
    public void Hash_EmptyPassword_StillWorks()
    {
        var salt = PasswordHasher.GenerateSalt();
        var hash = PasswordHasher.Hash("", salt);
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void VerifyLegacySha256_CorrectPassword_ReturnsTrue()
    {
        // Simulate a legacy SHA256 hash (password + salt concatenated, SHA256'd)
        var salt = PasswordHasher.GenerateSalt();
        var input = System.Text.Encoding.UTF8.GetBytes("legacypass" + salt);
        var legacyHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(input));

        Assert.True(PasswordHasher.VerifyLegacySha256("legacypass", salt, legacyHash));
    }

    [Fact]
    public void VerifyLegacySha256_WrongPassword_ReturnsFalse()
    {
        var salt = PasswordHasher.GenerateSalt();
        var input = System.Text.Encoding.UTF8.GetBytes("legacypass" + salt);
        var legacyHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(input));

        Assert.False(PasswordHasher.VerifyLegacySha256("wrongpass", salt, legacyHash));
    }

    [Fact]
    public void Argon2id_DoesNotMatchLegacySha256()
    {
        var salt = PasswordHasher.GenerateSalt();
        var argon2Hash = PasswordHasher.Hash("testpass", salt);

        // Legacy verification should NOT match an Argon2id hash
        Assert.False(PasswordHasher.VerifyLegacySha256("testpass", salt, argon2Hash));
    }
}
