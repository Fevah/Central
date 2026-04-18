-- =============================================================================
-- 094 — Networking engine Phase 6a: servers + NICs
--
-- See docs/NETWORKING_BUILDOUT_PLAN.md §5 Phase 6.
--
-- Replaces the legacy public.servers table (with NIC columns flattened
-- as nic1_ip / nic1_router / nic1_subnet / nic1_status × 4) with a
-- proper server + server_nic pair + a server_profile catalog that
-- carries the NIC count and a naming template.
--
-- Three tables:
--
--   server_profile    catalog per tenant. Seeded with Immunocore's
--                     4-NIC default. Carries a naming_template — per
--                     the Phase-4-review amendment, every *-type
--                     catalog table gets one. Plus nic_count +
--                     default_loopback_prefix as policy hints the
--                     server-creation flow reads.
--
--   server            the real server row. FKs to building / room /
--                     rack (Phase 2 hierarchy), server_profile,
--                     asn_allocation (Phase 3 — server ASN inherited
--                     from building's server-ASN block),
--                     loopback_ip_address_id (single loopback per
--                     server is the common case). Management-plane
--                     fields mirror net.device's shape so switches
--                     and servers present the same set of ping /
--                     SSH fields to the UI.
--
--   server_nic        one row per physical NIC. The Immunocore 4-NIC
--                     fan-out becomes 4 rows with nic_index 0..3.
--                     Each row points at a target net.port (the
--                     switch side) and a net.ip_address, plus an
--                     optional MLAG side letter (A / B) for the
--                     pair that carries this NIC.
--
-- Legacy provenance: net.server.legacy_server_id preserves the int
-- id of the public.servers row we imported from. Dropped in Phase 11
-- alongside the public.servers table itself.
--
-- Idempotent: IF NOT EXISTS + ON CONFLICT DO NOTHING.
-- =============================================================================

-- ═══════════════════════════════════════════════════════════════════════════
-- server_profile
-- ═══════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS net.server_profile (
    id                          uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id             uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    profile_code                varchar(32)        NOT NULL,
    display_name                varchar(128)       NOT NULL,
    description                 text,
    -- Number of NICs a server of this profile has. Immunocore: 4
    -- (two per MLAG pair, A + B). Other customers may prefer 2 (single
    -- pair) or 6 / 8 (multi-fabric). server_nic rows are expected to
    -- match this count, but the DB doesn't enforce — drift is caught
    -- by the Phase-6 parity test.
    nic_count                   int                NOT NULL DEFAULT 4,
    -- Hint — the server-creation flow asks the allocation service
    -- for a /32 (IPv4) or /128 (IPv6) from the building's server-
    -- loopback subnet by default.
    default_loopback_prefix     int                NOT NULL DEFAULT 32,
    -- Tokenised hostname template, expanded by ServerNamingService
    -- (Phase 6b). Tokens: {region_code}, {site_code}, {building_code},
    -- {rack_code}, {profile_code}, {instance}.
    naming_template             varchar(255)       NOT NULL
                                DEFAULT '{building_code}-SRV{instance}',
    color_hint                  varchar(16),
    status                      net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state                  net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason                 text,
    locked_by                   int,
    locked_at                   timestamptz,
    created_at                  timestamptz        NOT NULL DEFAULT now(),
    created_by                  int,
    updated_at                  timestamptz        NOT NULL DEFAULT now(),
    updated_by                  int,
    deleted_at                  timestamptz,
    deleted_by                  int,
    notes                       text,
    tags                        jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs               jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version                     int                NOT NULL DEFAULT 1,
    UNIQUE (organization_id, profile_code),
    CHECK (nic_count >= 1),
    CHECK (default_loopback_prefix BETWEEN 1 AND 128)
);
CREATE INDEX IF NOT EXISTS ix_server_profile_org
    ON net.server_profile(organization_id) WHERE deleted_at IS NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- server
-- ═══════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS net.server (
    id                          uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id             uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    server_profile_id           uuid               REFERENCES net.server_profile(id) ON DELETE SET NULL,
    building_id                 uuid               REFERENCES net.building(id) ON DELETE SET NULL,
    room_id                     uuid               REFERENCES net.room(id) ON DELETE SET NULL,
    rack_id                     uuid               REFERENCES net.rack(id) ON DELETE SET NULL,
    asn_allocation_id           uuid               REFERENCES net.asn_allocation(id) ON DELETE SET NULL,
    loopback_ip_address_id      uuid               REFERENCES net.ip_address(id) ON DELETE SET NULL,

    hostname                    varchar(64)        NOT NULL,
    server_code                 varchar(32),
    display_name                varchar(128),

    hardware_model              varchar(64),
    serial_number               varchar(64),
    mac_address                 macaddr,
    firmware_version            varchar(64),

    -- Management plane, mirrors net.device shape on purpose so UI
    -- panels can treat switches + servers uniformly for probe status.
    management_ip               inet,
    ssh_username                varchar(64),
    ssh_port                    int,
    last_ping_at                timestamptz,
    last_ping_ok                boolean,
    last_ping_ms                numeric,
    last_ssh_at                 timestamptz,
    last_ssh_ok                 boolean,

    legacy_server_id            int,    -- public.servers.id; dropped in Phase 11

    status                      net.entity_status  NOT NULL DEFAULT 'Planned',
    lock_state                  net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason                 text,
    locked_by                   int,
    locked_at                   timestamptz,
    created_at                  timestamptz        NOT NULL DEFAULT now(),
    created_by                  int,
    updated_at                  timestamptz        NOT NULL DEFAULT now(),
    updated_by                  int,
    deleted_at                  timestamptz,
    deleted_by                  int,
    notes                       text,
    tags                        jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs               jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version                     int                NOT NULL DEFAULT 1,
    UNIQUE (organization_id, hostname)
);
CREATE INDEX IF NOT EXISTS ix_server_org       ON net.server(organization_id)    WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_server_profile   ON net.server(server_profile_id)  WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_server_building  ON net.server(building_id)        WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_server_rack      ON net.server(rack_id)            WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_server_asn_alloc ON net.server(asn_allocation_id)  WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_server_loopback  ON net.server(loopback_ip_address_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_server_legacy    ON net.server(legacy_server_id)
    WHERE legacy_server_id IS NOT NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- server_nic
-- ═══════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS net.server_nic (
    id                          uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id             uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    server_id                   uuid               NOT NULL REFERENCES net.server(id) ON DELETE CASCADE,

    -- 0-based index within the server. For the Immunocore 4-NIC
    -- profile the rows are nic_index = 0, 1, 2, 3.
    nic_index                   int                NOT NULL,

    -- Switch side — the port this NIC plugs into. target_device_id is
    -- denormalised from target_port_id.device_id for cheap filtering
    -- ("all NICs on this core"). Both nullable because the port row
    -- may not exist when the NIC is first created.
    target_port_id              uuid               REFERENCES net.port(id)   ON DELETE SET NULL,
    target_device_id            uuid               REFERENCES net.device(id) ON DELETE SET NULL,

    ip_address_id               uuid               REFERENCES net.ip_address(id) ON DELETE SET NULL,
    subnet_id                   uuid               REFERENCES net.subnet(id)     ON DELETE SET NULL,
    vlan_id                     uuid               REFERENCES net.vlan(id)       ON DELETE SET NULL,

    -- Which side of the MLAG peer this NIC lands on. Conventional
    -- 4-NIC fan-out: NIC 0+2 on side A, 1+3 on side B (exact pairing
    -- is a server_profile policy matter — this column just records
    -- the chosen side).
    mlag_side                   varchar(1),

    nic_name                    varchar(32),     -- "eth0", "ens3f0np0", "nic1"
    mac_address                 macaddr,
    admin_up                    boolean            NOT NULL DEFAULT false,

    -- Probe state (server-side ping / LLDP check), mirrors the server
    -- columns so the NIC grid can render per-NIC status independently.
    last_ping_at                timestamptz,
    last_ping_ok                boolean,

    status                      net.entity_status  NOT NULL DEFAULT 'Planned',
    lock_state                  net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason                 text,
    locked_by                   int,
    locked_at                   timestamptz,
    created_at                  timestamptz        NOT NULL DEFAULT now(),
    created_by                  int,
    updated_at                  timestamptz        NOT NULL DEFAULT now(),
    updated_by                  int,
    deleted_at                  timestamptz,
    deleted_by                  int,
    notes                       text,
    tags                        jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs               jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version                     int                NOT NULL DEFAULT 1,
    UNIQUE (server_id, nic_index),
    CHECK (nic_index >= 0),
    CHECK (mlag_side IS NULL OR mlag_side IN ('A','B'))
);
CREATE INDEX IF NOT EXISTS ix_server_nic_server ON net.server_nic(server_id)        WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_server_nic_port   ON net.server_nic(target_port_id)   WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_server_nic_device ON net.server_nic(target_device_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_server_nic_ip     ON net.server_nic(ip_address_id)    WHERE deleted_at IS NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- Seed: Immunocore's default 4-NIC server profile
-- ═══════════════════════════════════════════════════════════════════════════
INSERT INTO net.server_profile (organization_id, profile_code, display_name,
                                description, nic_count, default_loopback_prefix,
                                naming_template, color_hint)
VALUES (
    '00000000-0000-0000-0000-000000000000',
    'Server4NIC',
    'Standard 4-NIC server',
    'MLAG-paired 4-NIC fan-out — two NICs per MLAG side, single loopback. ' ||
    'Matches Immunocore''s existing convention.',
    4, 32,
    '{building_code}-SRV{instance}',
    'teal'
)
ON CONFLICT (organization_id, profile_code) DO NOTHING;
