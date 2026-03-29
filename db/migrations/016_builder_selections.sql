-- Persists config builder toggle state per device
CREATE TABLE IF NOT EXISTS builder_selections (
    id          SERIAL PRIMARY KEY,
    device_name TEXT NOT NULL,
    section_key TEXT NOT NULL,
    item_key    TEXT NOT NULL DEFAULT '',
    enabled     BOOLEAN NOT NULL DEFAULT TRUE,
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (device_name, section_key, item_key)
);
CREATE INDEX IF NOT EXISTS idx_builder_sel_device ON builder_selections (device_name);
