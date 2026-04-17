-- =============================================================================
-- Stage 3.7-3.9: Subscription management + revenue recognition (ASC 606)
-- =============================================================================

-- Subscriptions (customer-facing — different from platform tenant_subscriptions)
CREATE TABLE IF NOT EXISTS crm_subscriptions (
    id                  serial PRIMARY KEY,
    tenant_id           uuid,
    account_id          int NOT NULL REFERENCES crm_accounts(id) ON DELETE CASCADE,
    contract_id         int REFERENCES crm_contracts(id) ON DELETE SET NULL,
    product_id          int REFERENCES crm_products(id) ON DELETE SET NULL,
    subscription_number text NOT NULL,
    name                text NOT NULL,
    status              text NOT NULL DEFAULT 'active', -- active, trial, paused, cancelled, expired
    -- Pricing
    quantity            numeric(12,2) NOT NULL DEFAULT 1,
    unit_price          numeric(14,2) NOT NULL,
    mrr                 numeric(14,2) NOT NULL DEFAULT 0,    -- monthly recurring revenue
    arr                 numeric(14,2) NOT NULL DEFAULT 0,    -- annual recurring revenue
    currency            char(3) NOT NULL DEFAULT 'GBP',
    billing_period      text NOT NULL DEFAULT 'monthly',     -- monthly, quarterly, annual
    -- Dates
    start_date          date NOT NULL,
    end_date            date,
    next_billing_date   date,
    trial_end_date      date,
    cancel_at           date,
    cancelled_at        timestamptz,
    -- Lifecycle
    owner_id            int REFERENCES app_users(id),
    auto_renew          boolean NOT NULL DEFAULT true,
    metadata            jsonb DEFAULT '{}',
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, subscription_number)
);

CREATE INDEX IF NOT EXISTS idx_subs_account ON crm_subscriptions(account_id);
CREATE INDEX IF NOT EXISTS idx_subs_contract ON crm_subscriptions(contract_id);
CREATE INDEX IF NOT EXISTS idx_subs_status ON crm_subscriptions(status);
CREATE INDEX IF NOT EXISTS idx_subs_next_billing ON crm_subscriptions(next_billing_date) WHERE status = 'active';

