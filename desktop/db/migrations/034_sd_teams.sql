-- Migration 034: Service Desk teams for grouping technicians
BEGIN;

CREATE TABLE IF NOT EXISTS sd_teams (
    id          SERIAL PRIMARY KEY,
    name        TEXT NOT NULL UNIQUE,
    sort_order  INT NOT NULL DEFAULT 0,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS sd_team_members (
    id              SERIAL PRIMARY KEY,
    team_id         INT NOT NULL REFERENCES sd_teams(id) ON DELETE CASCADE,
    technician_name TEXT NOT NULL,
    UNIQUE (team_id, technician_name)
);

CREATE INDEX IF NOT EXISTS idx_sd_team_members_tech ON sd_team_members (technician_name);

COMMIT;
