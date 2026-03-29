-- Migration 052: Structured audit log + password history
-- Tracks all CRUD operations with before/after snapshots.

CREATE TABLE IF NOT EXISTS audit_log (
    id              bigserial PRIMARY KEY,
    action          varchar(32) NOT NULL,             -- Create, Update, Delete, View, Export, Login, SettingChange
    entity_type     varchar(64) NOT NULL,             -- Device, Switch, User, Role, SdRequest, etc.
    entity_id       varchar(64),
    entity_name     varchar(256),
    username        varchar(128),
    user_id         integer REFERENCES app_users(id),
    details         text,
    before_json     jsonb,                            -- snapshot before change
    after_json      jsonb,                            -- snapshot after change
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_audit_log_entity ON audit_log(entity_type, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_audit_log_user ON audit_log(username, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_audit_log_time ON audit_log(created_at DESC);

-- Password history (for password reuse prevention)
CREATE TABLE IF NOT EXISTS password_history (
    id              serial PRIMARY KEY,
    user_id         integer NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    password_hash   varchar(128) NOT NULL,
    changed_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_password_history_user ON password_history(user_id, changed_at DESC);

-- pg_notify triggers
DO $$ BEGIN
    EXECUTE 'DROP TRIGGER IF EXISTS trg_notify_audit_log ON audit_log; CREATE TRIGGER trg_notify_audit_log AFTER INSERT ON audit_log FOR EACH ROW EXECUTE FUNCTION notify_data_change();';
END; $$;
