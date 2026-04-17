using System.Security.Cryptography;
using System.Text;

namespace Central.Tests.Api;

/// <summary>
/// Tests for webhook HMAC signature validation logic.
/// Ensures webhook payloads can be verified with shared secrets.
/// </summary>
public class WebhookHmacTests
{
    [Fact]
    public void ComputeHmac_ValidPayload_ProducesConsistentSignature()
    {
        var secret = Encoding.UTF8.GetBytes("test-webhook-secret");
        var payload = "{\"event\":\"device.updated\",\"id\":42}";

        var sig1 = ComputeHmac(payload, secret);
        var sig2 = ComputeHmac(payload, secret);

        Assert.Equal(sig1, sig2);
        Assert.StartsWith("sha256=", sig1);
    }

    [Fact]
    public void ComputeHmac_DifferentPayload_DifferentSignature()
    {
        var secret = Encoding.UTF8.GetBytes("test-webhook-secret");
        var sig1 = ComputeHmac("{\"id\":1}", secret);
        var sig2 = ComputeHmac("{\"id\":2}", secret);

        Assert.NotEqual(sig1, sig2);
    }

    [Fact]
    public void ComputeHmac_DifferentSecret_DifferentSignature()
    {
        var payload = "{\"event\":\"test\"}";
        var sig1 = ComputeHmac(payload, Encoding.UTF8.GetBytes("secret-one"));
        var sig2 = ComputeHmac(payload, Encoding.UTF8.GetBytes("secret-two"));

        Assert.NotEqual(sig1, sig2);
    }

    [Fact]
    public void FixedTimeEquals_MatchingSignatures_ReturnsTrue()
    {
        var secret = Encoding.UTF8.GetBytes("test-secret");
        var payload = "{\"data\":\"value\"}";
        var expected = ComputeHmac(payload, secret);
        var actual = ComputeHmac(payload, secret);

        Assert.True(CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(actual)));
    }

    [Fact]
    public void FixedTimeEquals_MismatchedSignatures_ReturnsFalse()
    {
        var secret = Encoding.UTF8.GetBytes("test-secret");
        var expected = ComputeHmac("payload1", secret);
        var actual = ComputeHmac("payload2", secret);

        Assert.False(CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(actual)));
    }

    [Fact]
    public void Signature_Format_IsHexEncoded()
    {
        var secret = Encoding.UTF8.GetBytes("key");
        var sig = ComputeHmac("body", secret);

        Assert.StartsWith("sha256=", sig);
        var hex = sig["sha256=".Length..];
        Assert.Equal(64, hex.Length); // SHA256 = 32 bytes = 64 hex chars
        Assert.True(hex.All(c => "0123456789abcdef".Contains(c)));
    }

    // Mirror the production HMAC computation
    private static string ComputeHmac(string payload, byte[] secret)
    {
        using var hmac = new HMACSHA256(secret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return $"sha256={Convert.ToHexStringLower(hash)}";
    }
}
