-- General-purpose application log for errors, warnings, info, and audit events
CREATE TABLE IF NOT EXISTS app_log (
    id          SERIAL PRIMARY KEY,
    timestamp   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    level       TEXT NOT NULL DEFAULT 'Error',    -- Error, Warning, Info, Audit
    tag         TEXT NOT NULL DEFAULT '',          -- e.g. DevExpress, Grid, ASN, Device, SSH, DB
    source      TEXT NOT NULL DEFAULT '',          -- class/method that raised it
    message     TEXT NOT NULL DEFAULT '',
    detail      TEXT NOT NULL DEFAULT '',          -- stack trace or extra context
    username    TEXT NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS idx_app_log_level ON app_log (level);
CREATE INDEX IF NOT EXISTS idx_app_log_tag   ON app_log (tag);
CREATE INDEX IF NOT EXISTS idx_app_log_ts    ON app_log (timestamp DESC);
