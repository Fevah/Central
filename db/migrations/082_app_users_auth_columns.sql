-- =============================================================================
-- 082 — Add auth columns to app_users that the desktop's PermissionRepository
-- queries (password_changed_at for password expiry checks, mfa_secret_enc +
-- mfa_recovery_codes_enc for TOTP flow). These were referenced in code but
-- no migration defined them.
--
-- Without these columns GetUserByUsernameAsync silently catches the column
-- error and returns null — which surfaces as "No account found for Windows
-- user: <name>" on auto-login.
-- =============================================================================

ALTER TABLE app_users
    ADD COLUMN IF NOT EXISTS password_changed_at      timestamptz,
    ADD COLUMN IF NOT EXISTS mfa_secret_enc           text,
    ADD COLUMN IF NOT EXISTS mfa_recovery_codes_enc   text,
    ADD COLUMN IF NOT EXISTS mfa_enabled_at           timestamptz;

-- Seed password_changed_at for existing users so expiry checks don't
-- immediately flag every account as needing a password change.
UPDATE app_users
SET password_changed_at = COALESCE(created_at, now())
WHERE password_changed_at IS NULL;
