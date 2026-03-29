-- Migration 053: API keys for service-to-service authentication
CREATE TABLE IF NOT EXISTS api_keys (
    id              serial PRIMARY KEY,
    name            varchar(128) NOT NULL UNIQUE,
    key_hash        varchar(128) NOT NULL UNIQUE,    -- SHA256 hash of the key (key itself never stored)
    role            varchar(50) NOT NULL DEFAULT 'Viewer',
    is_active       boolean DEFAULT true,
    created_by      integer REFERENCES app_users(id),
    created_at      timestamptz DEFAULT now(),
    last_used_at    timestamptz,
    use_count       integer DEFAULT 0,
    expires_at      timestamptz                       -- null = never expires
);

-- pg_notify
DROP TRIGGER IF EXISTS trg_notify_api_keys ON api_keys;
CREATE TRIGGER trg_notify_api_keys AFTER INSERT OR UPDATE OR DELETE ON api_keys
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();
