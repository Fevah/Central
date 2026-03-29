-- Migration 037: Catch-up — add columns that were applied ad-hoc but missing from migration chain.
-- These columns exist in production but were never formally migrated.

BEGIN;

-- switch_guide: primary_ip (referenced by DbRepository.cs + v_master_devices view)
ALTER TABLE switch_guide ADD COLUMN IF NOT EXISTS primary_ip TEXT NOT NULL DEFAULT '';

-- vlan_inventory: sort_order, block_locked, is_default (referenced by DbRepository.Links.cs)
ALTER TABLE vlan_inventory ADD COLUMN IF NOT EXISTS sort_order INT NOT NULL DEFAULT 0;
ALTER TABLE vlan_inventory ADD COLUMN IF NOT EXISTS block_locked BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE vlan_inventory ADD COLUMN IF NOT EXISTS is_default BOOLEAN NOT NULL DEFAULT FALSE;

-- sd_technicians: is_active (referenced by DbRepository.ServiceDesk.cs, all dashboard queries)
ALTER TABLE sd_technicians ADD COLUMN IF NOT EXISTS is_active BOOLEAN NOT NULL DEFAULT TRUE;

-- sd_requests: urgency, impact (referenced by ManageEngineSyncService.cs)
ALTER TABLE sd_requests ADD COLUMN IF NOT EXISTS urgency TEXT NOT NULL DEFAULT '';
ALTER TABLE sd_requests ADD COLUMN IF NOT EXISTS impact TEXT NOT NULL DEFAULT '';

-- v_master_devices view (recreate to ensure primary_ip column is included)
CREATE OR REPLACE VIEW v_master_devices AS
SELECT
    sg.id,
    sg.switch_name AS device_name,
    COALESCE(sg.device_type, '') AS device_type,
    COALESCE(sg.region, '') AS region,
    COALESCE(sg.building, '') AS building,
    COALESCE(sg.status, 'Active') AS status,
    COALESCE(NULLIF(sg.primary_ip,''), cast(sg.ip AS text), '') AS primary_ip,
    COALESCE(cast(sg.management_ip AS text), '') AS management_ip,
    COALESCE(cast(sg.loopback_ip AS text), '') AS loopback_ip,
    COALESCE(sg.loopback_subnet::text, '') AS loopback_subnet,
    COALESCE(cast(sg.mgmt_l3_ip AS text), '') AS mgmt_l3_ip,
    COALESCE(sg.asn, '') AS asn,
    COALESCE(sg.mlag_domain, '') AS mlag_domain,
    COALESCE(sg.ae_range, '') AS ae_range,
    COALESCE(sg.model, '') AS model,
    COALESCE(sg.serial_number, '') AS serial_number,
    COALESCE(sg.uplink_switch, '') AS uplink_switch,
    COALESCE(sg.uplink_port, '') AS uplink_port,
    COALESCE(sg.notes, '') AS notes,
    (SELECT count(*) FROM p2p_links p WHERE p.device_a = sg.switch_name OR p.device_b = sg.switch_name)::int AS p2p_link_count,
    (SELECT count(*) FROM b2b_links b WHERE b.device_a = sg.switch_name OR b.device_b = sg.switch_name)::int AS b2b_link_count,
    (SELECT count(*) FROM fw_links f WHERE f.switch = sg.switch_name OR f.firewall = sg.switch_name)::int AS fw_link_count,
    '' AS mstp_priority,
    '' AS mlag_peer,
    EXISTS(SELECT 1 FROM switches sw WHERE UPPER(sw.hostname) = UPPER(sg.switch_name)) AS has_config
FROM switch_guide sg
WHERE COALESCE(sg.is_deleted, false) = false;

COMMIT;
