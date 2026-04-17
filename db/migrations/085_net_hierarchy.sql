-- =============================================================================
-- 085 — Networking engine Phase 2a: geographic hierarchy tables + Immunocore seed
--
-- See docs/NETWORKING_BUILDOUT_PLAN.md §5 Phase 2.
--
-- Creates the 9 hierarchy tables:
--   net.region
--   net.site_profile           -- defines what "default" looks like for new sites
--   net.site
--   net.building_profile       -- minimal shell; role counts / link rules land in later phases
--   net.building
--   net.floor_profile
--   net.floor
--   net.room
--   net.rack
--
-- "Organisation" is NOT a new table. It is central_platform.tenants — every
-- net.* table FKs its organization_id to tenants(id). Tenant = organisation
-- throughout the engine.
--
-- Universal base fields on every entity (matches attribute system §0.2):
--   id, organization_id, status, lock_state, lock_reason, locked_by, locked_at,
--   created_at, created_by, updated_at, updated_by, deleted_at, deleted_by,
--   notes, tags jsonb, external_refs jsonb, version int
--
-- Natural-key uniqueness scopes match the spec:
--   (organization_id, region_code)      — region_code unique within org
--   (region_id, site_code)              — site_code unique within region
--   (site_id, building_code)            — building_code unique within site
--   (building_id, floor_code)           — floor_code unique within building
--   (floor_id, room_code)               — room_code unique within floor
--   (room_id, rack_code)                — rack_code unique within room
--
-- Seeds Immunocore's known state (1 region UK, 1 site Milton Park, 5 buildings
-- MEP-91/92/93/94/96 scraped from public.switches) so the tree is populated
-- and Phase 2b's API / UI have real data to render immediately.
--
-- Idempotent (IF NOT EXISTS + ON CONFLICT).
-- =============================================================================

-- ─── Universal base columns helper ────────────────────────────────────────
-- Postgres has no table inheritance for per-column reuse. Each net.* table
-- declares its own universal columns. This is verbose but explicit and the
-- code-gen / ORM side maps cleanly.

-- ─── net.region ───────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS net.region (
    id                    uuid              PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id       uuid              NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    region_code           varchar(8)        NOT NULL,
    display_name          varchar(64)       NOT NULL,
    default_ip_pool_id    uuid,              -- FK added in Phase 3 when ip_pool exists
    default_asn_pool_id   uuid,              -- FK added in Phase 3
    b2b_mesh_policy       varchar(16)       NOT NULL DEFAULT 'None',   -- FullMesh / Hub&Spoke / None
    status                net.entity_status NOT NULL DEFAULT 'Active',
    lock_state            net.lock_state    NOT NULL DEFAULT 'Open',
    lock_reason           text,
    locked_by             int,
    locked_at             timestamptz,
    created_at            timestamptz       NOT NULL DEFAULT now(),
    created_by            int,
    updated_at            timestamptz       NOT NULL DEFAULT now(),
    updated_by            int,
    deleted_at            timestamptz,
    deleted_by            int,
    notes                 text,
    tags                  jsonb             NOT NULL DEFAULT '{}'::jsonb,
    external_refs         jsonb             NOT NULL DEFAULT '[]'::jsonb,
    version               int               NOT NULL DEFAULT 1,
    UNIQUE (organization_id, region_code),
    CHECK (b2b_mesh_policy IN ('FullMesh','Hub&Spoke','None'))
);
CREATE INDEX IF NOT EXISTS ix_region_org ON net.region(organization_id) WHERE deleted_at IS NULL;

-- ─── net.site_profile ─────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS net.site_profile (
    id                                uuid              PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id                   uuid              NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    profile_code                      varchar(32)       NOT NULL,
    display_name                      varchar(128)      NOT NULL,
    default_max_buildings             int               NOT NULL DEFAULT 12,
    default_building_profile_id       uuid,              -- self-FK once building_profile exists below
    default_floors_per_building       int               NOT NULL DEFAULT 1,
    allow_mixed_building_profiles     boolean           NOT NULL DEFAULT true,
    status                            net.entity_status NOT NULL DEFAULT 'Active',
    lock_state                        net.lock_state    NOT NULL DEFAULT 'Open',
    lock_reason                       text,
    locked_by                         int,
    locked_at                         timestamptz,
    created_at                        timestamptz       NOT NULL DEFAULT now(),
    created_by                        int,
    updated_at                        timestamptz       NOT NULL DEFAULT now(),
    updated_by                        int,
    deleted_at                        timestamptz,
    deleted_by                        int,
    notes                             text,
    tags                              jsonb             NOT NULL DEFAULT '{}'::jsonb,
    external_refs                     jsonb             NOT NULL DEFAULT '[]'::jsonb,
    version                           int               NOT NULL DEFAULT 1,
    UNIQUE (organization_id, profile_code)
);
CREATE INDEX IF NOT EXISTS ix_site_profile_org ON net.site_profile(organization_id) WHERE deleted_at IS NULL;

