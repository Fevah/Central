-- =============================================================================
-- 114 — secure_auth SSO / federation schema
--
-- Phase E.1 of the auth-service buildout (see docs/AUTH_SERVICE_BUILDOUT.md).
-- Backing store for:
--   * GET  /api/v1/auth/sso/providers         — list enabled providers
--   * POST /api/v1/auth/sso/:provider/start   — return redirect_url + state
--   * POST /api/v1/auth/sso/:provider/callback — exchange code -> our JWT
--
-- This migration creates the SCHEMA needed. Actual provider wiring
-- (OIDC token exchange, SAML AuthnRequest/Response) is Phase E.2 / E.3.
-- Phase E.1 ships the scaffold + a "mock" provider type that bypasses
-- real SSO for local testing (accepts an email, maps/creates a user,
-- issues tokens) so the code paths are exercisable before we commit to
-- a specific real-provider library.
--
-- Table design rationale:
--
-- identity_providers — one row per configured provider. `provider_code`
--   is the slug in URLs (`/sso/google/start`, `/sso/corp-okta/start`);
--   `kind` picks the handler (`mock`, `oidc`, `saml`, `google`, `okta`,
--   `entra`, `github` — scaffolded types that return 501 until E.2/E.3).
--   config_json carries the per-kind parameters (client_id, client_secret,
--   issuer URL, etc.). tenant_id is NULL for platform-global providers;
--   non-null scopes the provider to one tenant.
--
-- user_external_identities — maps a user to an (provider_code, external_id)
--   pair. One user can have multiple identities (log in via Google OR
--   Okta), and the same external_id is unique within a provider (primary
--   key shape is (provider_code, external_id) not user_id).
--
-- sso_sessions — short-lived state between /start and /callback. Stores
--   the state nonce + the provider_code + requested redirect_uri so the
--   callback handler can verify the origin of the callback + prevent
--   CSRF. Consumed on callback; never reused.
--
-- Safe to re-run.
-- =============================================================================

-- ─── 1. identity_providers ───────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS secure_auth.identity_providers (
    id             uuid         PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Slug used in the URL + /start/callback routing. Must be URL-safe.
    provider_code  varchar(64)  NOT NULL UNIQUE
                                  CHECK (provider_code ~ '^[a-z0-9][a-z0-9._-]{0,62}[a-z0-9]$'),

    -- Handler type. `mock` runs the local-testing dance + issues tokens
    -- without a real IdP round-trip; the others wire to real SSO in
    -- E.2 / E.3 (return 501 until then).
    kind           varchar(32)  NOT NULL
                                  CHECK (kind IN ('mock', 'oidc', 'saml',
                                                  'google', 'microsoft',
                                                  'entra', 'okta', 'github')),
    display_name   varchar(128) NOT NULL,
    enabled        boolean      NOT NULL DEFAULT true,

    -- NULL = platform-wide provider visible to all tenants. Non-null =
    -- per-tenant; the list + start + callback handlers filter on this.
    tenant_id      uuid,

    -- Per-kind config. OIDC expects issuer + client_id + client_secret
    -- + scopes; SAML expects idp_sso_url + idp_entity_id + signing cert.
    -- Stored jsonb so schema-level changes don't need migrations.
    config_json    jsonb        NOT NULL DEFAULT '{}'::jsonb,

    created_at     timestamptz  NOT NULL DEFAULT now(),
    updated_at     timestamptz  NOT NULL DEFAULT now(),
    deleted_at     timestamptz
);

CREATE INDEX IF NOT EXISTS identity_providers_active_idx
    ON secure_auth.identity_providers (provider_code)
    WHERE enabled = true AND deleted_at IS NULL;

COMMENT ON TABLE secure_auth.identity_providers IS
  'Configured SSO providers. kind picks the handler (mock / oidc / saml / '
  'google / microsoft / entra / okta / github). Phase E.1 only implements '
  'kind=mock; E.2/E.3 add the real-provider wiring.';

-- Seed one mock provider so the web can exercise the flow without any
-- real SSO configured.
INSERT INTO secure_auth.identity_providers
    (provider_code, kind, display_name, enabled, config_json)
