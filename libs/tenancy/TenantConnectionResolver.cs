using System.Collections.Concurrent;
using Npgsql;

namespace Central.Tenancy;

/// <summary>
/// Resolves the correct database connection for a tenant using a 3-tier lookup:
///   1. tenant_connection_map DB table (authoritative)
///   2. DNS-per-tenant (optional, for enterprise tenants with dedicated services)
///   3. Fallback to shared cluster with schema scoping (zoned tenants)
/// Results are cached with a short TTL to avoid hitting the platform DB every request.
/// </summary>
public interface ITenantConnectionResolver
{
    Task<TenantConnectionInfo> ResolveAsync(Guid tenantId, CancellationToken ct = default);
    void Invalidate(Guid tenantId);
}

public record TenantConnectionInfo(
    Guid TenantId,
    string SizingModel,        // zoned | dedicated
    string DatabaseName,       // central | central_<slug>
    string SchemaName,         // tenant_<slug> | public
    string ConnectionString,   // full NpgsqlConnectionString
    string? DnsName,
    string? K8sNamespace,
    int MaxConnections
);

public class TenantConnectionResolver : ITenantConnectionResolver
{
    private readonly string _defaultConnectionString;
    private readonly ConcurrentDictionary<Guid, (TenantConnectionInfo Info, DateTime ExpiresAt)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public TenantConnectionResolver(string defaultConnectionString)
    {
        _defaultConnectionString = defaultConnectionString;
    }

    public async Task<TenantConnectionInfo> ResolveAsync(Guid tenantId, CancellationToken ct = default)
    {
        // 1. Check cache
        if (_cache.TryGetValue(tenantId, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            return cached.Info;

        // 2. Look up tenant_connection_map (authoritative)
        var info = await LookupFromMapAsync(tenantId, ct);

        // 3. Fallback: build a zoned connection using tenant slug as schema
        info ??= await BuildFallbackAsync(tenantId, ct);

        // 4. Default-of-defaults: shared cluster, public schema
        info ??= new TenantConnectionInfo(
            tenantId, "zoned", "central", "public",
            _defaultConnectionString, null, null, 25);

        _cache[tenantId] = (info, DateTime.UtcNow + CacheTtl);
        return info;
    }

    public void Invalidate(Guid tenantId) => _cache.TryRemove(tenantId, out _);

    // ── Tier 1: tenant_connection_map table ──

    private async Task<TenantConnectionInfo?> LookupFromMapAsync(Guid tenantId, CancellationToken ct)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_defaultConnectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(
                @"SELECT sizing_model, database_name, schema_name, connection_string,
                         dns_name, k8s_namespace, max_connections
                  FROM central_platform.tenant_connection_map
                  WHERE tenant_id = @tid", conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            var sizingModel = r.GetString(0);
            var dbName = r.GetString(1);
            var schemaName = r.GetString(2);
            var customConn = r.IsDBNull(3) ? null : r.GetString(3);
            var dnsName = r.IsDBNull(4) ? null : r.GetString(4);
            var k8sNs = r.IsDBNull(5) ? null : r.GetString(5);
            var maxConn = r.GetInt32(6);

            // Build connection string
            string connStr;
            if (!string.IsNullOrEmpty(customConn))
            {
                // Tier 1a: explicit connection string stored in DB
                connStr = customConn;
            }
            else if (!string.IsNullOrEmpty(dnsName))
            {
                // Tier 2: DNS-per-tenant (swap Host + Database)
                var builder = new NpgsqlConnectionStringBuilder(_defaultConnectionString)
                {
                    Host = dnsName,
                    Database = dbName,
                    MaxPoolSize = maxConn
                };
                connStr = builder.ToString();
            }
            else
            {
                // Shared cluster, swap database name
                var builder = new NpgsqlConnectionStringBuilder(_defaultConnectionString)
                {
                    Database = dbName,
                    MaxPoolSize = maxConn
                };
                connStr = builder.ToString();
            }

            return new TenantConnectionInfo(tenantId, sizingModel, dbName, schemaName,
                connStr, dnsName, k8sNs, maxConn);
        }
        catch { return null; }
    }

    // ── Tier 3: fallback — resolve by slug, assume zoned ──

    private async Task<TenantConnectionInfo?> BuildFallbackAsync(Guid tenantId, CancellationToken ct)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_defaultConnectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(
                "SELECT slug, tier FROM central_platform.tenants WHERE id = @tid", conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            var slug = r.GetString(0);
            var schema = slug == "default" ? "public" : $"tenant_{slug}";
            return new TenantConnectionInfo(
                tenantId, "zoned", "central", schema,
                _defaultConnectionString, null, null, 25);
        }
        catch { return null; }
    }
}
