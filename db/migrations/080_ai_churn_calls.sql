-- =============================================================================
-- Stage 4: Churn Prediction + LTV + Call Transcription + Activity Auto-Capture
-- =============================================================================

-- ─── Churn risk records (one per account) ──────────────────────────────────
CREATE TABLE IF NOT EXISTS crm_churn_risks (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    account_id      int NOT NULL REFERENCES crm_accounts(id) ON DELETE CASCADE UNIQUE,
    model_id        int REFERENCES ai_ml_models(id),
    risk_score      numeric(5,4) NOT NULL,               -- 0..1 probability of churn in next 90 days
    risk_tier       text NOT NULL,                        -- low, medium, high, critical
    signals         jsonb,                                 -- which features contributed
    contributing_factors text[],                           -- human-readable list
    recommended_actions text[],                            -- suggested interventions
    -- Snapshot of key signals at scoring time
    last_activity_days_ago int,
    open_ticket_count int,
    contract_renews_in_days int,
    mrr_at_risk     numeric(14,2),
    scored_at       timestamptz NOT NULL DEFAULT now(),
    -- Outcome tracking
    action_taken    text,
    action_taken_at timestamptz,
    actual_outcome  text,                                  -- churned, retained, expanded
    outcome_at      timestamptz
);

CREATE INDEX IF NOT EXISTS idx_churn_account ON crm_churn_risks(account_id);
CREATE INDEX IF NOT EXISTS idx_churn_tier ON crm_churn_risks(risk_tier) WHERE actual_outcome IS NULL;

