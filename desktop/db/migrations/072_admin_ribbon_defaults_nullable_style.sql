-- Migration 072: allow NULL in admin_ribbon_defaults.display_style.
--
-- The COALESCE-based upsert pattern in UpsertAdminRibbonDefaultAsync sends
-- NULL to mean "no change — preserve existing". That collided with the
-- NOT NULL constraint introduced in migration 032 (default 'large'), causing
-- Push All Defaults to throw 23502 any time a row had no explicit style.
-- NULL now means "no style override — let the module registration decide".

ALTER TABLE admin_ribbon_defaults
    ALTER COLUMN display_style DROP NOT NULL,
    ALTER COLUMN display_style DROP DEFAULT;
