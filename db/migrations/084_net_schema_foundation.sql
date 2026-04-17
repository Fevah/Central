-- =============================================================================
-- 084 — Networking engine foundation (Phase 1 of the buildout)
--
-- See docs/NETWORKING_BUILDOUT_PLAN.md. This migration lays the groundwork
-- for every subsequent net.* entity without creating any of them yet:
--
--   1. `net` schema as the permanent home for the networking source-of-truth
--      engine. Sits alongside central_platform.* and public.*; legacy
--      networking tables in public.* will be migrated into net.* and dropped
--      in phases 11-12.
--
--   2. Universal enums (entity_status, lock_state) used on every net.*
--      entity from Phase 2 onwards. Postgres native enum types keep these
--      tight at the DB level and cheap to compare.
--
--   3. schema_versions table — referenced conditionally by migrations 080+
--      but never created. This migration creates it and backfills entries
--      for every 0*.sql already applied. Going forward, each migration
--      records itself here so "what's the DB at?" is a single query.
--
--   4. Default tenant renamed to "Immunocore" since they are de-facto
--      tenant 1 — every existing row in public.* is implicitly theirs. No
--      UUID change (keeps all downstream references intact).
--
-- Idempotent; safe to re-run.
-- =============================================================================

-- ─── 1. The net schema ────────────────────────────────────────────────────
CREATE SCHEMA IF NOT EXISTS net;

COMMENT ON SCHEMA net IS
    'Networking source-of-truth engine. All tenant-scoped network entities '
    '(organisations, hierarchy, devices, pools, links, servers) live here. '
    'See docs/NETWORKING_ATTRIBUTE_SYSTEM.md.';

-- ─── 2. Universal enums for every net.* entity ────────────────────────────
DO $$ BEGIN
    CREATE TYPE net.entity_status AS ENUM (
        'Planned',
        'Reserved',
        'Active',
        'Deprecated',
        'Retired'
    );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE net.lock_state AS ENUM (
        'Open',
        'SoftLock',
        'HardLock',
        'Immutable'
    );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

COMMENT ON TYPE net.entity_status IS
    'Lifecycle state. Planned->Reserved->Active->Deprecated->Retired. '
    'Transition to Active auto-applies HardLock to numbering.';
COMMENT ON TYPE net.lock_state IS
    'Lock level. Open=free edit; SoftLock=warning + reason; '
    'HardLock=requires Change Set + N approvers; Immutable=never editable.';

