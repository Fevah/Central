-- =============================================================================
-- 092 — Networking engine Phase 5d: legacy link import into net.link
--
-- Walks three legacy link tables and produces one net.link + two
-- net.link_endpoint rows per active row. Idempotent via
-- UNIQUE (organization_id, link_code) on net.link and
-- UNIQUE (link_id, endpoint_order) on net.link_endpoint.
--
-- Three source tables:
--
--   public.p2p_links    within-building L3 link between two switches.
--                       columns: building, link_id, vlan, device_a/b,
--                       port_a/b, device_a_ip/device_b_ip, subnet,
--                       desc_a, desc_b
--
--   public.b2b_links    inter-building eBGP link.
--                       columns: link_id, vlan, building_a/b,
--                       device_a/b, port_a/b, module_a/b, device_a_ip/_b_ip,
--                       subnet, tx, rx, media, speed
--
--   public.fw_links     switch to firewall uplink.
--                       columns: building, link_id, vlan, switch,
--                       switch_port, switch_ip, firewall,
--                       firewall_port, firewall_ip, subnet
--
-- Projection per source row:
--   net.link
--     link_type_id       = (P2P | B2B | FW depending on source)
--     link_code          = legacy.link_id
--     building_id        = net.building where code = legacy.building
--                          (B2B: legacy.building_a — B-side is
--                          captured on link_endpoint)
--     vlan_id            = net.vlan where vlan_id::text = legacy.vlan
--     subnet_id          = net.subnet where network matches legacy.subnet
--     config_json        = per-type extras (desc_a/_b for P2P;
--                          tx/rx/media/speed for B2B)
--     legacy_link_kind   = 'p2p' / 'b2b' / 'fw'
--     legacy_link_id     = legacy.id
--
--   net.link_endpoint (x2)
--     endpoint_order=0 = A side, endpoint_order=1 = B side
--     device_id          = net.device where hostname = legacy.device_a/_b
--                          (FW: A=switch, B=firewall)
--     interface_name     = legacy.port_a / port_b
--                          (FW: switch_port / firewall_port)
--     ip_address_id      = net.ip_address where address = legacy.*_ip
--
-- Not imported: ports resolution (Phase 5e's ports-sync service will
-- fill port_id from interface_name), status field (maps differently
-- per table).
-- =============================================================================

DO $$
DECLARE
    t_imm   CONSTANT uuid := '00000000-0000-0000-0000-000000000000';

    type_p2p uuid;
    type_b2b uuid;
    type_fw  uuid;

    r             record;
    v_link_id     uuid;
    v_building_id uuid;
    v_vlan_id     uuid;
    v_subnet_id   uuid;
    v_dev_a_id    uuid;
    v_dev_b_id    uuid;
    v_ip_a_id     uuid;
    v_ip_b_id     uuid;
    v_cfg         jsonb;
