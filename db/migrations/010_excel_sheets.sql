-- All Excel sheet data imported into queryable tables

-- P2P Links (Point-to-Point interconnects within a building)
CREATE TABLE IF NOT EXISTS p2p_links (
    id           SERIAL PRIMARY KEY,
    region       TEXT,
    building     TEXT NOT NULL,
    link_id      TEXT,
    vlan         TEXT,
    device_a     TEXT NOT NULL,
    port_a       TEXT,
    device_a_ip  TEXT,
    device_b     TEXT NOT NULL,
    port_b       TEXT,
    device_b_ip  TEXT,
    subnet       TEXT,
    status       TEXT DEFAULT 'Active'
);

-- B2B Links (Building-to-Building backbone)
CREATE TABLE IF NOT EXISTS b2b_links (
    id           SERIAL PRIMARY KEY,
    link_id      TEXT,
    vlan         TEXT,
    building_a   TEXT NOT NULL,
    device_a     TEXT NOT NULL,
    port_a       TEXT,
    module_a     TEXT,
    device_a_ip  TEXT,
    building_b   TEXT NOT NULL,
    device_b     TEXT NOT NULL,
    port_b       TEXT,
    module_b     TEXT,
    device_b_ip  TEXT,
    tx           TEXT,
    rx           TEXT,
    media        TEXT,
    speed        TEXT,
    subnet       TEXT,
    status       TEXT DEFAULT 'Active'
);

-- FW Links (Switch-to-Firewall)
CREATE TABLE IF NOT EXISTS fw_links (
    id           SERIAL PRIMARY KEY,
    building     TEXT NOT NULL,
    link_id      TEXT,
    vlan         TEXT,
    switch       TEXT NOT NULL,
    switch_port  TEXT,
    switch_ip    TEXT,
    firewall     TEXT NOT NULL,
    firewall_port TEXT,
    firewall_ip  TEXT,
    subnet       TEXT,
    status       TEXT DEFAULT 'Active'
);

-- Server AS (BGP AS numbers for server infrastructure per building)
CREATE TABLE IF NOT EXISTS server_as (
    id           SERIAL PRIMARY KEY,
    building     TEXT NOT NULL,
    server_as    TEXT NOT NULL,
    status       TEXT DEFAULT 'Active'
);

-- IP Ranges (IP pool allocations)
CREATE TABLE IF NOT EXISTS ip_ranges (
    id           SERIAL PRIMARY KEY,
    region       TEXT,
    pool_name    TEXT NOT NULL,
    block        TEXT,
    purpose      TEXT,
    notes        TEXT,
    status       TEXT DEFAULT 'Active'
);

-- MLAG Config (Multi-Chassis LAG domains)
CREATE TABLE IF NOT EXISTS mlag_config (
    id              SERIAL PRIMARY KEY,
    building        TEXT NOT NULL,
    domain_type     TEXT,
    mlag_domain     TEXT,
    switch_a        TEXT,
    switch_b        TEXT,
    b2b_partner     TEXT,
    status          TEXT DEFAULT 'Active',
    peer_link_ae    TEXT,
    physical_members TEXT,
    peer_vlan       TEXT,
    trunk_vlans     TEXT,
    shared_domain_mac TEXT,
    peer_link_subnet TEXT,
    node0_ip        TEXT,
    node1_ip        TEXT,
    node0_ip_link2  TEXT,
    node1_ip_link2  TEXT,
    notes           TEXT
);

-- MSTP Config (Spanning Tree priorities)
CREATE TABLE IF NOT EXISTS mstp_config (
    id             SERIAL PRIMARY KEY,
    building       TEXT NOT NULL,
    device_name    TEXT NOT NULL,
    device_role    TEXT,
    mstp_priority  TEXT,
    notes          TEXT,
    status         TEXT DEFAULT 'Active'
);

-- VLANs (full VLAN inventory across all sites)
CREATE TABLE IF NOT EXISTS vlan_inventory (
    id             SERIAL PRIMARY KEY,
    block          TEXT,
    vlan_id        TEXT,
    name           TEXT,
    network_address TEXT,
    subnet         TEXT,
    gateway        TEXT,
    usable_range   TEXT,
    status         TEXT DEFAULT 'Active'
);
