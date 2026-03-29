-- Migration 058: Sync engine resilience — dead letter queue, retry tracking, change hashes

-- Dead letter queue for failed sync records
CREATE TABLE IF NOT EXISTS sync_failed_records (
    id              bigserial PRIMARY KEY,
    sync_config_id  integer NOT NULL REFERENCES sync_configs(id) ON DELETE CASCADE,
    entity_map_id   integer REFERENCES sync_entity_maps(id) ON DELETE SET NULL,
    source_entity   varchar(128),
    record_key      varchar(256),                    -- source record identifier
    record_json     jsonb NOT NULL,                  -- the full source record that failed
    error_message   text NOT NULL,
    retry_count     integer DEFAULT 0,
    max_retries     integer DEFAULT 3,
    next_retry_at   timestamptz,
    status          varchar(32) DEFAULT 'pending',   -- pending, retrying, abandoned, resolved
    created_at      timestamptz NOT NULL DEFAULT now(),
    resolved_at     timestamptz
);

CREATE INDEX IF NOT EXISTS idx_sync_failed_status ON sync_failed_records(status, next_retry_at);
CREATE INDEX IF NOT EXISTS idx_sync_failed_config ON sync_failed_records(sync_config_id);

-- Change hash tracking (prevents fake updates)
CREATE TABLE IF NOT EXISTS sync_record_hashes (
    sync_config_id  integer NOT NULL REFERENCES sync_configs(id) ON DELETE CASCADE,
    entity_name     varchar(128) NOT NULL,
    record_key      varchar(256) NOT NULL,
    content_hash    varchar(64) NOT NULL,            -- SHA-256 of record content
    last_synced_at  timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (sync_config_id, entity_name, record_key)
);

-- Add retry config to sync_configs
ALTER TABLE sync_configs ADD COLUMN IF NOT EXISTS max_retries integer DEFAULT 3;
ALTER TABLE sync_configs ADD COLUMN IF NOT EXISTS retry_delay_seconds integer DEFAULT 60;

-- pg_notify
DO $$ BEGIN
    EXECUTE 'DROP TRIGGER IF EXISTS trg_notify_sync_failed_records ON sync_failed_records; CREATE TRIGGER trg_notify_sync_failed_records AFTER INSERT OR UPDATE ON sync_failed_records FOR EACH ROW EXECUTE FUNCTION notify_data_change();';
END; $$;
