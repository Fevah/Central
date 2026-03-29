-- Migration 038: Generalized icon override system for grids + ribbon + any UI element.
-- Admin sets defaults, users can override per-element.
-- context: 'ribbon', 'grid.devices', 'grid.switches', 'status', 'device_type', etc.
-- element_key: the specific item (column name, status value, device type name, ribbon button key)

BEGIN;

CREATE TABLE IF NOT EXISTS icon_defaults (
    id          SERIAL PRIMARY KEY,
    context     TEXT NOT NULL,           -- 'ribbon', 'grid.devices', 'status.device', 'device_type'
    element_key TEXT NOT NULL,           -- 'Save', 'Active', 'Core Switch', column name, etc.
    icon_name   TEXT,                    -- Axialist icon name from icon_library
    icon_id     INT REFERENCES icon_library(id) ON DELETE SET NULL,
    color       TEXT,                    -- Optional colour override (hex)
    updated_by  INT,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (context, element_key)
);

CREATE TABLE IF NOT EXISTS user_icon_overrides (
    id          SERIAL PRIMARY KEY,
    user_id     INT NOT NULL,
    context     TEXT NOT NULL,
    element_key TEXT NOT NULL,
    icon_name   TEXT,
    icon_id     INT REFERENCES icon_library(id) ON DELETE SET NULL,
    color       TEXT,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (user_id, context, element_key)
);

CREATE INDEX IF NOT EXISTS idx_icon_defaults_context ON icon_defaults (context);
CREATE INDEX IF NOT EXISTS idx_user_icon_overrides_user ON user_icon_overrides (user_id, context);

-- Seed device status icon defaults
INSERT INTO icon_defaults (context, element_key, color) VALUES
    ('status.device', 'Active', '#22C55E'),
    ('status.device', 'RESERVED', '#F59E0B'),
    ('status.device', 'Decommissioned', '#EF4444'),
    ('status.device', 'Maintenance', '#8B5CF6'),
    ('status.device', 'Unknown', '#6B7280')
ON CONFLICT (context, element_key) DO NOTHING;

-- Seed device type icon defaults (can be overridden with Axialist icons)
INSERT INTO icon_defaults (context, element_key, icon_name) VALUES
    ('device_type', 'Core Switch', 'network-server'),
    ('device_type', 'Access Switch', 'ethernet'),
    ('device_type', 'Router', 'router'),
    ('device_type', 'Firewall', 'firewall'),
    ('device_type', 'Server', 'server'),
    ('device_type', 'AP', 'wireless-signal'),
    ('device_type', 'Default', 'network-server')
ON CONFLICT (context, element_key) DO NOTHING;

COMMIT;
