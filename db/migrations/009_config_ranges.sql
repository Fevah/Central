-- Config ranges: reusable number/IP ranges for settings (ASN, VLAN, etc.)
CREATE TABLE IF NOT EXISTS config_ranges (
    id          SERIAL PRIMARY KEY,
    category    TEXT NOT NULL,          -- e.g. 'asn', 'vlan', 'loopback_ip'
    name        TEXT NOT NULL,          -- e.g. 'Site ASN Range', 'Server VLANs'
    range_start TEXT NOT NULL,          -- start of range (text to support IPs and numbers)
    range_end   TEXT NOT NULL,          -- end of range
    description TEXT DEFAULT '',
    sort_order  INT  DEFAULT 0,
    UNIQUE(category, name)
);

-- Seed ASN range
INSERT INTO config_ranges (category, name, range_start, range_end, description, sort_order)
VALUES ('asn', 'Site ASN Range', '65100', '65199', 'Private ASN range for eBGP site peerings', 0)
ON CONFLICT DO NOTHING;