-- ─── net.building_profile ─────────────────────────────────────────────────
-- Minimal shell: just identity + defaults that don't reference Phase-3/4
-- entities. Role counts, link rules, MLAG rules, server_profile FK,
-- vlan_template FK, mstp rule FK all added in later migrations.
CREATE TABLE IF NOT EXISTS net.building_profile (
    id                          uuid              PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id             uuid              NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    profile_code                varchar(32)       NOT NULL,
    display_name                varchar(128)      NOT NULL,
    default_floor_count         int               NOT NULL DEFAULT 1,
    status                      net.entity_status NOT NULL DEFAULT 'Active',
    lock_state                  net.lock_state    NOT NULL DEFAULT 'Open',
    lock_reason                 text,
    locked_by                   int,
    locked_at                   timestamptz,
    created_at                  timestamptz       NOT NULL DEFAULT now(),
    created_by                  int,
    updated_at                  timestamptz       NOT NULL DEFAULT now(),
    updated_by                  int,
    deleted_at                  timestamptz,
    deleted_by                  int,
    notes                       text,
    tags                        jsonb             NOT NULL DEFAULT '{}'::jsonb,
    external_refs               jsonb             NOT NULL DEFAULT '[]'::jsonb,
    version                     int               NOT NULL DEFAULT 1,
    UNIQUE (organization_id, profile_code)
);
CREATE INDEX IF NOT EXISTS ix_building_profile_org ON net.building_profile(organization_id) WHERE deleted_at IS NULL;

-- Now that building_profile exists, add the FK from site_profile.default_building_profile_id
ALTER TABLE net.site_profile
    DROP CONSTRAINT IF EXISTS fk_site_profile_default_building_profile;
ALTER TABLE net.site_profile
    ADD CONSTRAINT fk_site_profile_default_building_profile
        FOREIGN KEY (default_building_profile_id) REFERENCES net.building_profile(id);

-- ─── net.floor_profile ────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS net.floor_profile (
    id                              uuid              PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id                 uuid              NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    profile_code                    varchar(32)       NOT NULL,
    display_name                    varchar(128)      NOT NULL,
    default_room_count              int               NOT NULL DEFAULT 1,
    default_rack_count_per_room     int               NOT NULL DEFAULT 10,
    status                          net.entity_status NOT NULL DEFAULT 'Active',
    lock_state                      net.lock_state    NOT NULL DEFAULT 'Open',
    lock_reason                     text,
    locked_by                       int,
    locked_at                       timestamptz,
    created_at                      timestamptz       NOT NULL DEFAULT now(),
    created_by                      int,
    updated_at                      timestamptz       NOT NULL DEFAULT now(),
    updated_by                      int,
    deleted_at                      timestamptz,
    deleted_by                      int,
    notes                           text,
    tags                            jsonb             NOT NULL DEFAULT '{}'::jsonb,
    external_refs                   jsonb             NOT NULL DEFAULT '[]'::jsonb,
    version                         int               NOT NULL DEFAULT 1,
    UNIQUE (organization_id, profile_code)
);
CREATE INDEX IF NOT EXISTS ix_floor_profile_org ON net.floor_profile(organization_id) WHERE deleted_at IS NULL;

