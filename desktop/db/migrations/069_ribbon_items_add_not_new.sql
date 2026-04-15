-- Migration 069: Align ribbon_items CRUD labels on "Add" (the canonical term
-- used by ListViewModelBase.AddCommand and GlobalActionService.ActionAdd).
--
-- Home was seeded with "Add" but Devices / Switches / Admin were seeded with
-- "New", which showed up in the user's Ribbon Config tree as inconsistent
-- labels. GlobalActionService now canonicalises on "Add" everywhere, so align
-- these seed rows + any admin/user overrides that reference the old key.

-- 1. ribbon_items rows currently labelled "New" → rename to "Add".
--    Only affects "Actions" groups to avoid touching unrelated legitimate items.
UPDATE ribbon_items
SET content = 'Add', updated_at = NOW()
WHERE content = 'New'
  AND group_id IN (
    SELECT id FROM ribbon_groups WHERE header = 'Actions'
  );

-- 2. admin_ribbon_defaults — migrate any "<module>/new" keys to "<module>/add".
UPDATE admin_ribbon_defaults
SET item_key = regexp_replace(item_key, '/new$', '/add'),
    updated_at = NOW()
WHERE item_key LIKE '%/new';

-- 3. user_ribbon_overrides — same migration for per-user keys.
UPDATE user_ribbon_overrides
SET item_key = regexp_replace(item_key, '/new$', '/add'),
    updated_at = NOW()
WHERE item_key LIKE '%/new';

-- 4. Legacy tree-path overrides like "Devices/Actions/New" → "Devices/Actions/Add".
UPDATE user_ribbon_overrides
SET item_key = regexp_replace(item_key, '/New$', '/Add'),
    updated_at = NOW()
WHERE item_key LIKE '%/New';

UPDATE admin_ribbon_defaults
SET item_key = regexp_replace(item_key, '/New$', '/Add'),
    updated_at = NOW()
WHERE item_key LIKE '%/New';
