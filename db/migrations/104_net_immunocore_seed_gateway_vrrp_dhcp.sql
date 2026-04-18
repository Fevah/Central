-- =============================================================================
-- 104 — Networking engine Phase 10: Immunocore seed for byte-parity data
--
-- Imports the customer's legacy `public.vrrp_config` / `public.dhcp_relay` /
-- static-route next-hop data into the three new `net.*` surfaces that
-- unblock byte-for-byte PicOS renderer output:
--
--   1. Gateway IPs  → `net.ip_address` rows with assigned_to_type='Gateway'
--   2. VRRP VIPs    → `net.ip_address` rows with assigned_to_type='Vrrp'
--   3. DHCP relay   → `net.dhcp_relay_target` rows (migration 103)
--
-- Idempotent. Re-running the migration is a no-op for existing rows (ON
-- CONFLICT ... DO NOTHING on every insert). Rolling back cleanly means
-- deleting the rows with notes LIKE 'imported-from-public.%' — documented
-- at the bottom so nobody has to reverse-engineer the inserts.
--
-- Scope: single-tenant Immunocore only (organization_id = all-zeros UUID).
-- Multi-tenant customers coming later will run their own seed / CRUD.
-- =============================================================================

DO $$
DECLARE
    t_imm CONSTANT uuid := '00000000-0000-0000-0000-000000000000';

    -- Convention: the mgmt-VLAN gateway (used by the static default
    -- route emit) sits at .254 of the mgmt subnet for Immunocore. This
    -- matches the legacy `set protocols static route 0.0.0.0/0 next-hop
    -- 10.11.152.254` line the ConfigBuilderService emits.
    mgmt_gateway_offset CONSTANT int := 254;

    -- How many rows each block inserted — logged for sanity at the end.
    n_gateways int := 0;
    n_vrrp     int := 0;
    n_dhcp     int := 0;
