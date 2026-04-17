-- =============================================================================
-- Phases 24-25: CRM Dashboards + Reports + Forecasting
-- =============================================================================

-- ─── Saved CRM reports (extends saved_reports if present) ───────────────────
CREATE TABLE IF NOT EXISTS crm_saved_reports (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    description     text,
    report_type     text NOT NULL,           -- pipeline, sales_rep_perf, lead_source_roi, account_revenue, activity, forecast
    filters         jsonb DEFAULT '{}',
    columns         text[] DEFAULT '{}',
    group_by        text,
    sort_by         text,
    schedule_cron   text,                    -- optional scheduled delivery
    email_to        text[],                  -- scheduled report recipients
    export_format   text DEFAULT 'pdf',      -- pdf, excel, csv
    is_public       boolean NOT NULL DEFAULT false,
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_crm_reports_type ON crm_saved_reports(report_type);
CREATE INDEX IF NOT EXISTS idx_crm_reports_tenant ON crm_saved_reports(tenant_id);

-- ─── Forecast snapshots (weekly/monthly rollup of pipeline) ─────────────────
CREATE TABLE IF NOT EXISTS crm_forecast_snapshots (
    id                  serial PRIMARY KEY,
    tenant_id           uuid,
    snapshot_date       date NOT NULL DEFAULT CURRENT_DATE,
    period_start        date NOT NULL,
    period_end          date NOT NULL,
    owner_id            int REFERENCES app_users(id),   -- NULL = team-wide forecast
    committed_value     numeric(14,2) NOT NULL DEFAULT 0,  -- 90%+ probability
    best_case_value     numeric(14,2) NOT NULL DEFAULT 0,  -- 50%+ probability
    worst_case_value    numeric(14,2) NOT NULL DEFAULT 0,  -- 10%+ probability
    weighted_value      numeric(14,2) NOT NULL DEFAULT 0,  -- SUM(value * probability)
    closed_won          numeric(14,2) NOT NULL DEFAULT 0,
    closed_lost         numeric(14,2) NOT NULL DEFAULT 0,
    deal_count          int NOT NULL DEFAULT 0,
    created_at          timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_forecast_snapshots_period ON crm_forecast_snapshots(period_start, period_end);
CREATE INDEX IF NOT EXISTS idx_forecast_snapshots_owner ON crm_forecast_snapshots(owner_id);

-- Function to generate a forecast snapshot
CREATE OR REPLACE FUNCTION generate_crm_forecast(
    p_period_start date,
    p_period_end   date,
    p_owner_id     int DEFAULT NULL
) RETURNS int AS $$
DECLARE
    new_id int;
BEGIN
    INSERT INTO crm_forecast_snapshots
        (period_start, period_end, owner_id, committed_value, best_case_value,
         worst_case_value, weighted_value, closed_won, closed_lost, deal_count)
    SELECT p_period_start, p_period_end, p_owner_id,
        COALESCE(SUM(value) FILTER (WHERE probability >= 90 AND actual_close IS NULL), 0),
        COALESCE(SUM(value) FILTER (WHERE probability >= 50 AND actual_close IS NULL), 0),
        COALESCE(SUM(value) FILTER (WHERE probability >= 10 AND actual_close IS NULL), 0),
        COALESCE(SUM(value * probability / 100.0) FILTER (WHERE actual_close IS NULL), 0),
        COALESCE(SUM(value) FILTER (WHERE stage = 'Closed Won'), 0),
        COALESCE(SUM(value) FILTER (WHERE stage = 'Closed Lost'), 0),
        COUNT(*)
    FROM crm_deals
    WHERE is_deleted IS NOT TRUE
      AND expected_close BETWEEN p_period_start AND p_period_end
      AND (p_owner_id IS NULL OR owner_id = p_owner_id)
    RETURNING id INTO new_id;
    RETURN new_id;
END;
$$ LANGUAGE plpgsql;

-- ─── Dashboard materialized views (fast read) ───────────────────────────────

-- Revenue dashboard rollup (refreshed hourly by background job)
CREATE MATERIALIZED VIEW IF NOT EXISTS crm_revenue_dashboard AS
SELECT
    COALESCE(tenant_id, '00000000-0000-0000-0000-000000000001'::uuid) AS tenant_id,
    date_trunc('month', COALESCE(actual_close, expected_close, created_at)) AS month,
    currency,
    COUNT(*) FILTER (WHERE stage = 'Closed Won') AS deals_won,
    COUNT(*) FILTER (WHERE stage = 'Closed Lost') AS deals_lost,
    COUNT(*) FILTER (WHERE actual_close IS NULL) AS deals_open,
    COALESCE(SUM(value) FILTER (WHERE stage = 'Closed Won'), 0) AS revenue_won,
    COALESCE(SUM(value) FILTER (WHERE stage = 'Closed Lost'), 0) AS revenue_lost,
    COALESCE(SUM(value * probability / 100.0) FILTER (WHERE actual_close IS NULL), 0) AS pipeline_weighted,
    COALESCE(AVG(value) FILTER (WHERE stage = 'Closed Won'), 0) AS avg_deal_size,
    COALESCE(AVG(EXTRACT(EPOCH FROM (actual_close - created_at)) / 86400.0)
             FILTER (WHERE stage = 'Closed Won'), 0) AS avg_cycle_days
FROM crm_deals
WHERE is_deleted IS NOT TRUE
GROUP BY tenant_id, date_trunc('month', COALESCE(actual_close, expected_close, created_at)), currency;

CREATE UNIQUE INDEX IF NOT EXISTS idx_revenue_dash_tenant_month_cur
    ON crm_revenue_dashboard(tenant_id, month, currency);

-- Activity dashboard (calls/emails/meetings per rep per week)
CREATE MATERIALIZED VIEW IF NOT EXISTS crm_activity_dashboard AS
SELECT
    COALESCE(tenant_id, '00000000-0000-0000-0000-000000000001'::uuid) AS tenant_id,
    logged_by AS user_id,
    date_trunc('week', occurred_at) AS week,
    activity_type,
    COUNT(*) AS activity_count,
    COALESCE(SUM(duration_minutes), 0) AS total_minutes
FROM crm_activities
GROUP BY tenant_id, logged_by, date_trunc('week', occurred_at), activity_type;

CREATE UNIQUE INDEX IF NOT EXISTS idx_activity_dash_unique
    ON crm_activity_dashboard(tenant_id, user_id, week, activity_type);

-- Lead source ROI
CREATE MATERIALIZED VIEW IF NOT EXISTS crm_lead_source_roi AS
SELECT
    COALESCE(l.tenant_id, '00000000-0000-0000-0000-000000000001'::uuid) AS tenant_id,
    COALESCE(l.source, '(unknown)') AS source,
    COUNT(l.*) AS total_leads,
    COUNT(l.*) FILTER (WHERE l.status = 'converted') AS converted_leads,
    ROUND(COUNT(l.*) FILTER (WHERE l.status = 'converted')::numeric / NULLIF(COUNT(l.*), 0) * 100, 2) AS conversion_rate_pct,
    COALESCE(SUM(d.value) FILTER (WHERE d.stage = 'Closed Won'), 0) AS revenue_generated,
    COALESCE(AVG(EXTRACT(EPOCH FROM (l.converted_at - l.created_at)) / 86400.0)
             FILTER (WHERE l.converted_at IS NOT NULL), 0) AS avg_days_to_convert
FROM crm_leads l
LEFT JOIN crm_deals d ON d.id = l.converted_deal_id
WHERE l.is_deleted IS NOT TRUE
GROUP BY COALESCE(l.tenant_id, '00000000-0000-0000-0000-000000000001'::uuid), l.source;

CREATE UNIQUE INDEX IF NOT EXISTS idx_lead_source_roi_unique
    ON crm_lead_source_roi(tenant_id, source);

-- Account health (last activity, pipeline, risk)
CREATE MATERIALIZED VIEW IF NOT EXISTS crm_account_health AS
SELECT
    a.id AS account_id,
    a.tenant_id,
    a.name,
    a.account_type,
    a.rating,
    a.account_owner_id,
    a.last_activity_at,
    CASE
        WHEN a.last_activity_at IS NULL THEN 'no_contact'
        WHEN a.last_activity_at > NOW() - INTERVAL '30 days' THEN 'active'
        WHEN a.last_activity_at > NOW() - INTERVAL '90 days' THEN 'at_risk'
        ELSE 'churning'
    END AS health_status,
    COUNT(d.*) FILTER (WHERE d.is_deleted IS NOT TRUE AND d.actual_close IS NULL) AS open_deals,
    COALESCE(SUM(d.value) FILTER (WHERE d.is_deleted IS NOT TRUE AND d.actual_close IS NULL), 0) AS open_pipeline,
    COALESCE(SUM(d.value) FILTER (WHERE d.stage = 'Closed Won'), 0) AS lifetime_revenue
FROM crm_accounts a
LEFT JOIN crm_deals d ON d.account_id = a.id
WHERE a.is_deleted IS NOT TRUE
GROUP BY a.id, a.tenant_id, a.name, a.account_type, a.rating, a.account_owner_id, a.last_activity_at;

CREATE UNIQUE INDEX IF NOT EXISTS idx_account_health_id ON crm_account_health(account_id);
CREATE INDEX IF NOT EXISTS idx_account_health_status ON crm_account_health(health_status);

-- Refresh helper: call this from a background job every hour
CREATE OR REPLACE FUNCTION refresh_crm_dashboards() RETURNS void AS $$
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY crm_revenue_dashboard;
    REFRESH MATERIALIZED VIEW CONCURRENTLY crm_activity_dashboard;
    REFRESH MATERIALIZED VIEW CONCURRENTLY crm_lead_source_roi;
    REFRESH MATERIALIZED VIEW CONCURRENTLY crm_account_health;
END;
$$ LANGUAGE plpgsql;

-- Schedule hourly refresh via job_schedules (if the table exists)
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'job_schedules') THEN
        INSERT INTO job_schedules (job_type, name, interval_seconds, is_enabled)
        VALUES ('crm_dashboard_refresh', 'Refresh CRM Dashboards', 3600, true)
        ON CONFLICT DO NOTHING;
    END IF;
END $$;
