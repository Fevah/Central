using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Central.Desktop.Services;

/// <summary>
/// DevExpress built-in SVG Image Gallery — enumerates icons from DevExpress.Images assembly.
/// Theme-aware vector icons that adapt to the current DX theme.
///
/// Usage in XAML:  Glyph="{dx:DXImage 'SvgImages/Actions/Open2.svg'}"
/// Usage in code:  DxSvgGallery.GetSvgImage("Actions", "Open2")
/// </summary>
public static class DxSvgGallery
{
    private static List<DxSvgIcon>? _allIcons;
    private static readonly object _lock = new();

    /// <summary>Import DX SVG icons into the icon_library DB table (runs once if empty).</summary>
    public static async Task<int> SeedToDbIfNeededAsync(string connectionString)
    {
        try
        {
            await using var conn = new Npgsql.NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            // Check if any DevExpress icons exist
            await using var checkCmd = new Npgsql.NpgsqlCommand("SELECT count(*) FROM icon_library WHERE icon_set = 'DevExpress'", conn);
            var existing = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            if (existing > 0) return 0; // Already seeded

            var icons = AllIcons;
            if (icons.Count == 0) return 0;

            int imported = 0;
            var asm = typeof(DevExpress.Utils.Svg.SvgImage).Assembly;
            var resNames = asm.GetManifestResourceNames();

            foreach (var resName in resNames)
            {
                if (!resName.EndsWith(".resources")) continue;
                try
                {
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream == null) continue;
                    using var reader = new System.Resources.ResourceReader(stream);
                    foreach (System.Collections.DictionaryEntry entry in reader)
                    {
                        var key = entry.Key?.ToString() ?? "";
                        if (!key.StartsWith("svgimages/", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!key.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) continue;

                        var parts = key.Split('/');
                        if (parts.Length < 3) continue;
                        var category = char.ToUpper(parts[1][0]) + parts[1][1..];
                        var name = Path.GetFileNameWithoutExtension(parts[^1]);

                        string? svgText = null;
                        if (entry.Value is byte[] bytes) svgText = System.Text.Encoding.UTF8.GetString(bytes);
                        else if (entry.Value is Stream s) { using var sr = new StreamReader(s); svgText = sr.ReadToEnd(); }
                        if (string.IsNullOrEmpty(svgText)) continue;

                        try
                        {
                            await using var cmd = new Npgsql.NpgsqlCommand(
                                @"INSERT INTO icon_library (name, category, icon_set, svg_source, size)
                                  VALUES (@n, @c, 'DevExpress', @svg, 'svg')
                                  ON CONFLICT (name, category, size, icon_set) DO NOTHING", conn);
                            cmd.Parameters.AddWithValue("n", name);
                            cmd.Parameters.AddWithValue("c", category);
                            cmd.Parameters.AddWithValue("svg", svgText);
                            await cmd.ExecuteNonQueryAsync();
                            imported++;
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return imported;
        }
        catch { return 0; }
    }

    /// <summary>All DX SVG icons discovered from the embedded resources.</summary>
    public static List<DxSvgIcon> AllIcons
    {
        get
        {
            if (_allIcons == null)
                lock (_lock)
                    _allIcons ??= DiscoverIcons();
            return _allIcons;
        }
    }

    /// <summary>Get distinct categories.</summary>
    public static List<string> GetCategories() =>
        AllIcons.Select(i => i.Category).Distinct().OrderBy(c => c).ToList();

    /// <summary>Build a DX SVG image URI.</summary>
    public static string BuildUri(string category, string iconName) =>
        $"SvgImages/{category}/{iconName}.svg";

    /// <summary>Load a DX SVG icon as SvgImage for ribbon Glyph.</summary>
    public static DevExpress.Utils.Svg.SvgImage? GetSvgImage(string category, string iconName)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/DevExpress.Images.v25.2;component/SvgImages/{category}/{iconName}.svg", UriKind.Absolute);
            return DevExpress.Xpf.Core.SvgImageHelper.CreateImage(uri);
        }
        catch { return null; }
    }

    /// <summary>Render a DX SVG icon to a WPF BitmapSource at the given size (for picker display).</summary>
    /// <summary>Render a DX SVG icon to a WPF ImageSource at the given size (for picker display).
    /// Extracts the SVG XML from the embedded resource and renders via Svg.NET.</summary>
    public static ImageSource? RenderToBitmap(string resourceKey, int size = 32)
    {
        try
        {
            var asm = typeof(DevExpress.Utils.Svg.SvgImage).Assembly;
            foreach (var resName in asm.GetManifestResourceNames())
            {
                if (!resName.EndsWith(".resources")) continue;
                using var stream = asm.GetManifestResourceStream(resName);
                if (stream == null) continue;
                using var reader = new System.Resources.ResourceReader(stream);
                foreach (System.Collections.DictionaryEntry entry in reader)
                {
                    if (!string.Equals(entry.Key?.ToString(), resourceKey, StringComparison.OrdinalIgnoreCase)) continue;
                    if (entry.Value is byte[] svgBytes)
                    {
                        var svgText = System.Text.Encoding.UTF8.GetString(svgBytes);
                        return SvgHelper.RenderSvgToImageSource(svgText, size);
                    }
                    if (entry.Value is Stream svgStream)
                    {
                        using var sr = new StreamReader(svgStream);
                        return SvgHelper.RenderSvgToImageSource(sr.ReadToEnd(), size);
                    }
                }
            }
        }
        catch { }
        return null;
    }

    /// <summary>Discover all SVG icons from the DevExpress.Images assembly's resource stream.</summary>
    private static List<DxSvgIcon> DiscoverIcons()
    {
        var icons = new List<DxSvgIcon>();
        try
        {
            // The images assembly embeds all icons as BAML resources
            // We can discover them via the resource manager
            var asm = typeof(DevExpress.Utils.Svg.SvgImage).Assembly;
            var resourceNames = asm.GetManifestResourceNames();

            foreach (var resName in resourceNames)
            {
                if (!resName.EndsWith(".resources")) continue;
                try
                {
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream == null) continue;
                    using var reader = new System.Resources.ResourceReader(stream);
                    foreach (System.Collections.DictionaryEntry entry in reader)
                    {
                        var key = entry.Key?.ToString() ?? "";
                        // SVG icons have keys like: svgimages/actions/open2.svg
                        if (!key.StartsWith("svgimages/", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!key.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) continue;

                        var parts = key.Split('/');
                        if (parts.Length < 3) continue;

                        var category = parts[1];
                        var name = Path.GetFileNameWithoutExtension(parts[^1]);

                        // Capitalize category
                        category = char.ToUpper(category[0]) + category[1..];

                        icons.Add(new DxSvgIcon
                        {
                            Name = name,
                            Category = category,
                            ResourceKey = key,
                            Uri = $"SvgImages/{category}/{name}.svg"
                        });
                    }
                }
                catch { }
            }
        }
        catch { }

        return icons.OrderBy(i => i.Category).ThenBy(i => i.Name).ToList();
    }
}

public class DxSvgIcon
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string ResourceKey { get; set; } = "";
    public string Uri { get; set; } = "";

    /// <summary>For display in the picker grid.</summary>
    public override string ToString() => Name;
}
