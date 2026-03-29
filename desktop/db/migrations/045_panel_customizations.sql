-- Migration 045: Panel customization framework
CREATE TABLE IF NOT EXISTS panel_customizations (
    id              serial PRIMARY KEY,
    user_id         integer NOT NULL,
    panel_name      varchar(128) NOT NULL,
    setting_type    varchar(32) NOT NULL,
    setting_key     varchar(128) NOT NULL DEFAULT '',
    setting_json    jsonb NOT NULL DEFAULT '{}',
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(user_id, panel_name, setting_type, setting_key)
);

CREATE INDEX IF NOT EXISTS idx_panel_customizations_user ON panel_customizations(user_id, panel_name);
