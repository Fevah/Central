-- =============================================================================
-- 089 — Networking engine Phase 4d: public.switches -> net.device import
--
-- See docs/NETWORKING_BUILDOUT_PLAN.md §5 Phase 4 (acceptance: "every
-- currently-visible switch appears in the new grid with identical data").
--
-- For each row in public.switches we:
--   1. Find the building by public.switches.site vs. net.building.building_code.
--   2. Find the role by public.switches.role (case-insensitive) vs.
--      net.device_role.role_code. Fallback to 'SW' if no match.
--   3. Find the asn_allocation by joining bgp_config.local_as vs.
--      net.asn_allocation.asn (set by migration 087).
--   4. INSERT into net.device with legacy_switch_id = switches.id so the
--      Phase-4e dual-write trigger can mirror both directions.
--   5. If the switch has an lo0 interface in public.l3_interfaces, create
--      a net.loopback row pointing at the ip_address we already imported
--      for it in migration 087.
--
-- Idempotent: UNIQUE (organization_id, hostname) on net.device means a
-- second run is a no-op. Same trick on (device_id, loopback_number) for
-- loopbacks. Re-running after a hostname change on the legacy side would
-- create a new net.device row — expected, matches the "retire then replace"
-- workflow operators already use.
-- =============================================================================

DO $$
DECLARE
    t_imm  CONSTANT uuid := '00000000-0000-0000-0000-000000000000';

    sw            record;
    building_id   uuid;
    role_id       uuid;
    asn_alloc_id  uuid;
    v_device_id   uuid;
    lo_ip_id      uuid;
BEGIN
    FOR sw IN
        SELECT s.id        AS switch_id,
               s.hostname,
               s.site,
               s.role,
               s.picos_version,
               s.management_ip,
               s.ssh_username,
               s.ssh_port,
               s.management_vrf,
               s.inband_enabled,
               s.last_ping_at,
               s.last_ping_ok,
               s.last_ping_ms,
               s.last_ssh_at,
               s.last_ssh_ok,
               s.hardware_model,
               s.serial_number,
               s.mac_address,
               bc.local_as
          FROM public.switches s
          LEFT JOIN public.bgp_config bc ON bc.switch_id = s.id
         ORDER BY s.hostname
    LOOP
        -- Building by site code. Falls back to NULL if no match — the
        -- device row is still valid, it just lands unanchored.
        SELECT id INTO building_id
          FROM net.building
         WHERE organization_id = t_imm
           AND building_code = sw.site
           AND deleted_at IS NULL;

        -- Role: case-insensitive. Try exact first ('core' -> 'Core'),
        -- then prefix ('l1' -> 'L1Core', 'l2' -> 'L2Core'), then fall
        -- back to the generic 'SW' catalog entry. The legacy table
        -- uses lowercase 2-3 char codes so the prefix pass handles
        -- the shorthands Immunocore adopted.
        SELECT id INTO role_id
          FROM net.device_role
         WHERE organization_id = t_imm
           AND lower(role_code) = lower(sw.role)
           AND deleted_at IS NULL
         LIMIT 1;
        IF role_id IS NULL AND sw.role IS NOT NULL THEN
            -- When the prefix is ambiguous (e.g. 'l1' matches both
            -- 'L1Core' and 'L1SW'), use the hostname to disambiguate:
            -- if it contains 'CORE', pick the *Core* variant, else the
            -- *SW* variant. Falls back to any prefix match if neither.
            SELECT id INTO role_id
              FROM net.device_role
             WHERE organization_id = t_imm
               AND lower(role_code) LIKE lower(sw.role) || '%'
               AND deleted_at IS NULL
               AND lower(role_code) LIKE '%' ||
                   (CASE WHEN upper(sw.hostname) LIKE '%CORE%' THEN 'core' ELSE 'sw' END) || '%'
             LIMIT 1;
            IF role_id IS NULL THEN
                SELECT id INTO role_id
                  FROM net.device_role
                 WHERE organization_id = t_imm
                   AND lower(role_code) LIKE lower(sw.role) || '%'
                   AND deleted_at IS NULL
                 ORDER BY length(role_code)
                 LIMIT 1;
            END IF;
        END IF;
        IF role_id IS NULL THEN
            SELECT id INTO role_id FROM net.device_role
             WHERE organization_id = t_imm AND role_code = 'SW';
        END IF;

        -- ASN allocation: switches.bgp_config.local_as lives in an
        -- asn_allocation we imported in migration 087.
        SELECT a.id INTO asn_alloc_id
          FROM net.asn_allocation a
         WHERE a.organization_id = t_imm
           AND a.asn = sw.local_as
           AND a.deleted_at IS NULL
         LIMIT 1;

        -- Upsert device. ON CONFLICT (organization_id, hostname) makes
        -- the migration idempotent. We DO UPDATE so re-imports pick up
        -- any changes in the legacy fields without losing the
        -- net.device.id.
        INSERT INTO net.device
            (organization_id, device_role_id, building_id, asn_allocation_id,
             hostname, hardware_model, serial_number, mac_address,
             firmware_version, management_ip, ssh_username, ssh_port,
             management_vrf, inband_enabled,
             last_ping_at, last_ping_ok, last_ping_ms,
             last_ssh_at, last_ssh_ok,
             legacy_switch_id, status, notes)
        VALUES
            (t_imm, role_id, building_id, asn_alloc_id,
             sw.hostname, sw.hardware_model, sw.serial_number,
             CASE WHEN sw.mac_address ~ '^([0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}$'
                  THEN sw.mac_address::macaddr ELSE NULL END,
             sw.picos_version, sw.management_ip, sw.ssh_username, sw.ssh_port,
             COALESCE(sw.management_vrf, false), COALESCE(sw.inband_enabled, false),
             sw.last_ping_at, sw.last_ping_ok, sw.last_ping_ms,
             sw.last_ssh_at, sw.last_ssh_ok,
             sw.switch_id, 'Active',
             'Imported 2026-04-17 from public.switches (migration 089).')
        ON CONFLICT (organization_id, hostname) DO UPDATE SET
            device_role_id    = EXCLUDED.device_role_id,
            building_id       = EXCLUDED.building_id,
            asn_allocation_id = EXCLUDED.asn_allocation_id,
            hardware_model    = EXCLUDED.hardware_model,
            serial_number     = EXCLUDED.serial_number,
            legacy_switch_id  = EXCLUDED.legacy_switch_id,
            updated_at        = now()
        RETURNING id INTO v_device_id;

        -- Loopback: find the lo0 ip_address (imported in 087 into the
        -- per-site LOOPBACK subnet) and register a loopback row for it.
        SELECT ia.id INTO lo_ip_id
          FROM public.l3_interfaces li
          JOIN net.ip_address ia ON ia.organization_id = t_imm
                                AND host(ia.address) = host(li.ip_address)
                                AND ia.deleted_at IS NULL
         WHERE li.switch_id = sw.switch_id
           AND li.interface_name = 'lo0'
         LIMIT 1;

        IF lo_ip_id IS NOT NULL THEN
            INSERT INTO net.loopback
                (organization_id, device_id, loopback_number, ip_address_id,
                 description, status)
            VALUES
                (t_imm, v_device_id, 0, lo_ip_id,
                 'Primary loopback (lo0) — imported by migration 089.',
                 'Active')
            ON CONFLICT (device_id, loopback_number) DO UPDATE SET
                ip_address_id = EXCLUDED.ip_address_id,
                updated_at    = now();
        END IF;
    END LOOP;

    RAISE NOTICE 'Device import complete. Run SELECT COUNT(*) FROM net.device to verify.';
END $$;
