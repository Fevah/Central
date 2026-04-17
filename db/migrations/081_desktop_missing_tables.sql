-- =============================================================================
-- 081 — Hotfix for local dev: tables the desktop expects that were lost when
-- earlier migrations were renumbered (identity_providers, auth_events,
-- sync_configs family, panel_customizations). Schema derived from the
-- Central.Data DbRepository queries that read/write these tables.
-- =============================================================================

-- Identity providers (SAML/OIDC/SSO config)
CREATE TABLE IF NOT EXISTS identity_providers (
    id            serial PRIMARY KEY,
    provider_type text NOT NULL,                 -- saml, oidc, oauth, local, entra, okta, duo
    name          text NOT NULL,
    is_enabled    boolean NOT NULL DEFAULT true,
    is_default    boolean NOT NULL DEFAULT false,
    priority      int NOT NULL DEFAULT 100,
    config_json   jsonb NOT NULL DEFAULT '{}'::jsonb,
    metadata_url  text,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_idp_priority ON identity_providers(priority) WHERE is_enabled;

-- Email domain → identity provider routing
CREATE TABLE IF NOT EXISTS idp_domain_mappings (
    id            serial PRIMARY KEY,
    provider_id   int NOT NULL REFERENCES identity_providers(id) ON DELETE CASCADE,
    email_domain  text NOT NULL UNIQUE,
    created_at    timestamptz NOT NULL DEFAULT now()
);

-- Auth event log (success/failure records)
CREATE TABLE IF NOT EXISTS auth_events (
    id             bigserial PRIMARY KEY,
    timestamp      timestamptz NOT NULL DEFAULT now(),
    event_type     text NOT NULL,                -- login, logout, failed_login, mfa_challenge, password_change
    provider_type  text,
    username       text,
    user_id        int,
    success        boolean NOT NULL DEFAULT true,
    error_message  text,
    ip_address     text,
    user_agent     text
);

CREATE INDEX IF NOT EXISTS idx_auth_events_time ON auth_events(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_auth_events_user ON auth_events(user_id, timestamp DESC);

-- Sync engine — per-integration sync configurations
CREATE TABLE IF NOT EXISTS sync_configs (
    id                 serial PRIMARY KEY,
    name               text NOT NULL UNIQUE,
    agent_type         text NOT NULL,
    is_enabled         boolean NOT NULL DEFAULT true,
    direction          text NOT NULL DEFAULT 'pull',   -- pull, push, bidirectional
    schedule_cron      text,
    interval_minutes   int DEFAULT 60,
    max_concurrent     int DEFAULT 1,
    config_json        jsonb NOT NULL DEFAULT '{}'::jsonb,
    last_sync_at       timestamptz,
    last_sync_status   text,
    last_error         text,
    created_at         timestamptz NOT NULL DEFAULT now(),
    updated_at         timestamptz NOT NULL DEFAULT now()
);

-- Entity-to-table mapping for a sync config
CREATE TABLE IF NOT EXISTS sync_entity_maps (
    id               serial PRIMARY KEY,
    sync_config_id   int NOT NULL REFERENCES sync_configs(id) ON DELETE CASCADE,
    source_entity    text NOT NULL,
    target_table     text NOT NULL,
    mapping_type     text NOT NULL DEFAULT 'direct',
    is_enabled       boolean NOT NULL DEFAULT true,
    sync_direction   text NOT NULL DEFAULT 'pull',
    filter_expr      text,
    upsert_key       text,
    sort_order       int NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_sync_entity_config ON sync_entity_maps(sync_config_id);

-- Field-to-column mapping for an entity map
CREATE TABLE IF NOT EXISTS sync_field_maps (
    id               serial PRIMARY KEY,
    entity_map_id    int NOT NULL REFERENCES sync_entity_maps(id) ON DELETE CASCADE,
    source_field     text NOT NULL,
    target_column    text NOT NULL,
    converter_type   text NOT NULL DEFAULT 'identity',
    converter_expr   text,
    is_key           boolean NOT NULL DEFAULT false,
    is_required      boolean NOT NULL DEFAULT false,
    default_value    text,
    sort_order       int NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_sync_field_entity ON sync_field_maps(entity_map_id);

-- Per-user panel UI customizations (column layouts, filters, visible panels)
CREATE TABLE IF NOT EXISTS panel_customizations (
    id              serial PRIMARY KEY,
    user_id         int NOT NULL,
    panel_name      text NOT NULL,
    setting_type    text NOT NULL,                -- layout, columns, filter, sort
    setting_key     text NOT NULL,
    setting_json    jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (user_id, panel_name, setting_type, setting_key)
);

CREATE INDEX IF NOT EXISTS idx_panel_cust_user ON panel_customizations(user_id, panel_name);
