namespace Central.Engine.Services;

/// <summary>
/// Parses the admin-authored gallery-items string into (caption, icon) tuples.
///
/// Supported syntaxes (both inside the same string — one entry per semicolon):
///   Caption                      — tile reuses the parent gallery's glyph
///   Caption|IconName             — tile has its own icon (DX image name or
///                                  custom icon_library name)
///
/// Extra whitespace around separators is trimmed. Empty entries are skipped.
/// This is the single parser used by the admin preview, the live ribbon
/// runtime, and anywhere else that renders gallery contents from a string.
/// </summary>
public static class GalleryItemParser
{
    public static List<GalleryTile> Parse(string? raw)
    {
        var result = new List<GalleryTile>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pipeIdx = part.IndexOf('|');
            if (pipeIdx < 0)
            {
                result.Add(new GalleryTile(part, null));
            }
            else
            {
                var caption = part.Substring(0, pipeIdx).Trim();
                var icon    = part.Substring(pipeIdx + 1).Trim();
                result.Add(new GalleryTile(caption, string.IsNullOrEmpty(icon) ? null : icon));
            }
        }
        return result;
    }
}

public readonly record struct GalleryTile(string Caption, string? IconName);
