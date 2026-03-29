-- =============================================================================
-- SwitchBuilder Database Schema
-- PostgreSQL schema for FS/PicOS switch configuration management
-- =============================================================================

-- Extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "citext";

-- =============================================================================
-- SWITCHES (top-level inventory)
-- =============================================================================
CREATE TABLE switches (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    hostname        VARCHAR(128) NOT NULL UNIQUE,
    site            VARCHAR(32)  NOT NULL,  -- e.g. MEP-91, MEP-92
    role            VARCHAR(32)  NOT NULL,  -- core, l1, l2, access
    picos_version   VARCHAR(32),
    management_vrf  BOOLEAN DEFAULT TRUE,
    inband_enabled  BOOLEAN DEFAULT TRUE,
    ssh_root_login  VARCHAR(16) DEFAULT 'allow',
    loopback_ip     INET,
    loopback_prefix INT,
    source_file     VARCHAR(256),
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW()
);

-- =============================================================================
-- VLANs (per switch)
-- =============================================================================
CREATE TABLE vlans (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    switch_id       UUID NOT NULL REFERENCES switches(id) ON DELETE CASCADE,
    vlan_id         INT  NOT NULL,
    description     VARCHAR(256),
    l3_interface    VARCHAR(64),
    UNIQUE (switch_id, vlan_id)
);

-- =============================================================================
-- INTERFACES (physical + breakout sub-interfaces)
-- =============================================================================
CREATE TABLE interfaces (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    switch_id       UUID NOT NULL REFERENCES switches(id) ON DELETE CASCADE,
    interface_name  VARCHAR(64) NOT NULL,   -- e.g. xe-1/1/1, ge-1/1/1, xe-1/1/31.1
    description     VARCHAR(256),
    speed           VARCHAR(16),            -- e.g. 10000, 25000, 40000, 100000
    mtu             INT,
    fec             BOOLEAN DEFAULT FALSE,
    breakout        BOOLEAN DEFAULT FALSE,
    native_vlan_id  INT,
    port_mode       VARCHAR(16),            -- trunk, access
    vlan_members    VARCHAR(256),           -- raw string, e.g. 1-310,1017
    aggregate_member VARCHAR(64),           -- LAG membership
    UNIQUE (switch_id, interface_name)
);

-- =============================================================================
-- VOICE VLANs (per interface)
-- =============================================================================
CREATE TABLE interface_voice_vlans (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    interface_id    UUID NOT NULL REFERENCES interfaces(id) ON DELETE CASCADE,
    vlan_id         INT,
    mode            VARCHAR(32),  -- manual, auto
    tagged_mode     VARCHAR(16)   -- tag, untag
);

-- =============================================================================
-- L3 INTERFACES (SVIs + loopbacks)
-- =============================================================================
CREATE TABLE l3_interfaces (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    switch_id       UUID NOT NULL REFERENCES switches(id) ON DELETE CASCADE,
    interface_name  VARCHAR(64) NOT NULL,   -- vlan-1, vlan-100, lo0
    ip_address      INET,
    prefix_length   INT,
    description     VARCHAR(256),
    UNIQUE (switch_id, interface_name)
);

-- =============================================================================
-- BGP CONFIG (per switch)
-- =============================================================================
CREATE TABLE bgp_config (
    id                    UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    switch_id             UUID NOT NULL UNIQUE REFERENCES switches(id) ON DELETE CASCADE,
    local_as              BIGINT NOT NULL,
    router_id             INET,
    ebgp_requires_policy  BOOLEAN DEFAULT FALSE,
    max_paths             INT DEFAULT 4
);

CREATE TABLE bgp_neighbors (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    bgp_id          UUID NOT NULL REFERENCES bgp_config(id) ON DELETE CASCADE,
    neighbor_ip     INET NOT NULL,
    remote_as       BIGINT,
    description     VARCHAR(256),
    bfd_enabled     BOOLEAN DEFAULT FALSE,
    ipv4_unicast    BOOLEAN DEFAULT FALSE,
    UNIQUE (bgp_id, neighbor_ip)
);

CREATE TABLE bgp_networks (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    bgp_id          UUID NOT NULL REFERENCES bgp_config(id) ON DELETE CASCADE,
    network_prefix  CIDR NOT NULL,
    UNIQUE (bgp_id, network_prefix)
);

