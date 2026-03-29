-- 030_icon_library.sql — Icon library for customizable ribbon/grid/tree icons
-- Icons stored as PNG byte arrays in DB for fast load + caching
-- Admin sets default icons, users can override per-element

CREATE TABLE IF NOT EXISTS icon_library (
    id              serial PRIMARY KEY,
    name            varchar(128) NOT NULL,
    category        varchar(64) NOT NULL DEFAULT 'General',    -- Business, Core, Hardware, Users, etc.
    subcategory     varchar(64) DEFAULT '',                     -- matches folder structure
    size            varchar(10) NOT NULL DEFAULT '16x16',       -- 16x16, 24x24, 32x32, 48x48, 64x64
    icon_data       bytea NOT NULL,                             -- PNG image bytes
    file_path       varchar(256) DEFAULT '',                    -- original file path for reference
    created_at      timestamptz DEFAULT now()
);

-- Indexes for fast lookup
CREATE INDEX IF NOT EXISTS idx_icon_library_category ON icon_library (category, subcategory);
CREATE INDEX IF NOT EXISTS idx_icon_library_name ON icon_library (name);
CREATE INDEX IF NOT EXISTS idx_icon_library_size ON icon_library (size);
CREATE UNIQUE INDEX IF NOT EXISTS idx_icon_library_unique ON icon_library (name, category, size);

-- Admin-assigned icons for ribbon items
CREATE TABLE IF NOT EXISTS ribbon_icon_assignments (
    id              serial PRIMARY KEY,
    element_type    varchar(32) NOT NULL,    -- 'ribbon_button', 'grid_column', 'tree_node', 'panel'
    element_key     varchar(128) NOT NULL,   -- unique key like 'devices:add', 'switches:ping'
    icon_id         integer REFERENCES icon_library(id) ON DELETE SET NULL,
    assigned_by     varchar(128) DEFAULT 'admin',
    updated_at      timestamptz DEFAULT now(),
    UNIQUE (element_type, element_key)
);

-- Per-user icon overrides (user can customize their own icons)
CREATE TABLE IF NOT EXISTS user_icon_overrides (
    id              serial PRIMARY KEY,
    user_id         integer NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    element_type    varchar(32) NOT NULL,
    element_key     varchar(128) NOT NULL,
    icon_id         integer REFERENCES icon_library(id) ON DELETE SET NULL,
    updated_at      timestamptz DEFAULT now(),
    UNIQUE (user_id, element_type, element_key)
);
