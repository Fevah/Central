namespace Central.Data;

/// <summary>
/// Engine icon service. Loads icons from DB, caches in memory.
/// Supports admin-assigned defaults + per-user overrides.
///
/// Resolution order: User override → Admin assignment → DX built-in fallback
///
/// Usage:
///   var icon = IconService.Instance.GetIcon("devices:add", userId);
///   var categories = IconService.Instance.GetCategories();
///   var icons = IconService.Instance.Search("server", "Hardware", "32x32");
/// </summary>
public class IconService
{
    private static IconService? _instance;
    public static IconService Instance => _instance ??= new();

    // Cache: icon_id → byte[] PNG data
    private readonly Dictionary<int, byte[]> _cache = new();

    // Cache: (element_type, element_key) → icon_id (admin defaults)
    private readonly Dictionary<string, int> _adminAssignments = new();

    // Cache: (user_id, element_type, element_key) → icon_id
    private readonly Dictionary<string, int> _userOverrides = new();

    // Metadata cache
    private readonly List<IconInfo> _allIcons = new();
    private bool _loaded;

    /// <summary>All loaded icons (metadata only, not bytes).</summary>
    public IReadOnlyList<IconInfo> AllIcons => _allIcons;

    /// <summary>True after LoadFromDbAsync completes.</summary>
    public bool IsLoaded => _loaded;

    /// <summary>Load all icon metadata + assignments from DB. Call once at startup.</summary>
    public async Task LoadFromDbAsync(string connectionString)
    {
        _allIcons.Clear();
        _adminAssignments.Clear();
        _userOverrides.Clear();
        _cache.Clear();

        using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // Load icon metadata (not bytes — those are loaded on demand)
        using var cmd = new Npgsql.NpgsqlCommand(
            "SELECT id, name, category, subcategory, size, COALESCE(icon_set,'') FROM icon_library ORDER BY icon_set, category, name", conn);
        cmd.CommandTimeout = 10; // Don't block startup if icons table is huge
        using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            _allIcons.Add(new IconInfo(rdr.GetInt32(0), rdr.GetString(1), rdr.GetString(2),
                rdr.IsDBNull(3) ? "" : rdr.GetString(3), rdr.GetString(4),
                rdr.IsDBNull(5) ? "" : rdr.GetString(5)));
        await rdr.CloseAsync();

        // Load admin assignments (table may not exist yet)
        try
        {
            using var aCmd = new Npgsql.NpgsqlCommand(
                "SELECT element_type, element_key, icon_id FROM ribbon_icon_assignments WHERE icon_id IS NOT NULL", conn);
            using var aRdr = await aCmd.ExecuteReaderAsync();
            while (await aRdr.ReadAsync())
                _adminAssignments[$"{aRdr.GetString(0)}:{aRdr.GetString(1)}"] = aRdr.GetInt32(2);
            await aRdr.CloseAsync();
        }
        catch { /* table doesn't exist yet — skip */ }

        _loaded = true;
    }

    /// <summary>Load user-specific overrides.</summary>
    public async Task LoadUserOverridesAsync(string connectionString, int userId)
    {
        _userOverrides.Clear();
        try
        {
            using var conn = new Npgsql.NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            using var cmd = new Npgsql.NpgsqlCommand(
                "SELECT element_type, element_key, icon_id FROM user_icon_overrides WHERE user_id = @uid AND icon_id IS NOT NULL", conn);
            cmd.Parameters.AddWithValue("uid", userId);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                _userOverrides[$"{userId}:{rdr.GetString(0)}:{rdr.GetString(1)}"] = rdr.GetInt32(2);
        }
        catch { /* table doesn't exist yet — skip */ }
    }

    /// <summary>Resolve icon for an element. Returns PNG bytes or null.</summary>
    public async Task<byte[]?> GetIconBytesAsync(string connectionString, string elementType, string elementKey, int userId = 0)
    {
        // Check user override first
        if (userId > 0 && _userOverrides.TryGetValue($"{userId}:{elementType}:{elementKey}", out var userIconId))
            return await LoadIconDataAsync(connectionString, userIconId);

        // Check admin assignment
        if (_adminAssignments.TryGetValue($"{elementType}:{elementKey}", out var adminIconId))
            return await LoadIconDataAsync(connectionString, adminIconId);

        return null; // Fallback to DX built-in
    }

