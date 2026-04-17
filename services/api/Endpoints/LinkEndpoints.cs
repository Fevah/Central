using Central.Data;

namespace Central.Api.Endpoints;

public static class LinkEndpoints
{
    private static readonly HashSet<string> P2PWritableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "region", "building", "link_id", "vlan", "device_a", "port_a",
        "device_a_ip", "device_b", "port_b", "device_b_ip", "subnet",
        "status", "desc_a", "desc_b"
    };

    private static readonly HashSet<string> B2BWritableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "link_id", "vlan", "building_a", "device_a", "port_a", "module_a",
        "device_a_ip", "building_b", "device_b", "port_b", "module_b",
        "device_b_ip", "tx", "rx", "media", "speed", "subnet", "status"
    };

    private static readonly HashSet<string> FWWritableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "building", "link_id", "vlan", "switch", "switch_port", "switch_ip",
        "firewall", "firewall_port", "firewall_ip", "subnet", "status"
    };

    public static RouteGroupBuilder MapLinkEndpoints(this RouteGroupBuilder group)
    {
        // ── P2P Links ──────────────────────────────────────────────

        group.MapGet("/p2p", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM p2p_links WHERE is_deleted IS NOT TRUE ORDER BY id";
            return Results.Ok(await ReadAllRows(cmd));
        });

        group.MapPut("/p2p/{id:int}", async (int id, Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            return await UpdateRow("p2p_links", id, body, db, P2PWritableColumns);
        });

        group.MapPost("/p2p", async (Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            return await InsertRow("p2p_links", body, db, P2PWritableColumns);
        });

        group.MapDelete("/p2p/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            return await SoftDeleteRow("p2p_links", id, db);
        });

        // ── B2B Links ──────────────────────────────────────────────

        group.MapGet("/b2b", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM b2b_links WHERE is_deleted IS NOT TRUE ORDER BY id";
            return Results.Ok(await ReadAllRows(cmd));
        });

        group.MapPut("/b2b/{id:int}", async (int id, Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            return await UpdateRow("b2b_links", id, body, db, B2BWritableColumns);
        });

        group.MapPost("/b2b", async (Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            return await InsertRow("b2b_links", body, db, B2BWritableColumns);
        });

        group.MapDelete("/b2b/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            return await SoftDeleteRow("b2b_links", id, db);
        });

        // ── FW Links ───────────────────────────────────────────────

        group.MapGet("/fw", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM fw_links WHERE is_deleted IS NOT TRUE ORDER BY id";
            return Results.Ok(await ReadAllRows(cmd));
        });

        group.MapPut("/fw/{id:int}", async (int id, Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            return await UpdateRow("fw_links", id, body, db, FWWritableColumns);
        });

        group.MapPost("/fw", async (Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            return await InsertRow("fw_links", body, db, FWWritableColumns);
        });

        group.MapDelete("/fw/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            return await SoftDeleteRow("fw_links", id, db);
        });

        return group;
    }

    // ── Shared helpers ─────────────────────────────────────────────

    private static async Task<List<Dictionary<string, object?>>> ReadAllRows(Npgsql.NpgsqlCommand cmd)
    {
        await using var reader = await cmd.ExecuteReaderAsync();
        var results = await EndpointHelpers.ReadRowsAsync(reader);
        return results;
    }

    private static async Task<IResult> UpdateRow(string table, int id, Dictionary<string, object?> body, DbConnectionFactory db, HashSet<string> allowedColumns)
    {
        if (body.Count == 0)
            return Results.BadRequest("No fields to update.");

        var invalid = EndpointHelpers.ValidateColumns(body.Keys, allowedColumns);
        if (invalid is not null) return invalid;

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();

        var setClauses = new List<string>();
        int paramIndex = 0;
        foreach (var kvp in body)
        {
            var paramName = $"p{paramIndex++}";
            setClauses.Add($"{kvp.Key} = @{paramName}");
            cmd.Parameters.AddWithValue(paramName, kvp.Value ?? DBNull.Value);
        }

        cmd.CommandText = $"UPDATE {table} SET {string.Join(", ", setClauses)} WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id";
        cmd.Parameters.AddWithValue("id", id);

        var result = await cmd.ExecuteScalarAsync();
        return result is null ? Results.NotFound() : Results.Ok(new { id });
    }

    private static async Task<IResult> InsertRow(string table, Dictionary<string, object?> body, DbConnectionFactory db, HashSet<string> allowedColumns)
    {
        if (body.Count == 0)
            return Results.BadRequest("No fields provided.");

        var invalid = EndpointHelpers.ValidateColumns(body.Keys, allowedColumns);
        if (invalid is not null) return invalid;

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();

        var columns = new List<string>();
        var paramNames = new List<string>();
        int paramIndex = 0;
        foreach (var kvp in body)
        {
            var paramName = $"p{paramIndex++}";
            columns.Add(kvp.Key);
            paramNames.Add($"@{paramName}");
            cmd.Parameters.AddWithValue(paramName, kvp.Value ?? DBNull.Value);
        }

        cmd.CommandText = $"INSERT INTO {table} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", paramNames)}) RETURNING id";

        var newId = await cmd.ExecuteScalarAsync();
        return Results.Created($"/{newId}", new { id = newId });
    }

    private static async Task<IResult> SoftDeleteRow(string table, int id, DbConnectionFactory db)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {table} SET is_deleted = true WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id";
        cmd.Parameters.AddWithValue("id", id);
        var result = await cmd.ExecuteScalarAsync();
        return result is null ? Results.NotFound() : Results.NoContent();
    }
}
