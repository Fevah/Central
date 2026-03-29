-- Migration 040: Active Directory integration config
-- AD config stored in integrations table (reuse existing pattern)
INSERT INTO integrations (name, display_name, integration_type, base_url, is_enabled, config_json)
VALUES ('activedirectory', 'Active Directory', 'ldap', '', false,
    '{"domain":"","ou_filter":"","service_account":"","use_ssl":false}')
ON CONFLICT DO NOTHING;

-- Ensure admin:ad permission exists
INSERT INTO permissions (code, name, category, description) VALUES
    ('admin:ad', 'AD Integration', 'admin', 'Browse and import Active Directory users')
ON CONFLICT DO NOTHING;
