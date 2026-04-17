-- =============================================================================
-- Stage 4: ML Model Registry + Lead/Opportunity Scoring
-- =============================================================================

-- Model registry (platform-shared and tenant-trained models)
CREATE TABLE IF NOT EXISTS ai_ml_models (
    id                  serial PRIMARY KEY,
    tenant_id           uuid,                              -- NULL = platform-shared model
    model_name          text NOT NULL,
    model_kind          text NOT NULL,                     -- lead_scoring, opp_scoring, churn, ltv, segment, next_action, duplicate_match
    framework           text NOT NULL DEFAULT 'logreg',   -- logreg, gbm, rf, xgboost, pytorch, sklearn, rules
    feature_spec        jsonb NOT NULL DEFAULT '{}',      -- {features: [{name, type, source}]}
    target_label        text,                              -- what we're predicting
    -- Lifecycle
    status              text NOT NULL DEFAULT 'draft',   -- draft, training, active, deprecated, failed
    version             int NOT NULL DEFAULT 1,
    parent_version_id   int REFERENCES ai_ml_models(id),
    is_champion         boolean NOT NULL DEFAULT false,   -- currently serving predictions
    -- Performance metrics
    train_samples       int,
    train_auc           numeric(5,4),
    train_accuracy      numeric(5,4),
    train_precision     numeric(5,4),
    train_recall        numeric(5,4),
    train_f1            numeric(5,4),
    trained_at          timestamptz,
    -- Artifact storage (could be MinIO URL, HuggingFace model ID, local path)
    artifact_uri        text,
    artifact_format     text,                              -- pickle, onnx, gguf, pt, safetensors
    artifact_size_bytes bigint,
    -- Audit
    created_by          int,
    created_at          timestamptz NOT NULL DEFAULT now(),
    notes               text
);

CREATE INDEX IF NOT EXISTS idx_ml_models_tenant ON ai_ml_models(tenant_id);
CREATE INDEX IF NOT EXISTS idx_ml_models_kind ON ai_ml_models(model_kind);
CREATE INDEX IF NOT EXISTS idx_ml_models_champion ON ai_ml_models(model_kind, tenant_id) WHERE is_champion = true;

-- Score history — every prediction is logged for audit + model improvement
CREATE TABLE IF NOT EXISTS ai_model_scores (
    id              bigserial PRIMARY KEY,
    tenant_id       uuid,
    model_id        int NOT NULL REFERENCES ai_ml_models(id) ON DELETE CASCADE,
    entity_type     text NOT NULL,                        -- lead, deal, account, contact
    entity_id       int NOT NULL,
    score           numeric(8,4) NOT NULL,               -- 0..100 probability*100 for classifiers
    confidence      numeric(5,4),                         -- 0..1
    features_used   jsonb,                                 -- snapshot of feature values
    explanation     text,                                  -- SHAP-like explanation
    scored_at       timestamptz NOT NULL DEFAULT now(),
    -- Ground truth (populated later for training feedback)
    actual_outcome  text,                                  -- 'converted', 'won', 'churned', 'no_action'
    actual_value    numeric(14,2),
    labelled_at     timestamptz
);

CREATE INDEX IF NOT EXISTS idx_scores_entity ON ai_model_scores(entity_type, entity_id, scored_at DESC);
CREATE INDEX IF NOT EXISTS idx_scores_model ON ai_model_scores(model_id, scored_at DESC);
CREATE INDEX IF NOT EXISTS idx_scores_unlabelled ON ai_model_scores(model_id) WHERE actual_outcome IS NULL;

-- ─── Enhanced scores on existing entities (ML-augmented) ────────────────────
ALTER TABLE crm_leads ADD COLUMN IF NOT EXISTS ml_score numeric(5,2);
ALTER TABLE crm_leads ADD COLUMN IF NOT EXISTS ml_score_updated_at timestamptz;
ALTER TABLE crm_leads ADD COLUMN IF NOT EXISTS ml_score_model_id int REFERENCES ai_ml_models(id);

ALTER TABLE crm_deals ADD COLUMN IF NOT EXISTS ml_win_probability numeric(5,2);
ALTER TABLE crm_deals ADD COLUMN IF NOT EXISTS ml_score_updated_at timestamptz;
ALTER TABLE crm_deals ADD COLUMN IF NOT EXISTS ml_score_model_id int REFERENCES ai_ml_models(id);
ALTER TABLE crm_deals ADD COLUMN IF NOT EXISTS ml_stalled_risk numeric(5,2);

ALTER TABLE crm_accounts ADD COLUMN IF NOT EXISTS ml_churn_risk numeric(5,2);
ALTER TABLE crm_accounts ADD COLUMN IF NOT EXISTS ml_ltv_estimate numeric(14,2);
ALTER TABLE crm_accounts ADD COLUMN IF NOT EXISTS ml_score_updated_at timestamptz;

-- ─── Next-best-action recommendations ──────────────────────────────────────
CREATE TABLE IF NOT EXISTS ai_next_best_actions (
    id              bigserial PRIMARY KEY,
    tenant_id       uuid,
    entity_type     text NOT NULL,                        -- account, deal, contact, lead
    entity_id       int NOT NULL,
    action_code     text NOT NULL,                        -- send_email, schedule_call, add_product, escalate_to_manager, book_demo
    action_text     text NOT NULL,                        -- human-readable
    rationale       text,                                  -- why the AI recommends this
    expected_value  numeric(14,2),
    confidence      numeric(5,4),
    priority        int NOT NULL DEFAULT 100,
    source_model_id int REFERENCES ai_ml_models(id),
    expires_at      timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now(),
    -- Feedback loop
    accepted_at     timestamptz,
    accepted_by     int,
    dismissed_at    timestamptz,
    dismissed_reason text,
    acted_on_at     timestamptz,
    outcome         text                                   -- positive, negative, no_effect
);

CREATE INDEX IF NOT EXISTS idx_nba_entity ON ai_next_best_actions(entity_type, entity_id) WHERE dismissed_at IS NULL AND acted_on_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_nba_tenant ON ai_next_best_actions(tenant_id);

-- ─── Training job queue ────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ai_training_jobs (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    model_id        int REFERENCES ai_ml_models(id) ON DELETE CASCADE,
    job_type        text NOT NULL DEFAULT 'train',       -- train, retrain, eval, promote
    status          text NOT NULL DEFAULT 'queued',      -- queued, running, completed, failed
    priority        int NOT NULL DEFAULT 100,
    progress_pct    int DEFAULT 0,
    config          jsonb DEFAULT '{}',
    started_at      timestamptz,
    completed_at    timestamptz,
    error_message   text,
    metrics         jsonb,                                 -- final training metrics
    created_by      int,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_training_jobs_status ON ai_training_jobs(status, created_at);

-- Schedule weekly retrain of champion models
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'job_schedules') THEN
        INSERT INTO job_schedules (job_type, name, schedule_cron, is_enabled)
        VALUES
            ('ai_retrain_leads', 'Retrain Lead Scoring Model', '0 2 * * 0', true),      -- Sunday 02:00
            ('ai_retrain_deals', 'Retrain Deal Scoring Model', '0 3 * * 0', true),
            ('ai_retrain_churn', 'Retrain Churn Model',        '0 4 * * 0', true)
        ON CONFLICT DO NOTHING;
    END IF;
END $$;
