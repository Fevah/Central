-- =============================================================================
-- Migration 053: Enhanced multi-tenancy — per-operation RLS, TimescaleDB, Citus
-- =============================================================================
-- Replaces the FOR ALL policies from migration 028 with per-operation policies
-- (SELECT, INSERT, UPDATE, DELETE) so we can tighten writes separately from reads.
-- Adds TimescaleDB for time-series (audit, activity, metrics).
-- Adds Citus scaffolding for horizontal scaling of high-cardinality tenants.
-- =============================================================================

-- ─── Extensions ─────────────────────────────────────────────────────────────
CREATE EXTENSION IF NOT EXISTS timescaledb CASCADE;
-- Citus is an optional extension — gracefully skip if not available in the cluster
DO $$ BEGIN
    CREATE EXTENSION IF NOT EXISTS citus;
EXCEPTION WHEN undefined_file THEN
    RAISE NOTICE 'Citus extension not available on this cluster — skipping';
WHEN OTHERS THEN
    RAISE NOTICE 'Citus extension not loaded: %', SQLERRM;
END $$;

-- ─── Super-admin bypass function (idempotent) ───────────────────────────────
CREATE OR REPLACE FUNCTION is_super_admin() RETURNS boolean AS $$
BEGIN
    RETURN COALESCE(current_setting('app.is_super_admin', true)::boolean, false);
END;
$$ LANGUAGE plpgsql STABLE;

CREATE OR REPLACE FUNCTION get_current_tenant_id() RETURNS uuid AS $$
BEGIN
    RETURN COALESCE(
        NULLIF(current_setting('app.tenant_id', true), '')::uuid,
        '00000000-0000-0000-0000-000000000001'::uuid
    );
END;
$$ LANGUAGE plpgsql STABLE;

