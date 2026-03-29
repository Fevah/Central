-- =============================================================================
-- 007_roles_table.sql — Dedicated roles table for role management UI
-- =============================================================================

CREATE TABLE IF NOT EXISTS roles (
    id          SERIAL PRIMARY KEY,
    name        VARCHAR(50) NOT NULL UNIQUE,
    description TEXT,
    created_at  TIMESTAMPTZ DEFAULT NOW(),
    updated_at  TIMESTAMPTZ DEFAULT NOW()
);

CREATE TRIGGER trg_roles_updated_at
    BEFORE UPDATE ON roles
    FOR EACH ROW EXECUTE FUNCTION update_updated_at();

-- Seed from existing role_permissions distinct roles
INSERT INTO roles (name, description)
VALUES
    ('Admin',    'Full access to all modules'),
    ('Operator', 'Can view and edit IPAM and Switches'),
    ('Viewer',   'Read-only access to all modules')
ON CONFLICT (name) DO NOTHING;
