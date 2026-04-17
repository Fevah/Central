-- =============================================================================
-- Stage 2.4-2.7: Opportunity splits, account teams, account plans, org charts
-- =============================================================================

-- ─── Opportunity splits (multi-rep credit) ──────────────────────────────────
CREATE TABLE IF NOT EXISTS crm_opportunity_splits (
    id              serial PRIMARY KEY,
    deal_id         int NOT NULL REFERENCES crm_deals(id) ON DELETE CASCADE,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    split_type      text NOT NULL DEFAULT 'revenue', -- revenue, overlay, quota_attainment
    credit_pct      numeric(5,2) NOT NULL,            -- 0-100, sum across rows of same split_type = 100
    role            text,                              -- AE, SE, SDR, CSM
    added_by        int REFERENCES app_users(id),
    added_at        timestamptz NOT NULL DEFAULT now(),
    UNIQUE(deal_id, user_id, split_type)
);

CREATE INDEX IF NOT EXISTS idx_opp_splits_deal ON crm_opportunity_splits(deal_id);
CREATE INDEX IF NOT EXISTS idx_opp_splits_user ON crm_opportunity_splits(user_id);

-- Validation: splits of a given type must total 100%
CREATE OR REPLACE FUNCTION validate_opp_splits() RETURNS trigger AS $$
DECLARE
    total numeric;
BEGIN
    SELECT COALESCE(SUM(credit_pct), 0) INTO total
    FROM crm_opportunity_splits
    WHERE deal_id = NEW.deal_id AND split_type = NEW.split_type;
    IF total > 100.01 THEN
        RAISE EXCEPTION 'Opportunity split total %.2f%% exceeds 100%% for deal % / type %', total, NEW.deal_id, NEW.split_type;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_validate_opp_splits ON crm_opportunity_splits;
CREATE TRIGGER trg_validate_opp_splits
    AFTER INSERT OR UPDATE ON crm_opportunity_splits
    FOR EACH ROW EXECUTE FUNCTION validate_opp_splits();

-- ─── Account teams ──────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS crm_account_teams (
    id              serial PRIMARY KEY,
    account_id      int NOT NULL REFERENCES crm_accounts(id) ON DELETE CASCADE,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    team_role       text NOT NULL,                    -- AE, CSM, SE, Exec Sponsor, Champion
    access_level    text NOT NULL DEFAULT 'read',    -- read, edit, admin
    added_at        timestamptz NOT NULL DEFAULT now(),
    UNIQUE(account_id, user_id, team_role)
);

CREATE INDEX IF NOT EXISTS idx_account_teams_account ON crm_account_teams(account_id);
CREATE INDEX IF NOT EXISTS idx_account_teams_user ON crm_account_teams(user_id);

-- ─── Account plans ──────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS crm_account_plans (
    id              serial PRIMARY KEY,
    account_id      int NOT NULL REFERENCES crm_accounts(id) ON DELETE CASCADE UNIQUE,
    fiscal_year     int,
    annual_target   numeric(14,2),
    strategic_goals text,
    known_initiatives text,                           -- what the customer is working on
    known_budget    numeric(14,2),
    known_budget_period text,                         -- FY26, H1-2026, etc.
    whitespace_products text[] DEFAULT '{}',          -- which products the account does NOT have
    last_reviewed_at timestamptz,
    next_review_at  timestamptz,
    owner_id        int REFERENCES app_users(id),
    status          text NOT NULL DEFAULT 'active',   -- active, archived
    updated_at      timestamptz NOT NULL DEFAULT now(),
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Stakeholders within an account plan
CREATE TABLE IF NOT EXISTS crm_account_plan_stakeholders (
    id              serial PRIMARY KEY,
    plan_id         int NOT NULL REFERENCES crm_account_plans(id) ON DELETE CASCADE,
    contact_id      int REFERENCES contacts(id) ON DELETE CASCADE,
    role            text,                              -- Economic Buyer, Champion, Blocker, Decision Maker, User
    influence_level text,                              -- high, medium, low
    sentiment       text,                              -- supporter, neutral, detractor
    notes           text,
    added_at        timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_plan_stakeholders_plan ON crm_account_plan_stakeholders(plan_id);

-- ─── Org chart (relationship map within an account) ─────────────────────────
CREATE TABLE IF NOT EXISTS crm_org_chart_edges (
    id              serial PRIMARY KEY,
    account_id      int NOT NULL REFERENCES crm_accounts(id) ON DELETE CASCADE,
    from_contact_id int NOT NULL REFERENCES contacts(id) ON DELETE CASCADE,
    to_contact_id   int NOT NULL REFERENCES contacts(id) ON DELETE CASCADE,
    relationship    text NOT NULL DEFAULT 'reports_to', -- reports_to, dotted_line, influences, blocks, champions
    notes           text,
    UNIQUE(account_id, from_contact_id, to_contact_id, relationship)
);

CREATE INDEX IF NOT EXISTS idx_org_chart_account ON crm_org_chart_edges(account_id);
CREATE INDEX IF NOT EXISTS idx_org_chart_from ON crm_org_chart_edges(from_contact_id);