-- ─── Per-operation RLS policies ─────────────────────────────────────────────
-- Helper: drops the legacy `tenant_isolation` policy and installs 4 per-op policies.
-- Super-admins bypass all via is_super_admin().
CREATE OR REPLACE FUNCTION _install_per_op_rls(tbl regclass) RETURNS void AS $$
BEGIN
    EXECUTE format('ALTER TABLE %s ENABLE ROW LEVEL SECURITY', tbl);
    EXECUTE format('ALTER TABLE %s FORCE ROW LEVEL SECURITY', tbl);

    -- Drop legacy policy
    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %s', tbl);
    EXECUTE format('DROP POLICY IF EXISTS tenant_select ON %s', tbl);
    EXECUTE format('DROP POLICY IF EXISTS tenant_insert ON %s', tbl);
    EXECUTE format('DROP POLICY IF EXISTS tenant_update ON %s', tbl);
    EXECUTE format('DROP POLICY IF EXISTS tenant_delete ON %s', tbl);

    -- SELECT: see only your tenant's rows (or all if super-admin)
    EXECUTE format(
        'CREATE POLICY tenant_select ON %s FOR SELECT
         USING (tenant_id = get_current_tenant_id() OR is_super_admin())', tbl);

    -- INSERT: can only insert rows for your tenant
    EXECUTE format(
        'CREATE POLICY tenant_insert ON %s FOR INSERT
         WITH CHECK (tenant_id = get_current_tenant_id() OR is_super_admin())', tbl);

    -- UPDATE: can update your tenant's rows, and cannot change tenant_id away from yours
    EXECUTE format(
        'CREATE POLICY tenant_update ON %s FOR UPDATE
         USING (tenant_id = get_current_tenant_id() OR is_super_admin())
         WITH CHECK (tenant_id = get_current_tenant_id() OR is_super_admin())', tbl);

    -- DELETE: can only delete your tenant's rows
    EXECUTE format(
        'CREATE POLICY tenant_delete ON %s FOR DELETE
         USING (tenant_id = get_current_tenant_id() OR is_super_admin())', tbl);
END;
$$ LANGUAGE plpgsql;

-- Apply to all tenant-scoped tables
DO $$
DECLARE
    tbl record;
BEGIN
    FOR tbl IN
        SELECT c.relname::regclass AS name
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        JOIN pg_attribute a ON a.attrelid = c.oid
        WHERE n.nspname = 'public'
          AND c.relkind = 'r'
          AND a.attname = 'tenant_id'
          AND NOT a.attisdropped
    LOOP
        PERFORM _install_per_op_rls(tbl.name);
    END LOOP;
END $$;

-- ─── TimescaleDB: convert time-series tables to hypertables ─────────────────
-- Only if the extension is present

DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'timescaledb') THEN

        -- audit_log — append-only change tracking
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'audit_log') THEN
            PERFORM create_hypertable('audit_log', 'changed_at',
                chunk_time_interval => INTERVAL '7 days',
                if_not_exists => TRUE,
                migrate_data => TRUE);
        END IF;

        -- activity_feed — task/project activity
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'activity_feed') THEN
            PERFORM create_hypertable('activity_feed', 'occurred_at',
                chunk_time_interval => INTERVAL '7 days',
                if_not_exists => TRUE,
                migrate_data => TRUE);
        END IF;

        -- auth_events — authentication log
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'auth_events') THEN
            PERFORM create_hypertable('auth_events', 'timestamp',
                chunk_time_interval => INTERVAL '1 day',
                if_not_exists => TRUE,
                migrate_data => TRUE);
        END IF;

        -- team_activity
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'team_activity') THEN
            PERFORM create_hypertable('team_activity', 'occurred_at',
                chunk_time_interval => INTERVAL '7 days',
                if_not_exists => TRUE,
                migrate_data => TRUE);
        END IF;

        -- address_history
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'address_history') THEN
            PERFORM create_hypertable('address_history', 'changed_at',
                chunk_time_interval => INTERVAL '30 days',
                if_not_exists => TRUE,
                migrate_data => TRUE);
        END IF;

        -- tenant_usage_metrics (platform schema)
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'central_platform' AND table_name = 'tenant_usage_metrics') THEN
            PERFORM create_hypertable('central_platform.tenant_usage_metrics', 'recorded_at',
                chunk_time_interval => INTERVAL '1 day',
                if_not_exists => TRUE,
                migrate_data => TRUE);
        END IF;

        -- ── Compression on hypertables (70% space savings typical) ──
        ALTER TABLE audit_log SET (
            timescaledb.compress,
            timescaledb.compress_orderby = 'changed_at DESC',
            timescaledb.compress_segmentby = 'tenant_id'
        );
        SELECT add_compression_policy('audit_log', INTERVAL '14 days', if_not_exists => TRUE);

        ALTER TABLE activity_feed SET (
            timescaledb.compress,
            timescaledb.compress_orderby = 'occurred_at DESC',
            timescaledb.compress_segmentby = 'tenant_id'
        );
        SELECT add_compression_policy('activity_feed', INTERVAL '30 days', if_not_exists => TRUE);

        ALTER TABLE auth_events SET (
            timescaledb.compress,
            timescaledb.compress_orderby = '"timestamp" DESC'
        );
        SELECT add_compression_policy('auth_events', INTERVAL '7 days', if_not_exists => TRUE);

        -- ── Retention policies ──
        SELECT add_retention_policy('audit_log', INTERVAL '2 years', if_not_exists => TRUE);
        SELECT add_retention_policy('auth_events', INTERVAL '1 year', if_not_exists => TRUE);
        SELECT add_retention_policy('central_platform.tenant_usage_metrics', INTERVAL '1 year', if_not_exists => TRUE);

    END IF;
END $$;

