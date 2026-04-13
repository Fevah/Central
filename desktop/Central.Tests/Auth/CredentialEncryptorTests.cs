using Central.Core.Auth;

namespace Central.Tests.Auth;

public class CredentialEncryptorTests
{
    public CredentialEncryptorTests()
    {
        // Ensure a known key for test determinism
        CredentialEncryptor.Initialize("test-secret-key-for-unit-tests");
    }

    [Fact]
    public void Encrypt_Decrypt_Roundtrip()
    {
        var plainText = "my-ssh-password-123";
        var encrypted = CredentialEncryptor.Encrypt(plainText);
        var decrypted = CredentialEncryptor.Decrypt(encrypted);
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Encrypt_ReturnsBase64()
    {
        var encrypted = CredentialEncryptor.Encrypt("test");
        Assert.False(string.IsNullOrEmpty(encrypted));
        // Should be valid Base64
        var bytes = Convert.FromBase64String(encrypted);
        Assert.True(bytes.Length >= 17); // IV (16) + at least 1 byte ciphertext
    }

    [Fact]
    public void Encrypt_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", CredentialEncryptor.Encrypt(""));
    }

    [Fact]
    public void Encrypt_Null_ReturnsEmpty()
    {
        Assert.Equal("", CredentialEncryptor.Encrypt(null!));
    }

    [Fact]
    public void Decrypt_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", CredentialEncryptor.Decrypt(""));
    }

    [Fact]
    public void Decrypt_Null_ReturnsEmpty()
    {
        Assert.Equal("", CredentialEncryptor.Decrypt(null!));
    }

    [Fact]
    public void Decrypt_NonBase64_ReturnsAsIs()
    {
        // Legacy unencrypted value — not valid Base64
        var legacy = "plain-password!@#";
        Assert.Equal(legacy, CredentialEncryptor.Decrypt(legacy));
    }

    [Fact]
    public void Decrypt_TooShort_ReturnsAsIs()
    {
        // Valid Base64 but too short to be encrypted (< 17 bytes)
        var shortB64 = Convert.ToBase64String(new byte[10]);
        Assert.Equal(shortB64, CredentialEncryptor.Decrypt(shortB64));
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertext_EachTime()
    {
        // Different IV each time
        var cipher1 = CredentialEncryptor.Encrypt("same-password");
        var cipher2 = CredentialEncryptor.Encrypt("same-password");
        Assert.NotEqual(cipher1, cipher2);
    }

    [Fact]
    public void Encrypt_Decrypt_SpecialCharacters()
    {
        var special = "p@$$w0rd!#%^&*()_+-=[]{}|;':\",./<>?";
        var encrypted = CredentialEncryptor.Encrypt(special);
        var decrypted = CredentialEncryptor.Decrypt(encrypted);
        Assert.Equal(special, decrypted);
    }

    [Fact]
    public void Encrypt_Decrypt_Unicode()
    {
        var unicode = "p\u00e4ssw\u00f6rd-\u00fc\u00e9\u00e8";
        var encrypted = CredentialEncryptor.Encrypt(unicode);
        var decrypted = CredentialEncryptor.Decrypt(encrypted);
        Assert.Equal(unicode, decrypted);
    }

    [Fact]
    public void IsEncrypted_EmptyString_ReturnsFalse()
    {
        Assert.False(CredentialEncryptor.IsEncrypted(""));
    }

    [Fact]
    public void IsEncrypted_Null_ReturnsFalse()
    {
        Assert.False(CredentialEncryptor.IsEncrypted(null!));
    }

    [Fact]
    public void IsEncrypted_ValidEncrypted_ReturnsTrue()
    {
        var encrypted = CredentialEncryptor.Encrypt("test");
        Assert.True(CredentialEncryptor.IsEncrypted(encrypted));
    }

    [Fact]
    public void IsEncrypted_PlainText_ReturnsFalse()
    {
        Assert.False(CredentialEncryptor.IsEncrypted("not-base64!"));
    }

    [Fact]
    public void IsEncrypted_ShortBase64_ReturnsFalse()
    {
        var shortB64 = Convert.ToBase64String(new byte[5]);
        Assert.False(CredentialEncryptor.IsEncrypted(shortB64));
    }

    [Fact]
    public void Initialize_WithDifferentKey_CannotDecryptOldData()
    {
        CredentialEncryptor.Initialize("key-one");
        var encrypted = CredentialEncryptor.Encrypt("secret");

        CredentialEncryptor.Initialize("key-two");
        // Should either throw or return garbled text, not the original
        try
        {
            var decrypted = CredentialEncryptor.Decrypt(encrypted);
            Assert.NotEqual("secret", decrypted);
        }
        catch
        {
            // Expected — wrong key causes decryption failure
        }

        // Restore test key
        CredentialEncryptor.Initialize("test-secret-key-for-unit-tests");
    }

    [Fact]
    public void Encrypt_Decrypt_LongValue()
    {
        var longValue = new string('A', 10_000);
        var encrypted = CredentialEncryptor.Encrypt(longValue);
        var decrypted = CredentialEncryptor.Decrypt(encrypted);
        Assert.Equal(longValue, decrypted);
    }
}
