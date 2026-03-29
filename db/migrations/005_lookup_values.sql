-- 005_lookup_values.sql
-- Editable dropdown reference data for the desktop app admin panel

CREATE TABLE IF NOT EXISTS lookup_values (
    id          SERIAL PRIMARY KEY,
    category    VARCHAR(64)  NOT NULL,
    value       VARCHAR(128) NOT NULL,
    sort_order  INTEGER      NOT NULL DEFAULT 0,
    grid_name   TEXT         NOT NULL DEFAULT '',
    module      TEXT         NOT NULL DEFAULT '',
    UNIQUE (category, value)
);

-- Seed from existing data
INSERT INTO lookup_values (category, value, sort_order)
SELECT DISTINCT 'status', status, ROW_NUMBER() OVER (ORDER BY status)
FROM switch_guide WHERE status IS NOT NULL
ON CONFLICT DO NOTHING;

INSERT INTO lookup_values (category, value, sort_order)
SELECT DISTINCT 'device_type', device_type, ROW_NUMBER() OVER (ORDER BY device_type)
FROM switch_guide WHERE device_type IS NOT NULL
ON CONFLICT DO NOTHING;

INSERT INTO lookup_values (category, value, sort_order)
SELECT DISTINCT 'building', building, ROW_NUMBER() OVER (ORDER BY building)
FROM switch_guide WHERE building IS NOT NULL
ON CONFLICT DO NOTHING;

INSERT INTO lookup_values (category, value, sort_order)
SELECT DISTINCT 'region', region, ROW_NUMBER() OVER (ORDER BY region)
FROM switch_guide WHERE region IS NOT NULL
ON CONFLICT DO NOTHING;
