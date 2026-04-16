-- Global Admin audit trail — tracks all platform-level operations
-- Used by GlobalAdminAuditPanel in the WPF desktop app

CREATE TABLE IF NOT EXISTS central_platform.global_admin_audit_log (
    id              serial PRIMARY KEY,
    actor_user_id   uuid,
    actor_email     varchar(255) NOT NULL DEFAULT '',
    action          varchar(64) NOT NULL,
    entity_type     varchar(64),
    entity_id       varchar(128),
    details         jsonb DEFAULT '{}',
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_ga_audit_action ON central_platform.global_admin_audit_log (action);
CREATE INDEX IF NOT EXISTS idx_ga_audit_entity ON central_platform.global_admin_audit_log (entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_ga_audit_actor ON central_platform.global_admin_audit_log (actor_user_id);
CREATE INDEX IF NOT EXISTS idx_ga_audit_created ON central_platform.global_admin_audit_log (created_at DESC);
