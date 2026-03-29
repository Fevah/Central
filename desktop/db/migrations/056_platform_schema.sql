-- Migration 056: Multi-tenant platform schema
-- Creates the central_platform schema for cross-tenant data:
-- tenants, subscriptions, module licenses, global users, environments, update management.

CREATE SCHEMA IF NOT EXISTS central_platform;

-- ── Tenants ──
CREATE TABLE IF NOT EXISTS central_platform.tenants (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    slug            varchar(64) NOT NULL UNIQUE,
    display_name    varchar(255) NOT NULL,
    domain          varchar(255),
    is_active       boolean NOT NULL DEFAULT true,
    tier            varchar(32) NOT NULL DEFAULT 'free',
    created_at      timestamptz NOT NULL DEFAULT now(),
    suspended_at    timestamptz,
    metadata        jsonb DEFAULT '{}'
);

-- ── Subscription Plans ──
CREATE TABLE IF NOT EXISTS central_platform.subscription_plans (
    id              serial PRIMARY KEY,
    tier            varchar(32) NOT NULL UNIQUE,
    display_name    varchar(128) NOT NULL,
    max_users       integer,
    max_devices     integer,
    price_monthly   decimal(10,2),
    features_json   jsonb DEFAULT '{}'
);

CREATE TABLE IF NOT EXISTS central_platform.tenant_subscriptions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id),
    plan_id         integer NOT NULL REFERENCES central_platform.subscription_plans(id),
    status          varchar(32) NOT NULL DEFAULT 'active',
    started_at      timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz,
    stripe_sub_id   varchar(128),
    metadata        jsonb DEFAULT '{}'
);

-- ── Module Licensing ──
CREATE TABLE IF NOT EXISTS central_platform.module_catalog (
    id              serial PRIMARY KEY,
    code            varchar(64) NOT NULL UNIQUE,
    display_name    varchar(128) NOT NULL,
    description     text,
    price_monthly   decimal(10,2),
    is_base         boolean DEFAULT false
);

CREATE TABLE IF NOT EXISTS central_platform.tenant_module_licenses (
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id),
    module_id       integer NOT NULL REFERENCES central_platform.module_catalog(id),
    granted_at      timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz,
    PRIMARY KEY (tenant_id, module_id)
);

-- ── License Keys ──
CREATE TABLE IF NOT EXISTS central_platform.license_keys (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id),
    key_hash        varchar(128) NOT NULL UNIQUE,
    hardware_id     varchar(256),
    issued_at       timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz,
    is_revoked      boolean DEFAULT false,
    metadata        jsonb DEFAULT '{}'
);

-- ── Global Users (cross-tenant) ──
CREATE TABLE IF NOT EXISTS central_platform.global_users (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email           varchar(256) NOT NULL UNIQUE,
    display_name    varchar(255),
    password_hash   varchar(512),
    salt            varchar(128),
    email_verified  boolean DEFAULT false,
    verify_token    varchar(128),
    created_at      timestamptz NOT NULL DEFAULT now(),
    last_login_at   timestamptz
);

CREATE TABLE IF NOT EXISTS central_platform.tenant_memberships (
    user_id         uuid NOT NULL REFERENCES central_platform.global_users(id),
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id),
    local_user_id   integer,
    role            varchar(64) NOT NULL DEFAULT 'Viewer',
    joined_at       timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, tenant_id)
);

-- ── Environment Profiles ──
CREATE TABLE IF NOT EXISTS central_platform.environments (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id),
    name            varchar(64) NOT NULL,
    environment_type varchar(16) NOT NULL DEFAULT 'live',
    api_url         text NOT NULL,
    signalr_url     text,
    cert_fingerprint varchar(128),
    is_default      boolean DEFAULT false,
    metadata        jsonb DEFAULT '{}',
    UNIQUE (tenant_id, name)
);

-- ── Update Management ──
CREATE TABLE IF NOT EXISTS central_platform.release_channels (
    id              serial PRIMARY KEY,
    name            varchar(64) NOT NULL UNIQUE,
    is_default      boolean DEFAULT false
);

CREATE TABLE IF NOT EXISTS central_platform.client_versions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    version         varchar(32) NOT NULL,
    channel_id      integer REFERENCES central_platform.release_channels(id),
    platform        varchar(32) NOT NULL,
    manifest_json   jsonb NOT NULL,
    package_url     text NOT NULL,
    delta_from      varchar(32),
    release_notes   text,
    published_at    timestamptz NOT NULL DEFAULT now(),
    is_mandatory    boolean DEFAULT false
);

CREATE TABLE IF NOT EXISTS central_platform.tenant_version_policy (
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id),
    channel_id      integer NOT NULL REFERENCES central_platform.release_channels(id),
    pinned_version  varchar(32),
    auto_update     boolean DEFAULT true,
    PRIMARY KEY (tenant_id)
);

CREATE TABLE IF NOT EXISTS central_platform.client_installations (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id),
    hardware_id     varchar(256),
    current_version varchar(32),
    platform        varchar(32),
    last_check_at   timestamptz,
    last_update_at  timestamptz
);

-- ── Seed Data ──
INSERT INTO central_platform.subscription_plans (tier, display_name, max_users, max_devices, price_monthly) VALUES
    ('free', 'Free', 3, 50, 0),
    ('professional', 'Professional', 25, 500, 49.00),
    ('enterprise', 'Enterprise', null, null, 199.00)
ON CONFLICT DO NOTHING;

INSERT INTO central_platform.module_catalog (code, display_name, is_base) VALUES
    ('devices', 'Device Management (IPAM)', true),
    ('switches', 'Switch Configuration', true),
    ('links', 'Network Links', true),
    ('routing', 'BGP Routing', false),
    ('vlans', 'VLAN Management', false),
    ('servicedesk', 'Service Desk', false),
    ('tasks', 'Task Management', false),
    ('admin', 'Administration', true)
ON CONFLICT DO NOTHING;

INSERT INTO central_platform.release_channels (name, is_default) VALUES
    ('stable', true),
    ('beta', false),
    ('canary', false)
ON CONFLICT DO NOTHING;

-- Seed default tenant (backward compatibility for existing single-tenant deployments)
INSERT INTO central_platform.tenants (id, slug, display_name, tier, metadata) VALUES
    ('00000000-0000-0000-0000-000000000000', 'default', 'Default Tenant', 'enterprise', '{"single_tenant": true}')
ON CONFLICT DO NOTHING;
