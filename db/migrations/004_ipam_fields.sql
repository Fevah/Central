-- =============================================================================
-- Migration 004: Extract IPAM fields from raw_data into proper columns
-- =============================================================================

ALTER TABLE switch_guide
    ADD COLUMN IF NOT EXISTS device_type      VARCHAR(64),
    ADD COLUMN IF NOT EXISTS region           VARCHAR(64),
    ADD COLUMN IF NOT EXISTS status           VARCHAR(32)  DEFAULT 'Active',
    ADD COLUMN IF NOT EXISTS ip               INET,          -- VLAN-152 / primary IP
    ADD COLUMN IF NOT EXISTS asn              VARCHAR(16),
    ADD COLUMN IF NOT EXISTS loopback_ip      INET,
    ADD COLUMN IF NOT EXISTS loopback_subnet  CIDR,
    ADD COLUMN IF NOT EXISTS mlag_domain      VARCHAR(16),
    ADD COLUMN IF NOT EXISTS ae_range         VARCHAR(32),
    ADD COLUMN IF NOT EXISTS mgmt_l3_ip       INET;

-- Populate from raw_data
UPDATE switch_guide SET
    device_type     = raw_data->>'Device Type',
    region          = COALESCE(raw_data->>'Region', raw_data->>'Region'),
    status          = COALESCE(raw_data->>'Status', 'Active'),
    ip              = NULLIF(raw_data->>'IP', '')::INET,
    asn             = NULLIF(raw_data->>'ASN', ''),
    loopback_ip     = NULLIF(raw_data->>'Loopback IP', '')::INET,
    loopback_subnet = NULLIF(raw_data->>'Loopback Subnet', '')::CIDR,
    mlag_domain     = NULLIF(raw_data->>'MLAG Domain', ''),
    ae_range        = NULLIF(raw_data->>'AE Range', ''),
    mgmt_l3_ip      = NULLIF(raw_data->>'Management L3 IP', '')::INET
WHERE raw_data IS NOT NULL;

-- Indexes for common filter/group operations
CREATE INDEX IF NOT EXISTS idx_sg_device_type ON switch_guide(device_type);
CREATE INDEX IF NOT EXISTS idx_sg_status       ON switch_guide(status);
CREATE INDEX IF NOT EXISTS idx_sg_building     ON switch_guide(building);
CREATE INDEX IF NOT EXISTS idx_sg_region       ON switch_guide(region);
CREATE INDEX IF NOT EXISTS idx_sg_ip           ON switch_guide(ip);
