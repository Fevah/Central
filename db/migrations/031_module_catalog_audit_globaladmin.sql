-- 031_module_catalog_audit_globaladmin.sql
-- Extend module_catalog with the two modules the web UI ships but that
-- were missing from the seed in 026: audit + globaladmin.
--
-- `audit` is optional (M365 forensics + GDPR scoring) — paid add-on.
-- `globaladmin` is base for platform operators and has special handling:
-- access is gated by the `global_admin` claim, not just the tenant license,
-- so every tenant "has" the module but only global admins see it.

INSERT INTO central_platform.module_catalog (code, display_name, is_base) VALUES
    ('audit',       'Audit & Forensics',  false),
    ('globaladmin', 'Global Admin',       true)
ON CONFLICT (code) DO NOTHING;

-- Grant the two new modules to every existing tenant that has a base-module
-- license, so nobody loses UI after the upgrade.
INSERT INTO central_platform.tenant_module_licenses (tenant_id, module_id)
SELECT DISTINCT l.tenant_id, m.id
FROM central_platform.tenant_module_licenses l
CROSS JOIN central_platform.module_catalog m
WHERE m.code IN ('audit', 'globaladmin')
ON CONFLICT (tenant_id, module_id) DO NOTHING;
