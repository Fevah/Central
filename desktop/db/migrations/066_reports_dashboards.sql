-- 066_reports_dashboards.sql — Phase 8: Reporting & Dashboards

CREATE TABLE IF NOT EXISTS saved_reports (
    id              serial PRIMARY KEY,
    project_id      integer REFERENCES task_projects(id) ON DELETE SET NULL,
    name            varchar(128) NOT NULL,
    folder          varchar(128),
    query_json      jsonb NOT NULL,     -- columns, filters, sort, group
    created_by      integer REFERENCES app_users(id) ON DELETE SET NULL,
    shared_with     jsonb,              -- [{"type":"user","id":1}, {"type":"group","name":"Dev"}]
    created_at      timestamptz DEFAULT now(),
    updated_at      timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS dashboards (
    id              serial PRIMARY KEY,
    name            varchar(128) NOT NULL,
    layout_json     jsonb NOT NULL,     -- tile positions, sizes, chart configs
    template        varchar(64),
    created_by      integer REFERENCES app_users(id) ON DELETE SET NULL,
    shared_with     jsonb,
    created_at      timestamptz DEFAULT now(),
    updated_at      timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS dashboard_snapshots (
    id              serial PRIMARY KEY,
    dashboard_id    integer NOT NULL REFERENCES dashboards(id) ON DELETE CASCADE,
    snapshot_date   date NOT NULL,
    data_json       jsonb NOT NULL,
    UNIQUE(dashboard_id, snapshot_date)
);

CREATE INDEX IF NOT EXISTS idx_saved_reports_project ON saved_reports (project_id);
CREATE INDEX IF NOT EXISTS idx_saved_reports_user ON saved_reports (created_by);
CREATE INDEX IF NOT EXISTS idx_dashboards_user ON dashboards (created_by);
CREATE INDEX IF NOT EXISTS idx_dashboard_snapshots_dash ON dashboard_snapshots (dashboard_id, snapshot_date);

DROP TRIGGER IF EXISTS trg_notify_saved_reports ON saved_reports;
CREATE TRIGGER trg_notify_saved_reports AFTER INSERT OR UPDATE OR DELETE ON saved_reports
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

DROP TRIGGER IF EXISTS trg_notify_dashboards ON dashboards;
CREATE TRIGGER trg_notify_dashboards AFTER INSERT OR UPDATE OR DELETE ON dashboards
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();
