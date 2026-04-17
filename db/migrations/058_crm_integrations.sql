-- =============================================================================
-- Phases 26-28: CRM Sync Agents + Email Providers + Document Management
-- =============================================================================

-- ─── Register CRM sync agents in integrations table ─────────────────────────
-- Uses existing integrations + sync_configs framework from migration 049.

INSERT INTO integrations (name, integration_type, base_url, is_enabled, config_json)
VALUES
    ('Salesforce',     'salesforce', 'https://login.salesforce.com',          false, '{"api_version":"v60.0","entities":["Account","Contact","Opportunity","Lead"]}'),
    ('HubSpot',        'hubspot',    'https://api.hubapi.com',                false, '{"entities":["companies","contacts","deals"]}'),
    ('Microsoft Dynamics', 'dynamics', 'https://dynamics.microsoft.com',      false, '{"entities":["accounts","contacts","opportunities"]}'),
    ('Exchange Online','exchange',   'https://graph.microsoft.com/v1.0',      false, '{"entities":["messages","calendar"]}'),
    ('Gmail',          'gmail',      'https://gmail.googleapis.com/gmail/v1', false, '{"entities":["messages","threads"]}'),
    ('Pipedrive',      'pipedrive',  'https://api.pipedrive.com/v1',          false, '{"entities":["organizations","persons","deals"]}')
ON CONFLICT (name) DO NOTHING;

-- Sync configs for bidirectional CRM sync
-- The existing sync_configs framework handles the actual sync execution via SyncEngine
INSERT INTO sync_configs (name, agent_type, direction, schedule_cron, is_enabled, max_concurrent, config_json)
VALUES
    ('Salesforce: Accounts', 'salesforce', 'bidirectional', '*/15 * * * *', false, 2,
     '{"source_entity":"Account","target_table":"crm_accounts","upsert_key":"external_id"}'),
    ('Salesforce: Contacts', 'salesforce', 'bidirectional', '*/15 * * * *', false, 2,
     '{"source_entity":"Contact","target_table":"contacts","upsert_key":"external_id"}'),
    ('Salesforce: Opportunities', 'salesforce', 'bidirectional', '*/15 * * * *', false, 2,
     '{"source_entity":"Opportunity","target_table":"crm_deals","upsert_key":"external_id"}'),
    ('HubSpot: Companies', 'hubspot', 'bidirectional', '*/15 * * * *', false, 2,
     '{"source_entity":"companies","target_table":"crm_accounts","upsert_key":"external_id"}'),
    ('HubSpot: Contacts', 'hubspot', 'bidirectional', '*/15 * * * *', false, 2,
     '{"source_entity":"contacts","target_table":"contacts","upsert_key":"external_id"}'),
    ('HubSpot: Deals', 'hubspot', 'bidirectional', '*/15 * * * *', false, 2,
     '{"source_entity":"deals","target_table":"crm_deals","upsert_key":"external_id"}'),
    ('Exchange: Mail auto-log', 'exchange', 'pull', '*/5 * * * *', false, 1,
     '{"source_entity":"messages","target_table":"email_messages","auto_link_to_crm":true}'),
    ('Gmail: Mail auto-log', 'gmail', 'pull', '*/5 * * * *', false, 1,
     '{"source_entity":"messages","target_table":"email_messages","auto_link_to_crm":true}')
ON CONFLICT DO NOTHING;

-- External-id map — links CRM records to their counterpart in external systems
CREATE TABLE IF NOT EXISTS crm_external_ids (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    integration_id  int NOT NULL REFERENCES integrations(id) ON DELETE CASCADE,
    entity_type     text NOT NULL,              -- account, contact, deal, lead
    entity_id       int NOT NULL,
    external_id     text NOT NULL,
    external_url    text,
    last_synced_at  timestamptz,
    sync_status     text DEFAULT 'ok',          -- ok, failed, conflict
    sync_hash       text,                       -- detect drift
    UNIQUE(integration_id, entity_type, entity_id),
    UNIQUE(integration_id, external_id)
);

CREATE INDEX IF NOT EXISTS idx_external_ids_entity ON crm_external_ids(entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_external_ids_external ON crm_external_ids(external_id);

-- Conflict resolution log
CREATE TABLE IF NOT EXISTS crm_sync_conflicts (
    id              bigserial PRIMARY KEY,
    external_id_ref int REFERENCES crm_external_ids(id) ON DELETE CASCADE,
    entity_type     text NOT NULL,
    entity_id       int,
    field_name      text NOT NULL,
    local_value     text,
    remote_value    text,
    resolution      text DEFAULT 'pending',     -- pending, local_wins, remote_wins, merged
    resolved_by     int REFERENCES app_users(id),
    resolved_at     timestamptz,
    detected_at     timestamptz NOT NULL DEFAULT now()
);

-- ─── Document Management (Phase 28) ─────────────────────────────────────────

CREATE TABLE IF NOT EXISTS crm_documents (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    entity_type     text NOT NULL,              -- account, contact, deal, quote, lead
    entity_id       int NOT NULL,
    file_id         int,                        -- references files table
    document_type   text NOT NULL,              -- proposal, contract, nda, invoice, other
    name            text NOT NULL,
    version         int NOT NULL DEFAULT 1,
    status          text NOT NULL DEFAULT 'draft',  -- draft, review, final, signed, archived
    approved_by     int REFERENCES app_users(id),
    approved_at     timestamptz,
    signed_at       timestamptz,
    signed_by_name  text,                       -- captured from DocuSign/Adobe
    signature_provider text,                    -- docusign, adobe_sign, manual
    signature_envelope_id text,                 -- external signature service ID
    tags            text[] DEFAULT '{}',
    metadata        jsonb DEFAULT '{}',
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_crm_documents_entity ON crm_documents(entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_crm_documents_type ON crm_documents(document_type);
CREATE INDEX IF NOT EXISTS idx_crm_documents_status ON crm_documents(status);

-- Document templates (generate contracts, NDAs, invoices from templates)
CREATE TABLE IF NOT EXISTS crm_document_templates (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    template_type   text NOT NULL,              -- proposal, contract, nda, invoice
    category        text,
    subject         text,
    body_html       text NOT NULL,              -- rich text with merge fields
    variables       text[] DEFAULT '{}',
    is_default      boolean NOT NULL DEFAULT false,
    is_active       boolean NOT NULL DEFAULT true,
    version         int NOT NULL DEFAULT 1,
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_doc_templates_type ON crm_document_templates(template_type);

-- Document approval workflow
CREATE TABLE IF NOT EXISTS crm_document_approvals (
    id              serial PRIMARY KEY,
    document_id     int NOT NULL REFERENCES crm_documents(id) ON DELETE CASCADE,
    approver_id     int NOT NULL REFERENCES app_users(id),
    status          text NOT NULL DEFAULT 'pending',  -- pending, approved, rejected
    comments        text,
    responded_at    timestamptz,
    sort_order      int NOT NULL DEFAULT 0,     -- for sequential approvals
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_doc_approvals_doc ON crm_document_approvals(document_id);
