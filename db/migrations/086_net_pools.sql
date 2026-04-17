-- =============================================================================
-- 086 — Networking engine Phase 3a: numbering pool schema (ASN / IP / VLAN /
-- MLAG / MSTP) + reservation shelf
--
-- See docs/NETWORKING_BUILDOUT_PLAN.md §5 Phase 3.
--
-- Builds the "locked" core of the system. This migration is schema only:
--   - pool / block / template tables (read-mostly, CRUD'd by operators)
--   - allocation tables (individual ASN / IP / VLAN / MLAG / MSTP values
--     handed out to switches, servers, etc.)
--   - reservation_shelf for retired values awaiting cool-down before re-issue
--
-- Allocation logic lands in Phase 3b inside Central.NetEngine.Allocation.
-- The schema here sets the invariant-level guarantees that make double-
-- allocation structurally impossible where a CHECK / UNIQUE / EXCLUDE can
-- do the job:
--
--   - asn_allocation         UNIQUE (organization_id, asn)          — ASN is globally unique within tenant
--   - ip_address             UNIQUE (organization_id, address)      — same, per tenant
--   - vlan                   UNIQUE (block_id, vlan_id)             — VLAN unique within its /21 block
--   - mlag_domain            UNIQUE (organization_id, domain_id)    — MLAG domain unique within tenant
--   - mstp_priority_allocation UNIQUE (organization_id, bridge_mac) — one priority per bridge
--   - subnet                 EXCLUDE USING gist ON (organization_id = , network &&) — no overlap
--   - pool.*_first <= pool.*_last                                   — sanity CHECK
--   - pool range contains block range                               — enforced in application (cross-row)
--
-- Also forward-wires net.region.default_ip_pool_id and default_asn_pool_id
-- to the new pool tables (they were left dangling in Phase 2 with a note).
--
-- Every table carries the 17 universal base columns (id, organization_id,
-- status, lock_state, ..., version). Same pattern as net.region in
-- migration 085.
--
-- Idempotent (IF NOT EXISTS + ON CONFLICT).
-- =============================================================================

-- ─── Extensions ───────────────────────────────────────────────────────────
-- Subnet overlap detection uses the btree_gist extension so we can build an
-- EXCLUDE constraint mixing = (organization_id) and && (network).
CREATE EXTENSION IF NOT EXISTS btree_gist;

-- ═══════════════════════════════════════════════════════════════════════════
-- ASN pools
-- ═══════════════════════════════════════════════════════════════════════════

-- ─── net.asn_pool ─────────────────────────────────────────────────────────
-- Top of the ASN hierarchy. A pool defines a range, an address family
-- (private vs public), and acts as the parent for one or more blocks.
CREATE TABLE IF NOT EXISTS net.asn_pool (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    pool_code         varchar(32)        NOT NULL,
    display_name      varchar(128)       NOT NULL,
    asn_first         bigint             NOT NULL,
    asn_last          bigint             NOT NULL,
    -- Private2 (64512-65534) / Private4 (4200000000-4294967294) / Public
    asn_kind          varchar(16)        NOT NULL DEFAULT 'Private2',
    status            net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state        net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason       text,
    locked_by         int,
    locked_at         timestamptz,
    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        int,
    deleted_at        timestamptz,
    deleted_by        int,
    notes             text,
    tags              jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs     jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version           int                NOT NULL DEFAULT 1,
    UNIQUE (organization_id, pool_code),
    CHECK (asn_first <= asn_last),
    CHECK (asn_first >= 1 AND asn_last <= 4294967294),
    CHECK (asn_kind IN ('Private2','Private4','Public'))
);
CREATE INDEX IF NOT EXISTS ix_asn_pool_org ON net.asn_pool(organization_id) WHERE deleted_at IS NULL;

-- ─── net.asn_block ────────────────────────────────────────────────────────
-- A sub-range carved out of a pool and assigned to a scope (region /
-- site / building). "Free" scope means the block is pre-carved but not yet
-- attached to any hierarchy entity.
CREATE TABLE IF NOT EXISTS net.asn_block (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    pool_id           uuid               NOT NULL REFERENCES net.asn_pool(id) ON DELETE CASCADE,
    block_code        varchar(32)        NOT NULL,
    display_name      varchar(128)       NOT NULL,
    asn_first         bigint             NOT NULL,
    asn_last          bigint             NOT NULL,
    -- Free / Region / Site / Building
    scope_level       varchar(16)        NOT NULL DEFAULT 'Free',
    scope_entity_id   uuid,              -- polymorphic: points to region/site/building by level
    status            net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state        net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason       text,
    locked_by         int,
    locked_at         timestamptz,
    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        int,
    deleted_at        timestamptz,
    deleted_by        int,
    notes             text,
    tags              jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs     jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version           int                NOT NULL DEFAULT 1,
    UNIQUE (organization_id, block_code),
    CHECK (asn_first <= asn_last),
    CHECK (scope_level IN ('Free','Region','Site','Building'))
);
CREATE INDEX IF NOT EXISTS ix_asn_block_pool  ON net.asn_block(pool_id)          WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_asn_block_scope ON net.asn_block(scope_entity_id)  WHERE deleted_at IS NULL;

-- ─── net.asn_allocation ───────────────────────────────────────────────────
-- A single ASN handed out from a block to a specific consumer (switch,
-- server, building). UNIQUE (org, asn) guarantees no double allocation
-- even if two allocation transactions race.
CREATE TABLE IF NOT EXISTS net.asn_allocation (
    id                  uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id     uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    block_id            uuid               NOT NULL REFERENCES net.asn_block(id) ON DELETE RESTRICT,
    asn                 bigint             NOT NULL,
    allocated_to_type   varchar(32)        NOT NULL,   -- Device / Server / Building
    allocated_to_id     uuid               NOT NULL,
    allocated_at        timestamptz        NOT NULL DEFAULT now(),
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
    version             int                NOT NULL DEFAULT 1
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_asn_allocation_org_asn
    ON net.asn_allocation (organization_id, asn) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_asn_allocation_block    ON net.asn_allocation(block_id)        WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_asn_allocation_consumer ON net.asn_allocation(allocated_to_id) WHERE deleted_at IS NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- IP pools
-- ═══════════════════════════════════════════════════════════════════════════

-- ─── net.ip_pool ──────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS net.ip_pool (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    pool_code         varchar(32)        NOT NULL,
    display_name      varchar(128)       NOT NULL,
    network           cidr               NOT NULL,
    address_family    varchar(4)         NOT NULL DEFAULT 'v4',  -- v4 / v6
    status            net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state        net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason       text,
    locked_by         int,
    locked_at         timestamptz,
    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        int,
    deleted_at        timestamptz,
    deleted_by        int,
    notes             text,
    tags              jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs     jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version           int                NOT NULL DEFAULT 1,
    UNIQUE (organization_id, pool_code),
    CHECK (address_family IN ('v4','v6'))
);
CREATE INDEX IF NOT EXISTS ix_ip_pool_org ON net.ip_pool(organization_id) WHERE deleted_at IS NULL;

-- ─── net.subnet ───────────────────────────────────────────────────────────
-- A subnet carved from a pool. Overlap protection is enforced via an
-- EXCLUDE GIST constraint: two rows in the same org with overlapping
-- networks are rejected by the DB before a second transaction can commit.
--
-- parent_subnet_id lets us model nested /16 -> /24 -> /30 hierarchies for
-- rollup views; it is nullable (a subnet directly under its pool).
CREATE TABLE IF NOT EXISTS net.subnet (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    pool_id           uuid               NOT NULL REFERENCES net.ip_pool(id) ON DELETE CASCADE,
    parent_subnet_id  uuid               REFERENCES net.subnet(id) ON DELETE SET NULL,
    subnet_code       varchar(48)        NOT NULL,
    display_name      varchar(128)       NOT NULL,
    network           cidr               NOT NULL,
    scope_level       varchar(16)        NOT NULL DEFAULT 'Free',
    scope_entity_id   uuid,
    vlan_id           uuid,              -- optional link to net.vlan (wired below once vlan exists)
    status            net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state        net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason       text,
    locked_by         int,
    locked_at         timestamptz,
    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        int,
    deleted_at        timestamptz,
    deleted_by        int,
    notes             text,
    tags              jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs     jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version           int                NOT NULL DEFAULT 1,
    UNIQUE (organization_id, subnet_code),
    CHECK (scope_level IN ('Free','Region','Site','Building','Floor','Room')),
    -- Within a tenant, no two active subnets may overlap. Deleted subnets
    -- are excluded so we can re-use the range later. The inet_ops operator
    -- class is required — cidr's default gist family doesn't expose &&.
    EXCLUDE USING gist (
        organization_id WITH =,
        network inet_ops WITH &&
    ) WHERE (deleted_at IS NULL)
);
CREATE INDEX IF NOT EXISTS ix_subnet_pool     ON net.subnet(pool_id)         WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_subnet_parent   ON net.subnet(parent_subnet_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_subnet_scope    ON net.subnet(scope_entity_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_subnet_network  ON net.subnet USING gist (network inet_ops) WHERE deleted_at IS NULL;

-- ─── net.ip_address ───────────────────────────────────────────────────────
-- A concrete IP handed out from a subnet. UNIQUE (org, address) prevents
-- double allocation across the tenant.
CREATE TABLE IF NOT EXISTS net.ip_address (
    id                  uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id     uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    subnet_id           uuid               NOT NULL REFERENCES net.subnet(id) ON DELETE RESTRICT,
    address             inet               NOT NULL,
    assigned_to_type    varchar(32),       -- Device / Server / ServerNic / Vrrp / Gateway / Broadcast / Reserved
    assigned_to_id      uuid,
    is_reserved         boolean            NOT NULL DEFAULT false, -- gateway / broadcast / etc
    assigned_at         timestamptz        NOT NULL DEFAULT now(),
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
    version             int                NOT NULL DEFAULT 1
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ip_address_org_addr
    ON net.ip_address (organization_id, address) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_ip_address_subnet ON net.ip_address(subnet_id)      WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_ip_address_assigned ON net.ip_address(assigned_to_id) WHERE deleted_at IS NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- VLAN pools
-- ═══════════════════════════════════════════════════════════════════════════

-- ─── net.vlan_template ───────────────────────────────────────────────────
-- Reusable VLAN patterns (e.g. "Servers", "Management", "DMZ"). When an
-- operator instantiates a vlan_template against a scope, a net.vlan row is
-- created with the template's role + description so the generated configs
-- stay consistent across sites.
CREATE TABLE IF NOT EXISTS net.vlan_template (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    template_code     varchar(32)        NOT NULL,
    display_name      varchar(128)       NOT NULL,
    -- Role drives config-generation defaults (trunk membership, L3 SVI, etc).
    vlan_role         varchar(32)        NOT NULL,
    description       text,
    is_default        boolean            NOT NULL DEFAULT false,
    status            net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state        net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason       text,
    locked_by         int,
    locked_at         timestamptz,
    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        int,
    deleted_at        timestamptz,
    deleted_by        int,
    notes             text,
    tags              jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs     jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version           int                NOT NULL DEFAULT 1,
    UNIQUE (organization_id, template_code)
);
CREATE INDEX IF NOT EXISTS ix_vlan_template_org ON net.vlan_template(organization_id) WHERE deleted_at IS NULL;

-- ─── net.vlan_pool ────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS net.vlan_pool (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    pool_code         varchar(32)        NOT NULL,
    display_name      varchar(128)       NOT NULL,
    vlan_first        int                NOT NULL,
    vlan_last         int                NOT NULL,
    status            net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state        net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason       text,
    locked_by         int,
    locked_at         timestamptz,
    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        int,
    deleted_at        timestamptz,
    deleted_by        int,
    notes             text,
    tags              jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs     jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version           int                NOT NULL DEFAULT 1,
    UNIQUE (organization_id, pool_code),
    CHECK (vlan_first BETWEEN 1 AND 4094),
    CHECK (vlan_last  BETWEEN 1 AND 4094),
    CHECK (vlan_first <= vlan_last)
);
CREATE INDEX IF NOT EXISTS ix_vlan_pool_org ON net.vlan_pool(organization_id) WHERE deleted_at IS NULL;

-- ─── net.vlan_block ───────────────────────────────────────────────────────
-- A /21 block (2048 VLANs) is Immunocore's convention for per-building
-- carve-outs. Other customers can pick their own block size — we only
-- enforce the pool range, the "/21 rule" is an application-level validation
-- so customers can opt out.
CREATE TABLE IF NOT EXISTS net.vlan_block (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    pool_id           uuid               NOT NULL REFERENCES net.vlan_pool(id) ON DELETE CASCADE,
    block_code        varchar(32)        NOT NULL,
    display_name      varchar(128)       NOT NULL,
    vlan_first        int                NOT NULL,
    vlan_last         int                NOT NULL,
    scope_level       varchar(16)        NOT NULL DEFAULT 'Free',
    scope_entity_id   uuid,
    status            net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state        net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason       text,
    locked_by         int,
    locked_at         timestamptz,
    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        int,
    deleted_at        timestamptz,
    deleted_by        int,
    notes             text,
    tags              jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs     jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version           int                NOT NULL DEFAULT 1,
    UNIQUE (organization_id, block_code),
    CHECK (vlan_first BETWEEN 1 AND 4094),
    CHECK (vlan_last  BETWEEN 1 AND 4094),
    CHECK (vlan_first <= vlan_last),
    CHECK (scope_level IN ('Free','Region','Site','Building'))
);
CREATE INDEX IF NOT EXISTS ix_vlan_block_pool  ON net.vlan_block(pool_id)         WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_vlan_block_scope ON net.vlan_block(scope_entity_id) WHERE deleted_at IS NULL;

-- ─── net.vlan ─────────────────────────────────────────────────────────────
-- A specific VLAN ID in use. UNIQUE (block_id, vlan_id) enforces that each
-- VLAN inside a block is issued exactly once; combined with the EXCLUDE
-- constraint on blocks within a pool (application-enforced for now) this
-- gives the "VLAN unique in block" invariant.
CREATE TABLE IF NOT EXISTS net.vlan (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    block_id          uuid               NOT NULL REFERENCES net.vlan_block(id) ON DELETE RESTRICT,
    template_id       uuid               REFERENCES net.vlan_template(id) ON DELETE SET NULL,
    vlan_id           int                NOT NULL,
    display_name      varchar(128)       NOT NULL,
    description       text,
    scope_level       varchar(16)        NOT NULL DEFAULT 'Free',
    scope_entity_id   uuid,
    status            net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state        net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason       text,
    locked_by         int,
    locked_at         timestamptz,
    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        int,
    deleted_at        timestamptz,
    deleted_by        int,
    notes             text,
    tags              jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs     jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version           int                NOT NULL DEFAULT 1,
    CHECK (vlan_id BETWEEN 1 AND 4094),
    CHECK (scope_level IN ('Free','Region','Site','Building','Device'))
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_vlan_block_id
    ON net.vlan (block_id, vlan_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_vlan_org      ON net.vlan(organization_id)  WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_vlan_scope    ON net.vlan(scope_entity_id)  WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_vlan_template ON net.vlan(template_id)      WHERE deleted_at IS NULL;

-- Wire net.subnet.vlan_id -> net.vlan now that both tables exist.
ALTER TABLE net.subnet
    DROP CONSTRAINT IF EXISTS fk_subnet_vlan,
    ADD  CONSTRAINT fk_subnet_vlan FOREIGN KEY (vlan_id)
         REFERENCES net.vlan(id) ON DELETE SET NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- MLAG domains
-- ═══════════════════════════════════════════════════════════════════════════

-- ─── net.mlag_domain_pool ─────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS net.mlag_domain_pool (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    pool_code         varchar(32)        NOT NULL,
    display_name      varchar(128)       NOT NULL,
    domain_first      int                NOT NULL,
    domain_last       int                NOT NULL,
    status            net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state        net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason       text,
    locked_by         int,
    locked_at         timestamptz,
    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        int,
    deleted_at        timestamptz,
    deleted_by        int,
    notes             text,
    tags              jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs     jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version           int                NOT NULL DEFAULT 1,
    UNIQUE (organization_id, pool_code),
    CHECK (domain_first BETWEEN 1 AND 4094),
    CHECK (domain_last  BETWEEN 1 AND 4094),
    CHECK (domain_first <= domain_last)
);
CREATE INDEX IF NOT EXISTS ix_mlag_pool_org ON net.mlag_domain_pool(organization_id) WHERE deleted_at IS NULL;

-- ─── net.mlag_domain ──────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS net.mlag_domain (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    pool_id           uuid               NOT NULL REFERENCES net.mlag_domain_pool(id) ON DELETE RESTRICT,
    domain_id         int                NOT NULL,
    display_name      varchar(128)       NOT NULL,
    scope_level       varchar(16)        NOT NULL DEFAULT 'Building',
    scope_entity_id   uuid,
    status            net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state        net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason       text,
    locked_by         int,
    locked_at         timestamptz,
    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        int,
    deleted_at        timestamptz,
    deleted_by        int,
    notes             text,
    tags              jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs     jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version           int                NOT NULL DEFAULT 1,
    CHECK (domain_id BETWEEN 1 AND 4094),
    CHECK (scope_level IN ('Free','Region','Site','Building'))
);
-- MLAG domain IDs must be unique across the tenant — two buildings with
-- the same domain ID on shared infrastructure would collide.
CREATE UNIQUE INDEX IF NOT EXISTS ux_mlag_domain_org_id
    ON net.mlag_domain (organization_id, domain_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_mlag_domain_pool  ON net.mlag_domain(pool_id)         WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_mlag_domain_scope ON net.mlag_domain(scope_entity_id) WHERE deleted_at IS NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- MSTP priority rules
-- ═══════════════════════════════════════════════════════════════════════════

-- ─── net.mstp_priority_rule ──────────────────────────────────────────────
-- A policy document: "for devices matching these conditions, use this
-- priority". One rule can have multiple steps evaluated in order.
CREATE TABLE IF NOT EXISTS net.mstp_priority_rule (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    rule_code         varchar(32)        NOT NULL,
    display_name      varchar(128)       NOT NULL,
    scope_level       varchar(16)        NOT NULL DEFAULT 'Region',
    scope_entity_id   uuid,
    status            net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state        net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason       text,
    locked_by         int,
    locked_at         timestamptz,
    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        int,
    deleted_at        timestamptz,
    deleted_by        int,
    notes             text,
    tags              jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs     jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version           int                NOT NULL DEFAULT 1,
    UNIQUE (organization_id, rule_code),
    CHECK (scope_level IN ('Free','Region','Site','Building'))
);
CREATE INDEX IF NOT EXISTS ix_mstp_rule_org   ON net.mstp_priority_rule(organization_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_mstp_rule_scope ON net.mstp_priority_rule(scope_entity_id) WHERE deleted_at IS NULL;

-- ─── net.mstp_priority_rule_step ─────────────────────────────────────────
-- Ordered clauses under a rule. Each step has a match expression (stored
-- as JSONB so operators can express role / device-type filters) and the
-- priority to assign if matched. Lower priority = higher bridge preference.
-- Standard MSTP values are multiples of 4096 (0, 4096, 8192, 12288, 16384).
CREATE TABLE IF NOT EXISTS net.mstp_priority_rule_step (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    rule_id           uuid               NOT NULL REFERENCES net.mstp_priority_rule(id) ON DELETE CASCADE,
    step_order        int                NOT NULL,
    match_expression  jsonb              NOT NULL DEFAULT '{}'::jsonb,
    priority          int                NOT NULL,
    status            net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state        net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason       text,
    locked_by         int,
    locked_at         timestamptz,
    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        int,
    deleted_at        timestamptz,
    deleted_by        int,
    notes             text,
    tags              jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs     jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version           int                NOT NULL DEFAULT 1,
    UNIQUE (rule_id, step_order),
    CHECK (priority BETWEEN 0 AND 61440),
    CHECK (priority % 4096 = 0)
);
CREATE INDEX IF NOT EXISTS ix_mstp_step_rule ON net.mstp_priority_rule_step(rule_id) WHERE deleted_at IS NULL;

-- ─── net.mstp_priority_allocation ────────────────────────────────────────
-- Concrete MSTP priority assigned to a specific device/bridge. Bridge MAC
-- is the natural key so two devices can't end up with the same priority
-- on the same L2 domain (one-priority-per-bridge). Nullable bridge_mac for
-- the case where we track by device UUID only (MAC not yet known).
CREATE TABLE IF NOT EXISTS net.mstp_priority_allocation (
    id                  uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id     uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    rule_id             uuid               NOT NULL REFERENCES net.mstp_priority_rule(id) ON DELETE RESTRICT,
    device_id           uuid               NOT NULL,     -- wired to net.device in Phase 4
    bridge_mac          macaddr,
    priority            int                NOT NULL,
    allocated_at        timestamptz        NOT NULL DEFAULT now(),
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
    CHECK (priority BETWEEN 0 AND 61440),
    CHECK (priority % 4096 = 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_mstp_alloc_device
    ON net.mstp_priority_allocation (organization_id, device_id) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_mstp_alloc_bridge_mac
    ON net.mstp_priority_allocation (organization_id, bridge_mac)
    WHERE deleted_at IS NULL AND bridge_mac IS NOT NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- Reservation shelf
-- ═══════════════════════════════════════════════════════════════════════════

-- ─── net.reservation_shelf ───────────────────────────────────────────────
-- When a number (ASN / IP / VLAN / MLAG / MSTP) is retired, it goes onto
-- the shelf for a cool-down period before the allocation service can
-- re-issue it. Prevents zombie configs on retired gear from colliding
-- with newly-issued numbers.
--
-- resource_type is a discriminator; resource_key is a normalised string
-- representation (e.g. "65121" or "10.11.101.0/24") so the shelf is a
-- single table rather than five.
CREATE TABLE IF NOT EXISTS net.reservation_shelf (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    resource_type     varchar(16)        NOT NULL,
    resource_key      varchar(64)        NOT NULL,
    pool_id           uuid,              -- optional pointer to originating pool
    block_id          uuid,              -- optional pointer to originating block
    retired_at        timestamptz        NOT NULL DEFAULT now(),
    available_after   timestamptz        NOT NULL,   -- retired_at + cooldown
    retired_reason    text,
    status            net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state        net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason       text,
    locked_by         int,
    locked_at         timestamptz,
    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        int,
    deleted_at        timestamptz,
    deleted_by        int,
    notes             text,
    tags              jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs     jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version           int                NOT NULL DEFAULT 1,
    CHECK (resource_type IN ('asn','ip','subnet','vlan','mlag','mstp')),
    CHECK (available_after >= retired_at)
);
CREATE INDEX IF NOT EXISTS ix_shelf_org_type_key
    ON net.reservation_shelf (organization_id, resource_type, resource_key) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_shelf_available_after
    ON net.reservation_shelf (available_after) WHERE deleted_at IS NULL;

-- ═══════════════════════════════════════════════════════════════════════════
-- Forward-wire region defaults
-- ═══════════════════════════════════════════════════════════════════════════

-- Phase 2a left default_ip_pool_id / default_asn_pool_id dangling (column
-- exists, no FK). Wire them now that both targets exist.
ALTER TABLE net.region
    DROP CONSTRAINT IF EXISTS fk_region_default_ip_pool,
    DROP CONSTRAINT IF EXISTS fk_region_default_asn_pool,
    ADD  CONSTRAINT fk_region_default_ip_pool  FOREIGN KEY (default_ip_pool_id)
         REFERENCES net.ip_pool(id)  ON DELETE SET NULL,
    ADD  CONSTRAINT fk_region_default_asn_pool FOREIGN KEY (default_asn_pool_id)
         REFERENCES net.asn_pool(id) ON DELETE SET NULL;
