-- =============================================================================
-- Stage 5.5-5.6: Validation Rules + Workflow Rules
-- Integrates with existing Elsa workflow engine (Central.Workflows)
-- =============================================================================

-- ─── Validation Rules ───────────────────────────────────────────────────────
-- Pre-save field-level constraints. Evaluated by the API before INSERT/UPDATE.
-- Uses JSONLogic expressions for portable rule definition.

CREATE TABLE IF NOT EXISTS validation_rules (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    entity_type     text NOT NULL,                    -- deals, accounts, contacts, etc.
    name            text NOT NULL,
    description     text,
    rule_expr       jsonb NOT NULL,                   -- JSONLogic: {"<": [{"var":"value"}, 10000]}
    error_message   text NOT NULL,                    -- Shown to user on fail
    error_field     text,                              -- Field to highlight
    severity        text NOT NULL DEFAULT 'error',   -- error, warning
    is_active       boolean NOT NULL DEFAULT true,
    applies_on      text[] NOT NULL DEFAULT '{insert,update}', -- insert, update, delete
    priority        int NOT NULL DEFAULT 100,
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_validation_rules_entity ON validation_rules(entity_type) WHERE is_active = true;

-- ─── Workflow Rules ─────────────────────────────────────────────────────────
-- Field-change triggers that invoke Elsa workflows.
-- On record INSERT/UPDATE, we check matching rules and start the referenced Elsa workflow.

CREATE TABLE IF NOT EXISTS workflow_rules (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    entity_type     text NOT NULL,
    name            text NOT NULL,
    description     text,
    trigger_type    text NOT NULL,                    -- on_insert, on_update, on_delete, on_field_change, on_schedule
    trigger_fields  text[] DEFAULT '{}',               -- for on_field_change: which fields trigger
    condition_expr  jsonb,                             -- JSONLogic over NEW + OLD record
    -- Action: invoke an Elsa workflow or inline action
    action_type     text NOT NULL DEFAULT 'elsa_workflow', -- elsa_workflow, set_field, send_email, create_task, webhook
    elsa_workflow_definition_id text,                  -- references Elsa workflow
    inline_action   jsonb,                             -- for set_field/send_email/create_task inline actions
    is_active       boolean NOT NULL DEFAULT true,
    execution_order int NOT NULL DEFAULT 100,
    run_as_user_id  int REFERENCES app_users(id),      -- NULL = system
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_workflow_rules_entity ON workflow_rules(entity_type, trigger_type) WHERE is_active = true;

-- Execution log (for both validation + workflow rules)
CREATE TABLE IF NOT EXISTS rule_execution_log (
    id              bigserial PRIMARY KEY,
    rule_type       text NOT NULL,                    -- validation, workflow
    rule_id         int NOT NULL,
    entity_type     text NOT NULL,
    entity_id       int,
    triggered_at    timestamptz NOT NULL DEFAULT now(),
    result          text NOT NULL,                    -- pass, fail, workflow_started, error
    message         text,
    elsa_workflow_instance_id text,
    duration_ms     int
);

CREATE INDEX IF NOT EXISTS idx_rule_exec_log_rule ON rule_execution_log(rule_type, rule_id, triggered_at DESC);
CREATE INDEX IF NOT EXISTS idx_rule_exec_log_entity ON rule_execution_log(entity_type, entity_id);

-- ─── Universal record-change trigger (broadcasts to pg_notify for Elsa) ─────
-- A single trigger that fires on any tenant-scoped table — the Rust workflow
-- dispatcher (or a .NET background service) listens on 'record_changed' and
-- evaluates workflow_rules for that entity.

CREATE OR REPLACE FUNCTION broadcast_record_change() RETURNS trigger AS $$
BEGIN
    PERFORM pg_notify('record_changed', json_build_object(
        'table', TG_TABLE_NAME,
        'op', TG_OP,
        'id', CASE WHEN TG_OP = 'DELETE' THEN (to_jsonb(OLD)->>'id') ELSE (to_jsonb(NEW)->>'id') END,
        'new', CASE WHEN TG_OP = 'DELETE' THEN NULL ELSE to_jsonb(NEW) END,
        'old', CASE WHEN TG_OP = 'INSERT' THEN NULL ELSE to_jsonb(OLD) END,
        'timestamp', NOW()
    )::text);
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

-- Attach to key CRM tables (opt-in pattern — add more via DO blocks as needed)
DO $$
DECLARE
    tbl text;
BEGIN
    FOR tbl IN SELECT unnest(ARRAY[
        'crm_accounts', 'crm_deals', 'crm_leads', 'crm_activities',
        'crm_contracts', 'crm_subscriptions', 'crm_orders'
    ]) LOOP
        EXECUTE format('DROP TRIGGER IF EXISTS trg_broadcast_change ON %I', tbl);
        EXECUTE format('CREATE TRIGGER trg_broadcast_change
                        AFTER INSERT OR UPDATE OR DELETE ON %I
                        FOR EACH ROW EXECUTE FUNCTION broadcast_record_change()', tbl);
    END LOOP;
END $$;
