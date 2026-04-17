-- =============================================================================
-- Stage 1.2-1.4: Segments + Email Sequences + Landing Pages + Forms
-- =============================================================================

-- ─── Segments (static + dynamic) ────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS crm_segments (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    description     text,
    segment_type    text NOT NULL DEFAULT 'static',  -- static, dynamic
    member_type     text NOT NULL DEFAULT 'contact', -- contact, lead, account
    rule_expression jsonb,                            -- JSONLogic rule for dynamic segments
    cached_count    int DEFAULT 0,
    last_evaluated_at timestamptz,
    is_active       boolean NOT NULL DEFAULT true,
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_segments_tenant ON crm_segments(tenant_id);

CREATE TABLE IF NOT EXISTS crm_segment_members (
    id              serial PRIMARY KEY,
    segment_id      int NOT NULL REFERENCES crm_segments(id) ON DELETE CASCADE,
    member_id       int NOT NULL,
    member_type     text NOT NULL,
    added_at        timestamptz NOT NULL DEFAULT now(),
    UNIQUE(segment_id, member_type, member_id)
);

CREATE INDEX IF NOT EXISTS idx_segment_members_segment ON crm_segment_members(segment_id);
CREATE INDEX IF NOT EXISTS idx_segment_members_lookup ON crm_segment_members(member_type, member_id);

-- ─── Email Sequences (drip campaigns / nurture) ─────────────────────────────
CREATE TABLE IF NOT EXISTS crm_email_sequences (
    id                  serial PRIMARY KEY,
    tenant_id           uuid,
    name                text NOT NULL,
    description         text,
    trigger_event       text NOT NULL DEFAULT 'manual', -- manual, lead_created, deal_stage, form_submit, segment_added
    trigger_config      jsonb DEFAULT '{}',              -- trigger-specific config (stage name, segment id, etc.)
    is_active           boolean NOT NULL DEFAULT false,
    stop_on_reply       boolean NOT NULL DEFAULT true,
    stop_on_unsubscribe boolean NOT NULL DEFAULT true,
    stop_on_meeting     boolean NOT NULL DEFAULT true,
    total_enrollments   int NOT NULL DEFAULT 0,
    total_completions   int NOT NULL DEFAULT 0,
    total_replies       int NOT NULL DEFAULT 0,
    created_by          int REFERENCES app_users(id),
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_sequences_tenant ON crm_email_sequences(tenant_id);
CREATE INDEX IF NOT EXISTS idx_sequences_active ON crm_email_sequences(is_active) WHERE is_active = true;

-- Steps within a sequence (ordered)
CREATE TABLE IF NOT EXISTS crm_sequence_steps (
    id                  serial PRIMARY KEY,
    sequence_id         int NOT NULL REFERENCES crm_email_sequences(id) ON DELETE CASCADE,
    step_order          int NOT NULL,
    step_type           text NOT NULL DEFAULT 'email', -- email, wait, condition, task, webhook
    template_id         int REFERENCES email_templates(id) ON DELETE SET NULL,
    wait_days           int,                            -- for wait steps
    wait_hours          int,
    condition_expr      jsonb,                          -- for condition branching
    subject_override    text,
    body_override       text,
    send_time_of_day    time,                           -- e.g., always send at 09:00 local
    skip_weekends       boolean DEFAULT true,
    UNIQUE(sequence_id, step_order)
);

CREATE INDEX IF NOT EXISTS idx_seq_steps_sequence ON crm_sequence_steps(sequence_id, step_order);

-- Enrollment per contact/lead
CREATE TABLE IF NOT EXISTS crm_sequence_enrollments (
    id                  serial PRIMARY KEY,
    sequence_id         int NOT NULL REFERENCES crm_email_sequences(id) ON DELETE CASCADE,
    member_type         text NOT NULL,                 -- contact, lead
    member_id           int NOT NULL,
    current_step_order  int NOT NULL DEFAULT 0,
    status              text NOT NULL DEFAULT 'active',-- active, paused, completed, stopped_reply, stopped_unsubscribe, stopped_meeting, failed
    enrolled_at         timestamptz NOT NULL DEFAULT now(),
    next_action_at      timestamptz,
    last_action_at      timestamptz,
    completed_at        timestamptz,
    stopped_reason      text,
    enrolled_by         int REFERENCES app_users(id),
    UNIQUE(sequence_id, member_type, member_id)
);

CREATE INDEX IF NOT EXISTS idx_enrollments_sequence ON crm_sequence_enrollments(sequence_id);
CREATE INDEX IF NOT EXISTS idx_enrollments_member ON crm_sequence_enrollments(member_type, member_id);
CREATE INDEX IF NOT EXISTS idx_enrollments_next_action ON crm_sequence_enrollments(next_action_at) WHERE status = 'active';

-- ─── Landing Pages + Forms ──────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS crm_landing_pages (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    slug            text NOT NULL,                    -- /lp/<slug>
    title           text NOT NULL,
    content_html    text NOT NULL,
    campaign_id     int REFERENCES crm_campaigns(id) ON DELETE SET NULL,
    is_published    boolean NOT NULL DEFAULT false,
    view_count      int NOT NULL DEFAULT 0,
    submission_count int NOT NULL DEFAULT 0,
    redirect_on_submit text,
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, slug)
);

CREATE INDEX IF NOT EXISTS idx_landing_pages_slug ON crm_landing_pages(slug);

-- Generic forms (can be used inside landing pages or embedded)
CREATE TABLE IF NOT EXISTS crm_forms (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    slug            text NOT NULL,
    fields          jsonb NOT NULL DEFAULT '[]',      -- [{name, label, type, required, options}]
    landing_page_id int REFERENCES crm_landing_pages(id) ON DELETE SET NULL,
    campaign_id     int REFERENCES crm_campaigns(id) ON DELETE SET NULL,
    on_submit_action text NOT NULL DEFAULT 'create_lead', -- create_lead, create_contact, update_contact, enroll_sequence
    on_submit_config jsonb DEFAULT '{}',
    is_active       boolean NOT NULL DEFAULT true,
    submission_count int NOT NULL DEFAULT 0,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, slug)
);

