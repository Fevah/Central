-- Migration 042: Database backup tracking
CREATE TABLE IF NOT EXISTS backup_history (
    id              serial PRIMARY KEY,
    backup_type     varchar(32) NOT NULL DEFAULT 'full',
    file_path       text NOT NULL,
    file_size_bytes bigint,
    tables_included text[],
    started_at      timestamptz NOT NULL DEFAULT now(),
    completed_at    timestamptz,
    status          varchar(32) NOT NULL DEFAULT 'running',
    error_message   text,
    triggered_by    varchar(128) DEFAULT 'admin'
);

-- Add db_backup job type to scheduler
INSERT INTO job_schedules (job_type, name, is_enabled, interval_minutes)
VALUES ('db_backup', 'Scheduled Database Backup', false, 1440)
ON CONFLICT DO NOTHING;

-- Add permissions
INSERT INTO permissions (code, name, category, description) VALUES
    ('admin:backup', 'Database Backup', 'admin', 'Backup and restore database')
ON CONFLICT DO NOTHING;