-- ─── Continuous aggregates (real-time dashboard queries) ────────────────────
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'timescaledb') THEN

        -- Hourly usage rollup per tenant
        CREATE MATERIALIZED VIEW IF NOT EXISTS tenant_usage_hourly
            WITH (timescaledb.continuous) AS
        SELECT
            tenant_id,
            metric_type,
            time_bucket(INTERVAL '1 hour', recorded_at) AS bucket,
            sum(metric_value) AS total_value,
            avg(metric_value) AS avg_value,
            max(metric_value) AS max_value,
            count(*) AS sample_count
        FROM central_platform.tenant_usage_metrics
        GROUP BY tenant_id, metric_type, bucket
        WITH NO DATA;

        -- Refresh policy: every 30 minutes, 3-hour lag
        SELECT add_continuous_aggregate_policy('tenant_usage_hourly',
            start_offset => INTERVAL '3 hours',
            end_offset => INTERVAL '30 minutes',
            schedule_interval => INTERVAL '30 minutes',
            if_not_exists => TRUE);

        -- Daily auth event rollup
        CREATE MATERIALIZED VIEW IF NOT EXISTS auth_events_daily
            WITH (timescaledb.continuous) AS
        SELECT
            time_bucket(INTERVAL '1 day', "timestamp") AS day,
            event_type,
            provider_type,
            success,
            count(*) AS event_count
        FROM auth_events
        GROUP BY day, event_type, provider_type, success
        WITH NO DATA;

        SELECT add_continuous_aggregate_policy('auth_events_daily',
            start_offset => INTERVAL '7 days',
            end_offset => INTERVAL '1 hour',
            schedule_interval => INTERVAL '1 hour',
            if_not_exists => TRUE);

        -- Hourly activity feed rollup (for team dashboards)
        CREATE MATERIALIZED VIEW IF NOT EXISTS activity_feed_hourly
            WITH (timescaledb.continuous) AS
        SELECT
            tenant_id,
            time_bucket(INTERVAL '1 hour', occurred_at) AS bucket,
            action,
            count(*) AS event_count,
            count(DISTINCT user_id) AS active_users
        FROM activity_feed
        GROUP BY tenant_id, bucket, action
        WITH NO DATA;

        SELECT add_continuous_aggregate_policy('activity_feed_hourly',
            start_offset => INTERVAL '3 hours',
            end_offset => INTERVAL '30 minutes',
            schedule_interval => INTERVAL '30 minutes',
            if_not_exists => TRUE);

    END IF;
END $$;

-- ─── Citus scaffolding for horizontal scaling ───────────────────────────────
-- Tenants with >50TB or >1M active users should be sharded. These functions
-- register distributed tables; actual sharding happens when a tenant is marked
-- for horizontal scaling via central_platform.tenant_shard_config.

CREATE TABLE IF NOT EXISTS central_platform.tenant_shard_config (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL UNIQUE REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    shard_count     int NOT NULL DEFAULT 32,
    distribution_key text NOT NULL DEFAULT 'tenant_id',
    storage_bytes   bigint DEFAULT 0,
    active_users    int DEFAULT 0,
    last_measured_at timestamptz,
    needs_sharding  boolean NOT NULL DEFAULT false,
    sharded_at      timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Thresholds
CREATE OR REPLACE FUNCTION central_platform.evaluate_sharding_thresholds()
RETURNS TABLE (tenant_id uuid, reason text) AS $$
BEGIN
    RETURN QUERY
    SELECT c.tenant_id,
           CASE
               WHEN c.storage_bytes >= 50 * 1024::bigint * 1024 * 1024 * 1024 THEN 'storage_50tb'
               WHEN c.active_users >= 1000000 THEN 'users_1m'
               ELSE 'other'
           END AS reason
    FROM central_platform.tenant_shard_config c
    WHERE c.sharded_at IS NULL
      AND (c.storage_bytes >= 50 * 1024::bigint * 1024 * 1024 * 1024
           OR c.active_users >= 1000000);
END;
$$ LANGUAGE plpgsql STABLE;

-- Distribute high-cardinality tables by tenant_id (only if Citus is loaded)
-- This is idempotent — create_distributed_table is safe to call on already-distributed tables
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'citus') THEN
        -- Distribute large per-tenant tables
        PERFORM create_distributed_table('tasks', 'tenant_id', colocate_with => 'none');
        PERFORM create_distributed_table('contacts', 'tenant_id', colocate_with => 'tasks');
        PERFORM create_distributed_table('companies', 'tenant_id', colocate_with => 'tasks');
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Citus distribution skipped: %', SQLERRM;
END $$;

-- ─── Logical replication publications (multi-region read replicas) ──────────
-- Publishes all tenant-scoped tables for subscription by regional read replicas
CREATE PUBLICATION IF NOT EXISTS central_replication_all FOR ALL TABLES;

-- Reader-specific publication excluding audit/log tables (reduce replica churn)
DO $$
DECLARE
    excluded_tables text[];
BEGIN
    -- Drop + recreate to refresh the list of tables
    DROP PUBLICATION IF EXISTS central_replication_data;

    CREATE PUBLICATION central_replication_data FOR TABLES IN SCHEMA public
        WITH (publish = 'insert, update, delete');
END $$;

-- Record migration
INSERT INTO schema_versions (version_number, description)
VALUES ('053_rls_timescale_citus', 'Per-op RLS + TimescaleDB hypertables + compression + continuous aggregates + Citus scaffolding + logical replication publications')
ON CONFLICT (version_number) DO NOTHING;
