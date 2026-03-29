-- Migration 039: Extended user fields for AD integration + user type normalization
-- Adds department, title, phone, mobile, company, ad_guid, last_ad_sync to app_users

ALTER TABLE app_users ADD COLUMN IF NOT EXISTS department   varchar(128) DEFAULT '';
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS title        varchar(128) DEFAULT '';
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS phone        varchar(32)  DEFAULT '';
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS mobile       varchar(32)  DEFAULT '';
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS company      varchar(128) DEFAULT '';
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS ad_guid      varchar(64)  DEFAULT '';
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS last_ad_sync timestamptz;

-- Normalize user_type values (Manual -> Standard)
UPDATE app_users SET user_type = 'Standard' WHERE user_type = 'Manual';

-- Ensure admin user is System type
UPDATE app_users SET user_type = 'System' WHERE username = 'admin' AND user_type != 'System';
