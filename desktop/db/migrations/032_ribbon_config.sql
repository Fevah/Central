-- 032: DB-backed ribbon configuration
-- Modules register default ribbon items. Admins can override via these tables.
-- Engine merges: module defaults + DB overrides = final ribbon.

CREATE TABLE IF NOT EXISTS ribbon_pages (
    id              SERIAL PRIMARY KEY,
    header          VARCHAR(64) NOT NULL,
    sort_order      INTEGER NOT NULL DEFAULT 0,
    required_permission VARCHAR(100),
    icon_name       VARCHAR(128),
    is_visible      BOOLEAN NOT NULL DEFAULT TRUE,
    is_system       BOOLEAN NOT NULL DEFAULT FALSE,  -- system pages can't be deleted
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (header)
);

CREATE TABLE IF NOT EXISTS ribbon_groups (
    id              SERIAL PRIMARY KEY,
    page_id         INTEGER NOT NULL REFERENCES ribbon_pages(id) ON DELETE CASCADE,
    header          VARCHAR(64) NOT NULL,
    sort_order      INTEGER NOT NULL DEFAULT 0,
    is_visible      BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (page_id, header)
);

CREATE TABLE IF NOT EXISTS ribbon_items (
    id              SERIAL PRIMARY KEY,
    group_id        INTEGER NOT NULL REFERENCES ribbon_groups(id) ON DELETE CASCADE,
    content         VARCHAR(128) NOT NULL,
    item_type       VARCHAR(32) NOT NULL DEFAULT 'button',  -- button, split, check, toggle, separator
    sort_order      INTEGER NOT NULL DEFAULT 0,
    permission      VARCHAR(100),
    glyph           VARCHAR(128),
    large_glyph     VARCHAR(128),
    icon_id         INTEGER REFERENCES icon_library(id),     -- custom icon from icon_library
    command_type    VARCHAR(64),       -- navigate_panel, open_url, execute_action
    command_param   VARCHAR(256),      -- panel name, URL, or action key
    tooltip         TEXT,
    is_visible      BOOLEAN NOT NULL DEFAULT TRUE,
    is_system       BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW()
);

-- Seed system pages from modules
INSERT INTO ribbon_pages (header, sort_order, required_permission, is_system) VALUES
    ('Home',     0,  NULL, TRUE),
    ('Devices', 10,  'devices:read', TRUE),
    ('Switches', 20, 'switches:read', TRUE),
    ('Links',   30,  'links:read', TRUE),
    ('Routing', 40,  'bgp:read', TRUE),
    ('VLANs',   50,  'vlans:read', TRUE),
    ('Tasks',   60,  'tasks:read', TRUE),
    ('Admin',   90,  'admin:users', TRUE)
ON CONFLICT (header) DO NOTHING;

-- pg_notify triggers for real-time sync
CREATE OR REPLACE FUNCTION notify_ribbon_change() RETURNS trigger AS $$
BEGIN
    PERFORM pg_notify('data_changed', json_build_object(
        'table', TG_TABLE_NAME, 'op', TG_OP, 'id', COALESCE(NEW.id, OLD.id)
    )::text);
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_ribbon_pages ON ribbon_pages;
CREATE TRIGGER trg_ribbon_pages AFTER INSERT OR UPDATE OR DELETE ON ribbon_pages FOR EACH ROW EXECUTE FUNCTION notify_ribbon_change();
DROP TRIGGER IF EXISTS trg_ribbon_groups ON ribbon_groups;
CREATE TRIGGER trg_ribbon_groups AFTER INSERT OR UPDATE OR DELETE ON ribbon_groups FOR EACH ROW EXECUTE FUNCTION notify_ribbon_change();
DROP TRIGGER IF EXISTS trg_ribbon_items ON ribbon_items;
CREATE TRIGGER trg_ribbon_items AFTER INSERT OR UPDATE OR DELETE ON ribbon_items FOR EACH ROW EXECUTE FUNCTION notify_ribbon_change();
