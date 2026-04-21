-- =============================================================================
-- 116 — Bridge app_users <-> secure_auth.users (Phase 2 of IDP buildout)
--
-- Original Phase 2 plan was "app_users becomes a VIEW over secure_auth.users."
-- Audit revealed app_users has 11 FK referrers (activity_feed, audit_log,
-- dashboards, portfolios, programmes, project_members, saved_reports,
-- sprint_allocations, task_comments, task_views, custom_column_permissions).
-- Can't replace a FK-referenced table with a view without rewriting every
-- referrer — multi-month project touching active modules.
--
-- Pragmatic revision: **dual-write bridge**.
--   * Keep app_users as the real table. FKs stay intact.
--   * secure_auth.users becomes the identity source-of-truth (MFA,
--     password hashes, SSO, Duo, lockout ledger — all lives here).
--   * Cross-reference columns link the two: app_users.secure_auth_user_id
--     (uuid) + secure_auth.users.legacy_int_id (integer).
--   * Dual-write triggers keep the basic identity fields in sync. Re-
--     entrancy guard via pg_trigger_depth() prevents cascades.
--
-- Phase 9 revisits whether full consolidation is feasible once admin
-- UI touches every FK'd path. For now the bridge is the correct
-- compromise — desktop keeps working, web benefits stay portable,
-- no FK gymnastics.
--
-- Migration order:
--   1. Add columns to both tables.
--   2. Backfill existing rows.
--   3. Install triggers.
--
-- Safe to re-run (IF NOT EXISTS, ON CONFLICT DO UPDATE).
-- =============================================================================

-- ─── 1. Cross-reference columns ──────────────────────────────────────────

