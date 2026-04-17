-- =============================================================================
-- Phase 29: Cross-module linking + webhook events + polish
-- =============================================================================

-- Webhook event types supported (reference table)
CREATE TABLE IF NOT EXISTS webhook_event_types (
    event_type      text PRIMARY KEY,
    category        text NOT NULL,              -- crm, auth, billing, infra, task, sd
    description     text,
    payload_schema  jsonb                       -- JSON schema for webhook payload
);

INSERT INTO webhook_event_types (event_type, category, description) VALUES
    -- CRM events
    ('crm.account.created',      'crm',     'CRM account created'),
    ('crm.account.updated',      'crm',     'CRM account updated'),
    ('crm.account.deleted',      'crm',     'CRM account deleted'),
    ('crm.contact.created',      'crm',     'Contact created'),
    ('crm.contact.updated',      'crm',     'Contact updated'),
    ('crm.deal.created',         'crm',     'Deal created'),
    ('crm.deal.stage_changed',   'crm',     'Deal moved to a new stage'),
    ('crm.deal.won',             'crm',     'Deal closed as won'),
    ('crm.deal.lost',            'crm',     'Deal closed as lost'),
    ('crm.lead.created',         'crm',     'New lead captured'),
    ('crm.lead.converted',       'crm',     'Lead converted to account/contact/deal'),
    ('crm.lead.scored',          'crm',     'Lead score changed'),
    ('crm.quote.sent',           'crm',     'Quote sent to customer'),
    ('crm.quote.accepted',       'crm',     'Quote accepted'),
    ('crm.activity.created',     'crm',     'New activity logged'),
    ('crm.activity.completed',   'crm',     'Activity marked complete'),
    ('crm.document.signed',      'crm',     'Document signed'),
    -- Auth events
    ('auth.user.created',        'auth',    'User created'),
    ('auth.user.login',          'auth',    'User logged in'),
    ('auth.user.mfa_enabled',    'auth',    'User enabled MFA'),
    -- Billing events
    ('billing.invoice.created',  'billing', 'Invoice created'),
    ('billing.invoice.paid',     'billing', 'Invoice paid'),
    ('billing.subscription.upgraded', 'billing', 'Subscription upgraded'),
    ('billing.subscription.canceled', 'billing', 'Subscription canceled'),
    -- Tenant events
    ('tenant.provisioned',       'tenant',  'Tenant fully provisioned'),
    ('tenant.upgraded.dedicated','tenant',  'Tenant upgraded to dedicated DB'),
    -- Task events
    ('task.created',             'task',    'Task created'),
    ('task.completed',           'task',    'Task completed'),
    ('task.assigned',            'task',    'Task assigned to user')
ON CONFLICT (event_type) DO NOTHING;

