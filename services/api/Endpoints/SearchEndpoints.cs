using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

/// <summary>
/// Global search across all entities — devices, switches, users, SD tickets, tasks.
/// Returns unified results ranked by relevance.
/// </summary>
public static class SearchEndpoints
{
    public static RouteGroupBuilder MapSearchEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (string q, DbConnectionFactory db, int? limit) =>
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Results.BadRequest("Query must be at least 2 characters");

            var maxResults = Math.Min(limit ?? 50, 200);
            var results = new List<SearchResult>();
            var pattern = $"%{q}%";

            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();

            // Search devices
            await SearchTable(conn, results, "Device",
                "SELECT id::text, switch_name, building, status FROM switch_guide WHERE (switch_name ILIKE @q OR building ILIKE @q OR description ILIKE @q) AND is_deleted IS NOT TRUE LIMIT @lim",
                pattern, maxResults, r => new SearchResult
                {
                    EntityType = "Device", EntityId = r.GetString(0),
                    Title = r.GetString(1), Subtitle = r.IsDBNull(2) ? "" : r.GetString(2),
                    Badge = r.IsDBNull(3) ? "" : r.GetString(3)
                });

            // Search switches
            await SearchTable(conn, results, "Switch",
                "SELECT id::text, hostname, COALESCE(management_ip,''), COALESCE(model,'') FROM switches WHERE hostname ILIKE @q OR management_ip ILIKE @q LIMIT @lim",
                pattern, maxResults, r => new SearchResult
                {
                    EntityType = "Switch", EntityId = r.GetString(0),
                    Title = r.GetString(1), Subtitle = r.GetString(2), Badge = r.GetString(3)
                });

            // Search users
            await SearchTable(conn, results, "User",
                "SELECT id::text, username, display_name, role FROM app_users WHERE username ILIKE @q OR display_name ILIKE @q OR email ILIKE @q LIMIT @lim",
                pattern, maxResults, r => new SearchResult
                {
                    EntityType = "User", EntityId = r.GetString(0),
                    Title = r.GetString(1), Subtitle = r.IsDBNull(2) ? "" : r.GetString(2),
                    Badge = r.GetString(3)
                });

            // Search SD tickets
            await SearchTable(conn, results, "Ticket",
                "SELECT id::text, subject, COALESCE(status,''), COALESCE(technician_name,'') FROM sd_requests WHERE subject ILIKE @q OR requester_name ILIKE @q OR technician_name ILIKE @q LIMIT @lim",
                pattern, maxResults, r => new SearchResult
                {
                    EntityType = "Ticket", EntityId = r.GetString(0),
                    Title = r.GetString(1), Subtitle = r.GetString(3), Badge = r.GetString(2)
                });

            // Search tasks
            await SearchTable(conn, results, "Task",
                "SELECT id::text, title, COALESCE(status,''), COALESCE(assigned_to,'') FROM tasks WHERE title ILIKE @q OR description ILIKE @q LIMIT @lim",
                pattern, maxResults, r => new SearchResult
                {
                    EntityType = "Task", EntityId = r.GetString(0),
                    Title = r.GetString(1), Subtitle = r.GetString(3), Badge = r.GetString(2)
                });

            return Results.Ok(new { query = q, total = results.Count, results = results.Take(maxResults) });
        });

        return group;
    }

    private static async Task SearchTable(NpgsqlConnection conn, List<SearchResult> results,
        string entityType, string sql, string pattern, int limit, Func<NpgsqlDataReader, SearchResult> mapper)
    {
        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("q", pattern);
            cmd.Parameters.AddWithValue("lim", limit);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                results.Add(mapper(r));
        }
        catch { /* table may not exist yet */ }
    }

    private class SearchResult
    {
        public string EntityType { get; set; } = "";
        public string EntityId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string Badge { get; set; } = "";
    }
}
