-- Security enhancements: IP allowlist, user SSH keys, auto-deprovisioning, API key rotation

-- IP allowlist/blocklist per tenant
CREATE TABLE IF NOT EXISTS ip_access_rules (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    cidr            cidr NOT NULL,
    rule_type       text NOT NULL DEFAULT 'allow',   -- allow, block
    label           text,
    applies_to      text NOT NULL DEFAULT 'api',     -- api, admin, all
    is_active       boolean NOT NULL DEFAULT true,
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz
);

CREATE INDEX IF NOT EXISTS idx_ip_rules_tenant ON ip_access_rules(tenant_id);
CREATE INDEX IF NOT EXISTS idx_ip_rules_cidr ON ip_access_rules USING gist (cidr inet_ops);

-- User SSH/API keys (for key-based auth to switches or app)
CREATE TABLE IF NOT EXISTS user_ssh_keys (
    id              serial PRIMARY KEY,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    label           text NOT NULL,
    key_type        text NOT NULL,   -- rsa, ed25519, ecdsa
    public_key      text NOT NULL,
    fingerprint     text NOT NULL UNIQUE,
    is_active       boolean NOT NULL DEFAULT true,
    last_used_at    timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz
);

CREATE INDEX IF NOT EXISTS idx_user_ssh_user ON user_ssh_keys(user_id);

-- Auto-deprovisioning rules (inactive users, IdP sync)
CREATE TABLE IF NOT EXISTS deprovisioning_rules (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    rule_type       text NOT NULL,   -- inactivity_days, idp_removed, manager_requested
    threshold_days  int,             -- for inactivity_days
    action          text NOT NULL DEFAULT 'disable',  -- disable, delete, notify_only
    is_enabled      boolean NOT NULL DEFAULT true,
    last_run_at     timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Deprovisioning history (audit)
CREATE TABLE IF NOT EXISTS deprovisioning_log (
    id              serial PRIMARY KEY,
    user_id         int REFERENCES app_users(id) ON DELETE SET NULL,
    username        text NOT NULL,
    rule_id         int REFERENCES deprovisioning_rules(id) ON DELETE SET NULL,
    action          text NOT NULL,
    reason          text,
    executed_at     timestamptz NOT NULL DEFAULT now()
);

-- API key rotation tracking
ALTER TABLE api_keys ADD COLUMN IF NOT EXISTS last_rotated_at timestamptz;
ALTER TABLE api_keys ADD COLUMN IF NOT EXISTS rotation_warning_sent boolean DEFAULT false;
ALTER TABLE api_keys ADD COLUMN IF NOT EXISTS rotation_interval_days int;

-- Terms of Service tracking
CREATE TABLE IF NOT EXISTS terms_of_service (
    id              serial PRIMARY KEY,
    version         text NOT NULL,
    content_url     text NOT NULL,
    effective_date  date NOT NULL,
    requires_acceptance boolean NOT NULL DEFAULT true,
    published_at    timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS user_tos_acceptance (
    id              serial PRIMARY KEY,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    tos_id          int NOT NULL REFERENCES terms_of_service(id),
    accepted_at     timestamptz NOT NULL DEFAULT now(),
    ip_address      inet,
    user_agent      text,
    UNIQUE(user_id, tos_id)
);

-- Domain verification for tenant auto-assignment
CREATE TABLE IF NOT EXISTS domain_verifications (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL,
    domain          text NOT NULL UNIQUE,
    verification_token text NOT NULL,
    method          text NOT NULL DEFAULT 'dns_txt',  -- dns_txt, http_file, email
    is_verified     boolean NOT NULL DEFAULT false,
    verified_at     timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz
);

CREATE INDEX IF NOT EXISTS idx_domain_verify_tenant ON domain_verifications(tenant_id);
