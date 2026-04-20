-- =============================================================================
-- 112 — secure_auth MFA tables (secrets + login challenges + recovery codes)
--
-- Phase C of the auth-service buildout (see docs/AUTH_SERVICE_BUILDOUT.md).
-- Backing store for:
--   * POST /api/v1/auth/mfa/setup   — generates the TOTP secret + recovery codes
--   * POST /api/v1/auth/login       — returns an MFA challenge when the user
--                                     has mfa_enabled=true
--   * POST /api/v1/auth/mfa/verify  — exchanges the challenge + a TOTP code
--                                     (or a one-shot recovery code) for a
--                                     real access + refresh pair
--
-- TOTP secret: stored base32 (RFC 6238 canonical). Phase C stores it plain;
-- a future migration wraps it with the CredentialEncryptor key (same key
-- used for SSH credentials). Plaintext storage is honest here because
-- anyone with DB read access already has every user's password hash + API
-- keys + webhook secrets; TOTP secrets are the same blast radius.
--
-- Recovery codes: 10 random 8-byte codes per user, Argon2-hashed (lower
-- entropy than refresh tokens — typically displayed "XXXX-XXXX" format —
-- so Argon2 is the right tool; SHA-256 would be brute-forceable).
--
-- Safe to re-run (IF NOT EXISTS everywhere).
-- =============================================================================

-- ─── 1. TOTP secret per user ──────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS secure_auth.mfa_secrets (
    user_id            uuid               PRIMARY KEY REFERENCES secure_auth.users(id) ON DELETE CASCADE,

    -- Base32-encoded TOTP secret (RFC 6238). 160-bit (32 base32 chars)
    -- by default — the otpauth URI we emit uses the same encoding so
    -- the client's authenticator app + our verifier agree.
    secret_base32      text               NOT NULL,

    -- Standard TOTP parameters. Default algorithm SHA-1 matches Google
    -- Authenticator's default (+ most other apps). Digits/period
    -- likewise.
    algorithm          varchar(16)        NOT NULL DEFAULT 'SHA1',
    digits             int                NOT NULL DEFAULT 6
                                              CHECK (digits IN (6, 7, 8)),
    period_seconds     int                NOT NULL DEFAULT 30
                                              CHECK (period_seconds IN (15, 30, 60)),

    -- Stays NULL until the user enters a valid code for the first
    -- time (proves they scanned the QR correctly). mfa_enabled on
    -- users table flips when verified_at fills in.
    verified_at        timestamptz,

    created_at         timestamptz        NOT NULL DEFAULT now(),
    rotated_from       uuid               REFERENCES secure_auth.mfa_secrets(user_id) ON DELETE SET NULL
);

COMMENT ON TABLE secure_auth.mfa_secrets IS
  'One TOTP secret per user. Stored base32 (RFC 6238 canonical). verified_at '
  'fills in once the user completes the POST /mfa/setup -> verify dance and '
  'proves they scanned the QR correctly. Rotated_from chains old secrets for '
  'audit when a user re-enrols.';

-- ─── 2. Login challenges (login -> verify state) ─────────────────────────
-- When login succeeds on email+password for a user with mfa_enabled=true,
-- we create a short-lived challenge + return its id. The client then
-- POSTs /mfa/verify with { session_id: <challenge id>, code }. We match
-- the challenge row, verify the code, issue real tokens.

CREATE TABLE IF NOT EXISTS secure_auth.mfa_login_challenges (
    id                 uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id            uuid               NOT NULL REFERENCES secure_auth.users(id) ON DELETE CASCADE,
    issued_at          timestamptz        NOT NULL DEFAULT now(),

    -- 5-minute default; /mfa/verify rejects expired challenges.
    expires_at         timestamptz        NOT NULL,
    consumed_at        timestamptz,      -- set on successful verify; prevents replay

    -- Rate-limit: /mfa/verify increments on wrong code, aborts the
    -- challenge when failed_attempts >= 5. The user has to re-enter
    -- email+password.
    failed_attempts    int                NOT NULL DEFAULT 0,

    user_agent         text,
    ip_address         inet
);

CREATE INDEX IF NOT EXISTS mfa_login_challenges_active_idx
    ON secure_auth.mfa_login_challenges (id)
    WHERE consumed_at IS NULL;

COMMENT ON TABLE secure_auth.mfa_login_challenges IS
  'Short-lived state between login (email+password passed) and mfa/verify '
  '(second factor). Consumed on successful verify; never reused. Aborted '
  'after 5 failed attempts.';

-- ─── 3. Recovery codes (one-shot) ────────────────────────────────────────

CREATE TABLE IF NOT EXISTS secure_auth.mfa_recovery_codes (
    id                 uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id            uuid               NOT NULL REFERENCES secure_auth.users(id) ON DELETE CASCADE,

    -- Argon2id hash of the raw code. Raw value returned to client ONLY
    -- on /mfa/setup — never retrievable again. On verify we iterate
    -- active codes + compare each with argon2.verify (unavoidable
    -- because Argon2 hashes aren't lookup-indexable); 10 codes per
    -- user keeps the scan cheap.
    code_hash          text               NOT NULL,

    created_at         timestamptz        NOT NULL DEFAULT now(),
    consumed_at        timestamptz,
    consumed_ip        inet
);

CREATE INDEX IF NOT EXISTS mfa_recovery_codes_active_idx
    ON secure_auth.mfa_recovery_codes (user_id)
    WHERE consumed_at IS NULL;

COMMENT ON TABLE secure_auth.mfa_recovery_codes IS
  'One-shot backup codes for MFA. 10 per user on /mfa/setup. Consumed '
  'rows stay for audit (shows which code was used + when + from where).';

-- ─── 4. Record in schema_versions if present ─────────────────────────────
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'schema_versions') THEN
        INSERT INTO schema_versions (version_number, description)
        VALUES ('112_secure_auth_mfa',
                'Phase C of auth-service: TOTP secrets + login challenges + '
             || 'recovery codes. See docs/AUTH_SERVICE_BUILDOUT.md.')
        ON CONFLICT (version_number) DO NOTHING;
    END IF;
END $$;
