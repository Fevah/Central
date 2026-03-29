-- Migration 041: Migration history tracking
CREATE TABLE IF NOT EXISTS migration_history (
    id              serial PRIMARY KEY,
    migration_name  varchar(256) NOT NULL UNIQUE,
    applied_at      timestamptz NOT NULL DEFAULT now(),
    duration_ms     integer,
    applied_by      varchar(128) DEFAULT 'system',
    checksum        varchar(64)  DEFAULT ''
);

-- Seed existing migrations as already applied
INSERT INTO migration_history (migration_name, applied_at) VALUES
    ('026_pg_notify', now()), ('027_user_auth', now()), ('028_default_settings', now()),
    ('029_job_schedules', now()), ('030_icon_library', now()), ('031_tasks', now()),
    ('032_ribbon_config', now()), ('033_service_desk_incremental', now()),
    ('034_sd_teams', now()), ('035_sd_resolved_at', now()), ('036_sd_groups', now()),
    ('037_catchup_columns', now()), ('038_icon_overrides', now()),
    ('039_extended_user_fields', now()), ('040_ad_config', now()),
    ('041_migration_history', now())
ON CONFLICT DO NOTHING;

-- Add permissions
INSERT INTO permissions (code, name, category, description) VALUES
    ('admin:migrations', 'Schema Migrations', 'admin', 'View and apply database migrations'),
    ('admin:purge', 'Purge Records', 'admin', 'Purge soft-deleted records')
ON CONFLICT DO NOTHING;
