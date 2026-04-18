-- =============================================================================
-- 095 — Networking engine Phase 6d: public.servers -> net.server import
--
-- Walks public.servers (the legacy flat-NIC-columns table) and creates
-- one net.server + up to four net.server_nic rows per active row.
-- Idempotent via UNIQUE (organization_id, hostname) on net.server and
-- UNIQUE (server_id, nic_index) on net.server_nic.
--
-- Per-row projection:
--
--   net.server
--     hostname                = legacy.server_name
--     server_profile_id       = net.server_profile with profile_code
--                               'Server4NIC' (seeded in migration 094)
--     building_id             = net.building where building_code = legacy.building
--     asn_allocation_id       = net.asn_allocation where asn = legacy.server_as
--     loopback_ip_address_id  = net.ip_address where host(address) = legacy.loopback_ip
--     legacy_server_id        = legacy.id
--
--   net.server_nic (one per populated nic slot — NIC rows are NOT
--                   created for slots where nicN_ip is blank, so
--                   placeholder servers don't accumulate 4 empty
--                   NIC rows each)
--     nic_index               = N - 1 (nic1 -> 0, nic4 -> 3)
--     target_device_id        = net.device where hostname = legacy.nicN_router
--     ip_address_id           = net.ip_address where host(address) = legacy.nicN_ip
--     subnet_id               = net.subnet where network::text = legacy.nicN_subnet
--     mlag_side               = A for nic_index 0+2, B for 1+3 (common
--                               4-NIC fan-out; profiles with a different
--                               pairing override this post-import)
--
-- Legacy columns not imported: nicN_status. status is used on every
-- legacy table for stale-data tracking; net.server_nic has its own
-- lifecycle status via the base-column set.
--
-- Reality check for Immunocore: all 160 legacy rows are placeholders
-- (server_name like "10.249.0.11 - 10.249.0.60" IP-range reservations;
-- 0 rows have nic1_ip populated, 0 have server_as). So this migration
-- produces 160 net.server rows and 0 net.server_nic rows. The servers
-- land with their building FK resolved and nothing else — when real
-- NIC data arrives the NIC rows follow.
-- =============================================================================

DO $$
DECLARE
    t_imm       CONSTANT uuid := '00000000-0000-0000-0000-000000000000';

    profile_id  uuid;
    r           record;

    v_server_id   uuid;
    v_building_id uuid;
    v_asn_alloc_id uuid;
    v_loop_ip_id  uuid;

    v_nic_ip_id   uuid;
    v_nic_subnet_id uuid;
    v_nic_device_id uuid;
    v_nic_ip      text;
    v_nic_router  text;
    v_nic_subnet  text;
    v_side        char(1);

    i int;
