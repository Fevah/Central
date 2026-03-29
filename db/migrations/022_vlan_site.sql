-- 022_vlan_site.sql — per-site VLAN data
-- Adds site column to vlan_inventory so each site can have its own resolved rows.
-- Existing rows become 'Default' (the 10.x template).

ALTER TABLE vlan_inventory ADD COLUMN IF NOT EXISTS site TEXT NOT NULL DEFAULT 'Default';

-- Tag all existing rows as Default template
UPDATE vlan_inventory SET site = 'Default' WHERE site IS NULL OR site = '' OR site = 'Default';
