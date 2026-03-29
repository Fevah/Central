-- Migration 036: SD groups lookup table + seed from existing request data
BEGIN;

CREATE TABLE IF NOT EXISTS sd_groups (
    id          SERIAL PRIMARY KEY,
    name        TEXT NOT NULL UNIQUE,
    is_active   BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order  INT NOT NULL DEFAULT 0,
    synced_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Seed from existing request data
INSERT INTO sd_groups (name)
SELECT DISTINCT group_name FROM sd_requests WHERE group_name <> ''
ON CONFLICT (name) DO NOTHING;

COMMIT;