-- ─── 3. schema_versions tracking table ────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.schema_versions (
    version_number  text PRIMARY KEY,
    description     text NOT NULL,
    applied_at      timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE public.schema_versions IS
    'One row per applied migration. Lets tooling ask "what version is this DB at?" '
    'without scanning the filesystem. New migrations insert themselves at the end.';

-- Backfill every previously-applied migration we know shipped. Ordered.
INSERT INTO public.schema_versions (version_number, description) VALUES
    ('002_builder',                       'vlan_templates + switch_connections / bgp_config columns'),
    ('003_connectivity',                  'management_ip + ssh/ping columns + running_configs'),
    ('004_ipam_fields',                   'Extra IPAM device fields'),
    ('005_lookup_values',                 'lookup_values table'),
    ('006_users_roles',                   'app_users, role_permissions, user_settings'),
    ('007_roles_table',                   'roles table'),
    ('008_role_sites',                    'per-role site access'),
    ('009_config_ranges',                 'Config range definitions'),
    ('010_excel_sheets',                  'Excel sheet import tracking'),
    ('011_view_reserved',                 'Reserved device view'),
    ('012_config_versions',               'Config version history'),
    ('023_bgp_sync',                      'BGP sync columns'),
    ('024_permissions_v2',                'permissions table + role_permission_grants'),
    ('025_audit_log_v2',                  'audit_log + soft-delete columns'),
    ('026_pg_notify',                     'pg_notify triggers on 19 tables'),
    ('027_user_auth',                     'password_hash, salt, user_type on app_users'),
    ('028_default_settings',              'default_user_settings + auto-seed'),
    ('029_job_schedules',                 'job_schedules + job_history'),
    ('030_icon_library',                  'icon_library + ribbon_icon_assignments + user_icon_overrides'),
    ('031_tasks',                         'tasks table'),
    ('032_ribbon_config',                 'ribbon_pages/groups/items + pg_notify'),
    ('033_service_desk_incremental',     'sd_requests/requesters/technicians + integrations'),
    ('035_api_key_salt',                  'salt column on api_keys'),
    ('036_companies',                     'companies table + hierarchy'),
    ('037_contacts_v2',                   'Full CRM contacts'),
    ('038_teams_departments',             'departments + teams + team_members'),
    ('039_addresses_unified',             'Polymorphic addresses'),
    ('040_user_profiles',                 'user_profiles + invitations + role_templates'),
    ('041_global_admin_v2',               'tenant_onboarding + billing_accounts + FTS'),
    ('042_groups',                        'user_groups + permissions + resource access'),
    ('043_feature_flags',                 'feature_flags + tenant_feature_flags'),
    ('044_security_enhancements',         'ip_access_rules + user_ssh_keys + deprovisioning'),
    ('045_team_hierarchy',                'team parent_id + resources + permissions'),
    ('046_address_history',               'address_history + audit trigger'),
    ('047_permission_inheritance',        'role.parent_role_id + user_permission_overrides'),
    ('048_social_providers',              'social_providers + user_social_logins'),
    ('049_billing_extended',              'Annual pricing + trials + discounts + addons'),
    ('050_password_recovery',             'password_reset + email_verification'),
    ('051_seed_data',                     'Seed data'),
    ('052_tenant_sizing',                 'tenant_connection_map + provisioning jobs'),
    ('053_rls_timescale_citus',           'RLS + TimescaleDB + Citus scaffolding'),
    ('054_crm_core',                      'CRM accounts + deals + leads + activities'),
    ('055_crm_quotes_products',           'CRM quotes + products'),
    ('056_email_integration',             'email_accounts + templates + messages'),
    ('057_crm_dashboards_reports',        'saved_reports + forecasts + matviews'),
    ('058_crm_integrations',              'Salesforce/HubSpot/Gmail/etc'),
    ('059_crm_webhooks_polish',           'webhook_event_types + subscriptions'),
    ('060_crm_campaigns',                 'Marketing campaigns + members + costs'),
    ('061_crm_segments_sequences',        'Segments + email sequences + landing pages'),
    ('062_crm_attribution',               'UTM events + attribution touches'),
    ('063_crm_territories',               'Territories + rules'),
    ('064_crm_quotas_commissions',        'Quotas + commission plans + payouts'),
    ('065_crm_account_teams',             'Opportunity splits + account teams'),
    ('066_crm_forecast_hierarchies',      'Forecast adjustments + pipeline health'),
    ('067_crm_cpq',                       'Product bundles + pricing rules'),
    ('068_approval_engine',               'Generic approval engine'),
    ('069_crm_contracts',                 'Contracts + clauses + milestones'),
    ('070_crm_subscriptions_revenue',     'Subscriptions + MRR + revenue schedules'),
    ('071_crm_orders',                    'Orders + order lines'),
    ('072_portals_community',             'Portal users + KB + community'),
    ('073_rule_engines',                  'Validation rules + workflow rules'),
    ('074_custom_objects_field_security', 'Custom entities + field permissions'),
    ('075_import_commerce',               'Import jobs + carts + payments'),
    ('076_ai_providers',                  'AI providers + tenant BYOK + usage'),
    ('077_ai_ml_scoring',                 'ML models + scores + next-best-actions'),
    ('078_ai_assistant',                  'AI conversations + messages + templates + tools'),
    ('079_ai_dedup_enrichment',           'Duplicate detection + enrichment'),
    ('080_ai_churn_calls',                'Churn risk + LTV + call recordings'),
    ('081_desktop_missing_tables',        'Hotfix: identity_providers + sync_configs + panel_customizations'),
    ('082_app_users_auth_columns',        'password_changed_at + mfa_secret_enc'),
    ('083_module_catalog_reconcile',      'Post-merge module_catalog reconciliation'),
    ('084_net_schema_foundation',         'Networking engine Phase 1: net schema + enums + schema_versions + Immunocore tenant')
ON CONFLICT (version_number) DO NOTHING;

-- ─── 4. Rename default tenant -> Immunocore ───────────────────────────────
-- Every row in public.* is implicitly theirs since they've been the only
-- customer. Preserving the UUID means no downstream foreign-key disruption.
UPDATE central_platform.tenants
SET display_name = 'Immunocore',
    slug         = 'immunocore'
WHERE id = '00000000-0000-0000-0000-000000000000'
  AND slug = 'default';