-- =============================================================================
-- VRRP (per switch, per interface)
-- =============================================================================
CREATE TABLE vrrp_config (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    switch_id       UUID NOT NULL REFERENCES switches(id) ON DELETE CASCADE,
    interface_name  VARCHAR(64) NOT NULL,
    vrid            INT NOT NULL,
    virtual_ip      INET,
    load_balance    BOOLEAN DEFAULT FALSE,
    UNIQUE (switch_id, interface_name, vrid)
);

-- =============================================================================
-- STATIC ROUTES
-- =============================================================================
CREATE TABLE static_routes (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    switch_id       UUID NOT NULL REFERENCES switches(id) ON DELETE CASCADE,
    prefix          CIDR NOT NULL,
    next_hop        INET NOT NULL,
    UNIQUE (switch_id, prefix, next_hop)
);

-- =============================================================================
-- DHCP RELAY
-- =============================================================================
CREATE TABLE dhcp_relay (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    switch_id           UUID NOT NULL REFERENCES switches(id) ON DELETE CASCADE,
    interface_name      VARCHAR(64) NOT NULL,
    dhcp_server_address INET NOT NULL,
    relay_agent_address INET,
    disabled            BOOLEAN DEFAULT FALSE,
    UNIQUE (switch_id, interface_name, dhcp_server_address)
);

-- =============================================================================
-- SPANNING TREE
-- =============================================================================
CREATE TABLE spanning_tree (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    switch_id       UUID NOT NULL UNIQUE REFERENCES switches(id) ON DELETE CASCADE,
    protocol        VARCHAR(16) DEFAULT 'mstp',
    bridge_priority INT
);

-- =============================================================================
-- CLASS OF SERVICE / FORWARDING CLASSES
-- =============================================================================
CREATE TABLE cos_forwarding_classes (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    switch_id       UUID NOT NULL REFERENCES switches(id) ON DELETE CASCADE,
    class_name      VARCHAR(64) NOT NULL,
    local_priority  INT,
    UNIQUE (switch_id, class_name)
);

-- =============================================================================
-- FIREWALL FILTERS
-- =============================================================================
CREATE TABLE firewall_filters (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    switch_id       UUID NOT NULL REFERENCES switches(id) ON DELETE CASCADE,
    filter_name     VARCHAR(64) NOT NULL,
    sequence        INT NOT NULL,
    from_params     JSONB DEFAULT '{}',
    then_params     JSONB DEFAULT '{}',
    input_interface VARCHAR(64),
    UNIQUE (switch_id, filter_name, sequence)
);

-- =============================================================================
-- SWITCH GUIDE (from Excel import)
-- Flexible structure to capture guide/inventory data
-- =============================================================================
CREATE TABLE switch_guide (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    switch_name     VARCHAR(128),
    site            VARCHAR(64),
    building        VARCHAR(64),
    floor           VARCHAR(32),
    rack            VARCHAR(64),
    model           VARCHAR(128),
    serial_number   VARCHAR(128),
    management_ip   INET,
    uplink_switch   VARCHAR(128),
    uplink_port     VARCHAR(64),
    notes           TEXT,
    enabled         BOOLEAN DEFAULT TRUE,
    raw_data        JSONB DEFAULT '{}',     -- catch-all for extra columns
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW()
);

-- =============================================================================
-- SWITCH CONNECTIONS (from Excel guide - port-level connections)
-- =============================================================================
CREATE TABLE switch_connections (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    guide_id            UUID REFERENCES switch_guide(id) ON DELETE CASCADE,
    switch_id           UUID REFERENCES switches(id) ON DELETE SET NULL,
    local_port          VARCHAR(64),
    remote_device       VARCHAR(128),
    remote_port         VARCHAR(64),
    connection_type     VARCHAR(32),    -- trunk, access, uplink, downlink
    enabled             BOOLEAN DEFAULT TRUE,
    notes               TEXT,
    created_at          TIMESTAMPTZ DEFAULT NOW()
);

-- =============================================================================
-- CONFIG TEMPLATES (for the Rust desktop app)
-- Stores generated config snippets per switch
-- =============================================================================
CREATE TABLE config_templates (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    switch_id       UUID NOT NULL REFERENCES switches(id) ON DELETE CASCADE,
    template_type   VARCHAR(64) NOT NULL,   -- interface, vlan, bgp, vrrp, full
    config_text     TEXT NOT NULL,
    generated_at    TIMESTAMPTZ DEFAULT NOW(),
    version         INT DEFAULT 1
);

