using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Central.Licensing;

namespace Central.Protection;

/// <summary>
/// Client-side license validation — works offline with embedded RSA public key.
/// Validates: signature integrity, hardware binding, expiry, module access.
/// Caches validated license to DPAPI-encrypted local file for offline grace period.
/// </summary>
public class ClientLicenseValidator
{
    private readonly string _publicKeyPem;
    private readonly string _cachePath;
    private LicenseValidationResult? _cachedResult;
    private DateTime _cacheExpiry;

    private static readonly TimeSpan OfflineGracePeriod = TimeSpan.FromDays(7);

    public ClientLicenseValidator(string publicKeyPem)
    {
        _publicKeyPem = publicKeyPem;
        _cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Central", "license_cache.dat");
    }

    /// <summary>Validate a license key. Uses cache if available and within grace period.</summary>
    public LicenseValidationResult Validate(string licenseKey)
    {
        var hardwareId = HardwareFingerprint.Generate();

        // Try live validation
        var svc = new LicenseKeyService("");
        var rsa = RSA.Create();
        rsa.ImportFromPem(_publicKeyPem);
        svc.LoadKeys("", ""); // Public key only mode — we only validate, not sign

        // Manual validation since LoadKeys needs both keys
        var result = ValidateWithPublicKey(licenseKey, hardwareId, rsa);

        if (result.IsValid)
        {
            CacheResult(result);
            _cachedResult = result;
            return result;
        }

        // If live validation fails, try cached result within grace period
        var cached = LoadCachedResult();
        if (cached != null && cached.IsValid && _cacheExpiry > DateTime.UtcNow)
        {
            return new LicenseValidationResult
            {
                IsValid = true,
                TenantId = cached.TenantId,
                Modules = cached.Modules,
                ExpiresAt = cached.ExpiresAt,
                ErrorMessage = "Offline mode — cached license"
            };
        }

        return result;
    }

    /// <summary>Check if a specific module is licensed.</summary>
    public bool IsModuleLicensed(string moduleCode)
    {
        return _cachedResult?.Modules?.Contains(moduleCode) == true;
    }

    /// <summary>Check if the license is in offline grace period.</summary>
    public bool IsInGracePeriod => _cacheExpiry > DateTime.UtcNow && _cachedResult?.IsValid == true;

    private LicenseValidationResult ValidateWithPublicKey(string licenseKey, string hardwareId, RSA publicKey)
    {
        try
        {
            var parts = licenseKey.Split('.');
            if (parts.Length != 2)
                return LicenseValidationResult.Invalid("Malformed license key");

            var jsonBytes = Convert.FromBase64String(parts[0]);
            var signature = Convert.FromBase64String(parts[1]);

            if (!publicKey.VerifyData(jsonBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                return LicenseValidationResult.Invalid("Invalid signature");

            var payload = JsonSerializer.Deserialize<LicensePayload>(jsonBytes);
            if (payload == null) return LicenseValidationResult.Invalid("Cannot parse payload");

            if (!string.IsNullOrEmpty(payload.HardwareId) && payload.HardwareId != hardwareId)
                return LicenseValidationResult.Invalid("License not valid for this machine");

            if (!string.IsNullOrEmpty(payload.ExpiresAt) && DateTime.Parse(payload.ExpiresAt) < DateTime.UtcNow)
                return LicenseValidationResult.Invalid("License expired");

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

    private void CacheResult(LicenseValidationResult result)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            var json = JsonSerializer.Serialize(new CachedLicense
            {
                TenantId = result.TenantId,
                Modules = result.Modules,
                ExpiresAt = result.ExpiresAt,
                CachedAt = DateTime.UtcNow,
                GraceExpiresAt = DateTime.UtcNow.Add(OfflineGracePeriod)
            });
            // DPAPI encrypt
            var encrypted = System.Security.Cryptography.ProtectedData.Protect(
                Encoding.UTF8.GetBytes(json), null,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_cachePath, encrypted);
        }
        catch { }
    }

    private LicenseValidationResult? LoadCachedResult()
    {
        try
        {
            if (!File.Exists(_cachePath)) return null;
            var encrypted = File.ReadAllBytes(_cachePath);
            var json = Encoding.UTF8.GetString(
                System.Security.Cryptography.ProtectedData.Unprotect(
                    encrypted, null, System.Security.Cryptography.DataProtectionScope.CurrentUser));
            var cached = JsonSerializer.Deserialize<CachedLicense>(json);
            if (cached == null) return null;

            _cacheExpiry = cached.GraceExpiresAt;
            return new LicenseValidationResult
            {
                IsValid = true,
                TenantId = cached.TenantId,
                Modules = cached.Modules,
                ExpiresAt = cached.ExpiresAt
            };
        }
        catch { return null; }
    }

    private class CachedLicense
    {
        public string? TenantId { get; set; }
        public string[] Modules { get; set; } = Array.Empty<string>();
        public DateTime? ExpiresAt { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime GraceExpiresAt { get; set; }
    }
}
