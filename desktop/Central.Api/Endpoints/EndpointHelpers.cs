using System.Data.Common;
using System.Net;

namespace Central.Api.Endpoints;

public static class EndpointHelpers
{
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
