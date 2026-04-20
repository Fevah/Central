-- =============================================================================
-- 113 — secure_auth password management + lockout
--
-- Phase D of the auth-service buildout (see docs/AUTH_SERVICE_BUILDOUT.md).
-- Backing store for:
--   * POST /api/v1/auth/change-password        — authed, old + new
--   * POST /api/v1/auth/password-reset/request — issue short-lived token
--   * POST /api/v1/auth/password-reset/confirm — consume token + set new pass
--   * Lockout on /api/v1/auth/login — 5 failures in 15 min -> 429 Too Many
--                                     Requests for 15 min, rolling window.
--
-- password_history — last N hashes per user; /change-password rejects any
-- new password whose Argon2 verifies against any of the kept hashes. N is
-- a config value (default 5); this migration only creates the storage.
--
-- login_attempts — per (email, ip) rolling-window ledger. Successful logins
-- reset the counter. /login inspects the window + returns 429 with a
-- Retry-After header when over the threshold. Rows older than the window
-- aren't deleted — audit + admin investigation value outweighs the disk.
--
-- password_reset_tokens — the reset flow. Tokens are 32-byte crypto-random
-- (same shape as refresh tokens), SHA-256 stored. 1-hour expiry. Single-
-- use. Phase D returns the raw token in the request response for local
-- dev so operators can exercise the flow without an email provider;
-- Phase E swaps the response for a silent-send + the token lands in the
-- user's inbox via the Central notifications pipeline.
--
-- Safe to re-run (IF NOT EXISTS everywhere).
-- =============================================================================

-- ─── 1. Password history ─────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS secure_auth.password_history (
    id                 uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id            uuid               NOT NULL REFERENCES secure_auth.users(id) ON DELETE CASCADE,
    password_hash      text               NOT NULL,
    retired_at         timestamptz        NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS password_history_user_id_idx
    ON secure_auth.password_history (user_id, retired_at DESC);

COMMENT ON TABLE secure_auth.password_history IS
  'Last N Argon2id hashes per user. /change-password argon2.verify '
  'scans this + rejects re-use. Retention: handler trims to the newest '
  'PASSWORD_HISTORY_MAX (default 5) after each change.';

-- ─── 2. Login attempts ───────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS secure_auth.login_attempts (
    id                 uuid               PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Normalise email lowercase at insert time so the windowed count
    -- groups `Alice@x` + `alice@x` together.
    email              varchar(320)       NOT NULL,

    ip_address         inet,
    user_agent         text,
    succeeded          boolean            NOT NULL,

    -- Reason when succeeded=false; useful for audit ("wrong password"
    -- vs "MFA verify wrong" vs "locked out already"). Nullable on
    -- success.
    failure_reason     varchar(64),

    attempted_at       timestamptz        NOT NULL DEFAULT now()
);

-- The lockout check is `SELECT COUNT(*) WHERE email = $1 AND succeeded
-- = false AND attempted_at > now() - interval '15 min'`. Partial
-- covering index keeps that query cheap as the table grows.
CREATE INDEX IF NOT EXISTS login_attempts_email_recent_idx
    ON secure_auth.login_attempts (email, attempted_at DESC)
    WHERE succeeded = false;

COMMENT ON TABLE secure_auth.login_attempts IS
  'Per-email + per-IP login ledger. Rolling-window lockout: 5 failures '
  'in 15 min -> login handler returns 429 Too Many Requests + Retry-'
  'After header. Successful logins reset by the handler querying only '
  'failures since the last success.';

-- ─── 3. Password reset tokens ────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS secure_auth.password_reset_tokens (
    id                 uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id            uuid               NOT NULL REFERENCES secure_auth.users(id) ON DELETE CASCADE,

    -- SHA-256 hex of the raw token. Same rationale as the refresh-
    -- token hash: 256 bits of crypto-random input doesn't need
    -- Argon2's slowness; we need O(1) lookup.
    token_hash         varchar(64)        NOT NULL,

    issued_at          timestamptz        NOT NULL DEFAULT now(),
    expires_at         timestamptz        NOT NULL,
    consumed_at        timestamptz,

    -- Diagnostic.
    issued_ip          inet,
    consumed_ip        inet,

    CONSTRAINT password_reset_tokens_hash_format
        CHECK (token_hash ~ '^[a-f0-9]{64}$')
);

CREATE INDEX IF NOT EXISTS password_reset_tokens_active_hash_idx
    ON secure_auth.password_reset_tokens (token_hash)
    WHERE consumed_at IS NULL;

COMMENT ON TABLE secure_auth.password_reset_tokens IS
  'Password-reset tokens. Raw value returned once from /password-'
  'reset/request; DB stores only SHA-256 hex. 1-hour TTL. Single-use.';

-- ─── 4. Record in schema_versions if present ─────────────────────────────
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'schema_versions') THEN
        INSERT INTO schema_versions (version_number, description)
        VALUES ('113_secure_auth_password_mgmt',
                'Phase D of auth-service: password_history + login_attempts + '
             || 'password_reset_tokens. See docs/AUTH_SERVICE_BUILDOUT.md.')
        ON CONFLICT (version_number) DO NOTHING;
    END IF;
END $$;
