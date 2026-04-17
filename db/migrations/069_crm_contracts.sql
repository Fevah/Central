-- =============================================================================
-- Stage 3.5-3.6: Contract lifecycle management
-- =============================================================================

-- Clause library (standard contract clauses)
CREATE TABLE IF NOT EXISTS crm_contract_clauses (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    clause_code     text NOT NULL,                    -- payment_terms, liability, confidentiality
    title           text NOT NULL,
    body_html       text NOT NULL,
    category        text,
    is_required     boolean NOT NULL DEFAULT false,
    is_negotiable   boolean NOT NULL DEFAULT true,
    version         int NOT NULL DEFAULT 1,
    is_active       boolean NOT NULL DEFAULT true,
    legal_approved  boolean NOT NULL DEFAULT false,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, clause_code, version)
);

-- Contract templates (reusable templates for contract creation)
CREATE TABLE IF NOT EXISTS crm_contract_templates (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    contract_type   text NOT NULL,                    -- msa, sow, nda, dpa, amendment, renewal
    body_html       text NOT NULL,
    default_clauses int[] DEFAULT '{}',               -- FKs to crm_contract_clauses
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Contracts
CREATE TABLE IF NOT EXISTS crm_contracts (
    id                  serial PRIMARY KEY,
    tenant_id           uuid,
    account_id          int REFERENCES crm_accounts(id) ON DELETE SET NULL,
    deal_id             int REFERENCES crm_deals(id) ON DELETE SET NULL,
    quote_id            int REFERENCES crm_quotes(id) ON DELETE SET NULL,
    contract_number     text NOT NULL,
    title               text NOT NULL,
    contract_type       text NOT NULL,                -- msa, sow, nda, dpa, amendment, renewal
    status              text NOT NULL DEFAULT 'draft',-- draft, review, negotiation, signed, active, renewing, expired, terminated
    contract_value      numeric(14,2),
    currency            char(3) NOT NULL DEFAULT 'GBP',
    start_date          date,
    end_date            date,
    auto_renew          boolean NOT NULL DEFAULT false,
    renewal_term_months int,
    renewal_notice_days int DEFAULT 90,               -- days before end_date to alert
    signed_at           timestamptz,
    signed_by_name      text,
    counter_party       text,                          -- customer/vendor name
    template_id         int REFERENCES crm_contract_templates(id),
    body_html           text,                          -- full contract body (template-expanded)
    parent_contract_id  int REFERENCES crm_contracts(id), -- for amendments/renewals
    owner_id            int REFERENCES app_users(id),
    document_id         int REFERENCES crm_documents(id), -- signed PDF
    metadata            jsonb DEFAULT '{}',
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, contract_number)
);

CREATE INDEX IF NOT EXISTS idx_contracts_account ON crm_contracts(account_id);
CREATE INDEX IF NOT EXISTS idx_contracts_deal ON crm_contracts(deal_id);
CREATE INDEX IF NOT EXISTS idx_contracts_status ON crm_contracts(status);
CREATE INDEX IF NOT EXISTS idx_contracts_end_date ON crm_contracts(end_date);

-- Contract versions (negotiation iterations)
CREATE TABLE IF NOT EXISTS crm_contract_versions (
    id              serial PRIMARY KEY,
    contract_id     int NOT NULL REFERENCES crm_contracts(id) ON DELETE CASCADE,
    version_number  int NOT NULL,
    body_html       text NOT NULL,
    changed_by      int REFERENCES app_users(id),
    change_summary  text,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(contract_id, version_number)
);

CREATE INDEX IF NOT EXISTS idx_contract_versions_contract ON crm_contract_versions(contract_id);

-- Contract clauses (which clauses are in a contract — allows tracking negotiated modifications)
CREATE TABLE IF NOT EXISTS crm_contract_clause_usage (
    id              serial PRIMARY KEY,
    contract_id     int NOT NULL REFERENCES crm_contracts(id) ON DELETE CASCADE,
    clause_id       int REFERENCES crm_contract_clauses(id),
    body_html       text,                              -- as-included text (may be negotiated variant)
    is_modified     boolean NOT NULL DEFAULT false,
    modification_reason text,
    sort_order      int NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_contract_clauses_contract ON crm_contract_clause_usage(contract_id);

-- Contract milestones (delivery + payment schedules)
CREATE TABLE IF NOT EXISTS crm_contract_milestones (
    id              serial PRIMARY KEY,
    contract_id     int NOT NULL REFERENCES crm_contracts(id) ON DELETE CASCADE,
    milestone_type  text NOT NULL,                    -- delivery, payment, review, renewal_notice
    name            text NOT NULL,
    due_date        date NOT NULL,
    amount          numeric(14,2),
    status          text NOT NULL DEFAULT 'pending', -- pending, completed, overdue, cancelled
    completed_at    timestamptz,
    notes           text
);

CREATE INDEX IF NOT EXISTS idx_milestones_contract ON crm_contract_milestones(contract_id);
CREATE INDEX IF NOT EXISTS idx_milestones_due ON crm_contract_milestones(due_date) WHERE status = 'pending';

-- Renewal forecast view
CREATE OR REPLACE VIEW crm_contract_renewals AS
SELECT
    c.id, c.contract_number, c.title, c.tenant_id,
    c.account_id, a.name AS account_name,
    c.contract_value, c.currency, c.start_date, c.end_date,
    c.auto_renew, c.renewal_term_months, c.renewal_notice_days,
    c.owner_id, u.display_name AS owner_name,
    (c.end_date - CURRENT_DATE) AS days_to_renewal,
    CASE
        WHEN c.end_date < CURRENT_DATE THEN 'expired'
        WHEN c.end_date < CURRENT_DATE + (c.renewal_notice_days || ' days')::interval THEN 'renewal_window'
        WHEN c.end_date < CURRENT_DATE + INTERVAL '90 days' THEN 'approaching'
        ELSE 'future'
    END AS renewal_status
FROM crm_contracts c
LEFT JOIN crm_accounts a ON a.id = c.account_id
LEFT JOIN app_users u ON u.id = c.owner_id
WHERE c.status IN ('active', 'renewing') AND c.is_deleted IS NOT TRUE;

-- Handle possibly-missing is_deleted on crm_contracts (add it for consistency)
ALTER TABLE crm_contracts ADD COLUMN IF NOT EXISTS is_deleted boolean DEFAULT false;
