-- 027_user_auth.sql — Enhanced user authentication fields
-- Adds password hash/salt for manual login, user type, display name, active flag

-- Password + auth fields
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS password_hash   varchar(128) DEFAULT '';
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS salt            varchar(64)  DEFAULT '';
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS display_name    varchar(200) DEFAULT '';
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS user_type       varchar(32)  DEFAULT 'ActiveDirectory';  -- ActiveDirectory, Manual, System
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS is_active       boolean      DEFAULT true;
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS ad_sid          varchar(256) DEFAULT '';  -- Windows SID for AD auto-login
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS email           varchar(256) DEFAULT '';
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS last_login_at   timestamptz;
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS login_count     integer      DEFAULT 0;
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS created_at      timestamptz  DEFAULT now();

-- Set display_name from username for existing users
UPDATE app_users SET display_name = username WHERE display_name = '' OR display_name IS NULL;

-- Role priority (higher = more powerful, used for role hierarchy)
ALTER TABLE roles ADD COLUMN IF NOT EXISTS priority    integer DEFAULT 0;
ALTER TABLE roles ADD COLUMN IF NOT EXISTS is_system   boolean DEFAULT false;
ALTER TABLE roles ADD COLUMN IF NOT EXISTS description varchar(500) DEFAULT '';

-- Mark Admin as system role
UPDATE roles SET is_system = true, priority = 100 WHERE name = 'Admin';
UPDATE roles SET priority = 50 WHERE name = 'Operator' AND priority = 0;
UPDATE roles SET priority = 10 WHERE name = 'Viewer' AND priority = 0;
