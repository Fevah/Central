-- 028_default_settings.sql — Default user settings + seed system default user settings
-- New users get these defaults on first login. Admin can customize defaults.

CREATE TABLE IF NOT EXISTS default_user_settings (
    id            serial PRIMARY KEY,
    setting_key   varchar(128) NOT NULL UNIQUE,
    setting_value text NOT NULL DEFAULT '',
    description   varchar(256) DEFAULT '',
    updated_at    timestamptz DEFAULT now()
);

-- Seed defaults
INSERT INTO default_user_settings (setting_key, setting_value, description) VALUES
    ('pref.theme',           'Office2019Colorful', 'Default application theme'),
    ('pref.hide_reserved',   'false',              'Hide reserved devices in IPAM grid'),
    ('pref.scan_enabled',    'false',              'Auto-ping scan on startup'),
    ('pref.scan_interval',   '10',                 'Ping scan interval in minutes'),
    ('layout.panel_states',  '{"devices":true,"switches":true}', 'Default open panels')
ON CONFLICT (setting_key) DO NOTHING;

-- Function to copy defaults for a new user
CREATE OR REPLACE FUNCTION seed_user_defaults(p_user_id integer) RETURNS void AS $$
BEGIN
    INSERT INTO user_settings (user_id, setting_key, setting_value)
    SELECT p_user_id, setting_key, setting_value
    FROM default_user_settings
    ON CONFLICT (user_id, setting_key) DO NOTHING;
END;
$$ LANGUAGE plpgsql;

-- Auto-seed defaults when a new user is created
CREATE OR REPLACE FUNCTION trg_seed_user_defaults() RETURNS trigger AS $$
BEGIN
    PERFORM seed_user_defaults(NEW.id);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_app_users_seed_defaults ON app_users;
CREATE TRIGGER trg_app_users_seed_defaults
    AFTER INSERT ON app_users
    FOR EACH ROW EXECUTE FUNCTION trg_seed_user_defaults();

-- Seed defaults for existing users who don't have settings yet
INSERT INTO user_settings (user_id, setting_key, setting_value)
SELECT u.id, d.setting_key, d.setting_value
FROM app_users u
CROSS JOIN default_user_settings d
ON CONFLICT (user_id, setting_key) DO NOTHING;
