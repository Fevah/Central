-- Migration 044: Reference number system
CREATE TABLE IF NOT EXISTS reference_config (
    id              serial PRIMARY KEY,
    entity_type     varchar(64) NOT NULL UNIQUE,
    prefix          varchar(16) NOT NULL DEFAULT '',
    suffix          varchar(16) NOT NULL DEFAULT '',
    pad_length      integer NOT NULL DEFAULT 6,
    next_value      bigint NOT NULL DEFAULT 1,
    description     varchar(256) DEFAULT ''
);

-- Seed default configs
INSERT INTO reference_config (entity_type, prefix, pad_length, description) VALUES
    ('device', 'DEV-', 6, 'Device/IPAM asset reference'),
    ('ticket', 'TKT-', 6, 'Service desk ticket reference'),
    ('asset',  'AST-', 6, 'Physical asset reference'),
    ('task',   'TSK-', 6, 'Task reference')
ON CONFLICT DO NOTHING;

-- Function to get next reference number atomically
CREATE OR REPLACE FUNCTION next_reference(p_entity_type varchar)
RETURNS text AS $$
DECLARE
    cfg reference_config;
    result text;
BEGIN
    SELECT * INTO cfg FROM reference_config WHERE entity_type = p_entity_type FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'No reference config for type: %', p_entity_type; END IF;
    result := cfg.prefix || lpad(cfg.next_value::text, cfg.pad_length, '0') || cfg.suffix;
    UPDATE reference_config SET next_value = next_value + 1 WHERE id = cfg.id;
    RETURN result;
END;
$$ LANGUAGE plpgsql;

-- Add permissions
INSERT INTO permissions (code, name, category, description) VALUES
    ('admin:references', 'Reference Numbers', 'admin', 'Configure reference number sequences'),
    ('admin:containers', 'Container Management', 'admin', 'Manage Podman containers')
ON CONFLICT DO NOTHING;