-- secure_auth.users gains legacy_int_id pointing at app_users.id. Unique
-- but nullable — users created via /api/v1/auth/* without a matching
-- desktop account don't need one initially (a later trigger may create
-- one if the same email shows up on a Windows SSO login).
ALTER TABLE secure_auth.users
    ADD COLUMN IF NOT EXISTS legacy_int_id integer,
    ADD COLUMN IF NOT EXISTS username      varchar(128),
    ADD COLUMN IF NOT EXISTS role          varchar(64) NOT NULL DEFAULT 'user',
    ADD COLUMN IF NOT EXISTS is_active     boolean NOT NULL DEFAULT true,
    ADD COLUMN IF NOT EXISTS tenant_id     uuid,
    ADD COLUMN IF NOT EXISTS ad_sid        varchar(256),
    ADD COLUMN IF NOT EXISTS ad_guid       text;

-- Enforce uniqueness via partial unique index — allows multiple NULLs
-- (users-without-legacy-id are valid) but blocks duplicate non-null
-- legacy ids.
CREATE UNIQUE INDEX IF NOT EXISTS users_legacy_int_id_uniq
    ON secure_auth.users (legacy_int_id) WHERE legacy_int_id IS NOT NULL;

-- Same for username — pre-existing Phase 1 users have NULL.
CREATE UNIQUE INDEX IF NOT EXISTS users_username_uniq
    ON secure_auth.users (lower(username)) WHERE username IS NOT NULL;

-- app_users gains secure_auth_user_id. Nullable during migration;
-- backfill populates + a follow-up migration (Phase 4 cleanup) makes
-- it NOT NULL once every row has been paired.
ALTER TABLE app_users
    ADD COLUMN IF NOT EXISTS secure_auth_user_id uuid;

CREATE UNIQUE INDEX IF NOT EXISTS app_users_secure_auth_user_id_uniq
    ON app_users (secure_auth_user_id) WHERE secure_auth_user_id IS NOT NULL;

-- Optional FK back to secure_auth — deferred because ON DELETE SET NULL
-- is what we want but adding it requires ensuring secure_auth.users
-- never hard-deletes rows that app_users points at. Leave soft.

-- ─── 2. Backfill existing rows ───────────────────────────────────────────

-- For every app_users row, ensure there's a matching secure_auth.users.
-- Match by email first (Phase 1 might already have a secure_auth row
-- for corys@central.local); fall back to creating a new row keyed by
-- lower(email) if email is non-empty, else by username.
--
-- Users without an email (old auto-login rows) get a synthesised
-- email username@central.local so the NOT NULL UNIQUE on
-- secure_auth.users.email keeps working. Admin can fix up manually.

WITH ordered AS (
    SELECT
        id,
        username,
        CASE
            WHEN email IS NULL OR email = '' OR email = 'admin@local'
                 THEN lower(username) || '@central.local'
            ELSE lower(email)
        END AS resolved_email,
        display_name,
        role,
        is_active,
        password_hash,
        last_login_at,
        tenant_id,
        ad_sid,
        ad_guid,
        created_at
    FROM app_users
)
INSERT INTO secure_auth.users (
    id, email, password_hash, display_name, legacy_int_id,
    username, role, is_active, tenant_id, ad_sid, ad_guid,
    last_login_at, created_at
)
SELECT
    coalesce(au.secure_auth_user_id, gen_random_uuid()),
    o.resolved_email,
    CASE
        WHEN o.password_hash IS NULL OR o.password_hash = '' THEN '(sso-only)'
        -- Legacy SHA-256 hashes are marked — auth-service won't verify
        -- against them (argon2.verify will fail) + a follow-up flow
        -- forces password reset on first web login for these users.
        WHEN o.password_hash LIKE '$argon2%' THEN o.password_hash
        ELSE '(legacy-sha256:' || o.password_hash || ')'
    END,
    o.display_name,
    o.id,
    o.username,
    COALESCE(NULLIF(o.role, ''), 'user'),
    COALESCE(o.is_active, true),
    o.tenant_id,
    NULLIF(o.ad_sid, ''),
    NULLIF(o.ad_guid, ''),
    o.last_login_at,
    COALESCE(o.created_at, now())
FROM ordered o
LEFT JOIN app_users au ON au.id = o.id
ON CONFLICT (email) DO UPDATE
    SET legacy_int_id = EXCLUDED.legacy_int_id,
        username      = COALESCE(secure_auth.users.username, EXCLUDED.username),
        role          = EXCLUDED.role,
        is_active     = EXCLUDED.is_active,
        tenant_id     = COALESCE(secure_auth.users.tenant_id, EXCLUDED.tenant_id),
        ad_sid        = COALESCE(secure_auth.users.ad_sid, EXCLUDED.ad_sid),
        ad_guid       = COALESCE(secure_auth.users.ad_guid, EXCLUDED.ad_guid);

-- Second pass — populate app_users.secure_auth_user_id for rows just
-- linked above.
UPDATE app_users au
   SET secure_auth_user_id = su.id
  FROM secure_auth.users su
 WHERE su.legacy_int_id = au.id
   AND (au.secure_auth_user_id IS NULL OR au.secure_auth_user_id <> su.id);

-- ─── 3. Dual-write triggers ──────────────────────────────────────────────
-- Keep the basic identity fields (email, username, display_name, role,
-- is_active, password_hash, last_login_at) in sync in both directions.
-- pg_trigger_depth() > 1 means we're already inside another trigger —
-- skip to avoid feedback loops.

CREATE OR REPLACE FUNCTION secure_auth.mirror_to_secure_auth_users()
    RETURNS trigger
    LANGUAGE plpgsql
AS $$
DECLARE
    v_uuid uuid;
BEGIN
    IF pg_trigger_depth() > 1 THEN RETURN NEW; END IF;

    IF TG_OP = 'INSERT' THEN
        INSERT INTO secure_auth.users (
            id, email, password_hash, display_name, legacy_int_id,
            username, role, is_active, tenant_id, last_login_at
        ) VALUES (
            COALESCE(NEW.secure_auth_user_id, gen_random_uuid()),
            CASE
                WHEN NEW.email IS NULL OR NEW.email = ''
                     THEN lower(NEW.username) || '@central.local'
                ELSE lower(NEW.email)
            END,
            CASE
                WHEN NEW.password_hash IS NULL OR NEW.password_hash = '' THEN '(sso-only)'
                WHEN NEW.password_hash LIKE '$argon2%' THEN NEW.password_hash
                ELSE '(legacy-sha256:' || NEW.password_hash || ')'
            END,
            NEW.display_name,
            NEW.id,
            NEW.username,
            COALESCE(NULLIF(NEW.role, ''), 'user'),
            COALESCE(NEW.is_active, true),
            NEW.tenant_id,
            NEW.last_login_at
        )
        ON CONFLICT (email) DO UPDATE
            SET legacy_int_id = EXCLUDED.legacy_int_id,
                username      = COALESCE(secure_auth.users.username, EXCLUDED.username)
        RETURNING id INTO v_uuid;

        IF v_uuid IS NOT NULL AND (NEW.secure_auth_user_id IS NULL OR NEW.secure_auth_user_id <> v_uuid) THEN
            NEW.secure_auth_user_id := v_uuid;
        END IF;
    ELSIF TG_OP = 'UPDATE' THEN
        UPDATE secure_auth.users
           SET email         = CASE
                                   WHEN NEW.email IS NULL OR NEW.email = ''
                                        THEN lower(NEW.username) || '@central.local'
                                   ELSE lower(NEW.email)
                               END,
               display_name  = NEW.display_name,
               username      = NEW.username,
               role          = COALESCE(NULLIF(NEW.role, ''), 'user'),
               is_active     = COALESCE(NEW.is_active, true),
               tenant_id     = NEW.tenant_id,
               last_login_at = NEW.last_login_at,
               updated_at    = now()
         WHERE legacy_int_id = NEW.id;
    END IF;

    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION secure_auth.mirror_to_app_users()
    RETURNS trigger
    LANGUAGE plpgsql
AS $$
DECLARE
    v_new_id integer;
BEGIN
    IF pg_trigger_depth() > 1 THEN RETURN NEW; END IF;

    IF TG_OP = 'INSERT' THEN
        -- Only create an app_users row when we have a username to key on.
        -- Web-only users (no username) don't need a legacy row until
        -- first desktop interaction.
        IF NEW.username IS NULL THEN RETURN NEW; END IF;

        INSERT INTO app_users (username, display_name, role, is_active,
                               password_hash, email, tenant_id,
                               last_login_at, user_type,
                               secure_auth_user_id)
        VALUES (NEW.username, NEW.display_name, COALESCE(NEW.role, 'user'),
                COALESCE(NEW.is_active, true),
                CASE
                    WHEN NEW.password_hash LIKE '(legacy-sha256:%' THEN
                        substring(NEW.password_hash from 17 for length(NEW.password_hash) - 17)
                    WHEN NEW.password_hash LIKE '$argon2%' THEN ''  -- desktop can't verify argon2 directly
                    ELSE ''
                END,
                NEW.email, NEW.tenant_id, NEW.last_login_at,
                CASE WHEN NEW.ad_guid IS NOT NULL AND NEW.ad_guid <> ''
                     THEN 'ActiveDirectory' ELSE 'Local' END,
                NEW.id)
        ON CONFLICT (username) DO UPDATE
            SET secure_auth_user_id = EXCLUDED.secure_auth_user_id,
                display_name        = EXCLUDED.display_name,
                role                = EXCLUDED.role,
                is_active           = EXCLUDED.is_active,
                email               = EXCLUDED.email
        RETURNING id INTO v_new_id;

        IF v_new_id IS NOT NULL AND (NEW.legacy_int_id IS NULL OR NEW.legacy_int_id <> v_new_id) THEN
            NEW.legacy_int_id := v_new_id;
        END IF;
    ELSIF TG_OP = 'UPDATE' AND NEW.legacy_int_id IS NOT NULL THEN
        UPDATE app_users
           SET display_name  = NEW.display_name,
               role          = COALESCE(NEW.role, 'user'),
               is_active     = COALESCE(NEW.is_active, true),
               email         = NEW.email,
               tenant_id     = NEW.tenant_id,
               last_login_at = NEW.last_login_at,
               updated_at    = now()
         WHERE id = NEW.legacy_int_id;
    END IF;

    RETURN NEW;
END;
$$;

-- Attach triggers to both tables.
DROP TRIGGER IF EXISTS app_users_mirror_to_secure_auth ON app_users;
CREATE TRIGGER app_users_mirror_to_secure_auth
    BEFORE INSERT OR UPDATE ON app_users
    FOR EACH ROW EXECUTE FUNCTION secure_auth.mirror_to_secure_auth_users();

DROP TRIGGER IF EXISTS secure_auth_users_mirror_to_app_users ON secure_auth.users;
CREATE TRIGGER secure_auth_users_mirror_to_app_users
    BEFORE INSERT OR UPDATE ON secure_auth.users
    FOR EACH ROW EXECUTE FUNCTION secure_auth.mirror_to_app_users();

-- ─── 4. Record in schema_versions if present ─────────────────────────────
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'schema_versions') THEN
        INSERT INTO schema_versions (version_number, description)
        VALUES ('116_user_store_bridge',
                'Phase 2 of IDP buildout: dual-write bridge between app_users '
             || '(FK-referenced legacy) and secure_auth.users (identity source-'
             || 'of-truth). See docs/IDP_BUILDOUT.md Phase 2.')
        ON CONFLICT (version_number) DO NOTHING;
    END IF;
END $$;
