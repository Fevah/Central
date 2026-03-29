-- =============================================================================
-- 006_users_roles.sql — Users, roles, permissions, user settings
-- =============================================================================

-- Users
CREATE TABLE IF NOT EXISTS app_users (
    id              SERIAL PRIMARY KEY,
    username        CITEXT NOT NULL UNIQUE,
    display_name    VARCHAR(128),
    role            VARCHAR(32) NOT NULL DEFAULT 'Viewer',
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    auto_login      BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW()
);

-- Role → module permissions
CREATE TABLE IF NOT EXISTS role_permissions (
    id              SERIAL PRIMARY KEY,
    role            VARCHAR(32) NOT NULL,
    module          VARCHAR(64) NOT NULL,
    can_view        BOOLEAN NOT NULL DEFAULT TRUE,
    can_edit        BOOLEAN NOT NULL DEFAULT FALSE,
    can_delete      BOOLEAN NOT NULL DEFAULT FALSE,
    UNIQUE (role, module)
);

-- Per-user settings (layouts, preferences)
CREATE TABLE IF NOT EXISTS user_settings (
    id              SERIAL PRIMARY KEY,
    user_id         INTEGER NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    setting_key     VARCHAR(128) NOT NULL,
    setting_value   TEXT NOT NULL,
    updated_at      TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (user_id, setting_key)
);

-- Triggers
CREATE TRIGGER trg_app_users_updated_at
    BEFORE UPDATE ON app_users
    FOR EACH ROW EXECUTE FUNCTION update_updated_at();

CREATE TRIGGER trg_user_settings_updated_at
    BEFORE UPDATE ON user_settings
    FOR EACH ROW EXECUTE FUNCTION update_updated_at();

-- Seed admin user (auto-login)
INSERT INTO app_users (username, display_name, role, auto_login)
VALUES ('cory.sharplin', 'Cory Sharplin', 'Admin', TRUE)
ON CONFLICT DO NOTHING;

-- Seed role permissions
INSERT INTO role_permissions (role, module, can_view, can_edit, can_delete) VALUES
    ('Admin',    'ipam',     TRUE, TRUE, TRUE),
    ('Admin',    'switches', TRUE, TRUE, TRUE),
    ('Admin',    'admin',    TRUE, TRUE, TRUE),
    ('Operator', 'ipam',     TRUE, TRUE, FALSE),
    ('Operator', 'switches', TRUE, TRUE, FALSE),
    ('Operator', 'admin',    TRUE, FALSE, FALSE),
    ('Viewer',   'ipam',     TRUE, FALSE, FALSE),
    ('Viewer',   'switches', TRUE, FALSE, FALSE),
    ('Viewer',   'admin',    TRUE, FALSE, FALSE)
ON CONFLICT DO NOTHING;
