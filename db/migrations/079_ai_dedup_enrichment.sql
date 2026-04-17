-- =============================================================================
-- Stage 4: Duplicate Detection + Data Enrichment
-- =============================================================================

-- ─── Duplicate match rules (per entity + matching strategy) ─────────────────
CREATE TABLE IF NOT EXISTS crm_duplicate_rules (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    entity_type     text NOT NULL,                        -- accounts, contacts, leads
    name            text NOT NULL,
    strategy        text NOT NULL DEFAULT 'fuzzy',       -- exact, fuzzy, ml, combined
    -- Fuzzy matching config
    match_fields    jsonb NOT NULL,                       -- [{field: email, weight: 1.0, algo: exact}, {field: name, weight: 0.5, algo: trigram}]
    similarity_threshold numeric(4,3) NOT NULL DEFAULT 0.85,
    -- Actions
    auto_merge_threshold numeric(4,3),                    -- above this, auto-merge; NULL = always require review
    block_on_match  boolean NOT NULL DEFAULT false,       -- prevent new record creation
    is_active       boolean NOT NULL DEFAULT true,
    priority        int NOT NULL DEFAULT 100,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_dup_rules_entity ON crm_duplicate_rules(entity_type) WHERE is_active = true;

-- Seed default rules
INSERT INTO crm_duplicate_rules (entity_type, name, strategy, match_fields, similarity_threshold, auto_merge_threshold) VALUES
    ('contacts', 'Email Match', 'exact',
     '[{"field":"email","weight":1.0,"algo":"exact"}]'::jsonb, 1.0, 1.0),
    ('contacts', 'Name + Company Fuzzy', 'fuzzy',
     '[{"field":"last_name","weight":0.4,"algo":"trigram"},{"field":"first_name","weight":0.3,"algo":"trigram"},{"field":"company_id","weight":0.3,"algo":"exact"}]'::jsonb, 0.85, NULL),
    ('accounts', 'Domain Match', 'fuzzy',
     '[{"field":"website","weight":0.6,"algo":"domain"},{"field":"name","weight":0.4,"algo":"trigram"}]'::jsonb, 0.80, NULL),
    ('leads', 'Email Match', 'exact',
     '[{"field":"email","weight":1.0,"algo":"exact"}]'::jsonb, 1.0, 1.0)
ON CONFLICT DO NOTHING;

-- Detected duplicates awaiting review
CREATE TABLE IF NOT EXISTS crm_duplicates (
    id              bigserial PRIMARY KEY,
    tenant_id       uuid,
    rule_id         int REFERENCES crm_duplicate_rules(id),
    entity_type     text NOT NULL,
    record_a_id     int NOT NULL,
    record_b_id     int NOT NULL,
    similarity_score numeric(5,4) NOT NULL,
    matched_fields  jsonb,                                 -- {email: {algo: exact, score: 1.0}, name: {algo: trigram, score: 0.87}}
    status          text NOT NULL DEFAULT 'pending',     -- pending, confirmed, rejected, merged
    reviewed_by     int,
    reviewed_at     timestamptz,
    merged_into_id  int,                                   -- the id of the surviving record after merge
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(entity_type, record_a_id, record_b_id)
);

CREATE INDEX IF NOT EXISTS idx_duplicates_pending ON crm_duplicates(entity_type, status) WHERE status = 'pending';
CREATE INDEX IF NOT EXISTS idx_duplicates_tenant ON crm_duplicates(tenant_id);

-- Merge operations log (full audit of what was merged)
CREATE TABLE IF NOT EXISTS crm_merge_operations (
    id              bigserial PRIMARY KEY,
    tenant_id       uuid,
    entity_type     text NOT NULL,
    surviving_id    int NOT NULL,
    merged_ids      int[] NOT NULL,
    field_precedence jsonb,                                -- which record's value won for each field
    merged_by       int,
    merged_at       timestamptz NOT NULL DEFAULT now(),
    can_undo        boolean NOT NULL DEFAULT true,
    undo_snapshot   jsonb                                  -- full record snapshot for undo
);

CREATE INDEX IF NOT EXISTS idx_merge_ops_surviving ON crm_merge_operations(entity_type, surviving_id);

-- ─── Data Enrichment ───────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS crm_enrichment_providers (
    id              serial PRIMARY KEY,
    provider_code   text NOT NULL UNIQUE,                 -- clearbit, apollo, zoominfo, peopledata, hunter
    display_name    text NOT NULL,
    provider_type   text NOT NULL,                        -- company, person, both
    base_url        text,
    auth_type       text NOT NULL DEFAULT 'api_key',
    -- Capabilities
    enriches_companies boolean NOT NULL DEFAULT true,
    enriches_contacts  boolean NOT NULL DEFAULT true,
    -- Pricing info
    price_per_match numeric(10,4),
    currency        char(3) DEFAULT 'USD',
    docs_url        text,
    is_enabled      boolean NOT NULL DEFAULT false
);

INSERT INTO crm_enrichment_providers (provider_code, display_name, provider_type, enriches_companies, enriches_contacts, price_per_match, docs_url) VALUES
    ('clearbit',    'Clearbit',     'both',   true,  true,  0.20, 'https://clearbit.com/docs'),
    ('apollo',      'Apollo.io',    'both',   true,  true,  0.10, 'https://apolloapi.readme.io'),
    ('zoominfo',    'ZoomInfo',     'both',   true,  true,  0.50, 'https://api-docs.zoominfo.com'),
    ('peopledata',  'People Data Labs','both',true,  true,  0.28, 'https://docs.peopledatalabs.com'),
    ('hunter',      'Hunter',       'person', false, true,  0.05, 'https://hunter.io/api-documentation')
ON CONFLICT (provider_code) DO NOTHING;

-- Per-tenant enrichment config (BYOK like AI providers)
CREATE TABLE IF NOT EXISTS tenant_enrichment_providers (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL,
    provider_id     int NOT NULL REFERENCES crm_enrichment_providers(id),
    is_enabled      boolean NOT NULL DEFAULT true,
    api_key_enc     text,
    monthly_budget  numeric(10,2),
    monthly_spend   numeric(10,2) NOT NULL DEFAULT 0,
    auto_enrich     boolean NOT NULL DEFAULT false,       -- enrich on record creation
    configured_at   timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, provider_id)
);

