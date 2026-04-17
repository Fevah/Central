-- =============================================================================
-- Stage 2.2-2.3: Quotas + Commission plans + tiers + payouts
-- =============================================================================

-- Quotas (per user, per period, optionally per territory/product)
CREATE TABLE IF NOT EXISTS crm_quotas (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    territory_id    int REFERENCES crm_territories(id) ON DELETE SET NULL,
    product_category text,
    period_type     text NOT NULL DEFAULT 'quarterly', -- monthly, quarterly, annual
    period_start    date NOT NULL,
    period_end      date NOT NULL,
    target_amount   numeric(14,2) NOT NULL,
    currency        char(3) NOT NULL DEFAULT 'GBP',
    ramp_pct        numeric(5,2) DEFAULT 100,         -- 50% for first quarter for new hires
    notes           text,
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(user_id, period_start, period_end, product_category)
);

CREATE INDEX IF NOT EXISTS idx_quotas_user_period ON crm_quotas(user_id, period_start, period_end);

-- Commission plans (reusable templates assigned to users)
CREATE TABLE IF NOT EXISTS crm_commission_plans (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    description     text,
    plan_type       text NOT NULL DEFAULT 'flat',    -- flat, tiered, gated
    base_rate_pct   numeric(5,2) NOT NULL DEFAULT 10,
    is_active       boolean NOT NULL DEFAULT true,
    effective_from  date,
    effective_to    date,
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Plan tiers (accelerators)
CREATE TABLE IF NOT EXISTS crm_commission_tiers (
    id              serial PRIMARY KEY,
    plan_id         int NOT NULL REFERENCES crm_commission_plans(id) ON DELETE CASCADE,
    tier_order      int NOT NULL,
    min_attainment_pct numeric(6,2) NOT NULL,        -- 100% = fully attained quota
    max_attainment_pct numeric(6,2),                  -- NULL = unbounded
    rate_pct        numeric(5,2) NOT NULL,
    UNIQUE(plan_id, tier_order)
);

-- Assign plans to users
CREATE TABLE IF NOT EXISTS crm_user_commission_plans (
    id              serial PRIMARY KEY,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    plan_id         int NOT NULL REFERENCES crm_commission_plans(id),
    effective_from  date NOT NULL DEFAULT CURRENT_DATE,
    effective_to    date,
    UNIQUE(user_id, plan_id, effective_from)
);

-- Commission payouts (calculated, one per user per period)
CREATE TABLE IF NOT EXISTS crm_commission_payouts (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    period_start    date NOT NULL,
    period_end      date NOT NULL,
    quota_amount    numeric(14,2) NOT NULL DEFAULT 0,
    achieved_amount numeric(14,2) NOT NULL DEFAULT 0,
    attainment_pct  numeric(6,2) NOT NULL DEFAULT 0,
    commission_amount numeric(14,2) NOT NULL DEFAULT 0,
    spiff_amount    numeric(14,2) DEFAULT 0,
    clawback_amount numeric(14,2) DEFAULT 0,
    net_payout      numeric(14,2) NOT NULL DEFAULT 0,
    currency        char(3) NOT NULL DEFAULT 'GBP',
    status          text NOT NULL DEFAULT 'draft',   -- draft, approved, paid
    approved_by     int REFERENCES app_users(id),
    approved_at     timestamptz,
    paid_at         timestamptz,
    breakdown       jsonb DEFAULT '{}',              -- deal-level contribution
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(user_id, period_start, period_end)
);

CREATE INDEX IF NOT EXISTS idx_payouts_user ON crm_commission_payouts(user_id);
CREATE INDEX IF NOT EXISTS idx_payouts_status ON crm_commission_payouts(status);
