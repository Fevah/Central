-- =============================================================================
-- 087 — Networking engine Phase 3g: Immunocore numbering import
--
-- Seeds net.* pool tables from the legacy public.switches / public.vlans /
-- public.bgp_config / public.l3_interfaces so the Pools panel has real
-- data to render on first open for the reference tenant.
--
-- Approach: one transaction, natural-key idempotence. Every pool / block
-- / subnet uses a stable code (IMM-ASN-MAIN, MEP-91-ASN-BLK, ...) and is
-- INSERT ... ON CONFLICT DO NOTHING so re-running the migration is a
-- no-op. Allocations rely on their own UNIQUE indexes (organization_id,
-- asn) / (block_id, vlan_id) / (organization_id, address) for the same
-- reason.
--
-- What we can import cleanly:
--   ASN pool, 5 per-site blocks, 5 switch allocations        (via bgp_config)
--   IP pool (10.0.0.0/8), 5 loopback /24 subnets, 5 /32s      (via l3_interfaces lo0)
--   VLAN pool, one block 1-4094, 63 distinct VLAN IDs         (via public.vlans)
--
-- What we don't try:
--   Subnet inference from /32 host IPs on non-loopback interfaces — the
--   legacy l3_interfaces table lost the prefix length, so we can't build
--   an accurate /24 just from "10.11.101.2/32". Operators carve those
--   from the UI when they need them.
--
-- Immunocore tenant UUID is the all-zeros tenant seeded in migration 084.
-- =============================================================================

DO $$
DECLARE
    t_imm     CONSTANT uuid := '00000000-0000-0000-0000-000000000000';

    asn_pool_id   uuid;
    ip_pool_id    uuid;
    vlan_pool_id  uuid;
    vlan_block_id uuid;

    site_row      record;
    vlan_row      record;
    switch_row    record;

    block_id      uuid;
    subnet_id     uuid;
    loopback_net  cidr;
