using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Svg;

namespace Central.Desktop.Services;

/// <summary>
/// Renders SVG text to WPF ImageSource using Svg.NET.
/// Replaces "currentColor" with a visible color for dark backgrounds.
/// Caches rendered images by content hash for performance.
/// </summary>
public static class SvgHelper
{
    private static readonly Dictionary<int, ImageSource> _cache = new();
    private static readonly string _diskCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Central", "icon_cache");

    /// <summary>Render SVG text to a WPF ImageSource. Returns null on failure.</summary>
    public static ImageSource? RenderSvgToImageSource(string svgText, int size = 32)
    {
        if (string.IsNullOrEmpty(svgText)) return null;

        var hash = svgText.GetHashCode() ^ size;
        if (_cache.TryGetValue(hash, out var cached)) return cached;

        try
        {
            // Keep original colors — only replace "currentColor" with white for visibility
            var fixedSvg = svgText.Replace("currentColor", "#FFFFFF");

            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(fixedSvg));
            var doc = SvgDocument.Open<SvgDocument>(stream);
            doc.Width = new SvgUnit(SvgUnitType.Pixel, size);
            doc.Height = new SvgUnit(SvgUnitType.Pixel, size);

            using var bitmap = doc.Draw(size, size);
            if (bitmap == null) return null;

            // Convert System.Drawing.Bitmap → WPF BitmapSource
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = ms;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            _cache[hash] = bmp;
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Clear the in-memory render cache.</summary>
    public static void ClearCache() => _cache.Clear();

    /// <summary>Cache SVG text to local disk for fast startup (no DB call needed).</summary>
    public static void CacheToDisk(string iconName, string svgText)
    {
        try
        {
            Directory.CreateDirectory(_diskCacheDir);
            var safeName = string.Join("_", iconName.Split(Path.GetInvalidFileNameChars()));
            File.WriteAllText(Path.Combine(_diskCacheDir, $"{safeName}.svg"), svgText);
        }
        catch { }
    }

    /// <summary>Load SVG from local disk cache. Returns null if not cached.</summary>
    public static string? LoadFromDiskCache(string iconName)
    {
        try
        {
            var safeName = string.Join("_", iconName.Split(Path.GetInvalidFileNameChars()));
            var path = Path.Combine(_diskCacheDir, $"{safeName}.svg");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch { return null; }
    }
}
