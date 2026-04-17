-- =============================================================================
-- Stage 3.3-3.4: Generic approval engine (reusable across modules)
-- =============================================================================

-- Approval request — one per record that needs approval
CREATE TABLE IF NOT EXISTS approval_requests (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    entity_type     text NOT NULL,                    -- deal, quote, discount, time_off, expense, custom
    entity_id       int NOT NULL,
    requested_by    int NOT NULL REFERENCES app_users(id),
    approval_type   text NOT NULL,                    -- discount, deal_size, contract, refund, custom
    status          text NOT NULL DEFAULT 'pending',  -- pending, approved, rejected, cancelled, expired
    priority        text DEFAULT 'normal',            -- low, normal, high, urgent
    context         jsonb DEFAULT '{}',               -- snapshot of what's being approved
    reason          text,
    requested_at    timestamptz NOT NULL DEFAULT now(),
    resolved_at     timestamptz,
    final_decision_by int REFERENCES app_users(id),
    final_decision_note text,
    expires_at      timestamptz
);

CREATE INDEX IF NOT EXISTS idx_approval_reqs_entity ON approval_requests(entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_approval_reqs_status ON approval_requests(status);
CREATE INDEX IF NOT EXISTS idx_approval_reqs_requested_by ON approval_requests(requested_by);

-- Approval steps — sequential or parallel approvers
CREATE TABLE IF NOT EXISTS approval_steps (
    id              serial PRIMARY KEY,
    request_id      int NOT NULL REFERENCES approval_requests(id) ON DELETE CASCADE,
    step_order      int NOT NULL,                     -- lower = earlier
    step_type       text NOT NULL DEFAULT 'approval', -- approval, notification, escalation
    approver_user_id int REFERENCES app_users(id),
    approver_role   text,                              -- role-based routing
    is_parallel     boolean NOT NULL DEFAULT false,   -- run with other same-order steps concurrently
    requires_all    boolean NOT NULL DEFAULT true,    -- if parallel, require all approvers vs any
    status          text NOT NULL DEFAULT 'waiting', -- waiting, pending, approved, rejected, skipped
    acted_at        timestamptz,
    comment         text,
    auto_approve_timeout_hours int                   -- escalate if no action
);

CREATE INDEX IF NOT EXISTS idx_approval_steps_request ON approval_steps(request_id, step_order);
CREATE INDEX IF NOT EXISTS idx_approval_steps_approver ON approval_steps(approver_user_id) WHERE status = 'pending';

-- Approval actions log (audit trail)
CREATE TABLE IF NOT EXISTS approval_actions (
    id              bigserial PRIMARY KEY,
    request_id      int NOT NULL REFERENCES approval_requests(id) ON DELETE CASCADE,
    step_id         int REFERENCES approval_steps(id),
    action          text NOT NULL,                    -- submitted, approved, rejected, commented, escalated, cancelled
    actor_id        int NOT NULL REFERENCES app_users(id),
    comment         text,
    occurred_at     timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_approval_actions_request ON approval_actions(request_id, occurred_at);

-- Auto-resolve request when all steps complete
CREATE OR REPLACE FUNCTION resolve_approval_request() RETURNS trigger AS $$
DECLARE
    req_id int;
    total_steps int;
    decided_steps int;
    rejected_count int;
BEGIN
    req_id := NEW.request_id;

    SELECT COUNT(*), COUNT(*) FILTER (WHERE status IN ('approved','rejected','skipped')),
           COUNT(*) FILTER (WHERE status = 'rejected')
      INTO total_steps, decided_steps, rejected_count
    FROM approval_steps WHERE request_id = req_id;

    IF rejected_count > 0 THEN
        UPDATE approval_requests SET status = 'rejected', resolved_at = NOW() WHERE id = req_id AND status = 'pending';
    ELSIF decided_steps = total_steps AND total_steps > 0 THEN
        UPDATE approval_requests SET status = 'approved', resolved_at = NOW() WHERE id = req_id AND status = 'pending';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_resolve_approval ON approval_steps;
CREATE TRIGGER trg_resolve_approval
    AFTER UPDATE OF status ON approval_steps
    FOR EACH ROW EXECUTE FUNCTION resolve_approval_request();
