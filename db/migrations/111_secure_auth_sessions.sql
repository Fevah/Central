-- =============================================================================
-- 111 — secure_auth.sessions + refresh-token rotation
--
-- Phase B of the auth-service buildout (see
-- docs/AUTH_SERVICE_BUILDOUT.md). Backing store for:
--   * POST /api/v1/auth/refresh — rotates refresh tokens
--   * POST /api/v1/auth/logout  — revokes the current session
--
-- Refresh tokens are NOT stored in the clear. The client receives a
-- 43-character base64url-encoded random token once (at login); the
-- server stores SHA-256(token) so lookup on refresh is O(1). SHA-256
-- is appropriate here — the input is 256 bits of crypto-random
-- entropy, so rainbow tables aren't a threat the way they are for
-- passwords. Passwords keep using Argon2id (slow by design) in
-- secure_auth.users.
--
-- Rotation chain: when a refresh token is exchanged, the old session
-- row gets revoked_at=now() + rotated_to_session_id pointing at the
-- new session. Lets forensics trace a token lineage after a
-- compromise. An attacker who steals a refresh token loses it on the
-- next legitimate refresh — the old token becomes revoked, the new
-- one is held only by the legitimate client.
--
-- Safe to re-run (IF NOT EXISTS).
-- =============================================================================

CREATE TABLE IF NOT EXISTS secure_auth.sessions (
    id                     uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                uuid               NOT NULL REFERENCES secure_auth.users(id) ON DELETE CASCADE,

    -- SHA-256 of the raw refresh token (64 hex chars). Deterministic
    -- so refresh-handler lookup is `WHERE refresh_token_hash = $1`.
    refresh_token_hash     varchar(64)        NOT NULL,

    issued_at              timestamptz        NOT NULL DEFAULT now(),
    expires_at             timestamptz        NOT NULL,

    -- NULL = active. Non-null = revoked (either by rotation, explicit
    -- logout, or admin revoke).
    revoked_at             timestamptz,

    -- Set on a successful rotation. Points at the session row the old
    -- token was exchanged for, so post-compromise forensics can
    -- walk the chain.
    rotated_to_session_id  uuid               REFERENCES secure_auth.sessions(id) ON DELETE SET NULL,

    -- Diagnostic metadata. Never used for auth decisions; operators
    -- slice the audit log by these when investigating suspicious
    -- refreshes.
    user_agent             text,
    ip_address             inet,

    CONSTRAINT sessions_hash_format CHECK (refresh_token_hash ~ '^[a-f0-9]{64}$')
);

-- Fast path for /refresh: lookup by hash, filtered to live rows.
-- Partial index keeps it small even as the table grows — revoked
-- rows accumulate for audit but don't participate in refresh lookup.
CREATE INDEX IF NOT EXISTS sessions_active_hash_idx
    ON secure_auth.sessions (refresh_token_hash)
    WHERE revoked_at IS NULL;

-- Per-user index for admin revoke-all-sessions + audit lookups
-- ("show me every session for this user").
CREATE INDEX IF NOT EXISTS sessions_user_id_idx
    ON secure_auth.sessions (user_id, issued_at DESC);

COMMENT ON TABLE secure_auth.sessions IS
  'Refresh-token session store (Phase B). One row per login; rotation '
  'chain linked via rotated_to_session_id. Raw refresh tokens never '
  'hit disk — only SHA-256 hashes. Logout / rotate / compromise-'
  'response all flip revoked_at.';

-- Record in schema_versions if present.
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'schema_versions') THEN
        INSERT INTO schema_versions (version_number, description)
        VALUES ('111_secure_auth_sessions',
                'Phase B of auth-service: secure_auth.sessions table + refresh-token '
             || 'rotation chain. See docs/AUTH_SERVICE_BUILDOUT.md.')
        ON CONFLICT (version_number) DO NOTHING;
    END IF;
END $$;
