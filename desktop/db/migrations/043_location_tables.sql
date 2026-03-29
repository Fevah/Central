-- Migration 043: Location tables (Country, Region, Postcode)
CREATE TABLE IF NOT EXISTS countries (
    id          serial PRIMARY KEY,
    code        varchar(3) NOT NULL UNIQUE,
    name        varchar(128) NOT NULL,
    sort_order  integer DEFAULT 0
);

CREATE TABLE IF NOT EXISTS regions (
    id          serial PRIMARY KEY,
    country_id  integer NOT NULL REFERENCES countries(id) ON DELETE CASCADE,
    code        varchar(10) NOT NULL,
    name        varchar(128) NOT NULL,
    sort_order  integer DEFAULT 0,
    UNIQUE(country_id, code)
);

CREATE TABLE IF NOT EXISTS postcodes (
    id          serial PRIMARY KEY,
    region_id   integer NOT NULL REFERENCES regions(id) ON DELETE CASCADE,
    code        varchar(16) NOT NULL,
    locality    varchar(128) DEFAULT '',
    latitude    decimal(9,6),
    longitude   decimal(9,6),
    UNIQUE(region_id, code)
);

CREATE INDEX IF NOT EXISTS idx_regions_country ON regions(country_id);
CREATE INDEX IF NOT EXISTS idx_postcodes_region ON postcodes(region_id);

-- Seed GB + common countries
INSERT INTO countries (code, name, sort_order) VALUES
    ('GBR', 'United Kingdom', 1),
    ('USA', 'United States', 2),
    ('AUS', 'Australia', 3),
    ('NZL', 'New Zealand', 4)
ON CONFLICT DO NOTHING;

-- Add permission
INSERT INTO permissions (code, name, category, description) VALUES
    ('admin:locations', 'Locations', 'admin', 'Manage countries, regions, postcodes')
ON CONFLICT DO NOTHING;
