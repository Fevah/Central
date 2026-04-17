-- =============================================================================
-- Tenant Sizing Model
--   Normal (zoned) — schema-per-tenant in shared `central` database
--   Enterprise     — dedicated database + dedicated K8s namespace
-- =============================================================================

-- Tenant size tier (in addition to subscription tier)
ALTER TABLE central_platform.tenants ADD COLUMN IF NOT EXISTS sizing_model text NOT NULL DEFAULT 'zoned';
-- sizing_model: 'zoned' (shared cluster, schema-per-tenant) or 'dedicated' (own DB + namespace)

ALTER TABLE central_platform.tenants ADD COLUMN IF NOT EXISTS provisioning_status text NOT NULL DEFAULT 'ready';
-- provisioning_status: ready, provisioning, migrating, failed, decommissioning

ALTER TABLE central_platform.tenants ADD COLUMN IF NOT EXISTS provisioning_error text;

-- Connection map — how the API finds each tenant's database
-- Lookup precedence: tenant_connection_map → DNS-per-tenant → fallback shared config
CREATE TABLE IF NOT EXISTS central_platform.tenant_connection_map (
    id                  serial PRIMARY KEY,
    tenant_id           uuid NOT NULL UNIQUE REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    sizing_model        text NOT NULL DEFAULT 'zoned',
    database_name       text NOT NULL,                -- 'central' for zoned, 'central_<slug>' for dedicated
    schema_name         text NOT NULL DEFAULT 'public', -- 'tenant_<slug>' for zoned, 'public' for dedicated
    connection_string   text,                         -- NULL = use default cluster
    dns_name            text,                         -- e.g., 'acme.db.svc.cluster.local' (optional)
    k8s_namespace       text,                         -- e.g., 'central-acme' for dedicated
    pgbouncer_host      text,                         -- optional per-tenant pooler
    read_replica_host   text,
    max_connections     int NOT NULL DEFAULT 25,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_tenant_conn_map_tenant ON central_platform.tenant_connection_map(tenant_id);

-- Dedicated database provisioning records
CREATE TABLE IF NOT EXISTS central_platform.tenant_dedicated_databases (
    id                  serial PRIMARY KEY,
    tenant_id           uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    database_name       text NOT NULL,
    namespace           text NOT NULL,
    status              text NOT NULL DEFAULT 'pending',  -- pending, provisioning, dumping_source, restoring, migrating, active, failed, decommissioned
    source_schema       text,                               -- the zoned schema being migrated from
    dump_file_path      text,                               -- backup dump location
    restored_at         timestamptz,
    migrated_at         timestamptz,
    activated_at        timestamptz,
    error_message       text,
    provisioned_by      int,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_tenant_dbs_tenant ON central_platform.tenant_dedicated_databases(tenant_id);
CREATE INDEX IF NOT EXISTS idx_tenant_dbs_status ON central_platform.tenant_dedicated_databases(status);

-- Provisioning job queue (for the Rust tenant-provisioner service to pick up)
CREATE TABLE IF NOT EXISTS central_platform.provisioning_jobs (
    id              bigserial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    job_type        text NOT NULL,   -- provision_dedicated, decommission_dedicated, resize
    status          text NOT NULL DEFAULT 'queued',  -- queued, running, completed, failed, cancelled
    payload         jsonb DEFAULT '{}',
    started_at      timestamptz,
    completed_at    timestamptz,
    error_message   text,
    retry_count     int NOT NULL DEFAULT 0,
    next_retry_at   timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_prov_jobs_status ON central_platform.provisioning_jobs(status, created_at);
CREATE INDEX IF NOT EXISTS idx_prov_jobs_tenant ON central_platform.provisioning_jobs(tenant_id);

-- Seed the default tenant into the connection map (zoned)
INSERT INTO central_platform.tenant_connection_map (tenant_id, sizing_model, database_name, schema_name)
SELECT id, 'zoned', 'central', 'public'
FROM central_platform.tenants
WHERE slug = 'default'
ON CONFLICT (tenant_id) DO NOTHING;

-- Helper function: resolve connection info for a tenant
CREATE OR REPLACE FUNCTION central_platform.resolve_tenant_connection(p_tenant_id uuid)
RETURNS TABLE (
    sizing_model text,
    database_name text,
    schema_name text,
    connection_string text,
    dns_name text,
    k8s_namespace text
) AS $$
BEGIN
    RETURN QUERY
    SELECT m.sizing_model, m.database_name, m.schema_name,
           m.connection_string, m.dns_name, m.k8s_namespace
    FROM central_platform.tenant_connection_map m
    WHERE m.tenant_id = p_tenant_id
    LIMIT 1;
END;
$$ LANGUAGE plpgsql STABLE;

-- Trigger: auto-queue provisioning job when tenant upgrades to Enterprise tier
CREATE OR REPLACE FUNCTION central_platform.on_tenant_tier_change() RETURNS trigger AS $$
BEGIN
    IF NEW.tier = 'enterprise' AND (OLD.tier IS DISTINCT FROM 'enterprise') AND NEW.sizing_model = 'zoned' THEN
        -- Queue provisioning job
        INSERT INTO central_platform.provisioning_jobs (tenant_id, job_type, payload)
        VALUES (NEW.id, 'provision_dedicated', jsonb_build_object(
            'target_database', 'central_' || regexp_replace(NEW.slug, '[^a-zA-Z0-9_]', '_', 'g'),
            'target_namespace', 'central-' || regexp_replace(NEW.slug, '[^a-zA-Z0-9_]', '-', 'g')
        ));
        -- Mark tenant as provisioning
        NEW.provisioning_status := 'provisioning';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_tenant_tier_upgrade ON central_platform.tenants;
CREATE TRIGGER trg_tenant_tier_upgrade
    BEFORE UPDATE OF tier ON central_platform.tenants
    FOR EACH ROW EXECUTE FUNCTION central_platform.on_tenant_tier_change();
