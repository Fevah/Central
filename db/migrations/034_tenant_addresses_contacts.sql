-- Tenant addresses (many-to-one: each tenant has multiple addresses)
CREATE TABLE IF NOT EXISTS central_platform.tenant_addresses (
    id          serial PRIMARY KEY,
    tenant_id   uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    address_type varchar(32) NOT NULL DEFAULT 'billing',  -- billing, shipping, hq, support, site
    label       varchar(128),
    line1       varchar(256) NOT NULL DEFAULT '',
    line2       varchar(256),
    city        varchar(128) NOT NULL DEFAULT '',
    state       varchar(128),
    postal_code varchar(32),
    country     varchar(64) NOT NULL DEFAULT '',
    is_primary  boolean NOT NULL DEFAULT false,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_tenant_addresses_tenant ON central_platform.tenant_addresses (tenant_id);

-- Contacts (many-to-many: contacts can be shared across tenants)
CREATE TABLE IF NOT EXISTS central_platform.contacts (
    id          serial PRIMARY KEY,
    first_name  varchar(128) NOT NULL DEFAULT '',
    last_name   varchar(128) NOT NULL DEFAULT '',
    email       varchar(256),
    phone       varchar(64),
    mobile      varchar(64),
    job_title   varchar(128),
    company     varchar(256),
    notes       text,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

-- Junction table: tenant <-> contact (many-to-many)
CREATE TABLE IF NOT EXISTS central_platform.tenant_contacts (
    id          serial PRIMARY KEY,
    tenant_id   uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    contact_id  integer NOT NULL REFERENCES central_platform.contacts(id) ON DELETE CASCADE,
    role        varchar(64) NOT NULL DEFAULT 'primary',  -- primary, billing, technical, emergency
    is_primary  boolean NOT NULL DEFAULT false,
    assigned_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, contact_id)
);

CREATE INDEX IF NOT EXISTS idx_tenant_contacts_tenant ON central_platform.tenant_contacts (tenant_id);
CREATE INDEX IF NOT EXISTS idx_tenant_contacts_contact ON central_platform.tenant_contacts (contact_id);
