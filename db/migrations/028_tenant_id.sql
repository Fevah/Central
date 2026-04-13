-- =============================================================================
-- Migration 028: Add tenant_id + Row-Level Security to Central tables
-- =============================================================================
-- Phase 1.4 of the Central + Secure merge plan.
--
-- Adds tenant_id UUID to every public-schema table, defaults to the
-- well-known default tenant (single-tenant backward compat).
-- Enables RLS with policies that filter on app.tenant_id session variable.
--
-- Pattern matches Secure's auth-service RLS (SET LOCAL app.tenant_id).
-- =============================================================================

-- Default tenant UUID (must match db/seed_auth.sql and Secure V013)
DO $$ BEGIN
    PERFORM set_config('app.default_tenant_id', '00000000-0000-0000-0000-000000000001', false);
END $$;

-- Helper: creates the standard tenant_id column + RLS policy on a table.
-- Idempotent — skips if column already exists.
CREATE OR REPLACE FUNCTION _add_tenant_rls(tbl regclass) RETURNS void AS $$
DECLARE
    col_exists boolean;
BEGIN
    -- Check if tenant_id column already exists
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = tbl::text
          AND column_name = 'tenant_id'
    ) INTO col_exists;

    IF NOT col_exists THEN
        EXECUTE format(
            'ALTER TABLE %s ADD COLUMN tenant_id UUID NOT NULL DEFAULT ''00000000-0000-0000-0000-000000000001''::uuid',
            tbl
        );
        EXECUTE format(
            'CREATE INDEX IF NOT EXISTS idx_%s_tenant ON %s (tenant_id)',
            replace(tbl::text, '.', '_'), tbl
        );
    END IF;

    -- Enable RLS (idempotent)
    EXECUTE format('ALTER TABLE %s ENABLE ROW LEVEL SECURITY', tbl);

    -- Drop existing policy if any, then create
    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %s', tbl);
    EXECUTE format(
        'CREATE POLICY tenant_isolation ON %s FOR ALL USING (tenant_id = current_setting(''app.tenant_id'', true)::uuid)',
        tbl
    );

    -- Allow superusers/owner to bypass RLS (for migrations, admin tasks)
    EXECUTE format('ALTER TABLE %s FORCE ROW LEVEL SECURITY', tbl);
END;
$$ LANGUAGE plpgsql;

-- =============================================================================
-- Apply to all core tables (schema.sql originals)
-- =============================================================================
SELECT _add_tenant_rls('switches');
SELECT _add_tenant_rls('vlans');
SELECT _add_tenant_rls('interfaces');
SELECT _add_tenant_rls('interface_voice_vlans');
SELECT _add_tenant_rls('l3_interfaces');
SELECT _add_tenant_rls('bgp_config');
SELECT _add_tenant_rls('bgp_neighbors');
SELECT _add_tenant_rls('bgp_networks');
SELECT _add_tenant_rls('vrrp_config');
SELECT _add_tenant_rls('static_routes');
SELECT _add_tenant_rls('dhcp_relay');
SELECT _add_tenant_rls('spanning_tree');
SELECT _add_tenant_rls('cos_forwarding_classes');
SELECT _add_tenant_rls('firewall_filters');
SELECT _add_tenant_rls('switch_guide');
SELECT _add_tenant_rls('switch_connections');
SELECT _add_tenant_rls('config_templates');

-- =============================================================================
-- Migration-added tables
-- =============================================================================
-- 002: vlan_templates
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'vlan_templates') THEN
        PERFORM _add_tenant_rls('vlan_templates');
    END IF;
END $$;

-- 003: running_configs
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'running_configs') THEN
        PERFORM _add_tenant_rls('running_configs');
    END IF;
END $$;

-- 005: lookup_values
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'lookup_values') THEN
        PERFORM _add_tenant_rls('lookup_values');
    END IF;
END $$;

-- 006: app_users, role_permissions, user_settings
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'app_users') THEN
        PERFORM _add_tenant_rls('app_users');
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'role_permissions') THEN
        PERFORM _add_tenant_rls('role_permissions');
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'user_settings') THEN
        PERFORM _add_tenant_rls('user_settings');
    END IF;
END $$;