BEGIN
    SELECT id INTO type_p2p FROM net.link_type WHERE organization_id=t_imm AND type_code='P2P';
    SELECT id INTO type_b2b FROM net.link_type WHERE organization_id=t_imm AND type_code='B2B';
    SELECT id INTO type_fw  FROM net.link_type WHERE organization_id=t_imm AND type_code='FW';

    -- ═══════════════════════════════════════════════════════════════════
    -- p2p_links
    -- ═══════════════════════════════════════════════════════════════════
    FOR r IN
        SELECT id, building, link_id, vlan, device_a, port_a, device_a_ip,
               device_b, port_b, device_b_ip, subnet, desc_a, desc_b
          FROM public.p2p_links
         WHERE NOT COALESCE(is_deleted, false)
         ORDER BY id
    LOOP
        SELECT id INTO v_building_id FROM net.building
         WHERE organization_id=t_imm AND building_code=r.building AND deleted_at IS NULL;
        SELECT v.id INTO v_vlan_id FROM net.vlan v
         WHERE v.organization_id=t_imm AND v.vlan_id::text=r.vlan AND v.deleted_at IS NULL LIMIT 1;
        SELECT s.id INTO v_subnet_id FROM net.subnet s
         WHERE s.organization_id=t_imm AND s.network::text=r.subnet AND s.deleted_at IS NULL LIMIT 1;
        SELECT id INTO v_dev_a_id FROM net.device
         WHERE organization_id=t_imm AND hostname=r.device_a AND deleted_at IS NULL;
        SELECT id INTO v_dev_b_id FROM net.device
         WHERE organization_id=t_imm AND hostname=r.device_b AND deleted_at IS NULL;
        SELECT id INTO v_ip_a_id FROM net.ip_address
         WHERE organization_id=t_imm AND host(address)=r.device_a_ip AND deleted_at IS NULL LIMIT 1;
        SELECT id INTO v_ip_b_id FROM net.ip_address
         WHERE organization_id=t_imm AND host(address)=r.device_b_ip AND deleted_at IS NULL LIMIT 1;

        v_cfg := jsonb_strip_nulls(jsonb_build_object(
            'desc_a', NULLIF(r.desc_a,''),
            'desc_b', NULLIF(r.desc_b,'')
        ));

        INSERT INTO net.link
            (organization_id, link_type_id, building_id, link_code,
             vlan_id, subnet_id, config_json,
             legacy_link_kind, legacy_link_id, status)
        VALUES
            (t_imm, type_p2p, v_building_id, r.link_id,
             v_vlan_id, v_subnet_id, v_cfg,
             'p2p', r.id, 'Active')
        ON CONFLICT (organization_id, link_code) DO UPDATE SET
            link_type_id     = EXCLUDED.link_type_id,
            building_id      = EXCLUDED.building_id,
            vlan_id          = EXCLUDED.vlan_id,
            subnet_id        = EXCLUDED.subnet_id,
            config_json      = EXCLUDED.config_json,
            legacy_link_kind = EXCLUDED.legacy_link_kind,
            legacy_link_id   = EXCLUDED.legacy_link_id,
            updated_at       = now()
        RETURNING id INTO v_link_id;

        INSERT INTO net.link_endpoint (organization_id, link_id, endpoint_order,
                                       device_id, ip_address_id, interface_name, status)
        VALUES
            (t_imm, v_link_id, 0, v_dev_a_id, v_ip_a_id, r.port_a, 'Active'),
            (t_imm, v_link_id, 1, v_dev_b_id, v_ip_b_id, r.port_b, 'Active')
        ON CONFLICT (link_id, endpoint_order) DO UPDATE SET
            device_id       = EXCLUDED.device_id,
            ip_address_id   = EXCLUDED.ip_address_id,
            interface_name  = EXCLUDED.interface_name,
            updated_at      = now();
    END LOOP;

    -- ═══════════════════════════════════════════════════════════════════
    -- b2b_links — inter-building, so building_id = A side only;
    -- the B side's building surfaces via the B endpoint's device->building FK.
    -- ═══════════════════════════════════════════════════════════════════
    FOR r IN
        SELECT id, link_id, vlan, building_a, device_a, port_a, device_a_ip,
               building_b, device_b, port_b, device_b_ip, subnet,
               tx, rx, media, speed
          FROM public.b2b_links
         WHERE NOT COALESCE(is_deleted, false)
         ORDER BY id
    LOOP
        SELECT id INTO v_building_id FROM net.building
         WHERE organization_id=t_imm AND building_code=r.building_a AND deleted_at IS NULL;
        SELECT v.id INTO v_vlan_id FROM net.vlan v
         WHERE v.organization_id=t_imm AND v.vlan_id::text=r.vlan AND v.deleted_at IS NULL LIMIT 1;
        SELECT s.id INTO v_subnet_id FROM net.subnet s
         WHERE s.organization_id=t_imm AND s.network::text=r.subnet AND s.deleted_at IS NULL LIMIT 1;
        SELECT id INTO v_dev_a_id FROM net.device
         WHERE organization_id=t_imm AND hostname=r.device_a AND deleted_at IS NULL;
        SELECT id INTO v_dev_b_id FROM net.device
         WHERE organization_id=t_imm AND hostname=r.device_b AND deleted_at IS NULL;
        SELECT id INTO v_ip_a_id FROM net.ip_address
         WHERE organization_id=t_imm AND host(address)=r.device_a_ip AND deleted_at IS NULL LIMIT 1;
        SELECT id INTO v_ip_b_id FROM net.ip_address
         WHERE organization_id=t_imm AND host(address)=r.device_b_ip AND deleted_at IS NULL LIMIT 1;

        v_cfg := jsonb_strip_nulls(jsonb_build_object(
            'tx',    NULLIF(r.tx,''),
            'rx',    NULLIF(r.rx,''),
            'media', NULLIF(r.media,''),
            'speed', NULLIF(r.speed,''),
            'building_b', NULLIF(r.building_b,'')
        ));

        INSERT INTO net.link
            (organization_id, link_type_id, building_id, link_code,
             vlan_id, subnet_id, config_json,
             legacy_link_kind, legacy_link_id, status)
        VALUES
            (t_imm, type_b2b, v_building_id, r.link_id,
             v_vlan_id, v_subnet_id, v_cfg,
             'b2b', r.id, 'Active')
        ON CONFLICT (organization_id, link_code) DO UPDATE SET
            link_type_id     = EXCLUDED.link_type_id,
            building_id      = EXCLUDED.building_id,
            vlan_id          = EXCLUDED.vlan_id,
            subnet_id        = EXCLUDED.subnet_id,
            config_json      = EXCLUDED.config_json,
            legacy_link_kind = EXCLUDED.legacy_link_kind,
            legacy_link_id   = EXCLUDED.legacy_link_id,
            updated_at       = now()
        RETURNING id INTO v_link_id;

        INSERT INTO net.link_endpoint (organization_id, link_id, endpoint_order,
                                       device_id, ip_address_id, interface_name, status)
        VALUES
            (t_imm, v_link_id, 0, v_dev_a_id, v_ip_a_id, r.port_a, 'Active'),
            (t_imm, v_link_id, 1, v_dev_b_id, v_ip_b_id, r.port_b, 'Active')
        ON CONFLICT (link_id, endpoint_order) DO UPDATE SET
            device_id       = EXCLUDED.device_id,
            ip_address_id   = EXCLUDED.ip_address_id,
            interface_name  = EXCLUDED.interface_name,
            updated_at      = now();
    END LOOP;

    -- ═══════════════════════════════════════════════════════════════════
    -- fw_links — switch (A) to firewall (B).
    -- ═══════════════════════════════════════════════════════════════════
    FOR r IN
        SELECT id, building, link_id, vlan, switch, switch_port, switch_ip,
               firewall, firewall_port, firewall_ip, subnet
          FROM public.fw_links
         WHERE NOT COALESCE(is_deleted, false)
         ORDER BY id
    LOOP
        SELECT id INTO v_building_id FROM net.building
         WHERE organization_id=t_imm AND building_code=r.building AND deleted_at IS NULL;
        SELECT v.id INTO v_vlan_id FROM net.vlan v
         WHERE v.organization_id=t_imm AND v.vlan_id::text=r.vlan AND v.deleted_at IS NULL LIMIT 1;
        SELECT s.id INTO v_subnet_id FROM net.subnet s
         WHERE s.organization_id=t_imm AND s.network::text=r.subnet AND s.deleted_at IS NULL LIMIT 1;
        SELECT id INTO v_dev_a_id FROM net.device
         WHERE organization_id=t_imm AND hostname=r.switch AND deleted_at IS NULL;
        -- Firewalls typically aren't in net.device yet; v_dev_b_id may be NULL.
        SELECT id INTO v_dev_b_id FROM net.device
         WHERE organization_id=t_imm AND hostname=r.firewall AND deleted_at IS NULL;
        SELECT id INTO v_ip_a_id FROM net.ip_address
         WHERE organization_id=t_imm AND host(address)=r.switch_ip AND deleted_at IS NULL LIMIT 1;
        SELECT id INTO v_ip_b_id FROM net.ip_address
         WHERE organization_id=t_imm AND host(address)=r.firewall_ip AND deleted_at IS NULL LIMIT 1;

        v_cfg := jsonb_strip_nulls(jsonb_build_object(
            'firewall_hostname', NULLIF(r.firewall,'')
        ));

        INSERT INTO net.link
            (organization_id, link_type_id, building_id, link_code,
             vlan_id, subnet_id, config_json,
             legacy_link_kind, legacy_link_id, status)
        VALUES
            (t_imm, type_fw, v_building_id, r.link_id,
             v_vlan_id, v_subnet_id, v_cfg,
             'fw', r.id, 'Active')
        ON CONFLICT (organization_id, link_code) DO UPDATE SET
            link_type_id     = EXCLUDED.link_type_id,
            building_id      = EXCLUDED.building_id,
            vlan_id          = EXCLUDED.vlan_id,
            subnet_id        = EXCLUDED.subnet_id,
            config_json      = EXCLUDED.config_json,
            legacy_link_kind = EXCLUDED.legacy_link_kind,
            legacy_link_id   = EXCLUDED.legacy_link_id,
            updated_at       = now()
        RETURNING id INTO v_link_id;

        INSERT INTO net.link_endpoint (organization_id, link_id, endpoint_order,
                                       device_id, ip_address_id, interface_name, status)
        VALUES
            (t_imm, v_link_id, 0, v_dev_a_id, v_ip_a_id, r.switch_port, 'Active'),
            (t_imm, v_link_id, 1, v_dev_b_id, v_ip_b_id, r.firewall_port, 'Active')
        ON CONFLICT (link_id, endpoint_order) DO UPDATE SET
            device_id       = EXCLUDED.device_id,
            ip_address_id   = EXCLUDED.ip_address_id,
            interface_name  = EXCLUDED.interface_name,
            updated_at      = now();
    END LOOP;

    RAISE NOTICE 'Link import complete. Spot-check net.link counts by legacy_link_kind.';
END $$;
