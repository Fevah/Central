-- =============================================================================
-- Stage 1.1: Marketing Campaigns
-- =============================================================================

CREATE TABLE IF NOT EXISTS crm_campaigns (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    description     text,
    campaign_type   text NOT NULL DEFAULT 'email',   -- email, event, webinar, ad, direct_mail, referral, content
    status          text NOT NULL DEFAULT 'planning',-- planning, active, completed, cancelled
    owner_id        int REFERENCES app_users(id),
    parent_id       int REFERENCES crm_campaigns(id), -- hierarchical campaigns
    start_date      date,
    end_date        date,
    budget          numeric(14,2),
    actual_cost     numeric(14,2) DEFAULT 0,
    expected_revenue numeric(14,2),
    expected_responses int,
    is_active       boolean NOT NULL DEFAULT true,
    source_code     text,                              -- for UTM tracking: utm_campaign value
    tags            text[] DEFAULT '{}',
    metadata        jsonb DEFAULT '{}',
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    is_deleted      boolean DEFAULT false
);

CREATE INDEX IF NOT EXISTS idx_campaigns_tenant ON crm_campaigns(tenant_id);
CREATE INDEX IF NOT EXISTS idx_campaigns_status ON crm_campaigns(status);
CREATE INDEX IF NOT EXISTS idx_campaigns_dates ON crm_campaigns(start_date, end_date);
CREATE INDEX IF NOT EXISTS idx_campaigns_source ON crm_campaigns(source_code);

-- Campaign members — who's targeted by a campaign (lead/contact/account)
CREATE TABLE IF NOT EXISTS crm_campaign_members (
    id              serial PRIMARY KEY,
    campaign_id     int NOT NULL REFERENCES crm_campaigns(id) ON DELETE CASCADE,
    member_type     text NOT NULL,                     -- lead, contact, account
    member_id       int NOT NULL,
    status          text NOT NULL DEFAULT 'sent',     -- sent, responded, converted, bounced, unsubscribed
    responded_at    timestamptz,
    response_type   text,                              -- opened, clicked, replied, registered
    added_at        timestamptz NOT NULL DEFAULT now(),
    UNIQUE(campaign_id, member_type, member_id)
);

CREATE INDEX IF NOT EXISTS idx_campaign_members_campaign ON crm_campaign_members(campaign_id);
CREATE INDEX IF NOT EXISTS idx_campaign_members_member ON crm_campaign_members(member_type, member_id);

-- Link deals to campaigns (primary campaign per deal)
ALTER TABLE crm_deals ADD COLUMN IF NOT EXISTS campaign_id int REFERENCES crm_campaigns(id) ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS idx_deals_campaign ON crm_deals(campaign_id);

-- Link leads to campaigns (primary source campaign)
ALTER TABLE crm_leads ADD COLUMN IF NOT EXISTS campaign_id int REFERENCES crm_campaigns(id) ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS idx_leads_campaign ON crm_leads(campaign_id);

-- Campaign costs (granular cost tracking)
CREATE TABLE IF NOT EXISTS crm_campaign_costs (
    id              serial PRIMARY KEY,
    campaign_id     int NOT NULL REFERENCES crm_campaigns(id) ON DELETE CASCADE,
    cost_category   text NOT NULL,                    -- media, production, labor, venue, software
    amount          numeric(14,2) NOT NULL,
    currency        char(3) NOT NULL DEFAULT 'GBP',
    occurred_at     date NOT NULL DEFAULT CURRENT_DATE,
    description     text,
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Auto-update actual_cost
CREATE OR REPLACE FUNCTION recalc_campaign_cost() RETURNS trigger AS $$
BEGIN
    UPDATE crm_campaigns SET actual_cost = (
        SELECT COALESCE(SUM(amount), 0) FROM crm_campaign_costs
        WHERE campaign_id = COALESCE(NEW.campaign_id, OLD.campaign_id)
    ) WHERE id = COALESCE(NEW.campaign_id, OLD.campaign_id);
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_campaign_costs ON crm_campaign_costs;
CREATE TRIGGER trg_campaign_costs
    AFTER INSERT OR UPDATE OR DELETE ON crm_campaign_costs
    FOR EACH ROW EXECUTE FUNCTION recalc_campaign_cost();

-- pg_notify
CREATE OR REPLACE FUNCTION notify_campaigns_change() RETURNS trigger AS $$
BEGIN PERFORM pg_notify('data_changed', json_build_object('table','crm_campaigns','op',TG_OP,'id',COALESCE(NEW.id,OLD.id))::text); RETURN COALESCE(NEW,OLD); END;
$$ LANGUAGE plpgsql;
DROP TRIGGER IF EXISTS trg_campaigns_notify ON crm_campaigns;
CREATE TRIGGER trg_campaigns_notify AFTER INSERT OR UPDATE OR DELETE ON crm_campaigns FOR EACH ROW EXECUTE FUNCTION notify_campaigns_change();
