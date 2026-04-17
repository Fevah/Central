-- =============================================================================
-- Stage 3.1-3.2: Product bundles + pricing rules (CPQ)
-- =============================================================================

-- Bundles (parent product composed of child SKUs)
CREATE TABLE IF NOT EXISTS crm_product_bundles (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    parent_product_id int NOT NULL REFERENCES crm_products(id) ON DELETE CASCADE,
    name            text NOT NULL,
    description     text,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS crm_bundle_components (
    id              serial PRIMARY KEY,
    bundle_id       int NOT NULL REFERENCES crm_product_bundles(id) ON DELETE CASCADE,
    component_product_id int NOT NULL REFERENCES crm_products(id),
    quantity        numeric(10,2) NOT NULL DEFAULT 1,
    is_optional     boolean NOT NULL DEFAULT false,
    override_price  numeric(14,2),                    -- override component price when in bundle
    sort_order      int NOT NULL DEFAULT 0,
    UNIQUE(bundle_id, component_product_id)
);

CREATE INDEX IF NOT EXISTS idx_bundle_components_bundle ON crm_bundle_components(bundle_id);

-- Pricing rules (volume breaks, customer-specific, promo, MAP floor)
CREATE TABLE IF NOT EXISTS crm_pricing_rules (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    rule_type       text NOT NULL,                    -- volume, customer, promo, map_floor, bundle
    product_id      int REFERENCES crm_products(id) ON DELETE CASCADE,
    bundle_id       int REFERENCES crm_product_bundles(id) ON DELETE CASCADE,
    -- Volume rules
    min_quantity    numeric(10,2),
    max_quantity    numeric(10,2),
    -- Customer-specific
    account_id      int REFERENCES crm_accounts(id) ON DELETE CASCADE,
    -- Promo
    promo_code      text,
    valid_from      date,
    valid_to        date,
    max_uses        int,
    times_used      int NOT NULL DEFAULT 0,
    -- All rules
    discount_pct    numeric(5,2),
    discount_amount numeric(14,2),
    fixed_price     numeric(14,2),                    -- MAP floor or override
    priority        int NOT NULL DEFAULT 100,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_pricing_rules_product ON crm_pricing_rules(product_id);
CREATE INDEX IF NOT EXISTS idx_pricing_rules_account ON crm_pricing_rules(account_id);
CREATE INDEX IF NOT EXISTS idx_pricing_rules_promo ON crm_pricing_rules(promo_code);

-- Discount approval matrix — routes approval based on thresholds
CREATE TABLE IF NOT EXISTS crm_discount_approval_matrix (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    min_discount_pct numeric(5,2) NOT NULL DEFAULT 0,
    max_discount_pct numeric(5,2) NOT NULL DEFAULT 100,
    min_deal_size   numeric(14,2),
    max_deal_size   numeric(14,2),
    product_category text,
    required_role   text NOT NULL,                   -- Sales Manager, VP Sales, CEO
    approver_user_id int REFERENCES app_users(id),    -- specific user or NULL for role-based
    priority        int NOT NULL DEFAULT 100,
    is_active       boolean NOT NULL DEFAULT true
);

CREATE INDEX IF NOT EXISTS idx_discount_matrix_tenant ON crm_discount_approval_matrix(tenant_id);

-- Seed reasonable defaults
INSERT INTO crm_discount_approval_matrix
    (name, min_discount_pct, max_discount_pct, required_role, priority) VALUES
    ('No approval needed',  0,  10, 'Sales Rep',       10),
    ('Manager approval',   10,  25, 'Sales Manager',   20),
    ('VP approval',        25,  40, 'VP Sales',        30),
    ('CEO approval',       40, 100, 'CEO',             40)
ON CONFLICT DO NOTHING;
