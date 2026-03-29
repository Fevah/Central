-- ASN definitions table — one row per distinct ASN with description and type
CREATE TABLE IF NOT EXISTS asn_definitions (
    id          SERIAL PRIMARY KEY,
    asn         TEXT NOT NULL UNIQUE,
    description TEXT NOT NULL DEFAULT '',
    asn_type    TEXT NOT NULL DEFAULT '',
    sort_order  INT  NOT NULL DEFAULT 0
);

-- Seed from existing ASN values in switch_guide
INSERT INTO asn_definitions (asn)
SELECT DISTINCT asn FROM switch_guide
WHERE asn IS NOT NULL AND asn <> ''
ORDER BY asn
ON CONFLICT (asn) DO NOTHING;

-- Set default descriptions and types from device data
UPDATE asn_definitions a SET
  description = COALESCE(sg.building, '') || ' ' || COALESCE(sg.device_type, '') || ' — ' || COALESCE(sg.switch_name, ''),
  asn_type = CASE
    WHEN sg.device_type ILIKE '%Core%' THEN 'Building Router'
    WHEN sg.device_type ILIKE '%Firewall%' THEN 'Firewall'
    WHEN sg.device_type ILIKE '%Storage%' THEN 'Server'
    WHEN sg.device_type ILIKE '%Management%' THEN 'Building Router'
    WHEN sg.device_type ILIKE '%Leaf%' THEN 'Building Router'
    WHEN sg.device_type ILIKE '%Reserved%' THEN 'Reserved'
    ELSE ''
  END
FROM switch_guide sg
WHERE sg.asn = a.asn AND a.description = '';
