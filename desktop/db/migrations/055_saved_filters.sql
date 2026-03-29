-- Migration 055: Saved filters table (was referenced but never created)
-- Used by GridCustomizerHelper for per-user per-panel filter presets.

CREATE TABLE IF NOT EXISTS saved_filters (
    id              serial PRIMARY KEY,
    user_id         integer REFERENCES app_users(id) ON DELETE CASCADE,
    panel_name      varchar(128) NOT NULL,
    filter_name     varchar(128) NOT NULL,
    filter_expr     text NOT NULL DEFAULT '',
    is_default      boolean DEFAULT false,
    sort_order      integer DEFAULT 0,
    created_at      timestamptz DEFAULT now(),
    updated_at      timestamptz DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_saved_filters_user ON saved_filters(user_id, panel_name);

-- pg_notify trigger (was referenced in 048 but table didn't exist)
DROP TRIGGER IF EXISTS trg_notify_saved_filters ON saved_filters;
CREATE TRIGGER trg_notify_saved_filters AFTER INSERT OR UPDATE OR DELETE ON saved_filters
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();
