using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Central.Protection;

/// <summary>
/// HttpClientHandler with certificate pinning.
/// Validates that the server's TLS certificate matches the expected fingerprint.
/// Prevents MITM attacks even if a CA is compromised.
/// </summary>
public class CertificatePinningHandler : HttpClientHandler
{
    private readonly HashSet<string> _pinnedFingerprints;

    /// <summary>
    /// Create a handler with pinned certificate fingerprints.
    /// Fingerprints are SHA-256 hashes of the server's certificate public key (base64).
    /// </summary>
    public CertificatePinningHandler(params string[] pinnedFingerprints)
    {
        _pinnedFingerprints = new HashSet<string>(pinnedFingerprints, StringComparer.OrdinalIgnoreCase);

        if (_pinnedFingerprints.Count > 0)
        {
            ServerCertificateCustomValidationCallback = ValidateCertificate;
        }
    }

    /// <summary>Create a handler that trusts all certificates (development only).</summary>
    public static CertificatePinningHandler TrustAll()
    {
        var handler = new CertificatePinningHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        return handler;
    }

    private bool ValidateCertificate(HttpRequestMessage request, X509Certificate2? cert,
        X509Chain? chain, SslPolicyErrors errors)
    {
        if (cert == null) return false;

        // Calculate SHA-256 fingerprint of the certificate's public key
        var publicKeyBytes = cert.GetPublicKey();
        var fingerprint = Convert.ToBase64String(SHA256.HashData(publicKeyBytes));

        return _pinnedFingerprints.Contains(fingerprint);
    }

    /// <summary>
    /// Calculate the pinning fingerprint for a certificate.
    /// Use this to get the fingerprint to embed in the client configuration.
    /// </summary>
    public static string CalculateFingerprint(X509Certificate2 cert)
    {
        return Convert.ToBase64String(SHA256.HashData(cert.GetPublicKey()));
    }
}
