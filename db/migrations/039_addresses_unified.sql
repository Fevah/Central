-- Phase 4: Unified polymorphic address system
-- Single table for company, contact, tenant, and location addresses.

CREATE TABLE IF NOT EXISTS addresses (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    entity_type     text NOT NULL,     -- company, contact, tenant, location, user
    entity_id       int NOT NULL,
    address_type    text NOT NULL DEFAULT 'hq',  -- billing, shipping, hq, branch, site, home, work
    label           text,              -- "London Office", "Warehouse 2"
    line1           text NOT NULL DEFAULT '',
    line2           text,
    line3           text,
    city            text NOT NULL DEFAULT '',
    state_region    text,
    postal_code     text,
    country_code    char(2) NOT NULL DEFAULT 'GB',
    latitude        numeric(9,6),
    longitude       numeric(9,6),
    is_primary      boolean NOT NULL DEFAULT false,
    is_verified     boolean NOT NULL DEFAULT false,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_addresses_entity ON addresses(entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_addresses_tenant ON addresses(tenant_id);
CREATE INDEX IF NOT EXISTS idx_addresses_city ON addresses(city);
CREATE INDEX IF NOT EXISTS idx_addresses_country ON addresses(country_code);
