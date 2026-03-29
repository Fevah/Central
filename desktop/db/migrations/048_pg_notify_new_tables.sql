-- Migration 048: pg_notify triggers for all new tables
-- Uses the same notify_data_change() function from migration 026

DO $$
DECLARE
    tbl text;
BEGIN
    FOREACH tbl IN ARRAY ARRAY[
        -- Admin features (039-046)
        'identity_providers', 'idp_domain_mappings', 'user_external_identities',
        'claim_mappings', 'auth_events',
        'social_providers', 'magic_link_tokens', 'mfa_recovery_codes', 'revoked_tokens',
        'migration_history', 'backup_history',
        'countries', 'regions', 'postcodes',
        'reference_config',
        'panel_customizations',
        'appointments', 'appointment_resources',
        -- Service Desk (033-036)
        'sd_requests', 'sd_technicians', 'sd_requesters', 'sd_groups',
        'sd_teams', 'sd_team_members',
        'sd_group_categories', 'sd_group_category_members',
        -- Tasks + Jobs (031, 029)
        'tasks', 'job_schedules', 'job_history',
        -- Icons + Ribbon (030, 032, 038)
        'icon_library', 'icon_defaults', 'user_icon_overrides',
        'ribbon_pages', 'ribbon_groups', 'ribbon_items',
        'admin_ribbon_defaults', 'user_ribbon_overrides',
        -- Saved filters
        'saved_filters',
        -- Integrations
        'integrations', 'integration_credentials'
    ]
    LOOP
        EXECUTE format(
            'DROP TRIGGER IF EXISTS trg_notify_%s ON %I; '
            'CREATE TRIGGER trg_notify_%s AFTER INSERT OR UPDATE OR DELETE ON %I '
            'FOR EACH ROW EXECUTE FUNCTION notify_data_change();',
            tbl, tbl, tbl, tbl
        );
    END LOOP;
END;
$$;
