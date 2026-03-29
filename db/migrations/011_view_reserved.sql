-- Add can_view_reserved permission to role_permissions
ALTER TABLE role_permissions ADD COLUMN IF NOT EXISTS can_view_reserved BOOLEAN DEFAULT true;
