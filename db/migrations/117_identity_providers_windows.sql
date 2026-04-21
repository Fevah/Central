-- =============================================================================
-- 117 — Windows SSO provider + AD identity migration (Phase 2 cont.)
--
-- Migration 116 put the user store bridge in place. This migration
-- (117) makes Windows SSO a first-class provider + migrates every AD-
-- linked app_users row into secure_auth.user_external_identities so
-- the /api/v1/auth/sso/windows/callback handler (coming in the same
-- commit) can resolve `{DOMAIN}\{username}` -> our user + issue JWTs.
--
-- Preamble: extend the identity_providers.kind CHECK to include
-- 'windows'. Migration 114's CHECK constraint has to be dropped +
-- re-added because PostgreSQL doesn't support ALTER CHECK in place.
--
-- Safe to re-run (IF NOT EXISTS, ON CONFLICT DO NOTHING).
-- =============================================================================

-- ─── 1. Extend kind CHECK to include 'windows' ──────────────────────────

ALTER TABLE secure_auth.identity_providers
    DROP CONSTRAINT IF EXISTS identity_providers_kind_check;

ALTER TABLE secure_auth.identity_providers
    ADD CONSTRAINT identity_providers_kind_check
    CHECK (kind IN ('mock', 'oidc', 'saml',
                    'google', 'microsoft',
                    'entra', 'okta', 'github',
                    'windows'));

-- ─── 2. Seed the windows provider ───────────────────────────────────────

INSERT INTO secure_auth.identity_providers
    (provider_code, kind, display_name, enabled, config_json)
VALUES (
    'windows',
    'windows',
    'Windows domain SSO',
    true,
    jsonb_build_object(
        'note', 'Desktop bridge — accepts { domain_username } from the WPF shell + issues a JWT when the external_id matches a user_external_identities row.'
    )
)
ON CONFLICT (provider_code) DO NOTHING;

-- ─── 3. Migrate AD identities ──────────────────────────────────────────
-- For every app_users row with a non-empty ad_guid OR ad_sid, add a
-- user_external_identities row keyed on the domain_username (which
-- equals app_users.username when the desktop auto-logs in via Windows
-- SSO). Raw claims carry the AD GUID + SID so a later AD-sync job
-- can reconcile.

-- First, ensure ad_guid + ad_sid are reflected on secure_auth.users.
-- Migration 116 added the columns; the backfill there populated them
-- for most rows. This SQL is a defensive pass for any row that
-- slipped through.
UPDATE secure_auth.users su
   SET ad_guid = COALESCE(su.ad_guid, NULLIF(au.ad_guid, '')),
       ad_sid  = COALESCE(su.ad_sid,  NULLIF(au.ad_sid, ''))
  FROM app_users au
 WHERE su.legacy_int_id = au.id
   AND ((su.ad_guid IS NULL AND au.ad_guid IS NOT NULL AND au.ad_guid <> '')
        OR (su.ad_sid IS NULL AND au.ad_sid IS NOT NULL AND au.ad_sid <> ''));

-- Windows provider identity for every AD-linked user. external_id is
-- the username (case-insensitive-matched on login); raw_claims carry
-- the GUID + SID so Phase 5.B can cross-reference with Entra/Okta
-- providers without re-querying AD.
INSERT INTO secure_auth.user_external_identities
    (user_id, provider_code, external_id, raw_claims)
SELECT
    su.id,
    'windows',
    lower(au.username),                        -- Case-fold: Windows SSO is case-insensitive
    jsonb_build_object(
        'ad_guid', NULLIF(au.ad_guid, ''),
        'ad_sid',  NULLIF(au.ad_sid, ''),
        'tenant_id', au.tenant_id
    )
  FROM app_users au
  JOIN secure_auth.users su ON su.legacy_int_id = au.id
 WHERE (au.ad_guid IS NOT NULL AND au.ad_guid <> '')
    OR (au.ad_sid IS NOT NULL AND au.ad_sid <> '')
    OR au.user_type = 'ActiveDirectory'
ON CONFLICT (provider_code, external_id) DO UPDATE
    SET raw_claims   = EXCLUDED.raw_claims,
        last_seen_at = now();

-- ─── 4. Record in schema_versions if present ─────────────────────────────
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'schema_versions') THEN
        INSERT INTO schema_versions (version_number, description)
        VALUES ('117_identity_providers_windows',
                'Phase 2 of IDP buildout: windows provider + AD identity migration '
             || 'into secure_auth.user_external_identities. Desktop Windows-SSO '
             || 'bridge endpoint lands in the same commit.')
        ON CONFLICT (version_number) DO NOTHING;
    END IF;
END $$;
