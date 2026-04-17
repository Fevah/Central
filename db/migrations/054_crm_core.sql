-- =============================================================================
-- CRM Core: Accounts, Deals, Leads, Activities
-- Phases 15-19 of the 29-phase CRM buildout.
-- =============================================================================

-- ─── CRM Accounts ────────────────────────────────────────────────────────────
-- Account = company in CRM context (customer/prospect/partner/vendor)
-- Wraps Company entity with CRM metadata: owner, pipeline stage, revenue, rating
CREATE TABLE IF NOT EXISTS crm_accounts (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    company_id      int REFERENCES companies(id) ON DELETE SET NULL,
    name            text NOT NULL,           -- denormalized for fast display
    account_type    text NOT NULL DEFAULT 'customer', -- customer, prospect, partner, vendor
    account_owner_id int REFERENCES app_users(id),
    annual_revenue  numeric(14,2),
    employee_count  int,
    industry        text,
    rating          text,                    -- hot, warm, cold
    source          text,                    -- web, referral, event, cold_call, partner
    last_activity_at timestamptz,
    next_follow_up  date,
    stage           text DEFAULT 'prospecting',
    website         text,
    description     text,
    tags            text[] DEFAULT '{}',
    metadata        jsonb DEFAULT '{}',
    is_active       boolean NOT NULL DEFAULT true,
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    is_deleted      boolean DEFAULT false,
    deleted_at      timestamptz
);

CREATE INDEX IF NOT EXISTS idx_crm_accounts_tenant ON crm_accounts(tenant_id);
CREATE INDEX IF NOT EXISTS idx_crm_accounts_company ON crm_accounts(company_id);
CREATE INDEX IF NOT EXISTS idx_crm_accounts_owner ON crm_accounts(account_owner_id);
CREATE INDEX IF NOT EXISTS idx_crm_accounts_type ON crm_accounts(account_type);
CREATE INDEX IF NOT EXISTS idx_crm_accounts_name_trgm ON crm_accounts USING gin(name gin_trgm_ops);

-- ─── Account ↔ Contact (many-to-many with roles) ────────────────────────────
-- A contact can belong to multiple accounts (consulting, multiple roles)
-- role_in_account: decision_maker, influencer, user, billing, technical, champion
CREATE TABLE IF NOT EXISTS crm_account_contacts (
    id                  serial PRIMARY KEY,
    account_id          int NOT NULL REFERENCES crm_accounts(id) ON DELETE CASCADE,
    contact_id          int NOT NULL REFERENCES contacts(id) ON DELETE CASCADE,
    role_in_account     text NOT NULL DEFAULT 'user',
    is_primary          boolean NOT NULL DEFAULT false,
    added_at            timestamptz NOT NULL DEFAULT now(),
    UNIQUE(account_id, contact_id)
);

CREATE INDEX IF NOT EXISTS idx_crm_account_contacts_account ON crm_account_contacts(account_id);
CREATE INDEX IF NOT EXISTS idx_crm_account_contacts_contact ON crm_account_contacts(contact_id);

