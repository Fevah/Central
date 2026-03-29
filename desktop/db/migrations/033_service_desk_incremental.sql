-- Migration 033: Service Desk + Integrations tables (IF NOT EXISTS) + incremental sync support
-- Adds me_updated_time for tracking ManageEngine last-modified timestamp per request.
-- Adds last_sync_at to integrations for tracking when we last pulled.

BEGIN;

-- ============================================================
-- Integration tables (may already exist from manual setup)
-- ============================================================

CREATE TABLE IF NOT EXISTS integrations (
    id              SERIAL PRIMARY KEY,
    name            TEXT NOT NULL UNIQUE,
    display_name    TEXT NOT NULL DEFAULT '',
    integration_type TEXT NOT NULL DEFAULT 'oauth2',
    base_url        TEXT NOT NULL DEFAULT '',
    is_enabled      BOOLEAN NOT NULL DEFAULT TRUE,
    config_json     JSONB NOT NULL DEFAULT '{}',
    last_sync_at    TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS integration_credentials (
    id              SERIAL PRIMARY KEY,
    integration_id  INT NOT NULL REFERENCES integrations(id) ON DELETE CASCADE,
    key             TEXT NOT NULL,
    value           TEXT NOT NULL DEFAULT '',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (integration_id, key)
);

CREATE TABLE IF NOT EXISTS integration_log (
    id              SERIAL PRIMARY KEY,
    integration_id  INT NOT NULL REFERENCES integrations(id) ON DELETE CASCADE,
    action          TEXT NOT NULL DEFAULT '',
    status          TEXT NOT NULL DEFAULT '',
    message         TEXT NOT NULL DEFAULT '',
    duration_ms     INT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================
-- Service Desk tables (may already exist from manual setup)
-- ============================================================

CREATE TABLE IF NOT EXISTS sd_requesters (
    id              BIGINT PRIMARY KEY,
    name            TEXT NOT NULL DEFAULT '',
    email           TEXT NOT NULL DEFAULT '',
    phone           TEXT NOT NULL DEFAULT '',
    department      TEXT NOT NULL DEFAULT '',
    site            TEXT NOT NULL DEFAULT '',
    job_title       TEXT NOT NULL DEFAULT '',
    is_vip          BOOLEAN NOT NULL DEFAULT FALSE,
    synced_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS sd_technicians (
    id              BIGINT PRIMARY KEY,
    name            TEXT NOT NULL DEFAULT '',
    email           TEXT NOT NULL DEFAULT '',
    department      TEXT NOT NULL DEFAULT '',
    synced_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS sd_requests (
    id                  BIGINT PRIMARY KEY,
    display_id          TEXT NOT NULL DEFAULT '',
    subject             TEXT NOT NULL DEFAULT '',
    status              TEXT NOT NULL DEFAULT '',
    priority            TEXT NOT NULL DEFAULT '',
    group_name          TEXT NOT NULL DEFAULT '',
    category            TEXT NOT NULL DEFAULT '',
    technician_id       BIGINT REFERENCES sd_technicians(id),
    technician_name     TEXT NOT NULL DEFAULT '',
    requester_id        BIGINT REFERENCES sd_requesters(id),
    requester_name      TEXT NOT NULL DEFAULT '',
    requester_email     TEXT NOT NULL DEFAULT '',
    site                TEXT NOT NULL DEFAULT '',
    department          TEXT NOT NULL DEFAULT '',
    template            TEXT NOT NULL DEFAULT '',
    is_service_request  BOOLEAN NOT NULL DEFAULT FALSE,
    created_at          TIMESTAMPTZ,
    due_by              TIMESTAMPTZ,
    me_created_time     BIGINT,
    me_updated_time     BIGINT,
    ticket_url          TEXT NOT NULL DEFAULT '',
    synced_at           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================
-- Add columns that may be missing from manual setup
-- ============================================================

-- me_updated_time tracks ManageEngine's updated_time (epoch ms) for incremental sync
ALTER TABLE sd_requests ADD COLUMN IF NOT EXISTS me_updated_time BIGINT;

-- ticket_url stores the direct link to the ticket in ManageEngine
ALTER TABLE sd_requests ADD COLUMN IF NOT EXISTS ticket_url TEXT NOT NULL DEFAULT '';

-- last_sync_at on integrations tracks when we last successfully synced
ALTER TABLE integrations ADD COLUMN IF NOT EXISTS last_sync_at TIMESTAMPTZ;

-- ============================================================
-- Indexes for sync performance
-- ============================================================

CREATE INDEX IF NOT EXISTS idx_sd_requests_status ON sd_requests (status);
CREATE INDEX IF NOT EXISTS idx_sd_requests_created_at ON sd_requests (created_at DESC);
CREATE INDEX IF NOT EXISTS idx_sd_requests_me_updated ON sd_requests (me_updated_time DESC);
CREATE INDEX IF NOT EXISTS idx_sd_requests_synced_at ON sd_requests (synced_at DESC);
CREATE INDEX IF NOT EXISTS idx_integration_log_integration ON integration_log (integration_id, created_at DESC);

-- ============================================================
-- pg_notify triggers for real-time SignalR
-- ============================================================

CREATE OR REPLACE FUNCTION notify_sd_requests_change() RETURNS trigger AS $$
BEGIN
    PERFORM pg_notify('table_change', 'sd_requests');
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS sd_requests_notify ON sd_requests;
CREATE TRIGGER sd_requests_notify
    AFTER INSERT OR UPDATE OR DELETE ON sd_requests
    FOR EACH STATEMENT EXECUTE FUNCTION notify_sd_requests_change();

-- ============================================================
-- Permissions for Service Desk module
-- ============================================================

INSERT INTO permissions (code, name, category, description, sort_order) VALUES
    ('servicedesk:read',   'Service Desk Read',   'servicedesk', 'View service desk requests',  1),
    ('servicedesk:write',  'Service Desk Write',  'servicedesk', 'Edit service desk requests',  2),
    ('servicedesk:sync',   'Service Desk Sync',   'servicedesk', 'Sync from ManageEngine',      3),
    ('servicedesk:delete', 'Service Desk Delete', 'servicedesk', 'Delete service desk requests', 4)
ON CONFLICT (code) DO NOTHING;

-- Grant all to Admin role
INSERT INTO role_permission_grants (role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.name = 'Admin' AND p.category = 'servicedesk'
ON CONFLICT DO NOTHING;

-- Grant read + sync + write to Operator
INSERT INTO role_permission_grants (role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.name = 'Operator' AND p.code IN ('servicedesk:read', 'servicedesk:sync', 'servicedesk:write')
ON CONFLICT DO NOTHING;

-- Grant read to Viewer
INSERT INTO role_permission_grants (role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.name = 'Viewer' AND p.code = 'servicedesk:read'
ON CONFLICT DO NOTHING;

-- ============================================================
-- Seed ManageEngine integration (config from DB, not hardcoded)
-- ============================================================

INSERT INTO integrations (name, display_name, integration_type, base_url, is_enabled, config_json)
VALUES (
    'manageengine',
    'ManageEngine ServiceDesk Plus',
    'oauth2',
    'https://sdpondemand.manageengine.eu',
    true,
    '{"oauth_url": "https://accounts.zoho.eu/oauth/v2/token", "portal_url": "https://itsupport.immunocore.com"}'::jsonb
)
ON CONFLICT (name) DO UPDATE SET
    config_json = EXCLUDED.config_json,
    base_url = EXCLUDED.base_url;

-- ============================================================
-- Admin default: expected daily closures per tech (configurable)
-- ============================================================

INSERT INTO default_user_settings (setting_key, setting_value)
VALUES ('sd.expected_daily_closures', '5')
ON CONFLICT (setting_key) DO NOTHING;

COMMIT;
