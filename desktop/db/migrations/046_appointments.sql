-- Migration 046: Scheduler / Appointments
CREATE TABLE IF NOT EXISTS appointments (
    id              serial PRIMARY KEY,
    subject         varchar(256) NOT NULL,
    description     text DEFAULT '',
    start_time      timestamptz NOT NULL,
    end_time        timestamptz NOT NULL,
    all_day         boolean DEFAULT false,
    location        varchar(256) DEFAULT '',
    resource_id     integer,
    status          integer DEFAULT 0,
    label           integer DEFAULT 0,
    recurrence_info text DEFAULT '',
    task_id         integer REFERENCES tasks(id) ON DELETE SET NULL,
    ticket_id       bigint,
    created_by      integer REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS appointment_resources (
    id              serial PRIMARY KEY,
    user_id         integer NOT NULL REFERENCES app_users(id),
    display_name    varchar(128) NOT NULL,
    color           varchar(16) DEFAULT '#3B82F6',
    is_active       boolean DEFAULT true
);

CREATE INDEX IF NOT EXISTS idx_appointments_time ON appointments(start_time, end_time);
CREATE INDEX IF NOT EXISTS idx_appointments_resource ON appointments(resource_id);

-- Add permissions
INSERT INTO permissions (code, name, category, description) VALUES
    ('scheduler:read', 'View Schedule', 'scheduler', 'View calendar and appointments'),
    ('scheduler:write', 'Edit Schedule', 'scheduler', 'Create and modify appointments')
ON CONFLICT DO NOTHING;