BEGIN
    SELECT id INTO profile_id
      FROM net.server_profile
     WHERE organization_id = t_imm AND profile_code = 'Server4NIC';

    FOR r IN
        SELECT id, server_name, building, server_as, loopback_ip,
               nic1_ip, nic1_router, nic1_subnet,
               nic2_ip, nic2_router, nic2_subnet,
               nic3_ip, nic3_router, nic3_subnet,
               nic4_ip, nic4_router, nic4_subnet
          FROM public.servers
         -- public.servers has no soft-delete column (unlike the link
         -- tables) — just a status string. Import every row; operators
         -- can soft-delete net.server entries later.
         ORDER BY id
    LOOP
        SELECT id INTO v_building_id FROM net.building
         WHERE organization_id = t_imm AND building_code = r.building AND deleted_at IS NULL;

        v_asn_alloc_id := NULL;
        IF r.server_as IS NOT NULL AND r.server_as <> '' THEN
            SELECT id INTO v_asn_alloc_id FROM net.asn_allocation
             WHERE organization_id = t_imm
               AND asn = r.server_as::bigint
               AND deleted_at IS NULL
             LIMIT 1;
        END IF;

        v_loop_ip_id := NULL;
        IF r.loopback_ip IS NOT NULL AND r.loopback_ip <> '' THEN
            SELECT id INTO v_loop_ip_id FROM net.ip_address
             WHERE organization_id = t_imm
               AND host(address) = r.loopback_ip
               AND deleted_at IS NULL
             LIMIT 1;
        END IF;

        INSERT INTO net.server
            (organization_id, server_profile_id, building_id, asn_allocation_id,
             loopback_ip_address_id, hostname, legacy_server_id, status, notes)
        VALUES
            (t_imm, profile_id, v_building_id, v_asn_alloc_id,
             v_loop_ip_id, r.server_name, r.id, 'Active',
             'Imported 2026-04-18 from public.servers (migration 095).')
        ON CONFLICT (organization_id, hostname) DO UPDATE SET
            server_profile_id      = EXCLUDED.server_profile_id,
            building_id            = EXCLUDED.building_id,
            asn_allocation_id      = EXCLUDED.asn_allocation_id,
            loopback_ip_address_id = EXCLUDED.loopback_ip_address_id,
            legacy_server_id       = EXCLUDED.legacy_server_id,
            updated_at             = now()
        RETURNING id INTO v_server_id;

        -- Walk the 4 flat NIC slots. Only materialise rows for slots
        -- that carry an actual IP — placeholder servers end up with
        -- no NIC rows rather than 4 empty ones.
        FOR i IN 0..3 LOOP
            v_nic_ip := CASE i WHEN 0 THEN r.nic1_ip WHEN 1 THEN r.nic2_ip
                               WHEN 2 THEN r.nic3_ip WHEN 3 THEN r.nic4_ip END;
            v_nic_router := CASE i WHEN 0 THEN r.nic1_router WHEN 1 THEN r.nic2_router
                                   WHEN 2 THEN r.nic3_router WHEN 3 THEN r.nic4_router END;
            v_nic_subnet := CASE i WHEN 0 THEN r.nic1_subnet WHEN 1 THEN r.nic2_subnet
                                   WHEN 2 THEN r.nic3_subnet WHEN 3 THEN r.nic4_subnet END;

            IF v_nic_ip IS NULL OR v_nic_ip = '' THEN
                CONTINUE;
            END IF;

            v_side := CASE WHEN i IN (0, 2) THEN 'A' ELSE 'B' END;

            SELECT id INTO v_nic_ip_id FROM net.ip_address
             WHERE organization_id = t_imm
               AND host(address) = v_nic_ip
               AND deleted_at IS NULL
             LIMIT 1;
            v_nic_subnet_id := NULL;
            IF v_nic_subnet IS NOT NULL AND v_nic_subnet <> '' THEN
                BEGIN
                    SELECT id INTO v_nic_subnet_id FROM net.subnet
                     WHERE organization_id = t_imm
                       AND network::text = v_nic_subnet
                       AND deleted_at IS NULL
                     LIMIT 1;
                EXCEPTION WHEN invalid_text_representation THEN
                    v_nic_subnet_id := NULL;
                END;
            END IF;
            v_nic_device_id := NULL;
            IF v_nic_router IS NOT NULL AND v_nic_router <> '' THEN
                SELECT id INTO v_nic_device_id FROM net.device
                 WHERE organization_id = t_imm
                   AND hostname = v_nic_router
                   AND deleted_at IS NULL
                 LIMIT 1;
            END IF;

            INSERT INTO net.server_nic
                (organization_id, server_id, nic_index,
                 target_device_id, ip_address_id, subnet_id,
                 mlag_side, status)
            VALUES
                (t_imm, v_server_id, i,
                 v_nic_device_id, v_nic_ip_id, v_nic_subnet_id,
                 v_side, 'Active')
            ON CONFLICT (server_id, nic_index) DO UPDATE SET
                target_device_id = EXCLUDED.target_device_id,
                ip_address_id    = EXCLUDED.ip_address_id,
                subnet_id        = EXCLUDED.subnet_id,
                mlag_side        = EXCLUDED.mlag_side,
                updated_at       = now();
        END LOOP;
    END LOOP;

    RAISE NOTICE 'Server import complete. Check net.server + net.server_nic counts.';
END $$;
