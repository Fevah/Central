-- 029_job_schedules.sql — Background job scheduling + execution history

CREATE TABLE IF NOT EXISTS job_schedules (
    id              serial PRIMARY KEY,
    job_type        varchar(64) NOT NULL,          -- 'ping_scan', 'config_backup', 'bgp_sync'
    name            varchar(128) NOT NULL,
    is_enabled      boolean NOT NULL DEFAULT false,
    interval_minutes integer NOT NULL DEFAULT 60,
    last_run_at     timestamptz,
    next_run_at     timestamptz,
    target_filter   text DEFAULT '',               -- JSON: {"buildings":["MEP-91"],"roles":["core"]}
    created_by      varchar(128) DEFAULT '',
    created_at      timestamptz DEFAULT now(),
    updated_at      timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS job_history (
    id              serial PRIMARY KEY,
    schedule_id     integer REFERENCES job_schedules(id) ON DELETE SET NULL,
    job_type        varchar(64) NOT NULL,
    started_at      timestamptz NOT NULL DEFAULT now(),
    completed_at    timestamptz,
    status          varchar(32) NOT NULL DEFAULT 'Running',  -- Running, Success, Failed, Cancelled
    result_summary  text DEFAULT '',
    items_total     integer DEFAULT 0,
    items_succeeded integer DEFAULT 0,
    items_failed    integer DEFAULT 0,
    error_message   text DEFAULT '',
    triggered_by    varchar(128) DEFAULT 'scheduler'         -- 'scheduler', 'admin:cory.sharplin', 'api'
);

-- Seed default schedules (disabled)
INSERT INTO job_schedules (job_type, name, is_enabled, interval_minutes) VALUES
    ('ping_scan',     'Ping All Switches',     false, 10),
    ('config_backup', 'Backup Running Configs', false, 1440),
    ('bgp_sync',      'Sync BGP Configs',      false, 360)
ON CONFLICT DO NOTHING;

-- Index for scheduler queries
CREATE INDEX IF NOT EXISTS idx_job_schedules_next_run ON job_schedules (next_run_at) WHERE is_enabled = true;
CREATE INDEX IF NOT EXISTS idx_job_history_started ON job_history (started_at DESC);
