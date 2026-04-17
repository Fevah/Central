-- =============================================================================
-- 088 — Networking engine Phase 4a: device catalog + devices + ports
--
-- See docs/NETWORKING_BUILDOUT_PLAN.md §5 Phase 4.
--
-- Replaces the legacy public.switches table with the real device model.
-- Schema-only: C# models / repo / API / UI land in subsequent 4* chunks.
-- Dual-write triggers (so writes to net.device mirror into public.switches
-- during the transition window) come with the 4e import step.
--
-- Tables (all in net.*):
--
--   device_role                   catalog of role types per tenant
--                                 (Core / L1Core / L2Core / MAN / STOR /
--                                  SW / FW / DMZ / L1SW / L2SW / Res / RES-FW)
--
--   device                        the real switch / router / firewall entity.
--                                 Replaces public.switches. FKs to building
--                                 + room + rack (from Phase 2 hierarchy),
--                                 device_role, asn_allocation, and a
--                                 loopback (via the loopback table below).
--
--   module                        pluggable linecards / transceivers / PSUs
--                                 inside a chassis. Optional — small switches
--                                 have no modules.
--
--   port                          physical or breakout port. xe-1/1/N and
--                                 ge-1/1/N style interface names. Can be
--                                 parented on a module for chassis switches,
--                                 or directly on a device for fixed-form.
--                                 breakout_parent_id lets xe-1/1/31.1..4
--                                 sit under xe-1/1/31.
--
--   aggregate_ethernet            LAG / port-channel. LACP mode + min_links.
--                                 Member ports set their aggregate_ethernet_id
--                                 on the port row.
--
--   loopback                      one row per loopback interface (lo0, lo1),
--                                 linking a device to the ip_address that
--                                 backs it. Separate table rather than a
--                                 column on device because a device may
--                                 have multiple loopbacks.
--
--   building_profile_role_count   cardinality rule: profile X expects N
--                                 devices of role Y. Feeds capacity planning
--                                 + validation in later phases.
--
-- Every table carries the 17 universal base columns (id, organization_id,
-- status, lock_state, ..., version) — same pattern as Phase 2 and Phase 3.
--
-- Idempotent (IF NOT EXISTS + ON CONFLICT). Seeds the Immunocore role
-- catalog on the default tenant; re-running is a no-op.
-- =============================================================================

