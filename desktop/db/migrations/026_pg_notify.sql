-- 026_pg_notify.sql — Real-time change notifications via PostgreSQL LISTEN/NOTIFY
-- Fires on INSERT/UPDATE/DELETE on key tables. Payload: {"table":"...", "op":"INSERT|UPDATE|DELETE", "id":"..."}

CREATE OR REPLACE FUNCTION notify_data_change() RETURNS trigger AS $$
DECLARE
    payload jsonb;
    row_id text;
BEGIN
    -- Get the row ID (prefer 'id' column, fall back to first primary key)
    IF TG_OP = 'DELETE' THEN
        row_id := COALESCE(OLD.id::text, '');
    ELSE
        row_id := COALESCE(NEW.id::text, '');
    END IF;

    payload := jsonb_build_object(
        'table', TG_TABLE_NAME,
        'op', TG_OP,
        'id', row_id,
        'at', now()::text
    );

    PERFORM pg_notify('data_changed', payload::text);
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- Apply trigger to key tables
DO $$
DECLARE
    tbl text;
BEGIN
    FOREACH tbl IN ARRAY ARRAY[
        'switch_guide', 'switches', 'p2p_links', 'b2b_links', 'fw_links',
        'vlan_inventory', 'bgp_config', 'bgp_neighbors', 'bgp_networks',
        'app_users', 'roles', 'role_permissions', 'role_sites',
        'lookup_values', 'running_configs', 'config_backups',
        'asn_definitions', 'servers', 'switch_interfaces'
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
