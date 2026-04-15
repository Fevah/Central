-- Migration 070: extend admin_ribbon_defaults / user_ribbon_overrides with a
-- JSONB "extras" column so the ribbon admin tree can author the full set of
-- DX RibbonControl properties without needing one schema column per property.
--
-- Layout of the JSONB document (all keys optional):
--   {
--     "tooltip":     "Save the current layout",
--     "key_tip":     "K",                  -- keyboard accelerator hint
--     "glyph_small": "Save_16x16",         -- separate from large_glyph
--     "glyph_large": "Save_32x32",
--     "color":       "#3B82F6",            -- contextual tab colour
--     "visibility_binding": "{Binding IsLinksPanelActive}",
--     "qat_pinned":  true,                 -- show in Quick Access Toolbar
--     "is_checked":  false,                -- default state for check/toggle
--     "dropdown_items": ["panel:Devices","action:RefreshAll"],  -- split/sub-menu children
--     "gallery_columns": 6,                -- gallery layout
--     "ribbon_style":   "Large"            -- duplicate of display_style for back-compat
--   }
--
-- Extras are merged at ribbon build time on top of the column-stored fields
-- (default_icon, default_text, display_style, link_target). Adding a new
-- ribbon property in future = adding a key to this JSONB; no further DDL.

ALTER TABLE admin_ribbon_defaults
    ADD COLUMN IF NOT EXISTS extras JSONB DEFAULT '{}'::jsonb;

ALTER TABLE user_ribbon_overrides
    ADD COLUMN IF NOT EXISTS extras JSONB DEFAULT '{}'::jsonb;

-- Backfill nulls just in case.
UPDATE admin_ribbon_defaults SET extras = '{}'::jsonb WHERE extras IS NULL;
UPDATE user_ribbon_overrides  SET extras = '{}'::jsonb WHERE extras IS NULL;
