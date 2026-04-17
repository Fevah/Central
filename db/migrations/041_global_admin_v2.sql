-- Phases 6-10: Global Admin enhancements
-- Onboarding, billing, usage analytics, search indexes.

-- Phase 6: Tenant onboarding wizard tracking
CREATE TABLE IF NOT EXISTS central_platform.tenant_onboarding (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    step_name       text NOT NULL,     -- company_setup, admin_user, branding, modules, billing, complete
    status          text NOT NULL DEFAULT 'pending', -- pending, in_progress, completed, skipped
    completed_at    timestamptz,
    metadata        jsonb DEFAULT '{}',
    UNIQUE(tenant_id, step_name)
);

-- Phase 6: Tenant branding
ALTER TABLE central_platform.tenants ADD COLUMN IF NOT EXISTS logo_url text;
ALTER TABLE central_platform.tenants ADD COLUMN IF NOT EXISTS primary_color text DEFAULT '#1976D2';
ALTER TABLE central_platform.tenants ADD COLUMN IF NOT EXISTS subdomain text;
ALTER TABLE central_platform.tenants ADD COLUMN IF NOT EXISTS template text DEFAULT 'default';

-- Phase 7: Billing
CREATE TABLE IF NOT EXISTS central_platform.billing_accounts (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE UNIQUE,
    stripe_customer_id text,
    payment_method  text,
    billing_email   text,
    billing_name    text,
    currency        char(3) NOT NULL DEFAULT 'GBP',
    tax_exempt      boolean NOT NULL DEFAULT false,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS central_platform.invoices (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    billing_account_id int REFERENCES central_platform.billing_accounts(id),
    invoice_number  text NOT NULL,
    amount          numeric(10,2) NOT NULL,
    currency        char(3) NOT NULL DEFAULT 'GBP',
    status          text NOT NULL DEFAULT 'draft',  -- draft, sent, paid, overdue, cancelled
    stripe_invoice_id text,
    pdf_url         text,
    due_date        date,
    paid_at         timestamptz,
    period_start    date,
    period_end      date,
    line_items      jsonb DEFAULT '[]',
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_invoices_tenant ON central_platform.invoices(tenant_id);

-- Phase 8: Usage analytics
CREATE TABLE IF NOT EXISTS central_platform.tenant_usage_metrics (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    metric_type     text NOT NULL,     -- active_users, api_calls, storage_bytes, devices, logins
    metric_value    numeric NOT NULL,
    recorded_at     timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_usage_metrics_tenant ON central_platform.tenant_usage_metrics(tenant_id, metric_type, recorded_at DESC);

-- Phase 9: Full-text search indexes
CREATE INDEX IF NOT EXISTS idx_companies_fts ON companies USING gin(to_tsvector('english', coalesce(name,'') || ' ' || coalesce(legal_name,'') || ' ' || coalesce(industry,'')));
CREATE INDEX IF NOT EXISTS idx_contacts_fts ON contacts USING gin(to_tsvector('english', coalesce(first_name,'') || ' ' || coalesce(last_name,'') || ' ' || coalesce(email,'') || ' ' || coalesce(job_title,'')));
CREATE INDEX IF NOT EXISTS idx_app_users_fts ON app_users USING gin(to_tsvector('english', coalesce(username,'') || ' ' || coalesce(display_name,'') || ' ' || coalesce(email,'')));