BEGIN
    -- ═══════════════════════════════════════════════════════════════════
    -- 1. Gateway IPs — one per mgmt subnet (vlan-152 on every building)
    -- ═══════════════════════════════════════════════════════════════════
    -- Seed the .254 address of every vlan-152-linked subnet as a
    -- Gateway ip_address row. host(network) gives the zero-host; we
    -- add the offset via `+ mgmt_gateway_offset::bigint` casting through
    -- inet arithmetic.
    --
    -- Guard: only inserts where a matching IP doesn't already exist
    -- (the renderer needs a unique Gateway per subnet; dupes would
    -- trip the ordering determinism).
    INSERT INTO net.ip_address
        (organization_id, subnet_id, address, assigned_to_type, is_reserved, notes)
    SELECT s.organization_id,
           s.id,
           (set_masklen(host(s.network)::inet + mgmt_gateway_offset, masklen(s.network))),
           'Gateway',
           true,   -- reserved; the IP isn't handed to a normal host
           'imported-from-public.switches (migration 104)'
      FROM net.subnet s
      JOIN net.vlan   v ON v.id = s.vlan_id AND v.deleted_at IS NULL
     WHERE s.organization_id = t_imm
       AND s.deleted_at      IS NULL
       AND v.vlan_id         = 152              -- Immunocore mgmt VLAN
       AND NOT EXISTS (
           SELECT 1 FROM net.ip_address existing
            WHERE existing.organization_id  = t_imm
              AND existing.subnet_id        = s.id
              AND existing.assigned_to_type = 'Gateway'
              AND existing.deleted_at       IS NULL);
    GET DIAGNOSTICS n_gateways = ROW_COUNT;

    -- ═══════════════════════════════════════════════════════════════════
    -- 2. VRRP VIPs — one per public.vrrp_config row
    -- ═══════════════════════════════════════════════════════════════════
    -- Legacy shape: (switch_id, interface_name, vrid, virtual_ip). We
    -- derive the subnet via the interface_name ('vlan-101' → vlan-id
    -- 101 → subnet with that vlan_id). `tags->>'vrid'` carries the
    -- VRID so the renderer can emit the tenant's actual value rather
    -- than defaulting to 1.
    WITH vrrp_rows AS (
        SELECT DISTINCT vc.virtual_ip,
                        vc.vrid,
                        v.id      AS vlan_uuid,
                        s.id      AS subnet_id
          FROM public.vrrp_config vc
          JOIN public.switches sw ON sw.id = vc.switch_id
          -- Extract numeric VLAN id from 'vlan-NNN' interface name.
          -- Pattern is consistent across the 5 buildings; if the
          -- regex fails (non-vlan interface), skip via the WHERE.
          CROSS JOIN LATERAL (SELECT substring(vc.interface_name from 'vlan-(\d+)')::int AS vlan_num) x
          JOIN net.vlan v
            ON v.organization_id = t_imm
           AND v.vlan_id         = x.vlan_num
           AND v.deleted_at      IS NULL
          JOIN net.subnet s
            ON s.vlan_id        = v.id
           AND s.organization_id = t_imm
           AND s.deleted_at      IS NULL
         WHERE vc.virtual_ip IS NOT NULL
    )
    INSERT INTO net.ip_address
        (organization_id, subnet_id, address, assigned_to_type,
         is_reserved, tags, notes)
    SELECT t_imm,
           vr.subnet_id,
           set_masklen(vr.virtual_ip, masklen((SELECT network FROM net.subnet WHERE id = vr.subnet_id))),
           'Vrrp',
           true,
           jsonb_build_object('vrid', vr.vrid),
           'imported-from-public.vrrp_config (migration 104)'
      FROM vrrp_rows vr
      WHERE NOT EXISTS (
          SELECT 1 FROM net.ip_address existing
           WHERE existing.organization_id  = t_imm
             AND existing.subnet_id        = vr.subnet_id
             AND existing.assigned_to_type = 'Vrrp'
             AND existing.address          = set_masklen(vr.virtual_ip, masklen((SELECT network FROM net.subnet WHERE id = vr.subnet_id)))
             AND existing.deleted_at       IS NULL);
    GET DIAGNOSTICS n_vrrp = ROW_COUNT;

    -- ═══════════════════════════════════════════════════════════════════
    -- 3. DHCP relay targets — one row per (vlan, server_ip)
    -- ═══════════════════════════════════════════════════════════════════
    -- Legacy shape: (switch_id, interface_name, dhcp_server_address,
    -- disabled). We dedupe via DISTINCT on (vlan_num, server_ip) so the
    -- same target configured on three switches produces one target row
    -- (the renderer applies the row to every switch with that SVI).
    INSERT INTO net.dhcp_relay_target
        (organization_id, vlan_id, server_ip, priority, notes)
    SELECT t_imm,
           v.id,
           drl.dhcp_server_address,
           ROW_NUMBER() OVER (PARTITION BY v.id ORDER BY drl.dhcp_server_address) * 10,
           'imported-from-public.dhcp_relay (migration 104)'
      FROM (
          SELECT DISTINCT
                 substring(interface_name from 'vlan-(\d+)')::int AS vlan_num,
                 dhcp_server_address
            FROM public.dhcp_relay
           WHERE COALESCE(disabled, false) = false
             AND interface_name IS NOT NULL
             AND interface_name LIKE 'vlan-%'
      ) drl
      JOIN net.vlan v
        ON v.organization_id = t_imm
       AND v.vlan_id         = drl.vlan_num
       AND v.deleted_at      IS NULL
      -- UNIQUE (organization_id, vlan_id, server_ip) guards duplicates;
      -- ON CONFLICT DO NOTHING makes the migration idempotent.
      ON CONFLICT (organization_id, vlan_id, server_ip) DO NOTHING;
    GET DIAGNOSTICS n_dhcp = ROW_COUNT;

    RAISE NOTICE 'Migration 104: seeded % Gateway IPs, % VRRP VIPs, % DHCP relay targets',
        n_gateways, n_vrrp, n_dhcp;
END $$;

-- ═════════════════════════════════════════════════════════════════════════
-- Rollback recipe (not auto-run):
--
--   UPDATE net.ip_address
--      SET deleted_at = now()
--    WHERE notes LIKE 'imported-from-public.%migration 104%'
--      AND assigned_to_type IN ('Gateway','Vrrp');
--
--   UPDATE net.dhcp_relay_target
--      SET deleted_at = now()
--    WHERE notes LIKE 'imported-from-public.%migration 104%';
-- ═════════════════════════════════════════════════════════════════════════
