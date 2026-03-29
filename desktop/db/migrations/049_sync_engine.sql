-- Migration 049: Integration Sync Engine
-- Pluggable sync framework for connecting to external systems.
-- Modernised from TotalLink's IntegrationServer (Topshelf + OData + Quartz)
-- into Central's ASP.NET background service + REST + pg_notify model.

-- Sync configurations (one per external system integration)
CREATE TABLE IF NOT EXISTS sync_configs (
    id              serial PRIMARY KEY,
    name            varchar(128) NOT NULL UNIQUE,
    agent_type      varchar(64) NOT NULL,           -- 'manage_engine', 'entra_id_scim', 'csv_import', 'rest_api', etc.
    is_enabled      boolean NOT NULL DEFAULT true,
    direction       varchar(16) NOT NULL DEFAULT 'pull',  -- 'pull', 'push', 'bidirectional'
    schedule_cron   varchar(64) DEFAULT '',          -- cron expression (empty = manual only)
    interval_minutes integer DEFAULT 60,             -- fallback if no cron
    max_concurrent  integer DEFAULT 1,               -- concurrent sync tasks
    config_json     jsonb NOT NULL DEFAULT '{}',     -- agent-specific config (URLs, auth, options)
    last_sync_at    timestamptz,
    last_sync_status varchar(32) DEFAULT 'never',    -- 'never', 'running', 'success', 'failed', 'partial'
    last_error      text,
    created_at      timestamptz DEFAULT now(),
    updated_at      timestamptz DEFAULT now()
);

-- Entity mapping (which source entities map to which target entities)
CREATE TABLE IF NOT EXISTS sync_entity_maps (
    id              serial PRIMARY KEY,
    sync_config_id  integer NOT NULL REFERENCES sync_configs(id) ON DELETE CASCADE,
    source_entity   varchar(128) NOT NULL,           -- 'requests', 'users', 'groups', etc.
    target_table    varchar(128) NOT NULL,           -- 'sd_requests', 'app_users', etc.
    mapping_type    varchar(16) DEFAULT 'one_to_one', -- 'one_to_one', 'one_to_many', 'many_to_one'
    is_enabled      boolean DEFAULT true,
    sync_direction  varchar(16) DEFAULT 'pull',       -- override per entity
    filter_expr     text DEFAULT '',                  -- source-side filter (e.g. "status != archived")
    upsert_key      varchar(128) DEFAULT 'id',        -- column(s) for ON CONFLICT matching
    sort_order      integer DEFAULT 0,
    UNIQUE(sync_config_id, source_entity, target_table)
);

-- Field mapping (source field → target column + optional transform)
CREATE TABLE IF NOT EXISTS sync_field_maps (
    id              serial PRIMARY KEY,
    entity_map_id   integer NOT NULL REFERENCES sync_entity_maps(id) ON DELETE CASCADE,
    source_field    varchar(128) NOT NULL,
    target_column   varchar(128) NOT NULL,
    converter_type  varchar(32) DEFAULT 'direct',     -- 'direct', 'constant', 'expression', 'lookup', 'combine', 'split', 'date_format'
    converter_expr  text DEFAULT '',                   -- expression for the converter (depends on type)
    is_key          boolean DEFAULT false,             -- part of the upsert key
    is_required     boolean DEFAULT false,
    default_value   text,
    sort_order      integer DEFAULT 0
);

-- Sync status per entity (tracks last synced version/timestamp to enable delta sync)
CREATE TABLE IF NOT EXISTS sync_status (
    id              serial PRIMARY KEY,
    sync_config_id  integer NOT NULL REFERENCES sync_configs(id) ON DELETE CASCADE,
    entity_name     varchar(128) NOT NULL,
    last_sync_at    timestamptz,
    last_watermark  text DEFAULT '',                   -- last synced ID/timestamp/version for delta
    records_synced  integer DEFAULT 0,
    records_failed  integer DEFAULT 0,
    last_error      text,
    UNIQUE(sync_config_id, entity_name)
);

-- Sync execution log (append-only history)
CREATE TABLE IF NOT EXISTS sync_log (
    id              bigserial PRIMARY KEY,
    sync_config_id  integer NOT NULL REFERENCES sync_configs(id) ON DELETE CASCADE,
    started_at      timestamptz NOT NULL DEFAULT now(),
    completed_at    timestamptz,
    status          varchar(32) NOT NULL DEFAULT 'running',
    entity_name     varchar(128),
    records_read    integer DEFAULT 0,
    records_created integer DEFAULT 0,
    records_updated integer DEFAULT 0,
    records_failed  integer DEFAULT 0,
    error_message   text,
    duration_ms     integer
);

CREATE INDEX IF NOT EXISTS idx_sync_log_config ON sync_log(sync_config_id, started_at DESC);
CREATE INDEX IF NOT EXISTS idx_sync_status_config ON sync_status(sync_config_id);

-- Seed ManageEngine as the first sync config
INSERT INTO sync_configs (name, agent_type, direction, interval_minutes, config_json) VALUES
    ('ManageEngine SDP', 'manage_engine', 'bidirectional', 30,
     '{"base_url":"https://sdpondemand.manageengine.eu/api/v3/","portal_url":"https://itsupport.immunocore.com","fields_required":"priority,urgency,impact","page_size":100,"max_pages":500}')
ON CONFLICT DO NOTHING;

-- Add permission
INSERT INTO permissions (code, name, category, description) VALUES
    ('admin:sync', 'Sync Engine', 'admin', 'Configure and manage data synchronization')
ON CONFLICT DO NOTHING;

-- pg_notify triggers
DO $$
DECLARE tbl text;
BEGIN
    FOREACH tbl IN ARRAY ARRAY['sync_configs', 'sync_entity_maps', 'sync_field_maps', 'sync_status', 'sync_log']
    LOOP
        EXECUTE format(
            'DROP TRIGGER IF EXISTS trg_notify_%s ON %I; CREATE TRIGGER trg_notify_%s AFTER INSERT OR UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION notify_data_change();',
            tbl, tbl, tbl, tbl);
    END LOOP;
END; $$;
