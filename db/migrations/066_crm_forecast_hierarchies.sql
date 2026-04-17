-- =============================================================================
-- Stage 2.8-2.10: Forecast hierarchies, pipeline health, deal insights
-- =============================================================================

-- Manager commit/adjust per period (overrides rollup)
CREATE TABLE IF NOT EXISTS crm_forecast_adjustments (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    user_id         int NOT NULL REFERENCES app_users(id),
    period_start    date NOT NULL,
    period_end      date NOT NULL,
    forecast_type   text NOT NULL DEFAULT 'commit',  -- commit, best_case, pipeline
    rep_rollup      numeric(14,2) NOT NULL DEFAULT 0,  -- calculated from reports
    manager_adjustment numeric(14,2) NOT NULL DEFAULT 0,  -- override
    final_forecast  numeric(14,2) NOT NULL DEFAULT 0,
    notes           text,
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(user_id, period_start, period_end, forecast_type)
);

CREATE INDEX IF NOT EXISTS idx_forecast_adj_user ON crm_forecast_adjustments(user_id);

-- Pipeline health (per user per period)
CREATE MATERIALIZED VIEW IF NOT EXISTS crm_pipeline_health AS
SELECT
    d.owner_id,
    COALESCE(u.display_name, '') AS owner_name,
    COUNT(*) AS open_deals,
    COALESCE(SUM(d.value), 0) AS open_pipeline,
    COALESCE(SUM(d.value * d.probability / 100.0), 0) AS weighted_pipeline,
    -- Coverage ratio: open pipeline / current quarter quota
    (SELECT COALESCE(SUM(q.target_amount), 0) FROM crm_quotas q
     WHERE q.user_id = d.owner_id
       AND q.period_start <= CURRENT_DATE AND q.period_end >= CURRENT_DATE) AS current_quota,
    CASE
        WHEN (SELECT COALESCE(SUM(q.target_amount), 0) FROM crm_quotas q
              WHERE q.user_id = d.owner_id
                AND q.period_start <= CURRENT_DATE AND q.period_end >= CURRENT_DATE) > 0
        THEN COALESCE(SUM(d.value), 0) / (SELECT SUM(q.target_amount) FROM crm_quotas q
              WHERE q.user_id = d.owner_id
                AND q.period_start <= CURRENT_DATE AND q.period_end >= CURRENT_DATE)
        ELSE NULL END AS coverage_ratio,
    COUNT(*) FILTER (WHERE d.updated_at < NOW() - INTERVAL '21 days') AS stalled_deals,
    COUNT(*) FILTER (WHERE d.expected_close < CURRENT_DATE) AS overdue_deals
FROM crm_deals d
LEFT JOIN app_users u ON u.id = d.owner_id
WHERE d.is_deleted IS NOT TRUE AND d.actual_close IS NULL
GROUP BY d.owner_id, u.display_name;

CREATE UNIQUE INDEX IF NOT EXISTS idx_pipeline_health_owner ON crm_pipeline_health(owner_id);

-- Deal insights — rule-based nudges
CREATE TABLE IF NOT EXISTS crm_deal_insights (
    id              bigserial PRIMARY KEY,
    deal_id         int NOT NULL REFERENCES crm_deals(id) ON DELETE CASCADE,
    insight_type    text NOT NULL,                    -- stalled, no_activity, probability_mismatch, close_date_slipping, missing_contact, next_step_missing
    severity        text NOT NULL DEFAULT 'warn',    -- info, warn, critical
    message         text NOT NULL,
    detected_at     timestamptz NOT NULL DEFAULT now(),
    acknowledged_at timestamptz,
    acknowledged_by int REFERENCES app_users(id),
    is_resolved     boolean DEFAULT false,
    UNIQUE(deal_id, insight_type, detected_at::date)
);

CREATE INDEX IF NOT EXISTS idx_deal_insights_deal ON crm_deal_insights(deal_id);
CREATE INDEX IF NOT EXISTS idx_deal_insights_active ON crm_deal_insights(deal_id) WHERE is_resolved = false;

-- Rule-based insight generator (runs hourly via background job)
CREATE OR REPLACE FUNCTION generate_deal_insights() RETURNS int AS $$
DECLARE
    inserted int := 0;
BEGIN
    -- Stalled: no update in 21+ days
    INSERT INTO crm_deal_insights (deal_id, insight_type, severity, message)
    SELECT d.id, 'stalled', 'warn',
           'Deal has not been updated in ' || (NOW()::date - d.updated_at::date) || ' days'
    FROM crm_deals d
    WHERE d.is_deleted IS NOT TRUE
      AND d.actual_close IS NULL
      AND d.updated_at < NOW() - INTERVAL '21 days'
      AND NOT EXISTS (SELECT 1 FROM crm_deal_insights i
                      WHERE i.deal_id = d.id AND i.insight_type = 'stalled'
                        AND i.detected_at::date = CURRENT_DATE);

    -- No activity in 14+ days
    INSERT INTO crm_deal_insights (deal_id, insight_type, severity, message)
    SELECT d.id, 'no_activity', 'warn',
           'No activity logged in 14+ days — last: ' || COALESCE(
               (SELECT MAX(occurred_at)::text FROM crm_activities WHERE entity_type = 'deal' AND entity_id = d.id),
               'never')
    FROM crm_deals d
    WHERE d.is_deleted IS NOT TRUE AND d.actual_close IS NULL
      AND NOT EXISTS (SELECT 1 FROM crm_activities a
                      WHERE a.entity_type = 'deal' AND a.entity_id = d.id
                        AND a.occurred_at > NOW() - INTERVAL '14 days')
      AND NOT EXISTS (SELECT 1 FROM crm_deal_insights i
                      WHERE i.deal_id = d.id AND i.insight_type = 'no_activity'
                        AND i.detected_at::date = CURRENT_DATE);

    -- Close date slipping — expected_close in past but still open
    INSERT INTO crm_deal_insights (deal_id, insight_type, severity, message)
    SELECT d.id, 'close_date_slipping', 'critical',
           'Expected close date passed ' || (CURRENT_DATE - d.expected_close) || ' days ago'
    FROM crm_deals d
    WHERE d.is_deleted IS NOT TRUE AND d.actual_close IS NULL
      AND d.expected_close < CURRENT_DATE
      AND NOT EXISTS (SELECT 1 FROM crm_deal_insights i
                      WHERE i.deal_id = d.id AND i.insight_type = 'close_date_slipping'
                        AND i.detected_at::date = CURRENT_DATE);

    -- Missing next step
    INSERT INTO crm_deal_insights (deal_id, insight_type, severity, message)
    SELECT d.id, 'next_step_missing', 'info',
           'No next_step defined'
    FROM crm_deals d
    WHERE d.is_deleted IS NOT TRUE AND d.actual_close IS NULL
      AND (d.next_step IS NULL OR d.next_step = '')
      AND NOT EXISTS (SELECT 1 FROM crm_deal_insights i
                      WHERE i.deal_id = d.id AND i.insight_type = 'next_step_missing'
                        AND i.detected_at::date = CURRENT_DATE);

    GET DIAGNOSTICS inserted = ROW_COUNT;
    RETURN inserted;
END;
$$ LANGUAGE plpgsql;

-- Schedule hourly
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'job_schedules') THEN
        INSERT INTO job_schedules (job_type, name, interval_seconds, is_enabled)
        VALUES ('crm_deal_insights', 'Generate CRM Deal Insights', 3600, true)
        ON CONFLICT DO NOTHING;
    END IF;
END $$;