    /// <summary>Get distinct categories.</summary>
    public List<string> GetCategories() => _allIcons.Select(i => i.Category).Distinct().OrderBy(c => c).ToList();

    /// <summary>Get distinct icon sets (packs).</summary>
    public List<string> GetIconSets() => _allIcons.Select(i => i.IconSet).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(s => s).ToList();

    /// <summary>Get categories filtered by icon set.</summary>
    public List<string> GetCategories(string? iconSet) =>
        (string.IsNullOrEmpty(iconSet) ? _allIcons : _allIcons.Where(i => i.IconSet == iconSet))
        .Select(i => i.Category).Distinct().OrderBy(c => c).ToList();

    /// <summary>Search icons by name/category/size.</summary>
    public List<IconInfo> Search(string? nameFilter = null, string? category = null, string? size = null)
    {
        var q = _allIcons.AsEnumerable();
        if (!string.IsNullOrEmpty(nameFilter))
            q = q.Where(i => i.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(category))
            q = q.Where(i => i.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(size))
            q = q.Where(i => i.Size == size);
        return q.ToList();
    }

    /// <summary>Total icon count in DB.</summary>
    public int TotalCount => _allIcons.Count;

    /// <summary>Max icons allowed (cap at 20,000).</summary>
    public const int MaxIcons = 20000;

    /// <summary>Delete an icon from DB and cache.</summary>
    public async Task DeleteIconAsync(string connectionString, int iconId)
    {
        using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        using var cmd = new Npgsql.NpgsqlCommand("DELETE FROM icon_library WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", iconId);
        await cmd.ExecuteNonQueryAsync();
        _cache.Remove(iconId);
        _allIcons.RemoveAll(i => i.Id == iconId);
    }

    /// <summary>Import an SVG icon into the library.</summary>
    public async Task<int> ImportSvgAsync(string connectionString, string name, string category, string svgContent, string size = "svg")
    {
        if (_allIcons.Count >= MaxIcons) return -1; // cap reached

        using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        using var cmd = new Npgsql.NpgsqlCommand(
            @"INSERT INTO icon_library (name, category, size, svg_data, icon_format)
              VALUES (@n, @c, @s, @svg, 'svg')
              ON CONFLICT DO NOTHING RETURNING id", conn);
        cmd.Parameters.AddWithValue("n", name);
        cmd.Parameters.AddWithValue("c", category);
        cmd.Parameters.AddWithValue("s", size);
        cmd.Parameters.AddWithValue("svg", svgContent);
        var result = await cmd.ExecuteScalarAsync();
        if (result is int id)
        {
            _allIcons.Add(new IconInfo(id, name, category, "", size, ""));
            return id;
        }
        return -1;
    }

    /// <summary>Bulk import SVG icons from a directory.</summary>
    public async Task<int> BulkImportSvgDirectoryAsync(string connectionString, string directory, string category, int maxImport = 1000)
    {
        if (!System.IO.Directory.Exists(directory)) return 0;
        var files = System.IO.Directory.GetFiles(directory, "*.svg");
        int imported = 0;
        foreach (var file in files.Take(Math.Min(maxImport, MaxIcons - _allIcons.Count)))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(file);
            var svg = await System.IO.File.ReadAllTextAsync(file);
            var id = await ImportSvgAsync(connectionString, name, category, svg);
            if (id > 0) imported++;
        }
        return imported;
    }

    private async Task<byte[]?> LoadIconDataAsync(string connectionString, int iconId)
    {
        if (_cache.TryGetValue(iconId, out var cached)) return cached;

        using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        using var cmd = new Npgsql.NpgsqlCommand("SELECT icon_data FROM icon_library WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", iconId);
        var data = await cmd.ExecuteScalarAsync() as byte[];
        if (data != null) _cache[iconId] = data;
        return data;
    }
}

public record IconInfo(int Id, string Name, string Category, string Subcategory, string Size, string IconSet);