-- ─── net.site ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS net.site (
    id                          uuid              PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id             uuid              NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    region_id                   uuid              NOT NULL REFERENCES net.region(id),
    site_profile_id             uuid              REFERENCES net.site_profile(id),
    site_code                   varchar(16)       NOT NULL,
    display_name                varchar(128)      NOT NULL,
    address_line1               varchar(128),
    address_line2               varchar(128),
    address_line3               varchar(128),
    city                        varchar(128),
    state_or_county             varchar(128),
    postcode                    varchar(32),
    country                     varchar(64),
    latitude                    decimal(9,6),
    longitude                   decimal(9,6),
    timezone                    varchar(64),
    primary_contact_user_id     int,              -- FK to app_users, not enforced cross-schema
    max_buildings               int,
    status                      net.entity_status NOT NULL DEFAULT 'Active',
    lock_state                  net.lock_state    NOT NULL DEFAULT 'Open',
    lock_reason                 text,
    locked_by                   int,
    locked_at                   timestamptz,
    created_at                  timestamptz       NOT NULL DEFAULT now(),
    created_by                  int,
    updated_at                  timestamptz       NOT NULL DEFAULT now(),
    updated_by                  int,
    deleted_at                  timestamptz,
    deleted_by                  int,
    notes                       text,
    tags                        jsonb             NOT NULL DEFAULT '{}'::jsonb,
    external_refs               jsonb             NOT NULL DEFAULT '[]'::jsonb,
    version                     int               NOT NULL DEFAULT 1,
    UNIQUE (region_id, site_code)
);
CREATE INDEX IF NOT EXISTS ix_site_org    ON net.site(organization_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_site_region ON net.site(region_id)       WHERE deleted_at IS NULL;

-- ─── net.building ─────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS net.building (
    id                                  uuid              PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id                     uuid              NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    site_id                             uuid              NOT NULL REFERENCES net.site(id),
    building_profile_id                 uuid              REFERENCES net.building_profile(id),
    building_code                       varchar(16)       NOT NULL,
    display_name                        varchar(128)      NOT NULL,
    building_number                     int,
    is_reserved                         boolean           NOT NULL DEFAULT false,
    -- Phase-3 pool FKs (deferred; nullable for now)
    assigned_slash16_subnet_id          uuid,
    assigned_asn_block_id               uuid,
    assigned_loopback_switch_block_id   uuid,
    assigned_loopback_server_block_id   uuid,
    server_asn_allocation_id            uuid,
    max_floors                          int,
    max_devices_total                   int,
    b2b_partners                        uuid[]            NOT NULL DEFAULT '{}'::uuid[],  -- array of peer building ids
    status                              net.entity_status NOT NULL DEFAULT 'Active',
    lock_state                          net.lock_state    NOT NULL DEFAULT 'Open',
    lock_reason                         text,
    locked_by                           int,
    locked_at                           timestamptz,
    created_at                          timestamptz       NOT NULL DEFAULT now(),
    created_by                          int,
    updated_at                          timestamptz       NOT NULL DEFAULT now(),
    updated_by                          int,
    deleted_at                          timestamptz,
    deleted_by                          int,
    notes                               text,
    tags                                jsonb             NOT NULL DEFAULT '{}'::jsonb,
    external_refs                       jsonb             NOT NULL DEFAULT '[]'::jsonb,
    version                             int               NOT NULL DEFAULT 1,
    UNIQUE (site_id, building_code),
    -- Recommended: building_code unique within organisation overall
    UNIQUE (organization_id, building_code)
);
CREATE INDEX IF NOT EXISTS ix_building_org  ON net.building(organization_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_building_site ON net.building(site_id)         WHERE deleted_at IS NULL;

-- ─── net.floor ────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS net.floor (
    id                      uuid              PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id         uuid              NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    building_id             uuid              NOT NULL REFERENCES net.building(id),
    floor_profile_id        uuid              REFERENCES net.floor_profile(id),
    floor_code              varchar(8)        NOT NULL,
    floor_number            int,              -- signed (negative = basement)
    display_name            varchar(64),
    max_rooms               int,
    status                  net.entity_status NOT NULL DEFAULT 'Active',
    lock_state              net.lock_state    NOT NULL DEFAULT 'Open',
    lock_reason             text,
    locked_by               int,
    locked_at               timestamptz,
    created_at              timestamptz       NOT NULL DEFAULT now(),
    created_by              int,
    updated_at              timestamptz       NOT NULL DEFAULT now(),
    updated_by              int,
    deleted_at              timestamptz,
    deleted_by              int,
    notes                   text,
    tags                    jsonb             NOT NULL DEFAULT '{}'::jsonb,
    external_refs           jsonb             NOT NULL DEFAULT '[]'::jsonb,
    version                 int               NOT NULL DEFAULT 1,
    UNIQUE (building_id, floor_code)
);
CREATE INDEX IF NOT EXISTS ix_floor_org      ON net.floor(organization_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_floor_building ON net.floor(building_id)     WHERE deleted_at IS NULL;

-- ─── net.room ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS net.room (
    id                      uuid              PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id         uuid              NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    floor_id                uuid              NOT NULL REFERENCES net.floor(id),
    room_code               varchar(16)       NOT NULL,
    room_type               varchar(16)       NOT NULL DEFAULT 'DataHall',
    max_racks               int,
    environmental_notes     text,
    power_feed_a_id         uuid,             -- Phase-13 FK to power entity
    power_feed_b_id         uuid,             -- Phase-13 FK
    status                  net.entity_status NOT NULL DEFAULT 'Active',
    lock_state              net.lock_state    NOT NULL DEFAULT 'Open',
    lock_reason             text,
    locked_by               int,
    locked_at               timestamptz,
    created_at              timestamptz       NOT NULL DEFAULT now(),
    created_by              int,
    updated_at              timestamptz       NOT NULL DEFAULT now(),
    updated_by              int,
    deleted_at              timestamptz,
    deleted_by              int,
    notes                   text,
    tags                    jsonb             NOT NULL DEFAULT '{}'::jsonb,
    external_refs           jsonb             NOT NULL DEFAULT '[]'::jsonb,
    version                 int               NOT NULL DEFAULT 1,
    UNIQUE (floor_id, room_code),
    CHECK (room_type IN ('MDF','IDF','DataHall','Office','Comms','Plant','Custom'))
);
CREATE INDEX IF NOT EXISTS ix_room_org   ON net.room(organization_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_room_floor ON net.room(floor_id)        WHERE deleted_at IS NULL;

-- ─── net.rack ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS net.rack (
    id                  uuid              PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id     uuid              NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    room_id             uuid              NOT NULL REFERENCES net.room(id),
    rack_code           varchar(16)       NOT NULL,
    u_height            int               NOT NULL DEFAULT 42,
    row                 varchar(8),
    position            int,
    pdu_a_id            uuid,             -- Phase-13 FK
    pdu_b_id            uuid,             -- Phase-13 FK
    max_devices         int,
    status              net.entity_status NOT NULL DEFAULT 'Active',
    lock_state          net.lock_state    NOT NULL DEFAULT 'Open',
    lock_reason         text,
    locked_by           int,
    locked_at           timestamptz,
    created_at          timestamptz       NOT NULL DEFAULT now(),
    created_by          int,
    updated_at          timestamptz       NOT NULL DEFAULT now(),
    updated_by          int,
    deleted_at          timestamptz,
    deleted_by          int,
    notes               text,
    tags                jsonb             NOT NULL DEFAULT '{}'::jsonb,
    external_refs       jsonb             NOT NULL DEFAULT '[]'::jsonb,
    version             int               NOT NULL DEFAULT 1,
    UNIQUE (room_id, rack_code)
);
CREATE INDEX IF NOT EXISTS ix_rack_org  ON net.rack(organization_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_rack_room ON net.rack(room_id)         WHERE deleted_at IS NULL;

-- =============================================================================
-- Seed Immunocore's known hierarchy
-- =============================================================================

-- Immunocore tenant id (created in migration 084)
DO $$
DECLARE
    v_org_id        uuid := '00000000-0000-0000-0000-000000000000';
    v_region_id     uuid;
    v_site_id       uuid;
    v_bp_id         uuid;
BEGIN
    -- Region: UK
    INSERT INTO net.region (organization_id, region_code, display_name, status)
    VALUES (v_org_id, 'UK', 'United Kingdom', 'Active')
    ON CONFLICT (organization_id, region_code) DO UPDATE SET version = net.region.version
    RETURNING id INTO v_region_id;
    IF v_region_id IS NULL THEN
        SELECT id INTO v_region_id FROM net.region
         WHERE organization_id = v_org_id AND region_code = 'UK';
    END IF;

    -- Default building profile: generic placeholder so buildings have something to bind to
    INSERT INTO net.building_profile (organization_id, profile_code, display_name, default_floor_count)
    VALUES (v_org_id, 'Standard-MEP', 'Immunocore standard MEP building', 3)
    ON CONFLICT (organization_id, profile_code) DO UPDATE SET version = net.building_profile.version
    RETURNING id INTO v_bp_id;
    IF v_bp_id IS NULL THEN
        SELECT id INTO v_bp_id FROM net.building_profile
         WHERE organization_id = v_org_id AND profile_code = 'Standard-MEP';
    END IF;

    -- Site: Milton Park (MP). All known Immunocore switches are on-campus here.
    INSERT INTO net.site (organization_id, region_id, site_code, display_name,
                          city, country, timezone, max_buildings, status)
    VALUES (v_org_id, v_region_id, 'MP', 'Milton Park', 'Abingdon', 'United Kingdom', 'Europe/London', 12, 'Active')
    ON CONFLICT (region_id, site_code) DO UPDATE SET version = net.site.version
    RETURNING id INTO v_site_id;
    IF v_site_id IS NULL THEN
        SELECT id INTO v_site_id FROM net.site
         WHERE region_id = v_region_id AND site_code = 'MP';
    END IF;

    -- Buildings: one net.building row per distinct public.switches.site
    INSERT INTO net.building (organization_id, site_id, building_profile_id,
                              building_code, display_name, building_number, status)
    SELECT v_org_id,
           v_site_id,
           v_bp_id,
           s.site,
           'Building ' || s.site,
           CASE
               WHEN s.site LIKE 'MEP-%' THEN CAST(substring(s.site FROM 5) AS int)
               ELSE NULL
           END,
           'Active'
    FROM (SELECT DISTINCT site FROM public.switches WHERE site IS NOT NULL) s
    ON CONFLICT (site_id, building_code) DO UPDATE SET version = net.building.version;
END $$;

-- ─── Record in schema_versions ────────────────────────────────────────────
INSERT INTO public.schema_versions (version_number, description)
VALUES ('085_net_hierarchy', 'Networking engine Phase 2a: region/site/building/floor/room/rack tables + profiles + Immunocore seed')
ON CONFLICT (version_number) DO NOTHING;