-- 007: roles
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'roles') THEN
        PERFORM _add_tenant_rls('roles');
    END IF;
END $$;

-- 008: role_sites
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'role_sites') THEN
        PERFORM _add_tenant_rls('role_sites');
    END IF;
END $$;

-- 009: config_ranges
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'config_ranges') THEN
        PERFORM _add_tenant_rls('config_ranges');
    END IF;
END $$;

-- 010: link tables, server_as, ip_ranges, mlag, mstp, vlan_inventory
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'p2p_links') THEN
        PERFORM _add_tenant_rls('p2p_links');
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'b2b_links') THEN
        PERFORM _add_tenant_rls('b2b_links');
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'fw_links') THEN
        PERFORM _add_tenant_rls('fw_links');
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'server_as') THEN
        PERFORM _add_tenant_rls('server_as');
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ip_ranges') THEN
        PERFORM _add_tenant_rls('ip_ranges');
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'mlag_config') THEN
        PERFORM _add_tenant_rls('mlag_config');
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'mstp_config') THEN
        PERFORM _add_tenant_rls('mstp_config');
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'vlan_inventory') THEN
        PERFORM _add_tenant_rls('vlan_inventory');
    END IF;
END $$;

-- 014: asn_definitions
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'asn_definitions') THEN
        PERFORM _add_tenant_rls('asn_definitions');
    END IF;
END $$;

-- 015: app_log
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'app_log') THEN
        PERFORM _add_tenant_rls('app_log');
    END IF;
END $$;

-- 016: builder_selections
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'builder_selections') THEN
        PERFORM _add_tenant_rls('builder_selections');
    END IF;
END $$;

-- 017: switch_audit_log, config_backups, switch_versions, switch_interfaces, ssh_logs, servers
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'switch_audit_log') THEN
        PERFORM _add_tenant_rls('switch_audit_log');
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'config_backups') THEN
        PERFORM _add_tenant_rls('config_backups');
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'switch_versions') THEN
        PERFORM _add_tenant_rls('switch_versions');
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'switch_interfaces') THEN
        PERFORM _add_tenant_rls('switch_interfaces');
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ssh_logs') THEN
        PERFORM _add_tenant_rls('ssh_logs');
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'servers') THEN
        PERFORM _add_tenant_rls('servers');
    END IF;
END $$;

-- 018: switch_model_interfaces
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'switch_model_interfaces') THEN
        PERFORM _add_tenant_rls('switch_model_interfaces');
    END IF;
END $$;

-- 020: interface_optics
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'interface_optics') THEN
        PERFORM _add_tenant_rls('interface_optics');
    END IF;
END $$;

-- 024: permissions, role_permission_grants
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'permissions') THEN
        PERFORM _add_tenant_rls('permissions');
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'role_permission_grants') THEN
        PERFORM _add_tenant_rls('role_permission_grants');
    END IF;
END $$;

-- 025: audit_log
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'audit_log') THEN
        PERFORM _add_tenant_rls('audit_log');
    END IF;
END $$;

-- =============================================================================
-- Tenant context helper (called by DbRepository before each query)
-- =============================================================================
CREATE OR REPLACE FUNCTION set_tenant_context(tid uuid) RETURNS void AS $$
BEGIN
    PERFORM set_config('app.tenant_id', tid::text, true);  -- true = local to transaction
END;
$$ LANGUAGE plpgsql;

-- Convenience: set default tenant (for backward compat / single-tenant mode)
CREATE OR REPLACE FUNCTION set_default_tenant() RETURNS void AS $$
BEGIN
    PERFORM set_tenant_context('00000000-0000-0000-0000-000000000001'::uuid);
END;
$$ LANGUAGE plpgsql;

-- =============================================================================
-- Bypass policy for the central superuser role (migrations, admin ops)
-- =============================================================================
-- The 'central' DB user is the owner and bypasses RLS by default.
-- If a separate app user is added later, grant bypass explicitly:
-- ALTER ROLE central_app BYPASSRLS;

-- =============================================================================
-- Cleanup helper function (keep it for future migrations)
-- =============================================================================
-- DROP FUNCTION IF EXISTS _add_tenant_rls(regclass);
-- Keeping it available for future tables added by new migrations.
