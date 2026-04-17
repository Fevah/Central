using System.Data.Common;
using System.Net;
using System.Text.RegularExpressions;

namespace Central.Api.Endpoints;

public static partial class EndpointHelpers
{
    /// <summary>
    /// Validate that all column names in a request body are in the allowed set.
    /// Prevents SQL injection via dynamic column names.
    /// Returns null if valid, or a BadRequest IResult if invalid.
    /// </summary>
    public static IResult? ValidateColumns(IEnumerable<string> requestedColumns, HashSet<string> allowedColumns)
    {
        foreach (var col in requestedColumns)
        {
            if (!allowedColumns.Contains(col))
                return Results.BadRequest(new { error = $"Column '{col}' is not allowed." });
            // Defense-in-depth: column names must be simple identifiers (lowercase for PG)
            if (!SafeIdentifierRegex().IsMatch(col.ToLowerInvariant()))
                return Results.BadRequest(new { error = $"Column '{col}' contains invalid characters." });
        }
        return null;
    }

    [GeneratedRegex(@"^[a-z_][a-z0-9_]*$")]
    private static partial Regex SafeIdentifierRegex();
    /// <summary>Read all rows from a DbDataReader into a list of dictionaries.
    /// Converts non-JSON-serializable types (IPAddress, etc.) to strings.</summary>
    public static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(DbDataReader reader)
    {
        var results = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.IsDBNull(i))
                {
                    row[reader.GetName(i)] = null;
                }
                else
                {
                    var val = reader.GetValue(i);
                    // Convert types that System.Text.Json can't serialize
                    row[reader.GetName(i)] = val switch
                    {
                        IPAddress ip => ip.ToString(),
                        _ => val
                    };
                }
            }
            results.Add(row);
        }
        return results;
    }
}