-- ─── LTV (Lifetime Value) per account ──────────────────────────────────────
CREATE TABLE IF NOT EXISTS crm_account_ltv (
    id              serial PRIMARY KEY,
    account_id      int NOT NULL REFERENCES crm_accounts(id) ON DELETE CASCADE UNIQUE,
    tenant_id       uuid,
    -- Historical
    historical_revenue numeric(14,2) NOT NULL DEFAULT 0,
    first_deal_at   timestamptz,
    most_recent_deal_at timestamptz,
    total_deals     int NOT NULL DEFAULT 0,
    -- Current
    active_mrr      numeric(14,2) NOT NULL DEFAULT 0,
    active_arr      numeric(14,2) NOT NULL DEFAULT 0,
    -- Predictive
    projected_ltv   numeric(14,2),                        -- ML-predicted total LTV
    projected_remaining_months int,                        -- expected months before churn
    projection_confidence numeric(5,4),
    model_id        int REFERENCES ai_ml_models(id),
    calculated_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_ltv_account ON crm_account_ltv(account_id);

-- ─── Call recordings + transcripts ─────────────────────────────────────────
CREATE TABLE IF NOT EXISTS crm_call_recordings (
    id              bigserial PRIMARY KEY,
    tenant_id       uuid,
    activity_id     bigint REFERENCES crm_activities(id) ON DELETE SET NULL,
    -- Meeting / call metadata
    external_id     text,                                  -- Zoom meeting ID, Gong ID, etc.
    provider        text,                                  -- zoom, teams, gong, chorus, fireflies, dialpad, aircall
    recording_url   text,
    duration_seconds int,
    started_at      timestamptz,
    ended_at        timestamptz,
    -- Participants
    host_user_id    int,
    participants    jsonb,                                 -- [{name, email, role}]
    linked_contact_id int REFERENCES contacts(id) ON DELETE SET NULL,
    linked_deal_id  int REFERENCES crm_deals(id) ON DELETE SET NULL,
    linked_account_id int REFERENCES crm_accounts(id) ON DELETE SET NULL,
    -- Transcript
    transcript_status text NOT NULL DEFAULT 'pending',   -- pending, processing, ready, failed
    transcript_language text DEFAULT 'en',
    transcript_text text,
    transcript_json jsonb,                                 -- turn-by-turn with timestamps + speaker
    -- AI analysis
    summary         text,
    action_items    text[],
    topics_discussed text[],
    -- Sentiment (-1.0 to 1.0, -1 = very negative)
    overall_sentiment numeric(3,2),
    sentiment_by_speaker jsonb,                            -- {john: 0.7, jane: -0.2}
    sentiment_trend  jsonb,                                -- timeline buckets
    -- Engagement metrics
    talk_ratio      jsonb,                                 -- {rep: 0.4, prospect: 0.6}
    longest_monologue_seconds int,
    question_count  int,
    filler_word_count int,
    -- Processing
    processed_at    timestamptz,
    processing_cost_usd numeric(10,4),
    processing_error text,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_calls_activity ON crm_call_recordings(activity_id);
CREATE INDEX IF NOT EXISTS idx_calls_deal ON crm_call_recordings(linked_deal_id);
CREATE INDEX IF NOT EXISTS idx_calls_account ON crm_call_recordings(linked_account_id);
CREATE INDEX IF NOT EXISTS idx_calls_status ON crm_call_recordings(transcript_status);

-- ─── Activity auto-capture (inbound email/calendar parsing) ────────────────
CREATE TABLE IF NOT EXISTS crm_auto_capture_rules (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    source_type     text NOT NULL,                        -- email, calendar
    match_strategy  text NOT NULL DEFAULT 'domain',      -- domain (email domain → account), contact (sender → contact), rule (JSONLogic)
    rule_config     jsonb,
    auto_create_activity boolean NOT NULL DEFAULT true,
    auto_link_to_deal boolean NOT NULL DEFAULT true,     -- if sender associated with an open deal, link it
    auto_update_last_activity boolean NOT NULL DEFAULT true,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Auto-captured items awaiting linking/review
CREATE TABLE IF NOT EXISTS crm_auto_capture_queue (
    id              bigserial PRIMARY KEY,
    tenant_id       uuid,
    source_type     text NOT NULL,                        -- email_message, calendar_event
    source_id       bigint,                                -- email_messages.id, or external ID
    -- Detection
    detected_contact_id int REFERENCES contacts(id),
    detected_account_id int REFERENCES crm_accounts(id),
    detected_deal_id    int REFERENCES crm_deals(id),
    confidence      numeric(5,4),
    -- Outcome
    status          text NOT NULL DEFAULT 'pending',     -- pending, linked, ignored, failed
    created_activity_id bigint REFERENCES crm_activities(id),
    processed_at    timestamptz,
    error_message   text,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_auto_capture_status ON crm_auto_capture_queue(status);

-- Schedule daily churn + LTV scoring jobs
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'job_schedules') THEN
        INSERT INTO job_schedules (job_type, name, schedule_cron, is_enabled)
        VALUES
            ('ai_score_churn', 'Score All Accounts for Churn Risk', '0 5 * * *', true),
            ('ai_calculate_ltv', 'Calculate Account LTV',            '0 6 * * *', true),
            ('ai_auto_capture', 'Process Activity Auto-Capture',     '*/5 * * * *', true)
        ON CONFLICT DO NOTHING;
    END IF;
END $$;

-- ─── Webhook event types for AI/ML ─────────────────────────────────────────
INSERT INTO webhook_event_types (event_type, category, description) VALUES
    ('ai.lead.scored',          'ai', 'Lead score updated by ML model'),
    ('ai.deal.win_probability', 'ai', 'Deal win probability updated'),
    ('ai.churn.detected',       'ai', 'Account flagged as high churn risk'),
    ('ai.duplicate.detected',   'ai', 'Potential duplicate record detected'),
    ('ai.nba.generated',        'ai', 'Next-best-action recommendation created'),
    ('ai.call.transcribed',     'ai', 'Call recording transcription complete'),
    ('ai.enrichment.completed', 'ai', 'Data enrichment job finished'),
    ('ai.assistant.conversation', 'ai', 'AI assistant conversation started')
ON CONFLICT (event_type) DO NOTHING;

INSERT INTO schema_versions (version_number, description)
VALUES ('080_ai_churn_calls', 'Churn + LTV + call transcription + activity auto-capture')
ON CONFLICT (version_number) DO NOTHING;
