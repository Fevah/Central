-- 017_missing_tables.sql
-- Creates all tables/views referenced by the desktop app that were missing

-- ── switch_audit_log ────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS switch_audit_log (
    id          SERIAL PRIMARY KEY,
    switch_id   UUID NOT NULL REFERENCES switches(id) ON DELETE CASCADE,
    timestamp   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    operator    TEXT NOT NULL DEFAULT '',
    action      TEXT NOT NULL DEFAULT '',
    field_name  TEXT,
    old_value   TEXT,
    new_value   TEXT,
    description TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS idx_audit_log_switch ON switch_audit_log(switch_id);

-- ── config_backups ──────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS config_backups (
    id          SERIAL PRIMARY KEY,
    switch_id   UUID NOT NULL REFERENCES switches(id) ON DELETE CASCADE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    operator    TEXT NOT NULL DEFAULT '',
    description TEXT NOT NULL DEFAULT '',
    config_text TEXT NOT NULL,
    line_count  INT NOT NULL DEFAULT 0,
    source_ip   TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS idx_config_backups_switch ON config_backups(switch_id);

-- ── switch_versions ─────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS switch_versions (
    id              SERIAL PRIMARY KEY,
    switch_id       UUID NOT NULL REFERENCES switches(id) ON DELETE CASCADE,
    captured_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    mac_address     TEXT,
    hardware_model  TEXT,
    linux_version   TEXT,
    linux_date      TEXT,
    l2l3_version    TEXT,
    l2l3_date       TEXT,
    ovs_version     TEXT,
    ovs_date        TEXT,
    raw_output      TEXT,
    serial_number   TEXT,
    uptime          TEXT
);
CREATE INDEX IF NOT EXISTS idx_switch_versions_switch ON switch_versions(switch_id);

-- ── switch_interfaces ───────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS switch_interfaces (
    id              SERIAL PRIMARY KEY,
    switch_id       UUID NOT NULL REFERENCES switches(id) ON DELETE CASCADE,
    captured_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    interface_name  TEXT NOT NULL DEFAULT '',
    admin_status    TEXT NOT NULL DEFAULT '',
    link_status     TEXT NOT NULL DEFAULT '',
    speed           TEXT NOT NULL DEFAULT '',
    mtu             TEXT NOT NULL DEFAULT '',
    description     TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS idx_switch_interfaces_switch ON switch_interfaces(switch_id);

-- ── ssh_logs ────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ssh_logs (
    id          SERIAL PRIMARY KEY,
    switch_id   UUID REFERENCES switches(id) ON DELETE SET NULL,
    hostname    TEXT NOT NULL DEFAULT '',
    host_ip     TEXT NOT NULL DEFAULT '',
    started_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    finished_at TIMESTAMPTZ,
    success     BOOLEAN NOT NULL DEFAULT FALSE,
    username    TEXT NOT NULL DEFAULT '',
    port        INT NOT NULL DEFAULT 22,
    error       TEXT,
    raw_output  TEXT,
    config_lines INT NOT NULL DEFAULT 0,
    log_entries TEXT
);
CREATE INDEX IF NOT EXISTS idx_ssh_logs_time ON ssh_logs(started_at DESC);

-- ── servers ─────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS servers (
    id          SERIAL PRIMARY KEY,
    building    TEXT NOT NULL DEFAULT '',
    server_name TEXT NOT NULL DEFAULT '',
    server_as   TEXT NOT NULL DEFAULT '',
    loopback_ip TEXT NOT NULL DEFAULT '',
    nic1_ip     TEXT NOT NULL DEFAULT '',
    nic1_router TEXT NOT NULL DEFAULT '',
    nic1_subnet TEXT NOT NULL DEFAULT '',
    nic1_status TEXT NOT NULL DEFAULT '',
    nic2_ip     TEXT NOT NULL DEFAULT '',
    nic2_router TEXT NOT NULL DEFAULT '',
    nic2_subnet TEXT NOT NULL DEFAULT '',
    nic2_status TEXT NOT NULL DEFAULT '',
    nic3_ip     TEXT NOT NULL DEFAULT '',
    nic3_router TEXT NOT NULL DEFAULT '',
    nic3_subnet TEXT NOT NULL DEFAULT '',
    nic3_status TEXT NOT NULL DEFAULT '',
    nic4_ip     TEXT NOT NULL DEFAULT '',
    nic4_router TEXT NOT NULL DEFAULT '',
    nic4_subnet TEXT NOT NULL DEFAULT '',
    nic4_status TEXT NOT NULL DEFAULT '',
    status      TEXT NOT NULL DEFAULT 'Active'
);

-- ── v_master_devices (view) ─────────────────────────────────────────
-- Enriched view of switch_guide joined with link counts, mstp, mlag
CREATE OR REPLACE VIEW v_master_devices AS
SELECT
    sg.id,
    sg.switch_name AS device_name,
    sg.device_type,
    sg.region,
    sg.building,
    sg.status,
    COALESCE(sg.primary_ip, '') AS primary_ip,
    COALESCE(split_part(cast(sg.management_ip as text), '/', 1), '') AS management_ip,
    COALESCE(split_part(cast(sg.loopback_ip as text), '/', 1), '') AS loopback_ip,
    COALESCE(cast(sg.loopback_subnet as text), '') AS loopback_subnet,
    COALESCE(split_part(cast(sg.mgmt_l3_ip as text), '/', 1), '') AS mgmt_l3_ip,
    COALESCE(sg.asn, '') AS asn,
    COALESCE(sg.mlag_domain, '') AS mlag_domain,
    COALESCE(sg.ae_range, '') AS ae_range,
    COALESCE(sg.model, '') AS model,
    COALESCE(sg.serial_number, '') AS serial_number,
    COALESCE(sg.uplink_switch, '') AS uplink_switch,
    COALESCE(sg.uplink_port, '') AS uplink_port,
    COALESCE(sg.notes, '') AS notes,
    COALESCE(p2p.cnt, 0) AS p2p_link_count,
    COALESCE(b2b.cnt, 0) AS b2b_link_count,
    COALESCE(fw.cnt, 0)  AS fw_link_count,
    COALESCE(ms.mstp_priority, '') AS mstp_priority,
    COALESCE(ml.peer, '') AS mlag_peer,
    EXISTS(SELECT 1 FROM switches sw WHERE UPPER(sw.hostname) = UPPER(sg.switch_name)) AS has_config
FROM switch_guide sg
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt FROM p2p_links
    WHERE device_a = sg.switch_name OR device_b = sg.switch_name
) p2p ON TRUE
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt FROM b2b_links
    WHERE building_a = sg.building OR building_b = sg.building
) b2b ON TRUE
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt FROM fw_links
    WHERE switch = sg.switch_name
) fw ON TRUE
LEFT JOIN LATERAL (
    SELECT mstp_priority FROM mstp_config
    WHERE device_name = sg.switch_name LIMIT 1
) ms ON TRUE
LEFT JOIN LATERAL (
    SELECT CASE
        WHEN switch_a = sg.switch_name THEN switch_b
        WHEN switch_b = sg.switch_name THEN switch_a
        ELSE ''
    END AS peer
    FROM mlag_config
    WHERE switch_a = sg.switch_name OR switch_b = sg.switch_name
    LIMIT 1
) ml ON TRUE;
