using Central.Engine.Models;

namespace Central.Engine.Services;

/// <summary>
/// Resolves the effective AI provider + API key for a tenant + feature combo.
///
/// Resolution precedence:
///   1. Tenant's BYOK for the feature's preferred provider
///   2. Platform-provided key for the feature's preferred provider (if tenant opts in)
///   3. Tenant's default provider BYOK
///   4. Platform default provider
///   5. None — caller should error or fall back gracefully
///
/// Uses CredentialEncryptor for all stored keys (AES-256).
/// Caches resolution for 2 minutes per tenant/feature.
/// Records usage to <c>central_platform.ai_usage_log</c> which triggers
/// quota aggregation on <c>tenant_ai_providers</c>.
/// </summary>
public interface ITenantAiProviderResolver
{
    Task<AiProviderResolution?> ResolveAsync(Guid tenantId, string featureCode, CancellationToken ct = default);
    Task<string?> GetApiKeyAsync(Guid tenantId, AiProviderResolution resolution, CancellationToken ct = default);
    Task LogUsageAsync(AiUsageEntry entry, CancellationToken ct = default);
    void Invalidate(Guid tenantId);
}