-- Webhook subscriptions (outbound — who wants which events)
CREATE TABLE IF NOT EXISTS webhook_subscriptions (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    target_url      text NOT NULL,
    secret_hash     text,                        -- HMAC signing key (hashed)
    event_types     text[] NOT NULL DEFAULT '{}',
    is_active       boolean NOT NULL DEFAULT true,
    retry_count     int NOT NULL DEFAULT 3,
    timeout_seconds int NOT NULL DEFAULT 30,
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    last_delivery_at timestamptz,
    last_delivery_status text,
    failure_count   int NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_webhook_subs_events ON webhook_subscriptions USING gin(event_types);

-- Outbound webhook delivery log (for retry and monitoring)
CREATE TABLE IF NOT EXISTS webhook_deliveries (
    id              bigserial PRIMARY KEY,
    subscription_id int NOT NULL REFERENCES webhook_subscriptions(id) ON DELETE CASCADE,
    event_type      text NOT NULL,
    event_id        text NOT NULL,               -- UUID generated per event
    payload         jsonb NOT NULL,
    status          text NOT NULL DEFAULT 'pending', -- pending, delivered, failed, retrying
    http_status     int,
    response_body   text,
    attempt_count   int NOT NULL DEFAULT 0,
    next_retry_at   timestamptz,
    delivered_at    timestamptz,
    error_message   text,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_webhook_deliveries_sub ON webhook_deliveries(subscription_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_webhook_deliveries_status ON webhook_deliveries(status, next_retry_at);

-- ─── Cross-module linking ─────────────────────────────────────────────────
-- When a CRM deal is won, automatically create an implementation task/project
CREATE OR REPLACE FUNCTION crm_on_deal_won() RETURNS trigger AS $$
BEGIN
    IF NEW.stage = 'Closed Won' AND (OLD.stage IS DISTINCT FROM 'Closed Won') THEN
        -- Log an activity
        INSERT INTO crm_activities (entity_type, entity_id, activity_type, subject, body, occurred_at, is_completed)
        VALUES ('deal', NEW.id, 'note', 'Deal won — $' || COALESCE(NEW.value, 0), NEW.next_step, NOW(), true);

        -- Update account last_activity_at
        IF NEW.account_id IS NOT NULL THEN
            UPDATE crm_accounts SET last_activity_at = NOW() WHERE id = NEW.account_id;
        END IF;

        -- Emit webhook event (payload queued via pg_notify for the webhook dispatcher)
        PERFORM pg_notify('webhook_event', json_build_object(
            'event_type', 'crm.deal.won',
            'event_id', gen_random_uuid(),
            'deal_id', NEW.id,
            'account_id', NEW.account_id,
            'value', NEW.value,
            'currency', NEW.currency
        )::text);
    END IF;

    IF NEW.stage = 'Closed Lost' AND (OLD.stage IS DISTINCT FROM 'Closed Lost') THEN
        PERFORM pg_notify('webhook_event', json_build_object(
            'event_type', 'crm.deal.lost',
            'event_id', gen_random_uuid(),
            'deal_id', NEW.id,
            'loss_reason', NEW.loss_reason
        )::text);
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_crm_deal_won ON crm_deals;
CREATE TRIGGER trg_crm_deal_won
    AFTER UPDATE OF stage ON crm_deals
    FOR EACH ROW EXECUTE FUNCTION crm_on_deal_won();

-- Contact → SD Requester auto-link (by email match)
-- When a contact is created/updated with an email that matches an sd_requester, link them
CREATE OR REPLACE FUNCTION crm_link_contact_to_sd_requester() RETURNS trigger AS $$
BEGIN
    IF NEW.email IS NOT NULL AND NEW.email <> '' THEN
        -- Update sd_requester to reference this contact (if column exists)
        PERFORM 1 FROM information_schema.columns
         WHERE table_name = 'sd_requesters' AND column_name = 'contact_id';
        IF FOUND THEN
            UPDATE sd_requesters SET contact_id = NEW.id
            WHERE LOWER(email) = LOWER(NEW.email) AND (contact_id IS NULL OR contact_id <> NEW.id);
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Add contact_id to sd_requesters for cross-module linking (if table exists)
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'sd_requesters') THEN
        ALTER TABLE sd_requesters ADD COLUMN IF NOT EXISTS contact_id int REFERENCES contacts(id) ON DELETE SET NULL;
        CREATE INDEX IF NOT EXISTS idx_sd_requesters_contact ON sd_requesters(contact_id);
    END IF;
END $$;

DROP TRIGGER IF EXISTS trg_contact_link_sd ON contacts;
CREATE TRIGGER trg_contact_link_sd
    AFTER INSERT OR UPDATE OF email ON contacts
    FOR EACH ROW EXECUTE FUNCTION crm_link_contact_to_sd_requester();

-- CRM Account → Infrastructure link (devices owned by company)
ALTER TABLE switch_guide ADD COLUMN IF NOT EXISTS crm_account_id int REFERENCES crm_accounts(id) ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS idx_switch_guide_crm_account ON switch_guide(crm_account_id);

-- CRM Deal → Task Project link
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'task_projects') THEN
        ALTER TABLE task_projects ADD COLUMN IF NOT EXISTS crm_deal_id int REFERENCES crm_deals(id) ON DELETE SET NULL;
        CREATE INDEX IF NOT EXISTS idx_task_projects_crm_deal ON task_projects(crm_deal_id);
    END IF;
END $$;

-- Central record
INSERT INTO schema_versions (version_number, description)
VALUES ('059_crm_webhooks_polish', 'Webhook event types + subscriptions + cross-module linking (CRM↔SD↔Infra↔Tasks)')
ON CONFLICT (version_number) DO NOTHING;