-- Subscription events (upgrades, downgrades, cancellations)
CREATE TABLE IF NOT EXISTS crm_subscription_events (
    id              bigserial PRIMARY KEY,
    subscription_id int NOT NULL REFERENCES crm_subscriptions(id) ON DELETE CASCADE,
    event_type      text NOT NULL,                    -- upgrade, downgrade, quantity_change, renewal, cancel, reactivate, pause
    previous_mrr    numeric(14,2),
    new_mrr         numeric(14,2),
    mrr_delta       numeric(14,2),                    -- positive = expansion, negative = contraction/churn
    previous_quantity numeric(12,2),
    new_quantity    numeric(12,2),
    reason          text,
    actor_id        int REFERENCES app_users(id),
    occurred_at     timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_sub_events_sub ON crm_subscription_events(subscription_id, occurred_at);

-- Auto-compute MRR delta on subscription change
CREATE OR REPLACE FUNCTION log_subscription_change() RETURNS trigger AS $$
DECLARE
    event_type_v text;
BEGIN
    IF TG_OP = 'UPDATE' THEN
        -- MRR changed?
        IF OLD.mrr IS DISTINCT FROM NEW.mrr THEN
            event_type_v := CASE
                WHEN NEW.mrr > OLD.mrr THEN 'upgrade'
                WHEN NEW.mrr < OLD.mrr THEN 'downgrade'
                ELSE 'no_change'
            END;
            INSERT INTO crm_subscription_events
                (subscription_id, event_type, previous_mrr, new_mrr, mrr_delta,
                 previous_quantity, new_quantity)
            VALUES (NEW.id, event_type_v, OLD.mrr, NEW.mrr, NEW.mrr - OLD.mrr,
                    OLD.quantity, NEW.quantity);
        END IF;
        -- Status transitions
        IF OLD.status IS DISTINCT FROM NEW.status THEN
            INSERT INTO crm_subscription_events
                (subscription_id, event_type, previous_mrr, new_mrr)
            VALUES (NEW.id, NEW.status, OLD.mrr, NEW.mrr);
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_subscription_change ON crm_subscriptions;
CREATE TRIGGER trg_subscription_change
    AFTER UPDATE ON crm_subscriptions
    FOR EACH ROW EXECUTE FUNCTION log_subscription_change();

-- MRR/ARR rollup materialized view
CREATE MATERIALIZED VIEW IF NOT EXISTS crm_mrr_dashboard AS
SELECT
    COALESCE(tenant_id, '00000000-0000-0000-0000-000000000001'::uuid) AS tenant_id,
    currency,
    COUNT(*) FILTER (WHERE status = 'active') AS active_subscriptions,
    COALESCE(SUM(mrr) FILTER (WHERE status = 'active'), 0) AS total_mrr,
    COALESCE(SUM(arr) FILTER (WHERE status = 'active'), 0) AS total_arr,
    COUNT(*) FILTER (WHERE status = 'trial') AS trial_count,
    COUNT(*) FILTER (WHERE status = 'cancelled' AND cancelled_at >= NOW() - INTERVAL '30 days') AS churned_last_30d,
    COALESCE(SUM(mrr) FILTER (WHERE status = 'cancelled' AND cancelled_at >= NOW() - INTERVAL '30 days'), 0) AS mrr_churned_last_30d
FROM crm_subscriptions
GROUP BY tenant_id, currency;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mrr_dash_unique ON crm_mrr_dashboard(tenant_id, currency);

-- Revenue recognition schedules (ASC 606 compliant)
-- One row per performance obligation, stores when revenue is recognized
CREATE TABLE IF NOT EXISTS crm_revenue_schedules (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    subscription_id int REFERENCES crm_subscriptions(id) ON DELETE CASCADE,
    contract_id     int REFERENCES crm_contracts(id) ON DELETE CASCADE,
    order_id        int,                               -- forward ref to crm_orders
    product_id      int REFERENCES crm_products(id),
    performance_obligation text NOT NULL,              -- "SaaS License - Year 1", "Professional Services", "Implementation Fee"
    recognition_method text NOT NULL DEFAULT 'ratable', -- ratable, point_in_time, milestone, percentage_of_completion
    total_amount    numeric(14,2) NOT NULL,
    currency        char(3) NOT NULL DEFAULT 'GBP',
    start_date      date NOT NULL,
    end_date        date,                              -- for ratable
    periods         int NOT NULL DEFAULT 1,            -- number of periods (months for ratable)
    status          text NOT NULL DEFAULT 'scheduled', -- scheduled, recognizing, completed, paused
    notes           text,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_rev_schedules_sub ON crm_revenue_schedules(subscription_id);
CREATE INDEX IF NOT EXISTS idx_rev_schedules_contract ON crm_revenue_schedules(contract_id);

-- Recognized revenue entries (one per recognition event)
CREATE TABLE IF NOT EXISTS crm_revenue_entries (
    id              serial PRIMARY KEY,
    schedule_id     int NOT NULL REFERENCES crm_revenue_schedules(id) ON DELETE CASCADE,
    period_start    date NOT NULL,
    period_end      date NOT NULL,
    amount          numeric(14,2) NOT NULL,
    currency        char(3) NOT NULL DEFAULT 'GBP',
    recognized_at   timestamptz NOT NULL DEFAULT now(),
    gl_journal_id   text,                              -- reference to accounting system
    is_reversed     boolean NOT NULL DEFAULT false
);

CREATE INDEX IF NOT EXISTS idx_rev_entries_schedule ON crm_revenue_entries(schedule_id);
CREATE INDEX IF NOT EXISTS idx_rev_entries_period ON crm_revenue_entries(period_start, period_end);

-- Function to generate the ratable entries for a schedule
CREATE OR REPLACE FUNCTION generate_revenue_entries(p_schedule_id int) RETURNS int AS $$
DECLARE
    sched record;
    per_period numeric(14,2);
    i int;
    period_start date;
    period_end date;
    inserted int := 0;
BEGIN
    SELECT * INTO sched FROM crm_revenue_schedules WHERE id = p_schedule_id;
    IF NOT FOUND THEN RETURN 0; END IF;

    IF sched.recognition_method = 'ratable' THEN
        per_period := sched.total_amount / sched.periods;
        FOR i IN 0..sched.periods - 1 LOOP
            period_start := sched.start_date + (i || ' months')::interval;
            period_end := period_start + INTERVAL '1 month' - INTERVAL '1 day';
            INSERT INTO crm_revenue_entries
                (schedule_id, period_start, period_end, amount, currency, recognized_at)
            VALUES (sched.id, period_start, period_end::date, per_period, sched.currency, period_start::timestamptz);
            inserted := inserted + 1;
        END LOOP;
    ELSIF sched.recognition_method = 'point_in_time' THEN
        INSERT INTO crm_revenue_entries
            (schedule_id, period_start, period_end, amount, currency)
        VALUES (sched.id, sched.start_date, sched.start_date, sched.total_amount, sched.currency);
        inserted := 1;
    END IF;

    UPDATE crm_revenue_schedules SET status = 'recognizing' WHERE id = p_schedule_id;
    RETURN inserted;
END;
$$ LANGUAGE plpgsql;
