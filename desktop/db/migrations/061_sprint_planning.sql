-- 061_sprint_planning.sql — Phase 2: Backlog & Sprint Planning
-- Adds: sprint_allocations (per-user capacity), sprint_burndown (daily snapshots)

-- ============================================================
-- 1. Sprint capacity per user
-- ============================================================

CREATE TABLE IF NOT EXISTS sprint_allocations (
    id              serial PRIMARY KEY,
    sprint_id       integer NOT NULL REFERENCES sprints(id) ON DELETE CASCADE,
    user_id         integer NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    capacity_hours  numeric(6,1),
    capacity_points numeric(6,1),
    UNIQUE(sprint_id, user_id)
);

CREATE INDEX IF NOT EXISTS idx_sprint_alloc_sprint ON sprint_allocations (sprint_id);

-- ============================================================
-- 2. Burndown daily snapshots
-- ============================================================

CREATE TABLE IF NOT EXISTS sprint_burndown (
    id                serial PRIMARY KEY,
    sprint_id         integer NOT NULL REFERENCES sprints(id) ON DELETE CASCADE,
    snapshot_date     date NOT NULL,
    points_remaining  numeric(8,1) DEFAULT 0,
    hours_remaining   numeric(8,1) DEFAULT 0,
    points_completed  numeric(8,1) DEFAULT 0,
    hours_completed   numeric(8,1) DEFAULT 0,
    UNIQUE(sprint_id, snapshot_date)
);

CREATE INDEX IF NOT EXISTS idx_burndown_sprint ON sprint_burndown (sprint_id, snapshot_date);

-- ============================================================
-- 3. pg_notify triggers
-- ============================================================

DROP TRIGGER IF EXISTS trg_notify_sprint_allocations ON sprint_allocations;
CREATE TRIGGER trg_notify_sprint_allocations AFTER INSERT OR UPDATE OR DELETE ON sprint_allocations
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

DROP TRIGGER IF EXISTS trg_notify_sprint_burndown ON sprint_burndown;
CREATE TRIGGER trg_notify_sprint_burndown AFTER INSERT OR UPDATE OR DELETE ON sprint_burndown
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

-- ============================================================
-- 4. Helper function: snapshot burndown for a sprint
-- ============================================================

CREATE OR REPLACE FUNCTION snapshot_sprint_burndown(p_sprint_id integer)
RETURNS void AS $$
BEGIN
    INSERT INTO sprint_burndown (sprint_id, snapshot_date, points_remaining, hours_remaining, points_completed, hours_completed)
    SELECT
        p_sprint_id,
        CURRENT_DATE,
        COALESCE(SUM(CASE WHEN status != 'Done' THEN points ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN status != 'Done' THEN work_remaining ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN status = 'Done' THEN points ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN status = 'Done' THEN COALESCE(estimated_hours, 0) ELSE 0 END), 0)
    FROM tasks
    WHERE committed_to = p_sprint_id OR sprint_id = p_sprint_id
    ON CONFLICT (sprint_id, snapshot_date) DO UPDATE SET
        points_remaining = EXCLUDED.points_remaining,
        hours_remaining = EXCLUDED.hours_remaining,
        points_completed = EXCLUDED.points_completed,
        hours_completed = EXCLUDED.hours_completed;
END;
$$ LANGUAGE plpgsql;
