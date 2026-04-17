-- Phase 1: Company/Organization entity
-- Central registry of companies that tenants, contacts, and users belong to.

CREATE TABLE IF NOT EXISTS companies (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    legal_name      text,
    registration_no text,
    tax_id          text,
    industry        text,
    size_band       text,             -- 1-10, 11-50, 51-200, 201-1000, 1000+
    website         text,
    logo_url        text,
    parent_id       int REFERENCES companies(id) ON DELETE SET NULL,
    is_active       boolean NOT NULL DEFAULT true,
    metadata        jsonb DEFAULT '{}',
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    is_deleted      boolean DEFAULT false,
    deleted_at      timestamptz
);

CREATE INDEX IF NOT EXISTS idx_companies_tenant ON companies(tenant_id);
CREATE INDEX IF NOT EXISTS idx_companies_name ON companies(name);
CREATE INDEX IF NOT EXISTS idx_companies_parent ON companies(parent_id);

-- Add company_id to app_users (link users to their company)
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS company_id int REFERENCES companies(id) ON DELETE SET NULL;

-- pg_notify trigger
CREATE OR REPLACE FUNCTION notify_companies_change() RETURNS trigger AS $$
BEGIN PERFORM pg_notify('data_changed', json_build_object('table','companies','op',TG_OP,'id',COALESCE(NEW.id,OLD.id))::text); RETURN COALESCE(NEW,OLD); END;
$$ LANGUAGE plpgsql;
DROP TRIGGER IF EXISTS trg_companies_notify ON companies;
CREATE TRIGGER trg_companies_notify AFTER INSERT OR UPDATE OR DELETE ON companies FOR EACH ROW EXECUTE FUNCTION notify_companies_change();
