using System.Collections.Generic;
using System.Text.Json;
using Central.Engine.Models;

namespace Central.Module.Global.Admin;

// NOTE: RibbonGlobalActionCatalogue moved to Central.Engine.Services so that
// GlobalActionsModule (in Central.Desktop) can share the same catalogue as
// the admin tree and GlobalActionService. Reference
// <see cref="Central.Engine.Services.RibbonGlobalActionCatalogue"/> instead.

/// <summary>
/// Shared JSONB round-trip for the extended ribbon properties that live on
/// <see cref="RibbonTreeItem"/> (Tooltip, KeyTip, small/large glyphs, colour,
/// visibility binding, QAT, checked state, dropdown items, gallery columns).
/// Consumed by both <see cref="RibbonAdminTreePanel"/> and
/// <see cref="RibbonTreePanel"/> so they stay in lock-step.
/// </summary>
public static class RibbonTreeItemExtras
{
    /// <summary>Parse a JSONB document and copy values onto <paramref name="row"/>.</summary>
    public static void Apply(RibbonTreeItem row, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;

            string? Str(string k) => root.TryGetProperty(k, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            bool Bool(string k) => root.TryGetProperty(k, out var p) && p.ValueKind == JsonValueKind.True;
            bool? BoolN(string k) => root.TryGetProperty(k, out var p)
                ? (p.ValueKind == JsonValueKind.True ? true
                 : p.ValueKind == JsonValueKind.False ? false
                 : (bool?)null)
                : null;
            int? IntN(string k) => root.TryGetProperty(k, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : (int?)null;

            row.Tooltip           = Str("tooltip");
            row.KeyTip            = Str("key_tip");
            row.GlyphSmall        = Str("glyph_small");
            row.GlyphLarge        = Str("glyph_large");
            row.Color             = Str("color");
            row.VisibilityBinding = Str("visibility_binding");
            row.QatPinned         = Bool("qat_pinned");
            row.IsChecked         = BoolN("is_checked");
            row.DropdownItems     = Str("dropdown_items");
            row.GalleryColumns    = IntN("gallery_columns");
            // sort_order stored here for synthetic rows (★ Global / per-module
            // Global Actions sections) — those rows have DbId=0 so the regular
            // ribbon_pages/groups/items.sort_order column doesn't apply.
            var so = IntN("sort_order");
            if (so.HasValue) row.SortOrder = so.Value;
        }
        catch { /* malformed JSONB should not break the editor */ }
    }

    /// <summary>
    /// Serialise <paramref name="row"/>'s extended properties back to a JSON
    /// object suitable for an <c>admin_ribbon_defaults.extras</c> /
    /// <c>user_ribbon_overrides.extras</c> column write. Returns <c>null</c>
    /// when no extras are set so callers can leave the column unchanged.
    /// </summary>
    public static string? Serialize(RibbonTreeItem row)
    {
        var dict = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(row.Tooltip))           dict["tooltip"] = row.Tooltip;
        if (!string.IsNullOrEmpty(row.KeyTip))            dict["key_tip"] = row.KeyTip;
        if (!string.IsNullOrEmpty(row.GlyphSmall))        dict["glyph_small"] = row.GlyphSmall;
        if (!string.IsNullOrEmpty(row.GlyphLarge))        dict["glyph_large"] = row.GlyphLarge;
        if (!string.IsNullOrEmpty(row.Color))             dict["color"] = row.Color;
        if (!string.IsNullOrEmpty(row.VisibilityBinding)) dict["visibility_binding"] = row.VisibilityBinding;
        if (row.QatPinned)                                 dict["qat_pinned"] = true;
        if (row.IsChecked.HasValue)                        dict["is_checked"] = row.IsChecked.Value;
        if (!string.IsNullOrEmpty(row.DropdownItems))     dict["dropdown_items"] = row.DropdownItems;
        if (row.GalleryColumns.HasValue)                  dict["gallery_columns"] = row.GalleryColumns.Value;
        // Always emit sort_order for synthetic rows so reorders in the
        // ★ Global / ★ Global Actions sections persist (those rows aren't
        // backed by ribbon_pages/groups/items so the regular sort_order
        // column path doesn't apply to them).
        if (row.IsSynthetic && row.SortOrder != 0)        dict["sort_order"] = row.SortOrder;
        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict);
    }
}
