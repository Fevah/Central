-- =============================================================================
-- 110 — secure_auth schema + users table + corys seed account
--
-- Phase A of the auth-service buildout. Creates the backing store the Rust
-- auth-service queries on POST /api/v1/auth/login. See
-- docs/AUTH_SERVICE_BUILDOUT.md for the full phased plan.
--
-- secure_auth is a separate schema (not a separate database) so every
-- Central service can reach it through the same connection string + the
-- same migration pipeline the rest of the platform uses. The original
-- design doc specified a separate `secure_auth` DATABASE; we're
-- consolidating into the `central` DB's secure_auth SCHEMA because:
--   1. One migration history to keep aligned (schema_versions).
--   2. pgBouncer pool per-database; a schema keeps us on the main pool.
--   3. Easier to reason about FKs out of secure_auth.users to e.g.
--      central_platform.global_users in a future bridging migration.
--
-- Safe to re-run (CREATE SCHEMA/TABLE IF NOT EXISTS + ON CONFLICT DO NOTHING).
-- =============================================================================

-- ─── 1. Schema + users table ──────────────────────────────────────────────

CREATE SCHEMA IF NOT EXISTS secure_auth;

CREATE TABLE IF NOT EXISTS secure_auth.users (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Lowercase-normalised email. The login handler lowercases the input
    -- before the lookup, so the DB never sees mixed-case comparisons.
    email             varchar(320)       NOT NULL UNIQUE,

    -- Argon2id phc string. Format:
    --   $argon2id$v=19$m=19456,t=2,p=1$<b64 salt>$<b64 hash>
    password_hash     text               NOT NULL,

    display_name      varchar(255),
    first_name        varchar(128),
    last_name         varchar(128),

    -- Role marker for Phase A. Real per-tenant role assignment lands in
    -- Phase E (IDP claim mapping). is_global_admin maps to role
    -- "global_admin" in the LoginResponse claims today.
    is_global_admin   boolean            NOT NULL DEFAULT false,

    -- MFA shape ready for Phase C. mfa_enabled flips when a user
    -- completes the POST /mfa/setup flow.
    mfa_enabled       boolean            NOT NULL DEFAULT false,

    -- Telemetry only — last successful login. Updated by the login
    -- handler on success; failure doesn't touch.
    last_login_at     timestamptz,

    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        uuid,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        uuid,
    deleted_at        timestamptz
);

-- Fast path for the login query; the UNIQUE constraint already
-- provides a b-tree but a partial index keyed to live rows keeps the
-- common lookup narrow as the table grows.
CREATE INDEX IF NOT EXISTS users_email_active_idx
    ON secure_auth.users (email)
    WHERE deleted_at IS NULL;

COMMENT ON TABLE secure_auth.users IS
  'Auth-service backing store — one row per web-loginable account. '
  'Argon2id password_hash is verified on POST /api/v1/auth/login. '
  'Phase A seeds corys@central.local; Phase E extends to federated users.';

-- ─── 2. Seed corys@central.local ─────────────────────────────────────────
-- Password: corys-dev-pass!
-- Hash generated via `cargo run -p auth-service --example hash_password`
-- at migration authoring time. Deterministic FORMAT (argon2id v=19,
-- m=19456, t=2, p=1) but the salt is random per generation, so if
-- this migration ever needs to be regenerated with a different password
-- the hash string changes entirely.
INSERT INTO secure_auth.users (id, email, password_hash, display_name,
                               first_name, is_global_admin)
VALUES (
    '00000000-0000-0000-0000-000000000010',
    'corys@central.local',
    '$argon2id$v=19$m=19456,t=2,p=1$gZBJXfy+R60l+oIwFYBAxQ$DnU393slPmtYcDdo2GQl2H4J02WyoaExARaJGWnx5XQ',
    'Corys Admin',
    'Corys',
    true
)
ON CONFLICT (email) DO NOTHING;

-- ─── 3. Record in schema_versions if present ─────────────────────────────
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'schema_versions') THEN
        INSERT INTO schema_versions (version_number, description)
        VALUES ('110_secure_auth_schema',
                'Phase A of auth-service: secure_auth.users table + corys@central.local seed. '
             || 'See docs/AUTH_SERVICE_BUILDOUT.md for the phased plan.')
        ON CONFLICT (version_number) DO NOTHING;
    END IF;
END $$;
