-- =============================================================================
-- Migration 002: Config Builder
-- Adds VLAN/speed/description fields to switch_connections so the guide
-- can fully drive interface configuration.
-- =============================================================================

-- Add port-config columns to switch_connections
ALTER TABLE switch_connections
    ADD COLUMN IF NOT EXISTS vlan_id         INT,           -- native/data VLAN for this port
    ADD COLUMN IF NOT EXISTS voice_vlan_id   INT,           -- voice VLAN (triggers voice-vlan config)
    ADD COLUMN IF NOT EXISTS vlan_members    VARCHAR(256),  -- trunk allowed VLANs, e.g. "1-310"
    ADD COLUMN IF NOT EXISTS speed           VARCHAR(16),   -- e.g. 1000, 10000, 25000, 100000
    ADD COLUMN IF NOT EXISTS description     VARCHAR(256);  -- override for interface description

-- Site-wide VLAN templates (standard VLANs that go on all switches of a role)
CREATE TABLE IF NOT EXISTS vlan_templates (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    site            VARCHAR(32),       -- NULL = all sites
    role            VARCHAR(32),       -- NULL = all roles
    vlan_id         INT  NOT NULL,
    description     VARCHAR(256),
    l3_address      VARCHAR(64),       -- e.g. "10.{site_octet}.{vlan}.1" pattern
    prefix_length   INT,
    vrrp_vip_pattern VARCHAR(64),      -- e.g. "10.{site_octet}.{vlan}.254"
    include_in_bgp  BOOLEAN DEFAULT FALSE,
    UNIQUE (site, role, vlan_id)
);

-- BGP redistribute flag (already in bgp_config but missing redistribute column)
ALTER TABLE bgp_config
    ADD COLUMN IF NOT EXISTS redistribute_connected BOOLEAN DEFAULT TRUE;

-- Track when a config was last exported
ALTER TABLE switches
    ADD COLUMN IF NOT EXISTS last_exported_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS picos_version     VARCHAR(32) DEFAULT '4.6';
