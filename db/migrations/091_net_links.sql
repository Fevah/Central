-- =============================================================================
-- 091 — Networking engine Phase 5a: unified link model
--
-- See docs/NETWORKING_BUILDOUT_PLAN.md §5 Phase 5.
--
-- Replaces the three legacy link tables (public.p2p_links, b2b_links,
-- fw_links) with a single net.link + net.link_endpoint pair and a
-- small net.link_type catalog. Schema + seed only; the import
-- migration, API, unified UI, and byte-for-byte config-generation
-- parity test land in the 5b-5f chunks.
--
-- Three tables:
--
--   link_type       catalog per tenant. Seeded with 7 types:
--                   P2P / B2B / FW / DMZ / MLAG-Peer / Server-NIC / WAN.
--                   Carries a naming_template (used by the config
--                   builder in Phase 5e) and a required_endpoints
--                   column — today always 2, but shapes like a MLAG
--                   peer + server NIC fan-out could want more later.
--
--   link            one row per network link. FK to link_type;
--                   optional FKs to vlan, subnet, building (for scope).
--                   config_json carries type-specific extensions
--                   (tx / rx / media / speed for B2B, desc_a / desc_b
--                   for P2P, etc) so we don't churn the schema when a
--                   new link type needs a new field.
--                   legacy_link_kind + legacy_link_id preserve the
--                   origin — dropped in a later phase once the
--                   legacy tables go.
--
--   link_endpoint   two rows per link (endpoint_order 0 = A, 1 = B).
--                   FKs to device and optionally port, ip_address,
--                   vlan. interface_name kept as a free-text column
--                   so the import from legacy rows (which only know
--                   interface names, not port_ids) can land without
--                   resolving every port up front.
--
-- Every table carries the 17 universal base columns; idempotent via
-- IF NOT EXISTS + ON CONFLICT DO NOTHING.
-- =============================================================================

