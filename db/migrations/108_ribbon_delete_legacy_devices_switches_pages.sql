-- ═════════════════════════════════════════════════════════════════════════
-- Migration 108: delete the legacy "Devices" + "Switches" ribbon pages
-- ═════════════════════════════════════════════════════════════════════════
--
-- Both pages were seeded in the pre-merge world (migration 030 +
-- earlier) when Devices and Switches were standalone modules. After
-- migration 083 folded Devices into the Networking module
-- (2026-04-17), the Networking module's in-code registrar
-- (NetworkingRibbonRegistrar.cs) builds a single "Networking" tab
-- with Devices + Switches as *groups* inside it. The hardcoded
-- RibbonPage sections in MainWindow.xaml have also been removed.
-- The DB rows are now pure legacy clutter.
--
-- This migration DELETEs the rows (not soft-hide). FK cascade from
-- ribbon_pages → ribbon_groups → ribbon_items removes children
-- automatically. user_ribbon_overrides.item_key is a text column
-- with no real FK, so any orphaned override rows simply won't
-- resolve to a live item — harmless.
--
-- Companion changes in the same commit series:
--   - apps/desktop/MainWindow.xaml: Devices + Switches <RibbonPage>
--     blocks removed (~449 lines).
--   - apps/desktop/MainWindow.xaml.cs: ApplyRibbonPermissions uses
--     FindName-with-null-check for both tab names so the mapping
--     survives the XAML removal.

BEGIN;

DELETE FROM public.ribbon_pages
 WHERE header IN ('Devices', 'Switches')
   AND is_system = true;

-- Clean up any user_ribbon_overrides whose item_key prefix ties
-- to those pages (best-effort — keys follow "Devices/<Group>/<Item>"
-- convention in the writer). No FK cascade so do it explicitly.
DELETE FROM public.user_ribbon_overrides
 WHERE item_key LIKE 'Devices/%'
    OR item_key LIKE 'Switches/%';

-- schema_versions record
INSERT INTO public.schema_versions (version_number, description)
VALUES ('108_ribbon_delete_legacy_devices_switches_pages',
        'Delete legacy Devices + Switches ribbon pages after Networking merge')
ON CONFLICT (version_number) DO NOTHING;

COMMIT;