-- Form submissions
CREATE TABLE IF NOT EXISTS crm_form_submissions (
    id              bigserial PRIMARY KEY,
    form_id         int NOT NULL REFERENCES crm_forms(id) ON DELETE CASCADE,
    tenant_id       uuid,
    payload         jsonb NOT NULL,
    ip_address      inet,
    user_agent      text,
    referrer        text,
    utm_source      text,
    utm_medium      text,
    utm_campaign    text,
    utm_term        text,
    utm_content     text,
    created_lead_id int REFERENCES crm_leads(id) ON DELETE SET NULL,
    created_contact_id int REFERENCES contacts(id) ON DELETE SET NULL,
    submitted_at    timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_form_subs_form ON crm_form_submissions(form_id, submitted_at DESC);
CREATE INDEX IF NOT EXISTS idx_form_subs_utm ON crm_form_submissions(utm_campaign) WHERE utm_campaign IS NOT NULL;

-- Pg_notify for real-time UI
CREATE OR REPLACE FUNCTION notify_segments_change() RETURNS trigger AS $$
BEGIN PERFORM pg_notify('data_changed', json_build_object('table','crm_segments','op',TG_OP,'id',COALESCE(NEW.id,OLD.id))::text); RETURN COALESCE(NEW,OLD); END;
$$ LANGUAGE plpgsql;
DROP TRIGGER IF EXISTS trg_segments_notify ON crm_segments;
CREATE TRIGGER trg_segments_notify AFTER INSERT OR UPDATE OR DELETE ON crm_segments FOR EACH ROW EXECUTE FUNCTION notify_segments_change();