-- =============================================================================
-- INDEXES
-- =============================================================================
CREATE INDEX idx_vlans_switch        ON vlans(switch_id);
CREATE INDEX idx_vlans_vlan_id       ON vlans(vlan_id);
CREATE INDEX idx_interfaces_switch   ON interfaces(switch_id);
CREATE INDEX idx_l3_iface_switch     ON l3_interfaces(switch_id);
CREATE INDEX idx_bgp_neighbors_bgp   ON bgp_neighbors(bgp_id);
CREATE INDEX idx_bgp_networks_bgp    ON bgp_networks(bgp_id);
CREATE INDEX idx_vrrp_switch         ON vrrp_config(switch_id);
CREATE INDEX idx_static_switch       ON static_routes(switch_id);
CREATE INDEX idx_dhcp_switch         ON dhcp_relay(switch_id);
CREATE INDEX idx_fw_switch           ON firewall_filters(switch_id);
CREATE INDEX idx_cos_switch          ON cos_forwarding_classes(switch_id);
CREATE INDEX idx_guide_site          ON switch_guide(site);
CREATE INDEX idx_guide_switch_name   ON switch_guide(switch_name);
CREATE INDEX idx_connections_switch  ON switch_connections(switch_id);
CREATE INDEX idx_config_switch       ON config_templates(switch_id);

-- Full-text search on firewall filter params
CREATE INDEX idx_fw_from_params  ON firewall_filters USING GIN(from_params);
CREATE INDEX idx_fw_then_params  ON firewall_filters USING GIN(then_params);
CREATE INDEX idx_guide_raw       ON switch_guide USING GIN(raw_data);

-- =============================================================================
-- VIEWS
-- =============================================================================

-- Full switch summary with L3 interface count
CREATE VIEW v_switch_summary AS
SELECT
    s.hostname,
    s.site,
    s.role,
    s.loopback_ip,
    COUNT(DISTINCT i.id)   AS interface_count,
    COUNT(DISTINCT v.id)   AS vlan_count,
    COUNT(DISTINCT l.id)   AS l3_interface_count,
    COUNT(DISTINCT sr.id)  AS static_route_count,
    b.local_as             AS bgp_as
FROM switches s
LEFT JOIN interfaces i         ON i.switch_id = s.id
LEFT JOIN vlans v              ON v.switch_id = s.id
LEFT JOIN l3_interfaces l      ON l.switch_id = s.id
LEFT JOIN static_routes sr     ON sr.switch_id = s.id
LEFT JOIN bgp_config b         ON b.switch_id = s.id
GROUP BY s.id, s.hostname, s.site, s.role, s.loopback_ip, b.local_as;

-- BGP peer summary across all switches
CREATE VIEW v_bgp_peers AS
SELECT
    s.hostname       AS local_switch,
    s.site           AS local_site,
    b.local_as,
    n.neighbor_ip,
    n.remote_as,
    n.description,
    n.bfd_enabled,
    n.ipv4_unicast
FROM bgp_neighbors n
JOIN bgp_config b  ON b.id = n.bgp_id
JOIN switches s    ON s.id = b.switch_id;

-- VLAN to IP mapping across all switches
CREATE VIEW v_vlan_ip_map AS
SELECT
    s.hostname,
    s.site,
    v.vlan_id,
    v.description   AS vlan_description,
    l.ip_address,
    l.prefix_length,
    vr.vrid,
    vr.virtual_ip   AS vrrp_vip
FROM vlans v
JOIN switches s          ON s.id = v.switch_id
LEFT JOIN l3_interfaces l ON l.switch_id = s.id AND l.interface_name = v.l3_interface
LEFT JOIN vrrp_config vr  ON vr.switch_id = s.id AND vr.interface_name = v.l3_interface
ORDER BY s.site, v.vlan_id;

-- =============================================================================
-- UPDATED_AT trigger
-- =============================================================================
CREATE OR REPLACE FUNCTION update_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_switches_updated_at
    BEFORE UPDATE ON switches
    FOR EACH ROW EXECUTE FUNCTION update_updated_at();

CREATE TRIGGER trg_switch_guide_updated_at
    BEFORE UPDATE ON switch_guide
    FOR EACH ROW EXECUTE FUNCTION update_updated_at();
