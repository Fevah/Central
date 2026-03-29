-- Migration 054: Notification preferences + active sessions

-- Notification preferences per user (which events trigger alerts)
CREATE TABLE IF NOT EXISTS notification_preferences (
    id              serial PRIMARY KEY,
    user_id         integer NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    event_type      varchar(64) NOT NULL,             -- 'sync_failure', 'auth_lockout', 'backup_complete', 'data_changed', etc.
    channel         varchar(32) NOT NULL DEFAULT 'toast', -- 'toast', 'email', 'both', 'none'
    is_enabled      boolean DEFAULT true,
    UNIQUE(user_id, event_type)
);

-- Active sessions (track who's logged in from where)
CREATE TABLE IF NOT EXISTS active_sessions (
    id              serial PRIMARY KEY,
    user_id         integer NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    session_token   varchar(256) NOT NULL UNIQUE,
    auth_method     varchar(32) NOT NULL,             -- 'windows', 'password', 'entra_id', 'okta', 'saml2'
    ip_address      varchar(45),
    machine_name    varchar(128),
    started_at      timestamptz NOT NULL DEFAULT now(),
    last_activity   timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz,
    is_active       boolean DEFAULT true
);

CREATE INDEX IF NOT EXISTS idx_active_sessions_user ON active_sessions(user_id, is_active);

-- Seed default notification preferences for existing users
INSERT INTO notification_preferences (user_id, event_type, channel)
SELECT id, 'sync_failure', 'toast' FROM app_users WHERE is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO notification_preferences (user_id, event_type, channel)
SELECT id, 'auth_lockout', 'toast' FROM app_users WHERE is_active = true
ON CONFLICT DO NOTHING;

-- pg_notify
DO $$ BEGIN
    EXECUTE 'DROP TRIGGER IF EXISTS trg_notify_notification_preferences ON notification_preferences; CREATE TRIGGER trg_notify_notification_preferences AFTER INSERT OR UPDATE OR DELETE ON notification_preferences FOR EACH ROW EXECUTE FUNCTION notify_data_change();';
    EXECUTE 'DROP TRIGGER IF EXISTS trg_notify_active_sessions ON active_sessions; CREATE TRIGGER trg_notify_active_sessions AFTER INSERT OR UPDATE OR DELETE ON active_sessions FOR EACH ROW EXECUTE FUNCTION notify_data_change();';
END; $$;