-- ─── Deal stages (customizable pipeline) ────────────────────────────────────
CREATE TABLE IF NOT EXISTS crm_deal_stages (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    sort_order      int NOT NULL DEFAULT 100,
    probability     int NOT NULL DEFAULT 50,    -- % default probability
    is_won          boolean NOT NULL DEFAULT false,
    is_lost         boolean NOT NULL DEFAULT false,
    color           text DEFAULT '#808080',
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Seed default pipeline
INSERT INTO crm_deal_stages (name, sort_order, probability, is_won, is_lost, color) VALUES
    ('Qualification', 10, 20, false, false, '#6366F1'),
    ('Proposal',      20, 40, false, false, '#3B82F6'),
    ('Negotiation',   30, 70, false, false, '#F59E0B'),
    ('Closed Won',    90, 100, true, false, '#22C55E'),
    ('Closed Lost',   99, 0, false, true, '#EF4444')
ON CONFLICT DO NOTHING;

-- ─── Deals / Opportunities ──────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS crm_deals (
    id                  serial PRIMARY KEY,
    tenant_id           uuid,
    account_id          int REFERENCES crm_accounts(id) ON DELETE SET NULL,
    contact_id          int REFERENCES contacts(id) ON DELETE SET NULL,
    title               text NOT NULL,
    description         text,
    value               numeric(14,2),
    currency            char(3) NOT NULL DEFAULT 'GBP',
    stage_id            int REFERENCES crm_deal_stages(id),
    stage               text,                    -- denormalized
    probability         int DEFAULT 50,
    expected_close      date,
    actual_close        timestamptz,
    owner_id            int REFERENCES app_users(id),
    source              text,
    competitor          text,
    loss_reason         text,
    next_step           text,
    tags                text[] DEFAULT '{}',
    metadata            jsonb DEFAULT '{}',
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    is_deleted          boolean DEFAULT false,
    deleted_at          timestamptz
);

CREATE INDEX IF NOT EXISTS idx_crm_deals_tenant ON crm_deals(tenant_id);
CREATE INDEX IF NOT EXISTS idx_crm_deals_account ON crm_deals(account_id);
CREATE INDEX IF NOT EXISTS idx_crm_deals_owner ON crm_deals(owner_id);
CREATE INDEX IF NOT EXISTS idx_crm_deals_stage ON crm_deals(stage_id);
CREATE INDEX IF NOT EXISTS idx_crm_deals_close ON crm_deals(expected_close);

-- Deal stage history (for velocity metrics)
CREATE TABLE IF NOT EXISTS crm_deal_stage_history (
    id              bigserial PRIMARY KEY,
    deal_id         int NOT NULL REFERENCES crm_deals(id) ON DELETE CASCADE,
    from_stage      text,
    to_stage        text NOT NULL,
    changed_by      int REFERENCES app_users(id),
    changed_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_deal_stage_history_deal ON crm_deal_stage_history(deal_id, changed_at);

-- Trigger: log stage changes
CREATE OR REPLACE FUNCTION log_deal_stage_change() RETURNS trigger AS $$
BEGIN
    IF OLD.stage IS DISTINCT FROM NEW.stage THEN
        INSERT INTO crm_deal_stage_history(deal_id, from_stage, to_stage)
        VALUES (NEW.id, OLD.stage, NEW.stage);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_deal_stage_change ON crm_deals;
CREATE TRIGGER trg_deal_stage_change
    AFTER UPDATE OF stage ON crm_deals
    FOR EACH ROW EXECUTE FUNCTION log_deal_stage_change();

-- ─── Leads ──────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS crm_leads (
    id                  serial PRIMARY KEY,
    tenant_id           uuid,
    first_name          text,
    last_name           text,
    email               text,
    phone               text,
    company_name        text,
    title               text,
    source              text,                    -- web, referral, event, cold_call, ad
    status              text NOT NULL DEFAULT 'new',  -- new, contacted, qualified, converted, lost
    score               int NOT NULL DEFAULT 0,
    owner_id            int REFERENCES app_users(id),
    converted_account_id int REFERENCES crm_accounts(id),
    converted_contact_id int REFERENCES contacts(id),
    converted_deal_id   int REFERENCES crm_deals(id),
    converted_at        timestamptz,
    notes               text,
    tags                text[] DEFAULT '{}',
    metadata            jsonb DEFAULT '{}',
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    is_deleted          boolean DEFAULT false,
    deleted_at          timestamptz
);

CREATE INDEX IF NOT EXISTS idx_crm_leads_tenant ON crm_leads(tenant_id);
CREATE INDEX IF NOT EXISTS idx_crm_leads_status ON crm_leads(status);
CREATE INDEX IF NOT EXISTS idx_crm_leads_owner ON crm_leads(owner_id);
CREATE INDEX IF NOT EXISTS idx_crm_leads_email ON crm_leads(email);
CREATE INDEX IF NOT EXISTS idx_crm_leads_score ON crm_leads(score DESC);

-- Lead scoring rules
CREATE TABLE IF NOT EXISTS crm_lead_scoring_rules (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    field           text NOT NULL,       -- lead.title, lead.source, lead.company_name, etc.
    operator        text NOT NULL,       -- equals, contains, startswith, greater_than
    value           text NOT NULL,
    points          int NOT NULL DEFAULT 10,
    is_enabled      boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- ─── Activities (unified timeline across CRM entities) ──────────────────────
CREATE TABLE IF NOT EXISTS crm_activities (
    id                  bigserial PRIMARY KEY,
    tenant_id           uuid,
    entity_type         text NOT NULL,        -- account, contact, deal, lead
    entity_id           int NOT NULL,
    activity_type       text NOT NULL,        -- call, email, meeting, note, task
    subject             text,
    body                text,
    direction           text,                 -- inbound, outbound
    duration_minutes    int,
    occurred_at         timestamptz NOT NULL DEFAULT now(),
    due_at              timestamptz,          -- for follow-ups/tasks
    is_completed        boolean DEFAULT true, -- false for pending follow-ups
    logged_by           int REFERENCES app_users(id),
    related_task_id     int,                  -- link to tasks module
    related_sd_request_id int,                -- link to Service Desk
    attachments         jsonb DEFAULT '[]',
    metadata            jsonb DEFAULT '{}',
    created_at          timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_crm_activities_tenant ON crm_activities(tenant_id);
CREATE INDEX IF NOT EXISTS idx_crm_activities_entity ON crm_activities(entity_type, entity_id, occurred_at DESC);
CREATE INDEX IF NOT EXISTS idx_crm_activities_user ON crm_activities(logged_by);
CREATE INDEX IF NOT EXISTS idx_crm_activities_type ON crm_activities(activity_type);
CREATE INDEX IF NOT EXISTS idx_crm_activities_due ON crm_activities(due_at) WHERE is_completed = false;

-- pg_notify triggers
CREATE OR REPLACE FUNCTION notify_crm_change() RETURNS trigger AS $$
BEGIN PERFORM pg_notify('data_changed', json_build_object('table',TG_TABLE_NAME,'op',TG_OP,'id',COALESCE(NEW.id,OLD.id))::text); RETURN COALESCE(NEW,OLD); END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_crm_accounts_notify ON crm_accounts;
CREATE TRIGGER trg_crm_accounts_notify AFTER INSERT OR UPDATE OR DELETE ON crm_accounts FOR EACH ROW EXECUTE FUNCTION notify_crm_change();
DROP TRIGGER IF EXISTS trg_crm_deals_notify ON crm_deals;
CREATE TRIGGER trg_crm_deals_notify AFTER INSERT OR UPDATE OR DELETE ON crm_deals FOR EACH ROW EXECUTE FUNCTION notify_crm_change();
DROP TRIGGER IF EXISTS trg_crm_leads_notify ON crm_leads;
CREATE TRIGGER trg_crm_leads_notify AFTER INSERT OR UPDATE OR DELETE ON crm_leads FOR EACH ROW EXECUTE FUNCTION notify_crm_change();
