-- Tenant-specific feature flags

CREATE TABLE IF NOT EXISTS feature_flags (
    id              serial PRIMARY KEY,
    flag_key        text NOT NULL UNIQUE,
    name            text NOT NULL,
    description     text,
    default_enabled boolean NOT NULL DEFAULT false,
    category        text,             -- ui, api, beta, experimental, rollout
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS tenant_feature_flags (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL,
    flag_key        text NOT NULL REFERENCES feature_flags(flag_key) ON DELETE CASCADE,
    is_enabled      boolean NOT NULL,
    rollout_pct     int DEFAULT 100,  -- percentage rollout for gradual release
    enabled_at      timestamptz,
    disabled_at     timestamptz,
    set_by          int REFERENCES app_users(id),
    UNIQUE(tenant_id, flag_key)
);

CREATE INDEX IF NOT EXISTS idx_tenant_flags_tenant ON tenant_feature_flags(tenant_id);

-- Seed core flags
INSERT INTO feature_flags (flag_key, name, description, default_enabled, category) VALUES
    ('crm.accounts', 'CRM Accounts', 'Enable CRM accounts module', false, 'beta'),
    ('crm.deals', 'CRM Deals', 'Enable deal pipeline', false, 'beta'),
    ('billing.stripe', 'Stripe Billing', 'Enable Stripe payment integration', false, 'rollout'),
    ('auth.social_login', 'Social Login', 'Enable Google/Microsoft/GitHub OAuth', false, 'rollout'),
    ('security.ip_allowlist', 'IP Allowlist', 'Enable per-tenant IP whitelisting', true, 'security'),
    ('ui.dark_mode', 'Dark Mode', 'Enable dark theme option', true, 'ui'),
    ('ui.dashboard_v2', 'New Dashboard', 'Enable redesigned dashboard', true, 'ui'),
    ('api.graphql', 'GraphQL API', 'Enable GraphQL endpoint', false, 'experimental'),
    ('workflow.approvals', 'Approval Workflows', 'Enable Elsa approval flows', false, 'beta')
ON CONFLICT (flag_key) DO NOTHING;
