-- =============================================================================
-- Migration 003: Switch Connectivity
-- Adds SSH/management fields and a status tracking table
-- =============================================================================

ALTER TABLE switches
    ADD COLUMN IF NOT EXISTS management_ip   INET,
    ADD COLUMN IF NOT EXISTS ssh_username    VARCHAR(64)  DEFAULT '',
    ADD COLUMN IF NOT EXISTS ssh_port        INT          DEFAULT 22,
    ADD COLUMN IF NOT EXISTS ssh_password    VARCHAR(256),  -- plaintext, local tool only
    ADD COLUMN IF NOT EXISTS last_ping_at    TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS last_ping_ok    BOOLEAN,
    ADD COLUMN IF NOT EXISTS last_ping_ms    NUMERIC(8,2),
    ADD COLUMN IF NOT EXISTS last_ssh_at     TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS last_ssh_ok     BOOLEAN,
    ADD COLUMN IF NOT EXISTS ssh_override_ip TEXT NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS hardware_model  TEXT NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS mac_address     TEXT NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS serial_number   TEXT NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS uptime          TEXT NOT NULL DEFAULT '';

-- Store downloaded running configs
CREATE TABLE IF NOT EXISTS running_configs (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    switch_id       UUID NOT NULL REFERENCES switches(id) ON DELETE CASCADE,
    downloaded_at   TIMESTAMPTZ DEFAULT NOW(),
    source_ip       INET,
    config_text     TEXT NOT NULL,
    line_count      INT,
    diff_from_prev  TEXT    -- unified diff vs previous download, NULL on first
);

CREATE INDEX IF NOT EXISTS idx_running_configs_switch ON running_configs(switch_id);
CREATE INDEX IF NOT EXISTS idx_running_configs_time   ON running_configs(switch_id, downloaded_at DESC);