-- ═══════════════════════════════════════════════════════════════════════════
-- link_type
-- ═══════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS net.link_type (
    id                  uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id     uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    type_code           varchar(32)        NOT NULL,
    display_name        varchar(128)       NOT NULL,
    description         text,
    -- Tokenised template the config builder expands per link instance:
    --   {site_a} / {site_b}  — building codes on either endpoint
    --   {device_a} / {device_b} — hostnames
    --   {vlan}              — tagged VLAN id
    --   {role_a} / {role_b} — device role codes
    -- Example: "{device_a}_{port_a}_to_{device_b}_{port_b}"
    naming_template     varchar(255)       NOT NULL DEFAULT '{device_a}-to-{device_b}',
    required_endpoints  int                NOT NULL DEFAULT 2,
    color_hint          varchar(16),
    status              net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state          net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason         text,
    locked_by           int,
    locked_at           timestamptz,
    created_at          timestamptz        NOT NULL DEFAULT now(),
    created_by          int,
    updated_at          timestamptz        NOT NULL DEFAULT now(),
    updated_by          int,
    deleted_at          timestamptz,
    deleted_by          int,
    notes               text,
    tags                jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs       jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version             int                NOT NULL DEFAULT 1,
    UNIQUE (organization_id, type_code),
    CHECK (required_endpoints >= 2)
);
CREATE INDEX IF NOT EXISTS ix_link_type_org ON net.link_type(organization_id) WHERE deleted_at IS NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- link
-- ═══════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS net.link (
    id                  uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id     uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    link_type_id        uuid               NOT NULL REFERENCES net.link_type(id) ON DELETE RESTRICT,
    building_id         uuid               REFERENCES net.building(id) ON DELETE SET NULL,

    link_code           varchar(128)       NOT NULL,    -- legacy link_id lives here (e.g. "MEP-91-CORE02_p2p_MEP-91-L1-CORE02")
    display_name        varchar(255),
    description         text,

    vlan_id             uuid               REFERENCES net.vlan(id)    ON DELETE SET NULL,
    subnet_id           uuid               REFERENCES net.subnet(id)  ON DELETE SET NULL,

    -- Richer type-specific fields live here without schema churn.
    -- B2B: {tx, rx, media, speed}
    -- P2P: {desc_a, desc_b}
    -- FW:  nothing yet
    config_json         jsonb              NOT NULL DEFAULT '{}'::jsonb,

    -- Legacy provenance — which of the three old tables a link came
    -- from, and the row's integer id there. Lets the Phase-5f
    -- config-parity test join back to the original row for diffing.
    legacy_link_kind    varchar(16),       -- 'p2p' / 'b2b' / 'fw' / NULL for post-cutover
    legacy_link_id      int,

    status              net.entity_status  NOT NULL DEFAULT 'Planned',
    lock_state          net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason         text,
    locked_by           int,
    locked_at           timestamptz,
    created_at          timestamptz        NOT NULL DEFAULT now(),
    created_by          int,
    updated_at          timestamptz        NOT NULL DEFAULT now(),
    updated_by          int,
    deleted_at          timestamptz,
    deleted_by          int,
    notes               text,
    tags                jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs       jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version             int                NOT NULL DEFAULT 1,
    UNIQUE (organization_id, link_code),
    CHECK (legacy_link_kind IS NULL OR legacy_link_kind IN ('p2p','b2b','fw'))
);
CREATE INDEX IF NOT EXISTS ix_link_org      ON net.link(organization_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_link_type     ON net.link(link_type_id)    WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_link_building ON net.link(building_id)     WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_link_vlan     ON net.link(vlan_id)         WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_link_subnet   ON net.link(subnet_id)       WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_link_legacy
    ON net.link(legacy_link_kind, legacy_link_id)
    WHERE legacy_link_id IS NOT NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- link_endpoint
-- ═══════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS net.link_endpoint (
    id                  uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id     uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    link_id             uuid               NOT NULL REFERENCES net.link(id) ON DELETE CASCADE,

    -- 0 = A, 1 = B. Future "hub + N spokes" link types might use
    -- higher values; UNIQUE (link_id, endpoint_order) keeps pairs
    -- unambiguous.
    endpoint_order      int                NOT NULL,

    device_id           uuid               REFERENCES net.device(id)       ON DELETE SET NULL,
    port_id             uuid               REFERENCES net.port(id)         ON DELETE SET NULL,
    ip_address_id       uuid               REFERENCES net.ip_address(id)   ON DELETE SET NULL,
    vlan_id             uuid               REFERENCES net.vlan(id)         ON DELETE SET NULL,

    -- Free text copy of the interface name so legacy imports land
    -- without having to resolve every port row up front. Filled in
    -- by the ports-sync service (Phase 5d) which also populates
    -- port_id when the interface is known.
    interface_name      varchar(64),
    description         varchar(255),

    status              net.entity_status  NOT NULL DEFAULT 'Planned',
    lock_state          net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason         text,
    locked_by           int,
    locked_at           timestamptz,
    created_at          timestamptz        NOT NULL DEFAULT now(),
    created_by          int,
    updated_at          timestamptz        NOT NULL DEFAULT now(),
    updated_by          int,
    deleted_at          timestamptz,
    deleted_by          int,
    notes               text,
    tags                jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs       jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version             int                NOT NULL DEFAULT 1,
    UNIQUE (link_id, endpoint_order),
    CHECK (endpoint_order >= 0)
);
CREATE INDEX IF NOT EXISTS ix_link_endpoint_link    ON net.link_endpoint(link_id)       WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_link_endpoint_device  ON net.link_endpoint(device_id)     WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_link_endpoint_port    ON net.link_endpoint(port_id)       WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_link_endpoint_ip      ON net.link_endpoint(ip_address_id) WHERE deleted_at IS NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- Seed: the 7 link-type catalog for the Immunocore tenant
-- ═══════════════════════════════════════════════════════════════════════════
INSERT INTO net.link_type (organization_id, type_code, display_name, description,
                           naming_template, color_hint)
VALUES
  ('00000000-0000-0000-0000-000000000000', 'P2P', 'Point-to-point',
   'Within-building L3 link between two switches, typically a /30.',
   '{device_a}_{port_a}_p2p_{device_b}_{port_b}', 'blue'),
  ('00000000-0000-0000-0000-000000000000', 'B2B', 'Building-to-building',
   'Inter-building eBGP link. Carries VLAN-tagged traffic between sites.',
   '{site_a}_{device_a}_b2b_{site_b}_{device_b}', 'green'),
  ('00000000-0000-0000-0000-000000000000', 'FW', 'Firewall uplink',
   'Switch to perimeter / site firewall on the management or DMZ VRF.',
   '{device_a}_fw_{device_b}', 'red'),
  ('00000000-0000-0000-0000-000000000000', 'DMZ', 'DMZ link',
   'Link crossing a DMZ boundary — isolated from the internal fabric.',
   '{device_a}_dmz_{device_b}', 'orange'),
  ('00000000-0000-0000-0000-000000000000', 'MLAG-Peer', 'MLAG peer',
   'MLAG peer-link between paired switches.',
   '{device_a}_mlag_{device_b}', 'purple'),
  ('00000000-0000-0000-0000-000000000000', 'Server-NIC', 'Server NIC',
   'Switch port to server NIC. One row per NIC — a 4-NIC server has 4 links.',
   '{device_a}_{port_a}_srv_{device_b}', 'teal'),
  ('00000000-0000-0000-0000-000000000000', 'WAN', 'WAN uplink',
   'Site to carrier / MPLS / internet circuit.',
   '{device_a}_wan_{description}', 'amber')
ON CONFLICT (organization_id, type_code) DO NOTHING;
