-- 063_workflows.sql — Elsa Workflows integration tracking
-- Elsa manages its own tables via EF Core migrations.
-- These tables track Central-specific workflow metadata and approvals.

-- ============================================================
-- 1. Workflow assignments (which workflows apply to which entities)
-- ============================================================

CREATE TABLE IF NOT EXISTS workflow_assignments (
    id              serial PRIMARY KEY,
    workflow_definition_id varchar(128) NOT NULL,
    entity_type     varchar(64) NOT NULL,       -- 'task', 'device', 'sprint', 'project'
    entity_scope    varchar(128),               -- project name, task_type, or NULL for global
    is_active       boolean DEFAULT true,
    created_at      timestamptz DEFAULT now(),
    updated_at      timestamptz DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_workflow_assign_entity ON workflow_assignments (entity_type, entity_scope);

-- ============================================================
-- 2. Approval requests (for ApprovalActivity bookmarks)
-- ============================================================

CREATE TABLE IF NOT EXISTS workflow_approvals (
    id              serial PRIMARY KEY,
    workflow_instance_id varchar(128) NOT NULL,
    bookmark_id     varchar(128),
    approver_id     integer REFERENCES app_users(id) ON DELETE SET NULL,
    entity_type     varchar(64),
    entity_id       integer,
    description     text,
    status          varchar(32) DEFAULT 'Pending',  -- Pending | Approved | Rejected
    approved_at     timestamptz,
    approver_comment text,
    created_at      timestamptz DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_workflow_approvals_approver ON workflow_approvals (approver_id, status);
CREATE INDEX IF NOT EXISTS idx_workflow_approvals_instance ON workflow_approvals (workflow_instance_id);

-- ============================================================
-- 3. Workflow execution log (Central-side, supplements Elsa's logs)
-- ============================================================

CREATE TABLE IF NOT EXISTS workflow_execution_log (
    id              serial PRIMARY KEY,
    workflow_definition_id varchar(128),
    workflow_instance_id varchar(128),
    entity_type     varchar(64),
    entity_id       integer,
    action          varchar(128),
    status          varchar(32),        -- Started | Completed | Failed | Suspended
    details         jsonb,
    triggered_by    integer REFERENCES app_users(id) ON DELETE SET NULL,
    created_at      timestamptz DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_wf_exec_log_entity ON workflow_execution_log (entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_wf_exec_log_instance ON workflow_execution_log (workflow_instance_id);

-- ============================================================
-- 4. pg_notify triggers
-- ============================================================

DROP TRIGGER IF EXISTS trg_notify_workflow_approvals ON workflow_approvals;
CREATE TRIGGER trg_notify_workflow_approvals AFTER INSERT OR UPDATE OR DELETE ON workflow_approvals
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

DROP TRIGGER IF EXISTS trg_notify_workflow_assignments ON workflow_assignments;
CREATE TRIGGER trg_notify_workflow_assignments AFTER INSERT OR UPDATE OR DELETE ON workflow_assignments
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();
