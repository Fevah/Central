-- Migration 030: Migrate Central app_users to auth-service secure_auth database
-- Applied: 2026-04-14
--
-- This script exports Central's app_users into the auth-service format.
-- Run against the CENTRAL database. The auth-service will re-hash SHA256 passwords
-- to Argon2id on each user's first successful login.
--
-- IMPORTANT: Run this AFTER auth-service is running and secure_auth DB exists.
-- The auth-service handles the actual user import via its /api/v1/admin/import-users endpoint.

-- Step 1: Mark local auth columns as deprecated
COMMENT ON COLUMN app_users.password_hash IS 'DEPRECATED — auth handled by auth-service. Kept for offline fallback only.';
COMMENT ON COLUMN app_users.salt IS 'DEPRECATED — auth handled by auth-service. Kept for offline fallback only.';

-- Step 2: Add migration tracking column
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS auth_migrated boolean DEFAULT false;
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS auth_service_id uuid;

-- Step 3: Export query — run this and POST the results to auth-service /api/v1/admin/import-users
-- SELECT json_agg(json_build_object(
--     'username', username,
--     'email', COALESCE(email, username || '@central.local'),
--     'display_name', COALESCE(display_name, username),
--     'role', role_name,
--     'password_hash', COALESCE(password_hash, '') || ':' || COALESCE(salt, ''),
--     'is_active', is_active,
--     'department', department,
--     'title', title
-- )) FROM app_users WHERE auth_migrated = false;

-- Step 4: After successful migration, mark users as migrated
-- UPDATE app_users SET auth_migrated = true, auth_service_id = '<uuid from auth-service>'
-- WHERE username = '<username>';

-- Step 5: Verify all users migrated
-- SELECT count(*) AS remaining FROM app_users WHERE auth_migrated = false;
-- When remaining = 0, local auth can be fully disabled.
