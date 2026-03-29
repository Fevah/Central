-- 064_baselines.sql — Phase 4: Gantt baseline schedules for comparison

CREATE TABLE IF NOT EXISTS task_baselines (
    id              serial PRIMARY KEY,
    task_id         integer NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    baseline_name   varchar(64) NOT NULL,
    start_date      date,
    finish_date     date,
    points          numeric(6,1),
    hours           numeric(8,1),
    saved_at        timestamptz DEFAULT now(),
    UNIQUE(task_id, baseline_name)
);

CREATE INDEX IF NOT EXISTS idx_task_baselines_task ON task_baselines (task_id);
CREATE INDEX IF NOT EXISTS idx_task_baselines_name ON task_baselines (baseline_name);

DROP TRIGGER IF EXISTS trg_notify_task_baselines ON task_baselines;
CREATE TRIGGER trg_notify_task_baselines AFTER INSERT OR UPDATE OR DELETE ON task_baselines
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

-- Helper: save a baseline snapshot for all tasks in a project
CREATE OR REPLACE FUNCTION save_project_baseline(p_project_id integer, p_baseline_name varchar)
RETURNS integer AS $$
DECLARE
    cnt integer;
BEGIN
    INSERT INTO task_baselines (task_id, baseline_name, start_date, finish_date, points, hours)
    SELECT id, p_baseline_name, start_date, finish_date, points, estimated_hours
    FROM tasks WHERE project_id = p_project_id
    ON CONFLICT (task_id, baseline_name) DO UPDATE SET
        start_date = EXCLUDED.start_date,
        finish_date = EXCLUDED.finish_date,
        points = EXCLUDED.points,
        hours = EXCLUDED.hours,
        saved_at = now();
    GET DIAGNOSTICS cnt = ROW_COUNT;
    RETURN cnt;
END;
$$ LANGUAGE plpgsql;
