using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Central.Licensing;

/// <summary>
/// Generates and validates RSA-signed license keys for client binary protection.
/// License keys contain: tenant_id, modules[], hardware_id, expires_at.
/// The private key stays on the server; the public key is embedded in client binaries.
/// </summary>
public class LicenseKeyService
{
    private readonly string _platformDsn;
    private RSA? _privateKey;
    private RSA? _publicKey;

    public LicenseKeyService(string platformDsn) => _platformDsn = platformDsn;

    /// <summary>Load RSA keys. Call once at startup.</summary>
    public void LoadKeys(string privateKeyPem, string publicKeyPem)
    {
        _privateKey = RSA.Create();
        _privateKey.ImportFromPem(privateKeyPem);

        _publicKey = RSA.Create();
        _publicKey.ImportFromPem(publicKeyPem);
    }

    /// <summary>Generate a new RSA-4096 key pair.</summary>
    public static (string PrivateKey, string PublicKey) GenerateKeyPair()
    {
        using var rsa = RSA.Create(4096);
        return (
            rsa.ExportRSAPrivateKeyPem(),
            rsa.ExportRSAPublicKeyPem()
        );
    }

    /// <summary>Issue a signed license key for a tenant + hardware.</summary>
    public async Task<string> IssueLicenseAsync(Guid tenantId, string hardwareId, string[] modules, DateTime? expiresAt = null)
    {
        if (_privateKey == null) throw new InvalidOperationException("Private key not loaded");

        var payload = new LicensePayload
        {
            TenantId = tenantId.ToString(),
            HardwareId = hardwareId,
            Modules = modules,
            ExpiresAt = expiresAt?.ToString("O"),
            IssuedAt = DateTime.UtcNow.ToString("O"),
            KeyId = Guid.NewGuid().ToString("N")
        };

        var json = JsonSerializer.Serialize(payload);
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var signature = _privateKey.SignData(jsonBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var licenseKey = $"{Convert.ToBase64String(jsonBytes)}.{Convert.ToBase64String(signature)}";

        // Store hash in DB
        var keyHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(licenseKey)));
        await using var conn = new NpgsqlConnection(_platformDsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO central_platform.license_keys (tenant_id, key_hash, hardware_id, expires_at)
              VALUES (@tid, @hash, @hw, @exp)", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("hash", keyHash);
        cmd.Parameters.AddWithValue("hw", hardwareId);
        cmd.Parameters.AddWithValue("exp", (object?)expiresAt ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();

        return licenseKey;
    }

    /// <summary>Validate a license key (can be done offline with public key).</summary>
    public LicenseValidationResult ValidateLicense(string licenseKey, string currentHardwareId)
    {
        if (_publicKey == null) throw new InvalidOperationException("Public key not loaded");

        try
        {
            var parts = licenseKey.Split('.');
            if (parts.Length != 2) return LicenseValidationResult.Invalid("Malformed license key");

            var jsonBytes = Convert.FromBase64String(parts[0]);
            var signature = Convert.FromBase64String(parts[1]);

            // Verify signature
            if (!_publicKey.VerifyData(jsonBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                return LicenseValidationResult.Invalid("Invalid signature — license key tampered");

            var payload = JsonSerializer.Deserialize<LicensePayload>(jsonBytes);
            if (payload == null) return LicenseValidationResult.Invalid("Cannot parse license payload");

            // Check hardware binding
            if (!string.IsNullOrEmpty(payload.HardwareId) && payload.HardwareId != currentHardwareId)
                return LicenseValidationResult.Invalid("License not valid for this machine");

            // Check expiry
            if (!string.IsNullOrEmpty(payload.ExpiresAt) && DateTime.Parse(payload.ExpiresAt) < DateTime.UtcNow)
                return LicenseValidationResult.Invalid("License has expired");

            return new LicenseValidationResult
            {
                IsValid = true,
                TenantId = payload.TenantId,
                Modules = payload.Modules ?? Array.Empty<string>(),
                ExpiresAt = string.IsNullOrEmpty(payload.ExpiresAt) ? null : DateTime.Parse(payload.ExpiresAt)
            };
        }
        catch (Exception ex)
        {
            return LicenseValidationResult.Invalid($"Validation error: {ex.Message}");
        }
    }

    /// <summary>Revoke a license key server-side.</summary>
    public async Task RevokeLicenseAsync(Guid tenantId, string hardwareId)
    {
        await using var conn = new NpgsqlConnection(_platformDsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE central_platform.license_keys SET is_revoked = true WHERE tenant_id = @tid AND hardware_id = @hw", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("hw", hardwareId);
        await cmd.ExecuteNonQueryAsync();
    }
}

public class LicensePayload
{
    public string TenantId { get; set; } = "";
    public string HardwareId { get; set; } = "";
    public string[]? Modules { get; set; }
    public string? ExpiresAt { get; set; }
    public string? IssuedAt { get; set; }
    public string? KeyId { get; set; }
}

public class LicenseValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TenantId { get; set; }
    public string[] Modules { get; set; } = Array.Empty<string>();
    public DateTime? ExpiresAt { get; set; }

    public static LicenseValidationResult Invalid(string msg) => new() { IsValid = false, ErrorMessage = msg };
}