VALUES (
    'mock',
    'mock',
    'Mock SSO (dev only)',
    true,
    '{"note": "Local-testing provider. Accepts any email in /callback and creates/binds a user. Never enable in production."}'::jsonb
)
ON CONFLICT (provider_code) DO NOTHING;

-- ─── 2. user_external_identities ─────────────────────────────────────────

CREATE TABLE IF NOT EXISTS secure_auth.user_external_identities (
    id             uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id        uuid         NOT NULL REFERENCES secure_auth.users(id) ON DELETE CASCADE,

    -- Foreign key to identity_providers.provider_code (soft; we want
    -- (provider, external_id) pairs to survive provider row renames).
    provider_code  varchar(64)  NOT NULL,

    -- IdP's user id. Shape depends on the provider — Google uses a 21-
    -- digit number, SAML uses NameID, Okta uses a 20-char id. Stored
    -- as text; uniqueness is enforced per-provider below.
    external_id    varchar(255) NOT NULL,

    -- Last seen claims from the IdP. Kept for debugging + for
    -- re-mapping when claim-mapping rules change. Not read for auth
    -- decisions.
    raw_claims     jsonb        NOT NULL DEFAULT '{}'::jsonb,

    linked_at      timestamptz  NOT NULL DEFAULT now(),
    last_seen_at   timestamptz  NOT NULL DEFAULT now(),

    -- One external id per provider + one per user per provider.
    UNIQUE (provider_code, external_id),
    UNIQUE (user_id, provider_code)
);

CREATE INDEX IF NOT EXISTS user_external_identities_user_id_idx
    ON secure_auth.user_external_identities (user_id);

COMMENT ON TABLE secure_auth.user_external_identities IS
  'Maps (provider, external_id) to one of our users. A user can have '
  'multiple identities (log in via Google OR Okta). Unique (provider, '
  'external_id) prevents two users claiming the same IdP identity; '
  'unique (user_id, provider_code) prevents one user linking the same '
  'provider twice.';

-- ─── 3. sso_sessions — short-lived /start -> /callback state ─────────────

CREATE TABLE IF NOT EXISTS secure_auth.sso_sessions (
    id              uuid         PRIMARY KEY DEFAULT gen_random_uuid(),

    -- State nonce returned to the IdP + checked on callback. Raw value
    -- goes to the IdP; DB stores the raw since we'll look it up in
    -- /callback + we'd need to decode it to know the provider anyway.
    state_nonce     varchar(128) NOT NULL UNIQUE,

    provider_code   varchar(64)  NOT NULL,
    redirect_uri    text,

    -- Optional — non-null when the user was logged in before the SSO
    -- flow (account-linking on an existing user). Null = create-or-
    -- login flow.
    link_user_id    uuid         REFERENCES secure_auth.users(id) ON DELETE CASCADE,

    issued_at       timestamptz  NOT NULL DEFAULT now(),
    expires_at      timestamptz  NOT NULL,
    consumed_at     timestamptz,

    issued_ip       inet
);

CREATE INDEX IF NOT EXISTS sso_sessions_active_nonce_idx
    ON secure_auth.sso_sessions (state_nonce)
    WHERE consumed_at IS NULL;

COMMENT ON TABLE secure_auth.sso_sessions IS
  'Short-lived state for the /start -> IdP -> /callback handoff. 5-min '
  'TTL. state_nonce round-trips to the IdP + is verified on callback; '
  'consumed_at prevents replay; link_user_id carries the "bind this '
  'IdP to my existing account" flow through the redirect.';

-- ─── 4. Record in schema_versions if present ─────────────────────────────
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'schema_versions') THEN
        INSERT INTO schema_versions (version_number, description)
        VALUES ('114_secure_auth_sso',
                'Phase E.1 of auth-service: SSO scaffold — identity_providers + '
             || 'user_external_identities + sso_sessions + mock provider seed. '
             || 'Real OIDC + SAML wiring lands in E.2 / E.3.')
        ON CONFLICT (version_number) DO NOTHING;
    END IF;
END $$;
