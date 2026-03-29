-- 027_global_admin.sql
-- Global admin role for platform-level management above per-tenant admin.

ALTER TABLE central_platform.global_users
    ADD COLUMN IF NOT EXISTS is_global_admin boolean NOT NULL DEFAULT false;

-- Seed a default global admin (you — platform operator)
-- Uses the existing default tenant admin or creates one if not exists
INSERT INTO central_platform.global_users (id, email, display_name, password_hash, salt, email_verified, is_global_admin)
VALUES (
    '00000000-0000-0000-0000-000000000001',
    'admin@central.local',
    'Platform Admin',
    -- SHA256("admin" + salt) — change immediately in production
    'jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=',
    'Y2VudHJhbA==',
    true,
    true
) ON CONFLICT (email) DO UPDATE SET is_global_admin = true;
