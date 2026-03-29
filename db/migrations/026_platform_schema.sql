-- 026_platform_schema.sql
-- Multi-tenant platform schema for enterprise features:
-- tenants, subscriptions, module licensing, global users, environments, updates, security policies

CREATE SCHEMA IF NOT EXISTS central_platform;

-- ── Tenants ──

CREATE TABLE IF NOT EXISTS central_platform.tenants (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    slug            varchar(64) NOT NULL UNIQUE,
    display_name    varchar(255) NOT NULL,
    domain          varchar(255),
    tier            varchar(32) NOT NULL DEFAULT 'free',
    metadata        jsonb DEFAULT '{}',
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

-- ── Subscription Plans ──

CREATE TABLE IF NOT EXISTS central_platform.subscription_plans (
    id              serial PRIMARY KEY,
    tier            varchar(32) NOT NULL UNIQUE,
    display_name    varchar(128) NOT NULL,
    max_users       integer,          -- NULL = unlimited
    max_devices     integer,          -- NULL = unlimited
    price_monthly   numeric(10,2),
    features        jsonb DEFAULT '{}',
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- ── Tenant Subscriptions ──

CREATE TABLE IF NOT EXISTS central_platform.tenant_subscriptions (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    plan_id         integer NOT NULL REFERENCES central_platform.subscription_plans(id),
    status          varchar(32) NOT NULL DEFAULT 'active',
    started_at      timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz,
    stripe_sub_id   varchar(255)
);
CREATE INDEX IF NOT EXISTS idx_tenant_sub_tenant ON central_platform.tenant_subscriptions(tenant_id);

-- ── Module Catalog ──

CREATE TABLE IF NOT EXISTS central_platform.module_catalog (
    id              serial PRIMARY KEY,
    code            varchar(64) NOT NULL UNIQUE,
    display_name    varchar(128) NOT NULL,
    description     text,
    is_base         boolean NOT NULL DEFAULT false
);

-- ── Tenant Module Licenses ──

CREATE TABLE IF NOT EXISTS central_platform.tenant_module_licenses (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    module_id       integer NOT NULL REFERENCES central_platform.module_catalog(id),
    granted_at      timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz,
    UNIQUE(tenant_id, module_id)
);

-- ── Global Users (cross-tenant) ──

CREATE TABLE IF NOT EXISTS central_platform.global_users (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email           varchar(255) NOT NULL UNIQUE,
    display_name    varchar(255),
    password_hash   varchar(255) NOT NULL,
    salt            varchar(64) NOT NULL,
    email_verified  boolean NOT NULL DEFAULT false,
    verify_token    varchar(64),
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- ── Tenant Memberships ──

CREATE TABLE IF NOT EXISTS central_platform.tenant_memberships (
    id              serial PRIMARY KEY,
    user_id         uuid NOT NULL REFERENCES central_platform.global_users(id) ON DELETE CASCADE,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    role            varchar(64) NOT NULL DEFAULT 'Viewer',
    joined_at       timestamptz NOT NULL DEFAULT now(),
    UNIQUE(user_id, tenant_id)
);

-- ── License Keys ──

CREATE TABLE IF NOT EXISTS central_platform.license_keys (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    key_hash        varchar(255) NOT NULL,
    hardware_id     varchar(255),
    is_revoked      boolean NOT NULL DEFAULT false,
    expires_at      timestamptz,
    issued_at       timestamptz NOT NULL DEFAULT now()
);

-- ── Environments ──

CREATE TABLE IF NOT EXISTS central_platform.environments (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    name            varchar(64) NOT NULL DEFAULT 'live',
    api_url         varchar(512) NOT NULL,
    description     text,
    cert_fingerprint varchar(128),
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, name)
);

-- ── Release Channels ──

CREATE TABLE IF NOT EXISTS central_platform.release_channels (
    id              serial PRIMARY KEY,
    name            varchar(64) NOT NULL UNIQUE,
    description     text
);

-- ── Client Versions ──

CREATE TABLE IF NOT EXISTS central_platform.client_versions (
    id              serial PRIMARY KEY,
    version         varchar(32) NOT NULL,
    platform        varchar(32) NOT NULL DEFAULT 'windows-x64',
    package_url     varchar(1024) NOT NULL,
    manifest_json   jsonb DEFAULT '{}',
    release_notes   text,
    is_mandatory    boolean NOT NULL DEFAULT false,
    delta_from      varchar(32),
    channel_id      integer REFERENCES central_platform.release_channels(id),
    published_at    timestamptz NOT NULL DEFAULT now()
);

-- ── Tenant Version Policy ──

CREATE TABLE IF NOT EXISTS central_platform.tenant_version_policy (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    pinned_version  varchar(32),
    auto_update     boolean NOT NULL DEFAULT true,
    channel_id      integer REFERENCES central_platform.release_channels(id),
    UNIQUE(tenant_id)
);

-- ── Client Installations ──

CREATE TABLE IF NOT EXISTS central_platform.client_installations (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    hardware_id     varchar(255) NOT NULL,
    current_version varchar(32),
    platform        varchar(32) NOT NULL DEFAULT 'windows-x64',
    last_seen_at    timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, hardware_id)
);

-- ── Security Policies (ABAC) ──

CREATE TABLE IF NOT EXISTS central_platform.security_policies (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    entity_type     varchar(64) NOT NULL,
    policy_type     varchar(16) NOT NULL DEFAULT 'row',
    effect          varchar(16) NOT NULL DEFAULT 'allow',
    conditions      jsonb DEFAULT '{}',
    hidden_fields   text[],
    priority        integer DEFAULT 100,
    is_enabled      boolean NOT NULL DEFAULT true,
    description     text,
    created_at      timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_security_policies_tenant ON central_platform.security_policies(tenant_id);

-- ══════════════════════════════════════════════════════════════════
-- Seed Data
-- ══════════════════════════════════════════════════════════════════

-- Subscription plans
INSERT INTO central_platform.subscription_plans (tier, display_name, max_users, max_devices, price_monthly)
VALUES
    ('free',         'Free',         3,    50,    0.00),
    ('professional', 'Professional', 25,   500,   49.00),
    ('enterprise',   'Enterprise',   NULL, NULL,  NULL)
ON CONFLICT (tier) DO NOTHING;

-- Module catalog
INSERT INTO central_platform.module_catalog (code, display_name, is_base) VALUES
    ('devices',     'Devices / IPAM',    true),
    ('switches',    'Switches',          true),
    ('links',       'Links',             true),
    ('routing',     'Routing / BGP',     false),
    ('vlans',       'VLANs',             false),
    ('admin',       'Administration',    true),
    ('tasks',       'Tasks',             false),
    ('servicedesk', 'Service Desk',      false)
ON CONFLICT (code) DO NOTHING;

-- Release channels
INSERT INTO central_platform.release_channels (name, description) VALUES
    ('stable', 'Production-ready releases'),
    ('beta',   'Pre-release testing'),
    ('canary', 'Early development builds')
ON CONFLICT (name) DO NOTHING;

-- Default tenant (backward compatibility for single-tenant installs)
INSERT INTO central_platform.tenants (id, slug, display_name, tier)
VALUES ('00000000-0000-0000-0000-000000000000', 'default', 'Default Tenant', 'enterprise')
ON CONFLICT (slug) DO NOTHING;

-- Default tenant gets enterprise subscription
INSERT INTO central_platform.tenant_subscriptions (tenant_id, plan_id, status)
SELECT '00000000-0000-0000-0000-000000000000', id, 'active'
FROM central_platform.subscription_plans WHERE tier = 'enterprise'
ON CONFLICT DO NOTHING;

-- Default tenant gets all modules
INSERT INTO central_platform.tenant_module_licenses (tenant_id, module_id)
SELECT '00000000-0000-0000-0000-000000000000', id FROM central_platform.module_catalog
ON CONFLICT (tenant_id, module_id) DO NOTHING;
