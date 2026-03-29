-- Migration 047: Multi-provider authentication framework
-- Supports SAML2/Duo, Entra ID, Okta, Public/Local auth

-- Identity provider configuration
CREATE TABLE IF NOT EXISTS identity_providers (
    id              serial PRIMARY KEY,
    provider_type   varchar(32) NOT NULL,        -- 'saml2', 'entra_id', 'okta', 'local'
    name            varchar(128) NOT NULL,
    is_enabled      boolean NOT NULL DEFAULT true,
    is_default      boolean NOT NULL DEFAULT false,
    priority        integer NOT NULL DEFAULT 100,
    config_json     jsonb NOT NULL DEFAULT '{}',
    metadata_url    varchar(512),
    created_at      timestamptz DEFAULT now(),
    updated_at      timestamptz DEFAULT now()
);

-- Domain-to-provider routing for IdP discovery
CREATE TABLE IF NOT EXISTS idp_domain_mappings (
    id              serial PRIMARY KEY,
    email_domain    varchar(256) NOT NULL UNIQUE,
    provider_id     integer NOT NULL REFERENCES identity_providers(id) ON DELETE CASCADE
);

-- External identity links (one user can have multiple IdP identities)
CREATE TABLE IF NOT EXISTS user_external_identities (
    id              serial PRIMARY KEY,
    user_id         integer NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    provider_id     integer NOT NULL REFERENCES identity_providers(id) ON DELETE CASCADE,
    external_id     varchar(512) NOT NULL,
    external_email  varchar(256),
    external_claims jsonb,
    linked_at       timestamptz DEFAULT now(),
    last_login_at   timestamptz,
    UNIQUE (provider_id, external_id)
);

-- Claims-to-role mapping rules
CREATE TABLE IF NOT EXISTS claim_mappings (
    id              serial PRIMARY KEY,
    provider_id     integer REFERENCES identity_providers(id) ON DELETE CASCADE,
    claim_type      varchar(256) NOT NULL,
    claim_value     varchar(256) NOT NULL,
    target_role     varchar(50) NOT NULL,
    priority        integer NOT NULL DEFAULT 100,
    is_enabled      boolean NOT NULL DEFAULT true
);

-- Auth event log (append-only audit trail)
CREATE TABLE IF NOT EXISTS auth_events (
    id              bigserial PRIMARY KEY,
    timestamp       timestamptz NOT NULL DEFAULT now(),
    event_type      varchar(32) NOT NULL,
    provider_type   varchar(32),
    username        varchar(128),
    user_id         integer REFERENCES app_users(id),
    ip_address      varchar(45),
    user_agent      varchar(512),
    success         boolean NOT NULL DEFAULT true,
    error_message   text,
    metadata        jsonb
);

CREATE INDEX IF NOT EXISTS idx_auth_events_user ON auth_events (username, timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_auth_events_type ON auth_events (event_type, timestamp DESC);

-- Extend app_users for multi-provider auth
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS idp_provider_id integer REFERENCES identity_providers(id);
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS external_id varchar(512);
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS mfa_enabled boolean DEFAULT false;
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS mfa_secret_enc varchar(512);
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS failed_login_count integer DEFAULT 0;
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS locked_until timestamptz;
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS password_changed_at timestamptz;
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS session_version integer DEFAULT 1;

-- Session revocation
CREATE TABLE IF NOT EXISTS revoked_tokens (
    jti             varchar(128) PRIMARY KEY,
    revoked_at      timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_revoked_tokens_expiry ON revoked_tokens (expires_at);

-- Social provider registrations
CREATE TABLE IF NOT EXISTS social_providers (
    id              serial PRIMARY KEY,
    provider_name   varchar(32) NOT NULL UNIQUE,
    client_id       varchar(256) NOT NULL,
    client_secret_enc varchar(512) NOT NULL,
    is_enabled      boolean DEFAULT false,
    scopes          text DEFAULT 'openid email profile'
);

-- Magic link tokens
CREATE TABLE IF NOT EXISTS magic_link_tokens (
    id              serial PRIMARY KEY,
    email           varchar(256) NOT NULL,
    token_hash      varchar(128) NOT NULL UNIQUE,
    expires_at      timestamptz NOT NULL,
    used_at         timestamptz,
    created_at      timestamptz DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_magic_link_email ON magic_link_tokens (email, expires_at);

-- MFA recovery codes
CREATE TABLE IF NOT EXISTS mfa_recovery_codes (
    id              serial PRIMARY KEY,
    user_id         integer NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    code_hash       varchar(128) NOT NULL,
    used_at         timestamptz
);

-- Seed the local auth provider (always exists)
INSERT INTO identity_providers (provider_type, name, is_enabled, is_default, priority, config_json)
VALUES ('local', 'Local Authentication', true, true, 999,
    '{"password_min_length":8,"lockout_threshold":5,"lockout_duration_minutes":30,"require_mfa":false}')
ON CONFLICT DO NOTHING;

-- Permissions
INSERT INTO permissions (code, name, category, description) VALUES
    ('admin:identity', 'Identity Providers', 'admin', 'Manage authentication providers and claim mappings')
ON CONFLICT DO NOTHING;
