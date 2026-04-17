-- =============================================================================
-- Seed Data — Default tenant, admin user, baseline subscription plans, modules
-- =============================================================================
-- Idempotent: uses ON CONFLICT DO NOTHING. Safe to re-run.
-- =============================================================================

-- Default subscription plans (if not already seeded)
INSERT INTO central_platform.subscription_plans (tier, display_name, max_users, max_devices, price_monthly, price_annual, annual_discount_pct)
VALUES
    ('free', 'Free', 3, 50, 0, 0, 0),
    ('professional', 'Professional', 25, 500, 49, 470, 20),
    ('enterprise', 'Enterprise', 99999, 99999, 199, 1910, 20)
ON CONFLICT (tier) DO NOTHING;

-- Default terms of service
INSERT INTO terms_of_service (version, content_url, effective_date, requires_acceptance)
VALUES ('1.0', 'https://central.local/legal/terms-v1.pdf', CURRENT_DATE, true)
ON CONFLICT DO NOTHING;

-- Default permissions catalogue (for new permission codes introduced in phases 1-14)
INSERT INTO permissions (code, category, name, description)
VALUES
    ('companies:read',     'companies', 'View Companies',        'View company records'),
    ('companies:write',    'companies', 'Edit Companies',        'Create and update companies'),
    ('companies:delete',   'companies', 'Delete Companies',      'Soft-delete companies'),
    ('contacts:read',      'contacts',  'View Contacts',         'View contact records'),
    ('contacts:write',     'contacts',  'Edit Contacts',         'Create and update contacts'),
    ('contacts:delete',    'contacts',  'Delete Contacts',       'Soft-delete contacts'),
    ('contacts:export',    'contacts',  'Export Contacts',       'Export contact data'),
    ('admin:teams',        'admin',     'Manage Teams',          'Create and manage teams'),
    ('admin:departments',  'admin',     'Manage Departments',    'Create and manage departments'),
    ('profiles:read',      'profiles',  'View Profiles',         'View user profiles'),
    ('profiles:write',     'profiles',  'Edit Profiles',         'Edit own or any profile'),
    ('groups:read',        'groups',    'View Groups',           'View user groups'),
    ('groups:write',       'groups',    'Edit Groups',           'Create and manage groups'),
    ('groups:delete',      'groups',    'Delete Groups',         'Delete user groups'),
    ('groups:assign',      'groups',    'Assign Members',        'Add/remove members from groups'),
    ('features:read',      'features',  'View Feature Flags',    'View feature flag state'),
    ('features:write',     'features',  'Toggle Feature Flags',  'Enable/disable features per tenant'),
    ('security:ip_rules',  'security',  'Manage IP Rules',       'Configure IP allowlist/blocklist'),
    ('security:keys',      'security',  'Manage Keys',           'Manage SSH/API keys'),
    ('security:deprovision','security', 'Deprovision Users',     'Configure auto-deprovisioning rules'),
    ('security:domains',   'security',  'Verify Domains',        'Manage domain verification'),
    ('billing:read',       'billing',   'View Billing',          'View invoices and billing status'),
    ('billing:write',      'billing',   'Manage Billing',        'Update payment methods, upgrade plans'),
    ('billing:discount',   'billing',   'Apply Discounts',       'Apply discount codes'),
    ('billing:invoice',    'billing',   'Manage Invoices',       'Issue and edit invoices'),
    ('crm:read',           'crm',       'View CRM',              'View CRM data'),
    ('crm:write',          'crm',       'Edit CRM',              'Create and update CRM records'),
    ('crm:delete',         'crm',       'Delete CRM Records',    'Delete CRM data'),
    ('crm:admin',          'crm',       'CRM Admin',             'Full CRM administration')
ON CONFLICT (code) DO NOTHING;

-- Auto-grant all new permissions to Admin role
INSERT INTO role_permission_grants (role_id, permission_code)
SELECT r.id, p.code
FROM roles r
CROSS JOIN permissions p
WHERE r.name = 'Admin'
  AND p.code IN (
    'companies:read','companies:write','companies:delete',
    'contacts:read','contacts:write','contacts:delete','contacts:export',
    'admin:teams','admin:departments',
    'profiles:read','profiles:write',
    'groups:read','groups:write','groups:delete','groups:assign',
    'features:read','features:write',
    'security:ip_rules','security:keys','security:deprovision','security:domains',
    'billing:read','billing:write','billing:discount','billing:invoice',
    'crm:read','crm:write','crm:delete','crm:admin'
  )
ON CONFLICT (role_id, permission_code) DO NOTHING;

-- Grant read permissions to Viewer role
INSERT INTO role_permission_grants (role_id, permission_code)
SELECT r.id, p.code
FROM roles r
CROSS JOIN permissions p
WHERE r.name = 'Viewer'
  AND p.code IN ('companies:read','contacts:read','profiles:read','groups:read','crm:read')
ON CONFLICT (role_id, permission_code) DO NOTHING;

-- Record seed completion
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM schema_versions WHERE version_number = '051_seed_data') THEN
        INSERT INTO schema_versions (version_number, description)
        VALUES ('051_seed_data', 'Seed permissions, plans, ToS, role grants');
    END IF;
END $$;
