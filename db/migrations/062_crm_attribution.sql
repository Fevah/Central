-- =============================================================================
-- Stage 1.5-1.9: UTM tracking + multi-touch attribution + campaign influence
-- =============================================================================

-- UTM events — every trackable visitor touch
CREATE TABLE IF NOT EXISTS crm_utm_events (
    id              bigserial PRIMARY KEY,
    tenant_id       uuid,
    visitor_id      text,                        -- browser fingerprint/cookie
    session_id      text,
    member_type     text,                        -- contact, lead (resolved later)
    member_id       int,
    event_type      text NOT NULL,               -- page_view, form_submit, click, conversion
    page_url        text,
    referrer        text,
    utm_source      text,
    utm_medium      text,
    utm_campaign    text,
    utm_term        text,
    utm_content     text,
    campaign_id     int REFERENCES crm_campaigns(id) ON DELETE SET NULL,
    ip_address      inet,
    user_agent      text,
    occurred_at     timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_utm_events_visitor ON crm_utm_events(visitor_id, occurred_at);
CREATE INDEX IF NOT EXISTS idx_utm_events_member ON crm_utm_events(member_type, member_id, occurred_at);
CREATE INDEX IF NOT EXISTS idx_utm_events_campaign ON crm_utm_events(campaign_id, occurred_at);
CREATE INDEX IF NOT EXISTS idx_utm_events_utm ON crm_utm_events(utm_source, utm_medium, utm_campaign);

-- Attribution touches — linked to an opportunity with a weight per model
CREATE TABLE IF NOT EXISTS crm_attribution_touches (
    id              bigserial PRIMARY KEY,
    deal_id         int NOT NULL REFERENCES crm_deals(id) ON DELETE CASCADE,
    campaign_id     int REFERENCES crm_campaigns(id) ON DELETE SET NULL,
    utm_event_id    bigint REFERENCES crm_utm_events(id) ON DELETE SET NULL,
    touch_type      text NOT NULL,               -- first_touch, last_touch, middle_touch, source_touch
    touched_at      timestamptz NOT NULL,
    first_touch_weight numeric(5,4) DEFAULT 0,   -- 1.0 if first, 0 otherwise
    last_touch_weight  numeric(5,4) DEFAULT 0,
    linear_weight      numeric(5,4) DEFAULT 0,   -- 1/N per touch
    position_weight    numeric(5,4) DEFAULT 0,   -- 40% first, 40% last, 20% middle
    time_decay_weight  numeric(5,4) DEFAULT 0    -- exponential decay toward close
);

CREATE INDEX IF NOT EXISTS idx_attr_touches_deal ON crm_attribution_touches(deal_id);
CREATE INDEX IF NOT EXISTS idx_attr_touches_campaign ON crm_attribution_touches(campaign_id);

-- Helper: generate attribution touches for a deal from its UTM event history
CREATE OR REPLACE FUNCTION crm_generate_attribution(p_deal_id int) RETURNS int AS $$
DECLARE
    touch_count int;
    deal_contact_id int;
    deal_account_id int;
BEGIN
    SELECT contact_id, account_id INTO deal_contact_id, deal_account_id FROM crm_deals WHERE id = p_deal_id;

    -- Clear existing touches
    DELETE FROM crm_attribution_touches WHERE deal_id = p_deal_id;

    -- Find all UTM events from the contact or any account_contact
    WITH touches AS (
        SELECT e.id, e.campaign_id, e.occurred_at
        FROM crm_utm_events e
        WHERE (e.member_type = 'contact' AND e.member_id = deal_contact_id)
           OR e.member_id IN (SELECT contact_id FROM crm_account_contacts WHERE account_id = deal_account_id)
        ORDER BY e.occurred_at
    ),
    numbered AS (
        SELECT id, campaign_id, occurred_at,
               ROW_NUMBER() OVER (ORDER BY occurred_at) AS touch_num,
               COUNT(*) OVER () AS total_touches,
               ROW_NUMBER() OVER (ORDER BY occurred_at DESC) AS rev_touch_num
        FROM touches
    )
    INSERT INTO crm_attribution_touches
        (deal_id, campaign_id, utm_event_id, touch_type, touched_at,
         first_touch_weight, last_touch_weight, linear_weight, position_weight, time_decay_weight)
    SELECT p_deal_id, campaign_id, id,
        CASE WHEN touch_num = 1 THEN 'first_touch'
             WHEN rev_touch_num = 1 THEN 'last_touch'
             ELSE 'middle_touch' END,
        occurred_at,
        CASE WHEN touch_num = 1 THEN 1.0 ELSE 0.0 END,
        CASE WHEN rev_touch_num = 1 THEN 1.0 ELSE 0.0 END,
        1.0 / total_touches,
        CASE
            WHEN total_touches = 1 THEN 1.0
            WHEN touch_num = 1 THEN 0.40
            WHEN rev_touch_num = 1 THEN 0.40
            ELSE 0.20 / NULLIF(total_touches - 2, 0)
        END,
        -- Time-decay: half-life of 7 days from close
        POWER(0.5, EXTRACT(EPOCH FROM (now() - occurred_at)) / (86400 * 7))
    FROM numbered;

    GET DIAGNOSTICS touch_count = ROW_COUNT;
    RETURN touch_count;
END;
$$ LANGUAGE plpgsql;

-- Campaign influence materialized view — revenue per campaign per attribution model
CREATE MATERIALIZED VIEW IF NOT EXISTS crm_campaign_influence AS
SELECT
    c.id AS campaign_id,
    c.name AS campaign_name,
    c.tenant_id,
    c.budget,
    c.actual_cost,
    COUNT(DISTINCT t.deal_id) AS influenced_deals,
    COUNT(DISTINCT t.deal_id) FILTER (WHERE d.stage = 'Closed Won') AS won_deals,
    COALESCE(SUM(d.value * t.first_touch_weight) FILTER (WHERE d.stage = 'Closed Won'), 0) AS revenue_first_touch,
    COALESCE(SUM(d.value * t.last_touch_weight)  FILTER (WHERE d.stage = 'Closed Won'), 0) AS revenue_last_touch,
    COALESCE(SUM(d.value * t.linear_weight)      FILTER (WHERE d.stage = 'Closed Won'), 0) AS revenue_linear,
    COALESCE(SUM(d.value * t.position_weight)    FILTER (WHERE d.stage = 'Closed Won'), 0) AS revenue_position,
    COALESCE(SUM(d.value * t.time_decay_weight)  FILTER (WHERE d.stage = 'Closed Won'), 0) AS revenue_time_decay,
    CASE WHEN c.actual_cost > 0 THEN
        COALESCE(SUM(d.value * t.linear_weight) FILTER (WHERE d.stage = 'Closed Won'), 0) / c.actual_cost
    ELSE NULL END AS roi_linear
FROM crm_campaigns c
LEFT JOIN crm_attribution_touches t ON t.campaign_id = c.id
LEFT JOIN crm_deals d ON d.id = t.deal_id AND d.is_deleted IS NOT TRUE
WHERE c.is_deleted IS NOT TRUE
GROUP BY c.id, c.name, c.tenant_id, c.budget, c.actual_cost;

CREATE UNIQUE INDEX IF NOT EXISTS idx_campaign_influence_id ON crm_campaign_influence(campaign_id);

-- Refresh function
CREATE OR REPLACE FUNCTION refresh_crm_attribution() RETURNS void AS $$
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY crm_campaign_influence;
END;
$$ LANGUAGE plpgsql;

-- Schedule hourly refresh
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'job_schedules') THEN
        INSERT INTO job_schedules (job_type, name, interval_seconds, is_enabled)
        VALUES ('crm_attribution_refresh', 'Refresh CRM Attribution', 3600, true)
        ON CONFLICT DO NOTHING;
    END IF;
END $$;