BEGIN
    -- ═══════════════════════════════════════════════════════════════════
    -- ASN pool
    -- ═══════════════════════════════════════════════════════════════════

    INSERT INTO net.asn_pool (organization_id, pool_code, display_name,
                              asn_first, asn_last, asn_kind, notes)
    VALUES (t_imm, 'IMM-ASN-MAIN', 'Immunocore private ASNs',
            64512, 65534, 'Private2',
            'Imported 2026-04-17 from public.bgp_config (migration 087).')
    ON CONFLICT (organization_id, pool_code) DO NOTHING;

    SELECT id INTO asn_pool_id FROM net.asn_pool
     WHERE organization_id = t_imm AND pool_code = 'IMM-ASN-MAIN';

    -- One ASN block per site. Block range = 10-wide window rounded down
    -- to the nearest 10 so MEP-91 (65112) gets 65110-65119, MEP-92 (65121)
    -- gets 65120-65129, and so on. Matches the documented 6511X / 6512X /
    -- 6513X layout without hardcoding the switch list.
    FOR site_row IN
        SELECT DISTINCT s.site, bc.local_as
        FROM public.switches s
        JOIN public.bgp_config bc ON bc.switch_id = s.id
        WHERE bc.local_as IS NOT NULL
        ORDER BY s.site
    LOOP
        INSERT INTO net.asn_block (organization_id, pool_id, block_code, display_name,
                                   asn_first, asn_last, scope_level, notes)
        VALUES (t_imm, asn_pool_id,
                site_row.site || '-ASN',
                site_row.site || ' ASN block',
                (site_row.local_as / 10) * 10,
                (site_row.local_as / 10) * 10 + 9,
                'Free',                                  -- Site FK fills in once Phase-4 ties hierarchy to blocks
                'Auto-carved from bgp_config.local_as = ' || site_row.local_as)
        ON CONFLICT (organization_id, block_code) DO NOTHING;
    END LOOP;

    -- Import each switch's local_as as an allocation. allocated_to_id
    -- points at the legacy public.switches.id — Phase 4 will re-home
    -- these to net.device.id once that table exists.
    FOR switch_row IN
        SELECT s.id AS switch_id, s.site, bc.local_as
        FROM public.switches s
        JOIN public.bgp_config bc ON bc.switch_id = s.id
        WHERE bc.local_as IS NOT NULL
    LOOP
        INSERT INTO net.asn_allocation (organization_id, block_id, asn,
                                        allocated_to_type, allocated_to_id)
        SELECT t_imm, b.id, switch_row.local_as, 'Switch', switch_row.switch_id
          FROM net.asn_block b
         WHERE b.organization_id = t_imm
           AND b.block_code = switch_row.site || '-ASN'
        ON CONFLICT DO NOTHING;
    END LOOP;

    -- ═══════════════════════════════════════════════════════════════════
    -- IP pool + loopback subnets
    -- ═══════════════════════════════════════════════════════════════════

    INSERT INTO net.ip_pool (organization_id, pool_code, display_name,
                             network, address_family, notes)
    VALUES (t_imm, 'IMM-IP-MAIN', 'Immunocore 10/8 supernet',
            '10.0.0.0/8'::cidr, 'v4',
            'Imported 2026-04-17 (migration 087).')
    ON CONFLICT (organization_id, pool_code) DO NOTHING;

    SELECT id INTO ip_pool_id FROM net.ip_pool
     WHERE organization_id = t_imm AND pool_code = 'IMM-IP-MAIN';

    -- For each site's loopback (lo0) interface we can cleanly infer a
    -- /24 subnet: 10.255.91.2 => 10.255.91.0/24 named after the site.
    FOR switch_row IN
        SELECT DISTINCT s.site, li.ip_address
        FROM public.switches s
        JOIN public.l3_interfaces li ON li.switch_id = s.id
        WHERE li.interface_name = 'lo0'
          AND li.ip_address IS NOT NULL
        ORDER BY s.site
    LOOP
        -- Set the prefix to /24 then normalise via network() so the
        -- host bits are masked off — cidr rejects "10.255.91.2/24" but
        -- accepts "10.255.91.0/24".
        loopback_net := network(set_masklen(switch_row.ip_address, 24));

        INSERT INTO net.subnet (organization_id, pool_id, subnet_code, display_name,
                                network, scope_level, notes)
        VALUES (t_imm, ip_pool_id,
                switch_row.site || '-LOOPBACK',
                switch_row.site || ' switch loopbacks',
                loopback_net,
                'Free',
                'Auto-carved from lo0 interface on site ' || switch_row.site)
        ON CONFLICT (organization_id, subnet_code) DO NOTHING;
    END LOOP;

    -- Allocate each switch's loopback /32 as an ip_address row inside
    -- its site's loopback subnet.
    FOR switch_row IN
        SELECT DISTINCT s.id AS switch_id, s.site, li.ip_address
        FROM public.switches s
        JOIN public.l3_interfaces li ON li.switch_id = s.id
        WHERE li.interface_name = 'lo0'
          AND li.ip_address IS NOT NULL
    LOOP
        SELECT id INTO subnet_id FROM net.subnet
         WHERE organization_id = t_imm
           AND subnet_code = switch_row.site || '-LOOPBACK';

        IF subnet_id IS NOT NULL THEN
            INSERT INTO net.ip_address (organization_id, subnet_id, address,
                                        assigned_to_type, assigned_to_id, is_reserved)
            VALUES (t_imm, subnet_id, switch_row.ip_address,
                    'Switch', switch_row.switch_id, false)
            ON CONFLICT DO NOTHING;
        END IF;
    END LOOP;

    -- ═══════════════════════════════════════════════════════════════════
    -- VLAN pool
    -- ═══════════════════════════════════════════════════════════════════

    INSERT INTO net.vlan_pool (organization_id, pool_code, display_name,
                               vlan_first, vlan_last, notes)
    VALUES (t_imm, 'IMM-VLAN-MAIN', 'Immunocore VLAN space',
            1, 4094,
            'Imported 2026-04-17 from public.vlans (migration 087).')
    ON CONFLICT (organization_id, pool_code) DO NOTHING;

    SELECT id INTO vlan_pool_id FROM net.vlan_pool
     WHERE organization_id = t_imm AND pool_code = 'IMM-VLAN-MAIN';

    INSERT INTO net.vlan_block (organization_id, pool_id, block_code, display_name,
                                vlan_first, vlan_last, scope_level, notes)
    VALUES (t_imm, vlan_pool_id, 'IMM-VLAN-ALL',
            'Immunocore VLANs (unsliced)', 1, 4094, 'Free',
            'Single block until operators subdivide per-site.')
    ON CONFLICT (organization_id, block_code) DO NOTHING;

    SELECT id INTO vlan_block_id FROM net.vlan_block
     WHERE organization_id = t_imm AND block_code = 'IMM-VLAN-ALL';

    -- One net.vlan row per distinct VLAN ID with the first description
    -- we see for that ID. Dedup is the UNIQUE(block_id, vlan_id) index.
    FOR vlan_row IN
        SELECT DISTINCT ON (vlan_id) vlan_id, description
        FROM public.vlans
        WHERE vlan_id BETWEEN 1 AND 4094
        ORDER BY vlan_id, description NULLS LAST
    LOOP
        INSERT INTO net.vlan (organization_id, block_id, vlan_id, display_name,
                              description, scope_level)
        VALUES (t_imm, vlan_block_id, vlan_row.vlan_id,
                COALESCE(NULLIF(TRIM(vlan_row.description), ''), 'VLAN ' || vlan_row.vlan_id),
                vlan_row.description,
                'Free')
        ON CONFLICT DO NOTHING;
    END LOOP;

    RAISE NOTICE 'Immunocore numbering import complete.';
END $$;
