-- Port descriptions for each side of a P2P link
ALTER TABLE p2p_links ADD COLUMN IF NOT EXISTS desc_a TEXT NOT NULL DEFAULT '';
ALTER TABLE p2p_links ADD COLUMN IF NOT EXISTS desc_b TEXT NOT NULL DEFAULT '';

-- Auto-generate initial descriptions from link data
UPDATE p2p_links SET
    desc_a = TRIM(device_a || CASE WHEN port_a <> '' THEN ' ' || port_a ELSE '' END) || ' → ' || device_b,
    desc_b = TRIM(device_b || CASE WHEN port_b <> '' THEN ' ' || port_b ELSE '' END) || ' → ' || device_a
WHERE desc_a = '' AND device_a <> '';
