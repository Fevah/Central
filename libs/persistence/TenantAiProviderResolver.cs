using System.Collections.Concurrent;
using Central.Engine.Auth;
using Central.Engine.Models;
using Central.Engine.Services;
using Npgsql;

namespace Central.Persistence;

public class TenantAiProviderResolver : ITenantAiProviderResolver
{
    private readonly string _platformDsn;
    private readonly ConcurrentDictionary<string, (AiProviderResolution Resolution, DateTime ExpiresAt)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    public TenantAiProviderResolver(string platformDsn)
    {
        _platformDsn = platformDsn;
    }

    public async Task<AiProviderResolution?> ResolveAsync(Guid tenantId, string featureCode, CancellationToken ct = default)
    {
        var cacheKey = $"{tenantId}:{featureCode}";
        if (_cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            return cached.Resolution;

        try
        {
            await using var conn = new NpgsqlConnection(_platformDsn);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM central_platform.resolve_ai_provider(@tid, @fc)", conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("fc", featureCode);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            var resolution = new AiProviderResolution
            {
                ProviderId     = r.GetInt32(0),
                ProviderCode   = r.GetString(1),
                ModelCode      = r.IsDBNull(2) ? "" : r.GetString(2),
                KeySource      = r.GetString(3),
                HasByok        = r.GetBoolean(4),
                QuotaRemaining = r.GetInt64(5),
                CostRemaining  = r.GetDecimal(6)
            };

            _cache[cacheKey] = (resolution, DateTime.UtcNow + CacheTtl);
            return resolution;
        }
        catch { return null; }
    }

    public async Task<string?> GetApiKeyAsync(Guid tenantId, AiProviderResolution resolution, CancellationToken ct = default)
    {
        if (!resolution.IsAvailable) return null;

        try
        {
            await using var conn = new NpgsqlConnection(_platformDsn);
            await conn.OpenAsync(ct);

            if (resolution.KeySource == "tenant_byok")
            {
                await using var cmd = new NpgsqlCommand(
                    @"SELECT api_key_enc FROM central_platform.tenant_ai_providers
                      WHERE tenant_id = @tid AND provider_id = @pid", conn);
                cmd.Parameters.AddWithValue("tid", tenantId);
                cmd.Parameters.AddWithValue("pid", resolution.ProviderId);
                var enc = await cmd.ExecuteScalarAsync(ct) as string;
                return string.IsNullOrEmpty(enc) ? null : CredentialEncryptor.DecryptOrPassthrough(enc);
            }

            if (resolution.KeySource == "platform")
            {
                await using var cmd = new NpgsqlCommand(
                    "SELECT platform_key_enc FROM central_platform.ai_providers WHERE id = @pid", conn);
                cmd.Parameters.AddWithValue("pid", resolution.ProviderId);
                var enc = await cmd.ExecuteScalarAsync(ct) as string;
                return string.IsNullOrEmpty(enc) ? null : CredentialEncryptor.DecryptOrPassthrough(enc);
            }

            return null;
        }
        catch { return null; }
    }

    public async Task LogUsageAsync(AiUsageEntry entry, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_platformDsn);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO central_platform.ai_usage_log
                    (tenant_id, user_id, provider_id, model_code, feature_code, key_source,
                     input_tokens, output_tokens, cost_usd, latency_ms, success, error_code, error_message,
                     prompt_preview, response_preview, called_at)
                  VALUES (@tid, @uid, @pid, @mc, @fc, @ks, @it, @ot, @c, @l, @s, @ec, @em, @pp, @rp, @ca)", conn);
            cmd.Parameters.AddWithValue("tid", entry.TenantId);
            cmd.Parameters.AddWithValue("uid", entry.UserId.HasValue ? entry.UserId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("pid", entry.ProviderId);
            cmd.Parameters.AddWithValue("mc", entry.ModelCode);
            cmd.Parameters.AddWithValue("fc", entry.FeatureCode ?? "");
            cmd.Parameters.AddWithValue("ks", entry.KeySource);
            cmd.Parameters.AddWithValue("it", entry.InputTokens);
            cmd.Parameters.AddWithValue("ot", entry.OutputTokens);
            cmd.Parameters.AddWithValue("c", entry.CostUsd);
            cmd.Parameters.AddWithValue("l", entry.LatencyMs.HasValue ? entry.LatencyMs.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("s", entry.Success);
            cmd.Parameters.AddWithValue("ec", (object?)entry.ErrorCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("em", DBNull.Value);
            cmd.Parameters.AddWithValue("pp", DBNull.Value);
            cmd.Parameters.AddWithValue("rp", DBNull.Value);
            cmd.Parameters.AddWithValue("ca", entry.CalledAt == default ? DateTime.UtcNow : entry.CalledAt);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch { /* best-effort logging */ }
    }

    public void Invalidate(Guid tenantId)
    {
        foreach (var key in _cache.Keys.Where(k => k.StartsWith(tenantId.ToString())).ToList())
            _cache.TryRemove(key, out _);
    }
}
