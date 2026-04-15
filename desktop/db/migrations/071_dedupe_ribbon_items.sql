-- Migration 071: collapse duplicate ribbon_items rows.
--
-- The runtime SyncModuleRibbonToDbAsync helper had a bug: when walking the
-- live ribbon it always INSERTed a new row per button instead of UPDATE-ing
-- the existing one, so every app load multiplied Add / Edit / Delete / Undo /
-- Redo across Home, Devices, Switches, Admin, Tasks, Routing, Service Desk,
-- Global Admin, VLANs.
--
-- Keep the row with the LOWEST id for each (group_id, content) pair — that's
-- typically the oldest legitimate seed row and preserves any icon / hidden
-- overrides the admin set before the dupes started accumulating. Also normal-
-- ises sort_order so the remaining rows have a deterministic gap-free order.

-- 1. Drop FKs pointing at ribbon_items so deletes aren't blocked. None exist
--    today outside CASCADE from ribbon_groups, but be explicit.

-- 2. Delete duplicates — keep MIN(id) per (group_id, content).
DELETE FROM ribbon_items a
USING  ribbon_items b
WHERE  a.group_id = b.group_id
  AND  LOWER(a.content) = LOWER(b.content)
  AND  a.id > b.id;

-- 3. Re-number sort_order within each group to keep the UI order clean.
WITH ranked AS (
    SELECT id,
           ROW_NUMBER() OVER (PARTITION BY group_id ORDER BY sort_order, id) - 1 AS rn
      FROM ribbon_items
)
UPDATE ribbon_items ri
SET    sort_order = ranked.rn * 10,
       updated_at = NOW()
  FROM ranked
 WHERE ri.id = ranked.id;
