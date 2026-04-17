-- =============================================================================
-- Stage 2.1: Territories + assignment rules
-- =============================================================================

CREATE TABLE IF NOT EXISTS crm_territories (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    parent_id       int REFERENCES crm_territories(id) ON DELETE SET NULL,
    territory_type  text NOT NULL DEFAULT 'geographic', -- geographic, industry, account_size, named_account, role_based
    description     text,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_territories_tenant ON crm_territories(tenant_id);
CREATE INDEX IF NOT EXISTS idx_territories_parent ON crm_territories(parent_id);

-- Territory membership (users assigned to territories)
CREATE TABLE IF NOT EXISTS crm_territory_members (
    id              serial PRIMARY KEY,
    territory_id    int NOT NULL REFERENCES crm_territories(id) ON DELETE CASCADE,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    role            text NOT NULL DEFAULT 'owner',    -- owner, overlay, manager
    is_forecast_manager boolean NOT NULL DEFAULT false,
    joined_at       timestamptz NOT NULL DEFAULT now(),
    UNIQUE(territory_id, user_id)
);

CREATE INDEX IF NOT EXISTS idx_territory_members_user ON crm_territory_members(user_id);

-- Assignment rules — auto-assign accounts/leads to territories
CREATE TABLE IF NOT EXISTS crm_territory_rules (
    id              serial PRIMARY KEY,
    territory_id    int NOT NULL REFERENCES crm_territories(id) ON DELETE CASCADE,
    rule_name       text NOT NULL,
    field           text NOT NULL,                    -- country, state, industry, employee_count, revenue
    operator        text NOT NULL,                    -- equals, in, range, starts_with, contains
    value           text NOT NULL,                    -- or JSON array for 'in'
    priority        int NOT NULL DEFAULT 100,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_territory_rules_territory ON crm_territory_rules(territory_id);

-- Link accounts + leads to territories (nullable; auto-filled by rules)
ALTER TABLE crm_accounts ADD COLUMN IF NOT EXISTS territory_id int REFERENCES crm_territories(id) ON DELETE SET NULL;
ALTER TABLE crm_leads    ADD COLUMN IF NOT EXISTS territory_id int REFERENCES crm_territories(id) ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS idx_accounts_territory ON crm_accounts(territory_id);
CREATE INDEX IF NOT EXISTS idx_leads_territory    ON crm_leads(territory_id);
