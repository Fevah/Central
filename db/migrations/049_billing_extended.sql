-- Billing extensions: trials, discounts, addons, POs, annual billing, grace periods, proration

-- Annual vs monthly pricing in plans
ALTER TABLE central_platform.subscription_plans ADD COLUMN IF NOT EXISTS price_annual numeric(10,2);
ALTER TABLE central_platform.subscription_plans ADD COLUMN IF NOT EXISTS annual_discount_pct int DEFAULT 20;

-- Extend tenant_subscriptions with billing cycle, trial, grace period
ALTER TABLE central_platform.tenant_subscriptions ADD COLUMN IF NOT EXISTS billing_cycle text DEFAULT 'monthly';  -- monthly, annual
ALTER TABLE central_platform.tenant_subscriptions ADD COLUMN IF NOT EXISTS trial_ends_at timestamptz;
ALTER TABLE central_platform.tenant_subscriptions ADD COLUMN IF NOT EXISTS grace_period_ends_at timestamptz;
ALTER TABLE central_platform.tenant_subscriptions ADD COLUMN IF NOT EXISTS is_trial boolean NOT NULL DEFAULT false;
ALTER TABLE central_platform.tenant_subscriptions ADD COLUMN IF NOT EXISTS discount_pct numeric(5,2) DEFAULT 0;
ALTER TABLE central_platform.tenant_subscriptions ADD COLUMN IF NOT EXISTS discount_reason text;
ALTER TABLE central_platform.tenant_subscriptions ADD COLUMN IF NOT EXISTS next_invoice_at timestamptz;
ALTER TABLE central_platform.tenant_subscriptions ADD COLUMN IF NOT EXISTS cancel_at timestamptz;
ALTER TABLE central_platform.tenant_subscriptions ADD COLUMN IF NOT EXISTS cancelled_at timestamptz;

-- Subscription addons (premium features on top of base plan)
CREATE TABLE IF NOT EXISTS central_platform.subscription_addons (
    id              serial PRIMARY KEY,
    code            text NOT NULL UNIQUE,
    name            text NOT NULL,
    description     text,
    price_monthly   numeric(10,2) NOT NULL DEFAULT 0,
    price_annual    numeric(10,2),
    is_active       boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS central_platform.tenant_addons (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    addon_code      text NOT NULL REFERENCES central_platform.subscription_addons(code),
    quantity        int NOT NULL DEFAULT 1,
    started_at      timestamptz NOT NULL DEFAULT now(),
    ends_at         timestamptz,
    UNIQUE(tenant_id, addon_code)
);

-- Discounts / coupons
CREATE TABLE IF NOT EXISTS central_platform.discount_codes (
    id              serial PRIMARY KEY,
    code            text NOT NULL UNIQUE,
    description     text,
    discount_type   text NOT NULL DEFAULT 'percent',  -- percent, fixed
    discount_value  numeric(10,2) NOT NULL,
    max_uses        int,
    times_used      int NOT NULL DEFAULT 0,
    valid_from      date,
    valid_to        date,
    is_active       boolean NOT NULL DEFAULT true,
    created_by      int,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS central_platform.discount_redemptions (
    id              serial PRIMARY KEY,
    discount_code_id int NOT NULL REFERENCES central_platform.discount_codes(id),
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    redeemed_at     timestamptz NOT NULL DEFAULT now(),
    invoice_id      int,
    UNIQUE(discount_code_id, tenant_id)
);

-- Payment methods (Stripe-compatible)
CREATE TABLE IF NOT EXISTS central_platform.payment_methods (
    id              serial PRIMARY KEY,
    billing_account_id int NOT NULL REFERENCES central_platform.billing_accounts(id) ON DELETE CASCADE,
    method_type     text NOT NULL,     -- card, bank, po
    stripe_pm_id    text,
    last4           text,
    brand           text,              -- visa, mastercard, amex
    exp_month       int,
    exp_year        int,
    is_default      boolean NOT NULL DEFAULT false,
    po_number       text,              -- for purchase order billing
    po_expires_at   date,
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Proration log (tracking changes during billing period)
CREATE TABLE IF NOT EXISTS central_platform.proration_events (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    event_type      text NOT NULL,     -- upgrade, downgrade, addon_added, addon_removed
    prev_plan       text,
    new_plan        text,
    amount_credited numeric(10,2) DEFAULT 0,
    amount_charged  numeric(10,2) DEFAULT 0,
    effective_from  timestamptz NOT NULL DEFAULT now(),
    metadata        jsonb DEFAULT '{}',
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Usage metering quotas (stricter enforcement)
CREATE TABLE IF NOT EXISTS central_platform.usage_quotas (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    quota_type      text NOT NULL,     -- api_calls_monthly, storage_gb, users, devices
    limit_value     numeric NOT NULL,
    current_usage   numeric NOT NULL DEFAULT 0,
    period_start    date NOT NULL DEFAULT CURRENT_DATE,
    period_end      date,
    reset_cron      text,              -- 'monthly', 'annual'
    overage_action  text NOT NULL DEFAULT 'warn',  -- warn, block, charge
    overage_unit_price numeric(10,4),
    UNIQUE(tenant_id, quota_type, period_start)
);

-- Seed some add-ons
INSERT INTO central_platform.subscription_addons (code, name, description, price_monthly, price_annual) VALUES
    ('extra_users_10', 'Extra 10 Users', 'Additional 10 user seats', 20, 200),
    ('extra_storage_100gb', 'Extra 100 GB Storage', 'Additional file storage', 15, 150),
    ('premium_support', 'Premium Support', '24/7 phone support + 1hr SLA', 99, 999),
    ('advanced_security', 'Advanced Security', 'IP allowlist, MFA enforcement, audit export', 49, 490),
    ('api_quota_boost', 'API Quota Boost', '10× API rate limit', 29, 290)
ON CONFLICT (code) DO NOTHING;
