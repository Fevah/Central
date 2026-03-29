-- 068_tenant_rls.sql — Add tenant_id + RLS to Central tables
-- Default tenant: 00000000-0000-0000-0000-000000000001

-- ============================================================
-- 1. Tenant context functions
-- ============================================================

CREATE OR REPLACE FUNCTION get_current_tenant_id() RETURNS uuid AS $$
BEGIN
    RETURN NULLIF(current_setting('app.tenant_id', true), '')::uuid;
EXCEPTION WHEN OTHERS THEN
    RETURN NULL;
END;
$$ LANGUAGE plpgsql STABLE SECURITY DEFINER;

CREATE OR REPLACE FUNCTION is_super_admin() RETURNS boolean AS $$
BEGIN
    RETURN COALESCE(current_setting('app.is_super_admin', true), 'false')::boolean;
EXCEPTION WHEN OTHERS THEN
    RETURN false;
END;
$$ LANGUAGE plpgsql STABLE SECURITY DEFINER;

-- ============================================================
-- 2. Add tenant_id to data tables
-- ============================================================

ALTER TABLE app_users ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE switches ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE vlans ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE interfaces ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE l3_interfaces ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE bgp_config ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE p2p_links ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE b2b_links ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE fw_links ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE task_projects ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE portfolios ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE programmes ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE sprints ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE releases ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE sd_requests ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE sd_technicians ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE sd_teams ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE sd_groups ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE user_settings ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE saved_filters ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE saved_reports ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE dashboards ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE time_entries ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';
ALTER TABLE running_configs ADD COLUMN IF NOT EXISTS tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001';

-- ============================================================
-- 3. Indexes
-- ============================================================

CREATE INDEX IF NOT EXISTS idx_app_users_tenant ON app_users (tenant_id);
CREATE INDEX IF NOT EXISTS idx_switches_tenant ON switches (tenant_id);
CREATE INDEX IF NOT EXISTS idx_tasks_tenant ON tasks (tenant_id);
CREATE INDEX IF NOT EXISTS idx_task_projects_tenant ON task_projects (tenant_id);
CREATE INDEX IF NOT EXISTS idx_sd_requests_tenant ON sd_requests (tenant_id);
CREATE INDEX IF NOT EXISTS idx_p2p_links_tenant ON p2p_links (tenant_id);
CREATE INDEX IF NOT EXISTS idx_b2b_links_tenant ON b2b_links (tenant_id);
CREATE INDEX IF NOT EXISTS idx_fw_links_tenant ON fw_links (tenant_id);
CREATE INDEX IF NOT EXISTS idx_portfolios_tenant ON portfolios (tenant_id);
CREATE INDEX IF NOT EXISTS idx_sprints_tenant ON sprints (tenant_id);

-- ============================================================
-- 4. Enable RLS + policies on key tables
-- ============================================================

ALTER TABLE app_users ENABLE ROW LEVEL SECURITY;
ALTER TABLE switches ENABLE ROW LEVEL SECURITY;
ALTER TABLE tasks ENABLE ROW LEVEL SECURITY;
ALTER TABLE task_projects ENABLE ROW LEVEL SECURITY;
ALTER TABLE sd_requests ENABLE ROW LEVEL SECURITY;
ALTER TABLE p2p_links ENABLE ROW LEVEL SECURITY;
ALTER TABLE b2b_links ENABLE ROW LEVEL SECURITY;
ALTER TABLE fw_links ENABLE ROW LEVEL SECURITY;
ALTER TABLE portfolios ENABLE ROW LEVEL SECURITY;
ALTER TABLE sprints ENABLE ROW LEVEL SECURITY;

-- Policies: tenant isolation with super admin bypass
CREATE POLICY tenant_isolation ON app_users USING (tenant_id = get_current_tenant_id() OR is_super_admin());
CREATE POLICY tenant_isolation ON switches USING (tenant_id = get_current_tenant_id() OR is_super_admin());
CREATE POLICY tenant_isolation ON tasks USING (tenant_id = get_current_tenant_id() OR is_super_admin());
CREATE POLICY tenant_isolation ON task_projects USING (tenant_id = get_current_tenant_id() OR is_super_admin());
CREATE POLICY tenant_isolation ON sd_requests USING (tenant_id = get_current_tenant_id() OR is_super_admin());
CREATE POLICY tenant_isolation ON p2p_links USING (tenant_id = get_current_tenant_id() OR is_super_admin());
CREATE POLICY tenant_isolation ON b2b_links USING (tenant_id = get_current_tenant_id() OR is_super_admin());
CREATE POLICY tenant_isolation ON fw_links USING (tenant_id = get_current_tenant_id() OR is_super_admin());
CREATE POLICY tenant_isolation ON portfolios USING (tenant_id = get_current_tenant_id() OR is_super_admin());
CREATE POLICY tenant_isolation ON sprints USING (tenant_id = get_current_tenant_id() OR is_super_admin());

-- Note: RLS only affects non-owner connections. The 'central' user is the owner,
-- so RLS is bypassed in development. Production uses a separate 'central_app' role.
