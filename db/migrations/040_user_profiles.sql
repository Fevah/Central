-- Phase 5: User profile system
-- Rich profiles with preferences, manager hierarchy, skills.

CREATE TABLE IF NOT EXISTS user_profiles (
    id              serial PRIMARY KEY,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE UNIQUE,
    avatar_url      text,
    bio             text,
    timezone        text NOT NULL DEFAULT 'UTC',
    locale          text NOT NULL DEFAULT 'en-GB',
    date_format     text NOT NULL DEFAULT 'dd/MM/yyyy',
    time_format     text NOT NULL DEFAULT 'HH:mm',
    linkedin_url    text,
    github_url      text,
    phone_ext       text,
    office_location text,
    start_date      date,
    manager_id      int REFERENCES app_users(id) ON DELETE SET NULL,
    skills          text[] DEFAULT '{}',
    certifications  text[] DEFAULT '{}',
    metadata        jsonb DEFAULT '{}',
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_user_profiles_user ON user_profiles(user_id);
CREATE INDEX IF NOT EXISTS idx_user_profiles_manager ON user_profiles(manager_id);

-- Phase 11: User invitation system
CREATE TABLE IF NOT EXISTS user_invitations (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    email           text NOT NULL,
    role            text NOT NULL DEFAULT 'Viewer',
    invited_by      int REFERENCES app_users(id),
    token           text NOT NULL UNIQUE,
    message         text,
    expires_at      timestamptz NOT NULL DEFAULT (now() + interval '7 days'),
    accepted_at     timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_invitations_token ON user_invitations(token);
CREATE INDEX IF NOT EXISTS idx_invitations_email ON user_invitations(email);

-- Phase 13: Role templates
CREATE TABLE IF NOT EXISTS role_templates (
    id              serial PRIMARY KEY,
    name            text NOT NULL,
    description     text,
    permission_codes text[] NOT NULL DEFAULT '{}',
    is_system       boolean NOT NULL DEFAULT false,  -- system templates can't be deleted
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Seed default role templates
INSERT INTO role_templates (name, description, permission_codes, is_system) VALUES
    ('IT Administrator', 'Full access to infrastructure and admin', ARRAY['devices:read','devices:write','devices:delete','devices:export','switches:read','switches:write','switches:delete','switches:ping','switches:ssh','switches:sync','links:read','links:write','bgp:read','bgp:write','bgp:sync','vlans:read','vlans:write','admin:users','admin:roles','admin:lookups','admin:settings','admin:audit','admin:backup'], true),
    ('Network Engineer', 'Infrastructure management without admin', ARRAY['devices:read','devices:write','devices:export','switches:read','switches:write','switches:ping','switches:ssh','switches:sync','links:read','links:write','bgp:read','bgp:write','bgp:sync','vlans:read','vlans:write'], true),
    ('Help Desk', 'Service desk and basic device view', ARRAY['devices:read','switches:read','tasks:read','tasks:write'], true),
    ('Viewer', 'Read-only access', ARRAY['devices:read','switches:read','links:read','bgp:read','vlans:read','tasks:read'], true),
    ('Manager', 'Read access with reporting', ARRAY['devices:read','devices:export','switches:read','links:read','bgp:read','vlans:read','tasks:read','tasks:write','projects:read','projects:write'], true),
    ('CRM User', 'Full CRM access', ARRAY['contacts:read','contacts:write','companies:read','companies:write','crm:read','crm:write'], true)
ON CONFLICT DO NOTHING;
