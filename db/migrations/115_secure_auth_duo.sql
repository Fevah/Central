-- =============================================================================
-- 115 — secure_auth Duo (MFA provider) scaffold
--
-- Phase C.1 of the auth-service buildout (see docs/AUTH_SERVICE_BUILDOUT.md).
-- Adds Duo Security as a second-factor option alongside TOTP + recovery
-- codes from Phase C. Duo's Universal Prompt is an OIDC-like redirect
-- flow — after password verify we redirect to Duo, the user approves via
-- push / SMS / call / passcode, Duo redirects back with a duo_code we
-- exchange for confirmation.
--
-- Duo username: the user's email. Duo's Universal Prompt takes a
-- `username` field in the authentication request; we pass the same
-- email that authenticated against secure_auth.users. No separate
-- identity column — one email, one identity, cross-system.
--
-- Backing store for:
--   * POST /api/v1/auth/mfa/duo/start     — create state, redirect to Duo
--   * POST /api/v1/auth/mfa/duo/callback  — verify Duo response, issue tokens
--
-- Phase C.1 ships a "mock" mode (activated when duo_config.mode='mock')
-- so the flow is exercisable without real Duo admin credentials. Phase
-- C.2 swaps the mock handler for the real duo-universal-sdk integration.
--
-- Schema:
--
-- duo_config — platform-level admin credentials. Singleton row. Per-
--   tenant Duo overrides are a Phase C.2 concern (column-level
--   `tenant_id` lands later). Stored credentials are NOT currently
--   encrypted at rest (same honest trade-off as TOTP secrets in
--   migration 112 — anyone with DB read access already has every
--   password hash + refresh-token metadata). Phase G wraps with
--   `CredentialEncryptor`.
--
-- users.duo_enabled — per-user opt-in. When true + duo_config.mode
--   is active, the login handler advertises "duo" as an mfa_method
--   for this user + uses the user's email as the Duo username.
--   Defaults false so existing users don't get blocked on rollout.
--
-- Safe to re-run.
-- =============================================================================

-- ─── 1. Platform-level Duo admin credentials ─────────────────────────────

CREATE TABLE IF NOT EXISTS secure_auth.duo_config (
    id                 uuid        PRIMARY KEY DEFAULT gen_random_uuid(),

    -- One logical config per deployment. Phase C.2 may split into
    -- per-tenant rows; for now keep it simple with a single
    -- enforced-singleton row.
    singleton          boolean     NOT NULL DEFAULT true
                          CHECK (singleton = true),

    -- Duo Universal Prompt OIDC-style credentials. Integration key +
    -- secret key + API hostname are what Duo's admin panel emits
    -- when you register a "Web SDK v4 / Universal Prompt"
    -- application. In mock mode all three are empty strings.
    integration_key    varchar(40) NOT NULL DEFAULT '',
    secret_key         text        NOT NULL DEFAULT '',
    api_hostname       varchar(64) NOT NULL DEFAULT '',

    -- 'mock' = Phase C.1 local-testing mode (handler accepts
    -- code="approve" or "deny" directly, no real Duo round-trip).
    -- 'live' = Phase C.2 + real Duo SDK wiring. Transition requires
    -- populating integration_key / secret_key / api_hostname above.
    mode               varchar(16) NOT NULL DEFAULT 'mock'
                          CHECK (mode IN ('mock', 'live')),

    created_at         timestamptz NOT NULL DEFAULT now(),
    updated_at         timestamptz NOT NULL DEFAULT now(),

    UNIQUE (singleton)
);

-- Seed the default mock-mode row so /mfa/duo/start + /callback
-- respond something sensible out of the box. An admin flipping
-- to live mode fills in the three credential columns + sets
-- mode='live' in the same UPDATE.
INSERT INTO secure_auth.duo_config (mode) VALUES ('mock')
ON CONFLICT (singleton) DO NOTHING;

COMMENT ON TABLE secure_auth.duo_config IS
  'Duo admin credentials for the Universal Prompt flow. Singleton '
  'row. mode=mock runs the Phase C.1 local-testing handler; '
  'mode=live requires integration_key + secret_key + api_hostname '
  'and runs the real Duo SDK in Phase C.2.';

-- ─── 2. Per-user Duo opt-in ──────────────────────────────────────────────

ALTER TABLE secure_auth.users
    ADD COLUMN IF NOT EXISTS duo_enabled boolean NOT NULL DEFAULT false;

-- Partial index so the "does this user have Duo enabled?" check at
-- login time is an index-only scan.
CREATE INDEX IF NOT EXISTS users_duo_enabled_idx
    ON secure_auth.users (id)
    WHERE duo_enabled = true;

COMMENT ON COLUMN secure_auth.users.duo_enabled IS
  'Per-user Duo MFA opt-in. Duo username is the user email — no '
  'separate identity column. When duo_enabled + duo_config.mode '
  'is active, the login handler adds "duo" to mfa_methods.';

-- ─── 3. Record in schema_versions if present ─────────────────────────────
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'schema_versions') THEN
        INSERT INTO schema_versions (version_number, description)
        VALUES ('115_secure_auth_duo',
                'Phase C.1 of auth-service: Duo MFA scaffold — duo_config singleton '
             || 'row + users.duo_enabled (Duo username = email). Real Duo SDK in C.2.')
        ON CONFLICT (version_number) DO NOTHING;
    END IF;
END $$;
