-- =============================================================================
-- 025_audit_log_v2.sql — Append-only audit log for all entities
-- Non-destructive: existing switch_audit_log table kept.
-- =============================================================================

CREATE TABLE IF NOT EXISTS audit_log (
    id          BIGSERIAL PRIMARY KEY,
    timestamp   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    user_id     INTEGER REFERENCES app_users(id),
    username    VARCHAR(128) NOT NULL DEFAULT 'system',
    category    VARCHAR(64) NOT NULL,
    entity_id   VARCHAR(64),
    action      VARCHAR(32) NOT NULL,
    summary     TEXT,
    old_value   JSONB,
    new_value   JSONB,
    ip_address  VARCHAR(45)
);

CREATE INDEX IF NOT EXISTS idx_audit_log_category
    ON audit_log (category, timestamp DESC);

CREATE INDEX IF NOT EXISTS idx_audit_log_entity
    ON audit_log (category, entity_id, timestamp DESC);

CREATE INDEX IF NOT EXISTS idx_audit_log_user
    ON audit_log (username, timestamp DESC);

-- Soft delete columns on key tables (non-destructive — adds columns only if missing)
ALTER TABLE p2p_links   ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN DEFAULT FALSE;
ALTER TABLE p2p_links   ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;
ALTER TABLE b2b_links   ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN DEFAULT FALSE;
ALTER TABLE b2b_links   ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;
ALTER TABLE fw_links    ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN DEFAULT FALSE;
ALTER TABLE fw_links    ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;
ALTER TABLE switch_guide ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN DEFAULT FALSE;
ALTER TABLE switch_guide ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;
