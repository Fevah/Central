using Central.Core.Auth;

namespace Central.Tests.Auth;

public class SecureStringExtensionTests
{
    [Fact]
    public void ToSecureString_EmptyString_ReturnsSecureString()
    {
        var ss = "".ToSecureString();
        Assert.NotNull(ss);
        Assert.Equal(0, ss.Length);
    }

    [Fact]
    public void ToSecureString_NonEmpty_CorrectLength()
    {
        var ss = "password".ToSecureString();
        Assert.Equal(8, ss.Length);
    }

    [Fact]
    public void ToSecureString_IsReadOnly()
    {
        var ss = "test".ToSecureString();
        Assert.True(ss.IsReadOnly());
    }

    [Fact]
    public void ToPlainText_Roundtrips()
    {
        var original = "MyP@ssw0rd!";
        var ss = original.ToSecureString();
        var plain = ss.ToPlainText();
        Assert.Equal(original, plain);
    }

    [Fact]
    public void ToPlainText_Empty_ReturnsEmpty()
    {
        var ss = "".ToSecureString();
        Assert.Equal("", ss.ToPlainText());
    }

    [Fact]
    public void ToPasswordHash_ProducesConsistentHash()
    {
        var salt = "mysalt";
        var ss1 = "password".ToSecureString();
        var ss2 = "password".ToSecureString();
        var hash1 = ss1.ToPasswordHash(salt);
        var hash2 = ss2.ToPasswordHash(salt);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ToPasswordHash_DifferentSalts_DifferentHashes()
    {
        var ss = "password".ToSecureString();
        var hash1 = ss.ToPasswordHash("salt1");
        var hash2 = "password".ToSecureString().ToPasswordHash("salt2");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyHash_CorrectPassword_ReturnsTrue()
    {
        var salt = "testsalt";
        var password = "MyP@ssw0rd!";
        var hash = password.ToSecureString().ToPasswordHash(salt);
        var result = password.ToSecureString().VerifyHash(salt, hash);
        Assert.True(result);
    }

    [Fact]
    public void VerifyHash_WrongPassword_ReturnsFalse()
    {
        var salt = "testsalt";
        var hash = "correct".ToSecureString().ToPasswordHash(salt);
        var result = "wrong".ToSecureString().VerifyHash(salt, hash);
        Assert.False(result);
    }

    [Fact]
    public void ToPasswordHash_UnicodeChars()
    {
        var ss = "Pässwörd!1".ToSecureString();
        var hash = ss.ToPasswordHash("salt");
        Assert.NotEmpty(hash);
        // Verify it's a valid Base64 string
        var bytes = Convert.FromBase64String(hash);
        Assert.Equal(32, bytes.Length); // SHA256 = 32 bytes
    }
}