-- Enrichment jobs (async)
CREATE TABLE IF NOT EXISTS crm_enrichment_jobs (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    entity_type     text NOT NULL,                        -- company, account, contact, lead
    entity_id       int NOT NULL,
    provider_id     int REFERENCES crm_enrichment_providers(id),
    status          text NOT NULL DEFAULT 'queued',      -- queued, running, completed, failed, skipped
    match_field     text,                                  -- email, domain, name
    match_value     text,
    -- Results
    match_confidence numeric(5,4),
    fields_updated  text[],                                -- which fields were written
    raw_response    jsonb,
    cost_usd        numeric(10,4),
    error_message   text,
    requested_by    int,
    requested_at    timestamptz NOT NULL DEFAULT now(),
    completed_at    timestamptz
);

CREATE INDEX IF NOT EXISTS idx_enrichment_entity ON crm_enrichment_jobs(entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_enrichment_status ON crm_enrichment_jobs(status, requested_at);

-- Helper view: find probable duplicates using trigram similarity
CREATE OR REPLACE VIEW v_contact_duplicate_candidates AS
SELECT
    c1.id AS record_a_id, c2.id AS record_b_id,
    c1.tenant_id,
    similarity(c1.last_name || ' ' || COALESCE(c1.first_name, ''),
               c2.last_name || ' ' || COALESCE(c2.first_name, '')) AS name_similarity,
    CASE WHEN c1.email IS NOT NULL AND LOWER(c1.email) = LOWER(c2.email) THEN 1.0 ELSE 0 END AS email_match,
    CASE WHEN c1.company_id = c2.company_id THEN 1.0 ELSE 0 END AS same_company
FROM contacts c1
JOIN contacts c2 ON c1.id < c2.id
    AND c1.tenant_id = c2.tenant_id
    AND c1.last_name % c2.last_name   -- trgm operator — requires pg_trgm
WHERE c1.is_deleted IS NOT TRUE AND c2.is_deleted IS NOT TRUE;