-- ═══════════════════════════════════════════════════════════════════════════
-- device_role
-- ═══════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS net.device_role (
    id                  uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id     uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    role_code           varchar(32)        NOT NULL,
    display_name        varchar(128)       NOT NULL,
    description         text,
    -- Hints, not enforcements — drive defaults in the device editor.
    default_asn_kind    varchar(16),       -- Private2 / Private4 / Public
    default_loopback_prefix int,            -- typically 32 for v4, 128 for v6
    color_hint          varchar(16),       -- UI accent colour (e.g. 'blue', 'green')
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
    UNIQUE (organization_id, role_code)
);
CREATE INDEX IF NOT EXISTS ix_device_role_org ON net.device_role(organization_id) WHERE deleted_at IS NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- device — replaces public.switches
-- ═══════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS net.device (
    id                  uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id     uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    device_role_id      uuid               REFERENCES net.device_role(id) ON DELETE SET NULL,
    building_id         uuid               REFERENCES net.building(id) ON DELETE SET NULL,
    room_id             uuid               REFERENCES net.room(id) ON DELETE SET NULL,
    rack_id             uuid               REFERENCES net.rack(id) ON DELETE SET NULL,
    asn_allocation_id   uuid               REFERENCES net.asn_allocation(id) ON DELETE SET NULL,

    hostname            varchar(64)        NOT NULL,
    device_code         varchar(32),           -- short code (optional)
    display_name        varchar(128),

    hardware_model      varchar(64),
    serial_number       varchar(64),
    mac_address         macaddr,
    firmware_version    varchar(64),          -- picos_version equivalent — generic name

    -- Management-plane fields — same shape as public.switches so the dual-
    -- write path in Phase 4e can mirror both directions cleanly.
    management_ip       inet,
    ssh_username        varchar(64),
    ssh_port            int,
    management_vrf      boolean            NOT NULL DEFAULT false,
    inband_enabled      boolean            NOT NULL DEFAULT false,

    -- Ping / SSH probe results, set by the SshOperationsService.
    last_ping_at        timestamptz,
    last_ping_ok        boolean,
    last_ping_ms        numeric,
    last_ssh_at         timestamptz,
    last_ssh_ok         boolean,

    -- Legacy link — the public.switches row this device was migrated from,
    -- if any. Dual-write triggers use this to sync the two tables during
    -- the transition window; Phase-11 removal drops the column + triggers.
    legacy_switch_id    uuid,

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
    UNIQUE (organization_id, hostname)
);
CREATE INDEX IF NOT EXISTS ix_device_org       ON net.device(organization_id)   WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_device_building  ON net.device(building_id)       WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_device_role      ON net.device(device_role_id)    WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_device_asn_alloc ON net.device(asn_allocation_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_device_legacy    ON net.device(legacy_switch_id)  WHERE legacy_switch_id IS NOT NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- module (pluggable linecards / transceivers / PSUs)
-- ═══════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS net.module (
    id                  uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id     uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    device_id           uuid               NOT NULL REFERENCES net.device(id) ON DELETE CASCADE,
    slot                varchar(32)        NOT NULL,              -- 'fpc0', 'psu0', 'sfp1'
    module_type         varchar(32)        NOT NULL DEFAULT 'Linecard', -- Linecard / Transceiver / PSU / Fan
    model               varchar(64),
    serial_number       varchar(64),
    part_number         varchar(64),
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
    UNIQUE (device_id, slot),
    CHECK (module_type IN ('Linecard','Transceiver','PSU','Fan','Other'))
);
CREATE INDEX IF NOT EXISTS ix_module_device ON net.module(device_id) WHERE deleted_at IS NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- aggregate_ethernet (LAG / port-channel)
-- ═══════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS net.aggregate_ethernet (
    id                  uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id     uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    device_id           uuid               NOT NULL REFERENCES net.device(id) ON DELETE CASCADE,
    ae_name             varchar(32)        NOT NULL,                -- 'ae-0', 'ae-1'
    lacp_mode           varchar(16)        NOT NULL DEFAULT 'active', -- active / passive / static
    min_links           int                NOT NULL DEFAULT 1,
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
    UNIQUE (device_id, ae_name),
    CHECK (lacp_mode IN ('active','passive','static')),
    CHECK (min_links >= 1)
);
CREATE INDEX IF NOT EXISTS ix_ae_device ON net.aggregate_ethernet(device_id) WHERE deleted_at IS NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- port (physical or breakout)
-- ═══════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS net.port (
    id                  uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id     uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    device_id           uuid               NOT NULL REFERENCES net.device(id) ON DELETE CASCADE,
    module_id           uuid               REFERENCES net.module(id) ON DELETE SET NULL,
    breakout_parent_id  uuid               REFERENCES net.port(id) ON DELETE CASCADE,
    aggregate_ethernet_id uuid             REFERENCES net.aggregate_ethernet(id) ON DELETE SET NULL,

    interface_name      varchar(48)        NOT NULL,  -- 'xe-1/1/1', 'ge-1/1/24', 'xe-1/1/31.2'
    -- Prefix discriminates 10G/25G/100G vs 1G copper in vendor-neutral terms.
    interface_prefix    varchar(8)         NOT NULL DEFAULT 'xe',   -- 'xe' / 'ge' / 'et' / 'fe'
    speed_mbps          bigint,
    admin_up            boolean            NOT NULL DEFAULT false,
    description         varchar(255),
    -- L2 posture at port level.
    port_mode           varchar(16)        NOT NULL DEFAULT 'unset', -- access / trunk / routed / unset
    native_vlan_id      int,
    -- Free-form additional config (e.g. QoS bindings, storm-control) so
    -- operators can extend without schema churn.
    config_json         jsonb              NOT NULL DEFAULT '{}'::jsonb,

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
    UNIQUE (device_id, interface_name),
    CHECK (port_mode IN ('access','trunk','routed','unset')),
    CHECK (native_vlan_id IS NULL OR native_vlan_id BETWEEN 1 AND 4094)
);
CREATE INDEX IF NOT EXISTS ix_port_device  ON net.port(device_id)          WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_port_module  ON net.port(module_id)          WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_port_ae      ON net.port(aggregate_ethernet_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_port_parent  ON net.port(breakout_parent_id) WHERE deleted_at IS NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- loopback
-- ═══════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS net.loopback (
    id                  uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id     uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    device_id           uuid               NOT NULL REFERENCES net.device(id) ON DELETE CASCADE,
    loopback_number     int                NOT NULL,                -- 0 for lo0, 1 for lo1, ...
    -- Points at the ip_address row that backs this loopback. Allocation
    -- goes through IpAllocationService so the /32 is tracked as "used"
    -- at the subnet level.
    ip_address_id       uuid               REFERENCES net.ip_address(id) ON DELETE SET NULL,
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
    UNIQUE (device_id, loopback_number),
    CHECK (loopback_number >= 0)
);
CREATE INDEX IF NOT EXISTS ix_loopback_device ON net.loopback(device_id)      WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_loopback_ip     ON net.loopback(ip_address_id)  WHERE deleted_at IS NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- building_profile_role_count (capacity rule per profile)
-- ═══════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS net.building_profile_role_count (
    id                  uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id     uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    building_profile_id uuid               NOT NULL REFERENCES net.building_profile(id) ON DELETE CASCADE,
    device_role_id      uuid               NOT NULL REFERENCES net.device_role(id) ON DELETE CASCADE,
    expected_count      int                NOT NULL,
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
    UNIQUE (building_profile_id, device_role_id),
    CHECK (expected_count >= 0)
);
CREATE INDEX IF NOT EXISTS ix_bprc_profile ON net.building_profile_role_count(building_profile_id)
    WHERE deleted_at IS NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- Seed: Immunocore device role catalog
-- ═══════════════════════════════════════════════════════════════════════════
-- Roles observed in public.switches + documented naming convention:
--   Core / Res / L1Core / L2Core / MAN / STOR / SW / FW / DMZ / L1SW /
--   L2SW / RES-FW. Codes stay short and ALLCAPS to match PicOS config
--   naming (MEP-91-CORE02 -> Core role).
INSERT INTO net.device_role (organization_id, role_code, display_name, description,
                             default_asn_kind, default_loopback_prefix, color_hint)
VALUES
  ('00000000-0000-0000-0000-000000000000', 'Core',   'Core switch',
   'Primary L3 fabric — runs eBGP to peers, carries inter-site traffic.',
   'Private2', 32, 'blue'),
  ('00000000-0000-0000-0000-000000000000', 'Res',    'Reserved core',
   'Shell core reserved for future site expansion.',
   'Private2', 32, 'grey'),
  ('00000000-0000-0000-0000-000000000000', 'L1Core', 'Distribution core (L1)',
   'First-level distribution above the access layer.',
   'Private2', 32, 'teal'),
  ('00000000-0000-0000-0000-000000000000', 'L2Core', 'Access core (L2)',
   'Access-layer core handling user-facing L2.',
   'Private2', 32, 'green'),
  ('00000000-0000-0000-0000-000000000000', 'MAN',    'Management switch',
   'Out-of-band management-network switch.',
   NULL, 32, 'amber'),
  ('00000000-0000-0000-0000-000000000000', 'STOR',   'Storage switch',
   'Storage fabric — typically 25/100G for NVMe-oF / iSCSI.',
   NULL, 32, 'purple'),
  ('00000000-0000-0000-0000-000000000000', 'SW',     'Edge switch',
   'Generic edge switch.',
   NULL, 32, 'olive'),
  ('00000000-0000-0000-0000-000000000000', 'FW',     'Firewall',
   'Perimeter / site firewall.',
   NULL, 32, 'red'),
  ('00000000-0000-0000-0000-000000000000', 'DMZ',    'DMZ switch',
   'DMZ-zone switch — isolated from the internal fabric.',
   NULL, 32, 'orange'),
  ('00000000-0000-0000-0000-000000000000', 'L1SW',   'L1 access switch',
   'First-level access switch.',
   NULL, 32, 'lime'),
  ('00000000-0000-0000-0000-000000000000', 'L2SW',   'L2 access switch',
   'Second-level access switch.',
   NULL, 32, 'lime'),
  ('00000000-0000-0000-0000-000000000000', 'RES-FW', 'Reserved firewall',
   'Shell firewall reserved for future expansion.',
   NULL, 32, 'pink')
ON CONFLICT (organization_id, role_code) DO NOTHING;
