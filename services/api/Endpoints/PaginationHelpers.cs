using System.Data.Common;
using System.Text.RegularExpressions;

namespace Central.Api.Endpoints;

/// <summary>
/// Shared pagination, filtering, and sorting for all list endpoints.
/// Usage: bind PaginatedQuery from query string, call BuildWhereClause / AppendPagination.
/// </summary>
public record PaginatedQuery(
    int? Offset = null,
    int? Limit = null,
    string? Sort = null,
    string? Order = null,   // "asc" or "desc"
    string? Search = null,  // Free-text search across searchable columns
    string? Filter = null   // Comma-separated "field:value" pairs
)
{
    public int EffectiveLimit => Math.Clamp(Limit ?? 100, 1, 1000);
    public int EffectiveOffset => Math.Max(Offset ?? 0, 0);
    public string EffectiveOrder => string.Equals(Order, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
}

/// <summary>Paginated response envelope with metadata.</summary>
public record PaginatedResponse<T>(
    IReadOnlyList<T> Data,
    int Total,
    int Offset,
    int Limit,
    bool HasMore
);

public static partial class PaginationHelpers
{
    // Valid SQL identifier (defense-in-depth for sort columns)
    [GeneratedRegex(@"^[a-z_][a-z0-9_]*$")]
    private static partial Regex SafeColumnRegex();

    /// <summary>
    /// Build a WHERE clause from filter string. Filter format: "field1:value1,field2:value2".
    /// Only allows columns in the provided whitelist. Returns (whereClause, parameters).
    /// </summary>
    public static (string Where, List<(string Name, object Value)> Params) BuildFilterClause(
        string? filter, HashSet<string> allowedColumns)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return ("", []);

        var clauses = new List<string>();
        var parameters = new List<(string, object)>();
        var pairs = filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int i = 0;

        foreach (var pair in pairs)
        {
            var colonIdx = pair.IndexOf(':');
            if (colonIdx < 1) continue;

            var field = pair[..colonIdx].Trim().ToLowerInvariant();
            var value = pair[(colonIdx + 1)..].Trim();

            if (!allowedColumns.Contains(field) || !SafeColumnRegex().IsMatch(field))
                continue;

            var paramName = $"f{i++}";
            // Support wildcard with ILIKE
            if (value.Contains('*'))
            {
                clauses.Add($"{field} ILIKE @{paramName}");
                parameters.Add((paramName, value.Replace('*', '%')));
            }
            else
            {
                clauses.Add($"{field} = @{paramName}");
                parameters.Add((paramName, value));
            }
        }

        var where = clauses.Count > 0 ? string.Join(" AND ", clauses) : "";
        return (where, parameters);
    }

    /// <summary>
    /// Build a search clause that searches across multiple columns with ILIKE.
    /// </summary>
    public static (string Where, List<(string Name, object Value)> Params) BuildSearchClause(
        string? search, string[] searchableColumns)
    {
        if (string.IsNullOrWhiteSpace(search) || searchableColumns.Length == 0)
            return ("", []);

        var clauses = searchableColumns
            .Select(col => $"COALESCE({col}::text, '') ILIKE @search")
            .ToArray();

        return (
            $"({string.Join(" OR ", clauses)})",
            [("search", (object)$"%{search}%")]
        );
    }

    /// <summary>
    /// Build sort clause. Validates column against whitelist.
    /// </summary>
    public static string BuildSortClause(PaginatedQuery q, string defaultSort, HashSet<string> allowedColumns)
    {
        if (!string.IsNullOrEmpty(q.Sort) && allowedColumns.Contains(q.Sort) && SafeColumnRegex().IsMatch(q.Sort))
            return $"{q.Sort} {q.EffectiveOrder}";
        return defaultSort;
    }

    /// <summary>
    /// Execute a paginated query. Returns the paginated envelope with total count.
    /// </summary>
    public static async Task<PaginatedResponse<Dictionary<string, object?>>> ExecutePaginatedAsync(
        DbDataReader reader, int total, PaginatedQuery q)
    {
        var rows = await EndpointHelpers.ReadRowsAsync(reader);
        return new PaginatedResponse<Dictionary<string, object?>>(
            rows, total, q.EffectiveOffset, q.EffectiveLimit,
            HasMore: q.EffectiveOffset + rows.Count < total);
    }
}
