-- Phase 2: Full CRM-ready contact entity
-- Replaces the basic sd_requesters pattern with a unified contact model.

CREATE TABLE IF NOT EXISTS contacts (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    company_id      int REFERENCES companies(id) ON DELETE SET NULL,
    prefix          text,
    first_name      text NOT NULL,
    last_name       text NOT NULL,
    email           text,
    phone           text,
    mobile          text,
    job_title       text,
    department      text,
    linkedin_url    text,
    is_primary      boolean NOT NULL DEFAULT false,
    contact_type    text NOT NULL DEFAULT 'customer',  -- customer, vendor, partner, internal
    status          text NOT NULL DEFAULT 'active',    -- active, inactive, archived
    source          text,             -- web, referral, import, manual, ad_sync
    tags            text[] DEFAULT '{}',
    notes           text,
    avatar_url      text,
    metadata        jsonb DEFAULT '{}',
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    is_deleted      boolean DEFAULT false,
    deleted_at      timestamptz
);

CREATE INDEX IF NOT EXISTS idx_contacts_tenant ON contacts(tenant_id);
CREATE INDEX IF NOT EXISTS idx_contacts_company ON contacts(company_id);
CREATE INDEX IF NOT EXISTS idx_contacts_email ON contacts(email);
CREATE INDEX IF NOT EXISTS idx_contacts_name ON contacts(last_name, first_name);
CREATE INDEX IF NOT EXISTS idx_contacts_type ON contacts(contact_type);

-- Contact addresses (work, home, mailing)
CREATE TABLE IF NOT EXISTS contact_addresses (
    id              serial PRIMARY KEY,
    contact_id      int NOT NULL REFERENCES contacts(id) ON DELETE CASCADE,
    address_type    text NOT NULL DEFAULT 'work',
    line1           text,
    line2           text,
    city            text,
    state_region    text,
    postal_code     text,
    country_code    char(2),
    is_primary      boolean NOT NULL DEFAULT false,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_contact_addresses_contact ON contact_addresses(contact_id);

-- Contact communication log
CREATE TABLE IF NOT EXISTS contact_communications (
    id              serial PRIMARY KEY,
    contact_id      int NOT NULL REFERENCES contacts(id) ON DELETE CASCADE,
    channel         text NOT NULL,     -- email, phone, meeting, note, sms
    direction       text,              -- inbound, outbound
    subject         text,
    body            text,
    occurred_at     timestamptz NOT NULL DEFAULT now(),
    logged_by       int REFERENCES app_users(id)
);

CREATE INDEX IF NOT EXISTS idx_contact_comms_contact ON contact_communications(contact_id);
CREATE INDEX IF NOT EXISTS idx_contact_comms_date ON contact_communications(occurred_at DESC);

-- pg_notify
CREATE OR REPLACE FUNCTION notify_contacts_change() RETURNS trigger AS $$
BEGIN PERFORM pg_notify('data_changed', json_build_object('table','contacts','op',TG_OP,'id',COALESCE(NEW.id,OLD.id))::text); RETURN COALESCE(NEW,OLD); END;
$$ LANGUAGE plpgsql;
DROP TRIGGER IF EXISTS trg_contacts_notify ON contacts;
CREATE TRIGGER trg_contacts_notify AFTER INSERT OR UPDATE OR DELETE ON contacts FOR EACH ROW EXECUTE FUNCTION notify_contacts_change();
