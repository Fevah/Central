-- =============================================================================
-- Stage 5.7-5.8: Custom Objects + Field-Level Security
-- =============================================================================

-- ─── Custom Objects — user-defined entities ─────────────────────────────────
-- Approach: metadata-driven with jsonb storage for values.
-- Simpler than dynamic schemas per tenant; scales to thousands of custom objects.

CREATE TABLE IF NOT EXISTS custom_entities (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    api_name        text NOT NULL,                    -- entity name for API: custom_project
    label           text NOT NULL,                    -- display name: "Custom Project"
    plural_label    text NOT NULL,                    -- "Custom Projects"
    description     text,
    icon            text,                              -- icon name/emoji
    color           text DEFAULT '#5B8AF5',
    is_active       boolean NOT NULL DEFAULT true,
    show_in_menu    boolean NOT NULL DEFAULT true,
    record_name_format text DEFAULT '{{name}}',       -- template for record display
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, api_name)
);

-- Field metadata
CREATE TABLE IF NOT EXISTS custom_fields (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    entity_type     text NOT NULL,                    -- custom entity api_name OR built-in (deals/contacts/accounts)
    api_name        text NOT NULL,                    -- field name: project_deadline
    label           text NOT NULL,                    -- display: "Project Deadline"
    field_type      text NOT NULL,                    -- text, number, date, datetime, bool, picklist, multipick, lookup, url, email, phone, currency, percent, richtext, file
    is_required     boolean NOT NULL DEFAULT false,
    is_unique       boolean NOT NULL DEFAULT false,
    is_external_id  boolean NOT NULL DEFAULT false,
    default_value   text,
    help_text       text,
    placeholder     text,
    -- Type-specific config
    max_length      int,                               -- for text
    precision_value int,                               -- for number/currency
    scale           int,
    picklist_values text[],                            -- for picklist
    picklist_restricted boolean DEFAULT false,
    lookup_entity   text,                              -- for lookup
    -- Validation
    validation_regex text,
    validation_message text,
    -- Organization
    section         text,
    sort_order      int NOT NULL DEFAULT 100,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, entity_type, api_name)
);

CREATE INDEX IF NOT EXISTS idx_custom_fields_entity ON custom_fields(entity_type) WHERE is_active = true;

-- Custom entity records (jsonb values)
CREATE TABLE IF NOT EXISTS custom_entity_records (
    id              bigserial PRIMARY KEY,
    tenant_id       uuid,
    entity_id       int NOT NULL REFERENCES custom_entities(id) ON DELETE CASCADE,
    values          jsonb NOT NULL DEFAULT '{}',      -- {field_api_name: value}
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    is_deleted      boolean DEFAULT false
);

CREATE INDEX IF NOT EXISTS idx_custom_records_entity ON custom_entity_records(entity_id);
CREATE INDEX IF NOT EXISTS idx_custom_records_values ON custom_entity_records USING gin(values);

-- Custom field values on built-in entities (deals/contacts/accounts/etc)
CREATE TABLE IF NOT EXISTS custom_field_values (
    id              bigserial PRIMARY KEY,
    tenant_id       uuid,
    entity_type     text NOT NULL,                    -- deals, accounts, contacts, etc.
    entity_id       int NOT NULL,
    field_id        int NOT NULL REFERENCES custom_fields(id) ON DELETE CASCADE,
    -- Multiple value columns — only one populated per row based on field type
    text_value      text,
    number_value    numeric(20,6),
    date_value      date,
    datetime_value  timestamptz,
    bool_value      boolean,
    json_value      jsonb,
    UNIQUE(entity_type, entity_id, field_id)
);

CREATE INDEX IF NOT EXISTS idx_custom_field_values_entity ON custom_field_values(entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_custom_field_values_field ON custom_field_values(field_id);

-- Relationships between custom entities (or custom → built-in)
CREATE TABLE IF NOT EXISTS custom_relationships (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    parent_entity   text NOT NULL,                    -- custom_entity api_name or built-in
    child_entity    text NOT NULL,
    relationship_name text NOT NULL,                  -- line_items, contacts, etc
    cardinality     text NOT NULL DEFAULT 'one_to_many',  -- one_to_one, one_to_many, many_to_many
    cascade_delete  boolean NOT NULL DEFAULT false,
    is_active       boolean NOT NULL DEFAULT true,
    UNIQUE(tenant_id, parent_entity, child_entity, relationship_name)
);

-- ─── Field-Level Security ───────────────────────────────────────────────────
-- Per role + per entity + per field: read / write / hidden

CREATE TABLE IF NOT EXISTS field_permissions (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    role_name       text NOT NULL,                    -- Admin, Manager, Viewer, custom
    entity_type     text NOT NULL,                    -- deals, contacts, custom_project, etc.
    field_name      text NOT NULL,                    -- standard field or custom field api_name
    permission      text NOT NULL DEFAULT 'read',    -- hidden, read, write
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, role_name, entity_type, field_name)
);

CREATE INDEX IF NOT EXISTS idx_field_permissions_role ON field_permissions(role_name, entity_type);

-- Helper function: get effective field permission for a user/entity/field
CREATE OR REPLACE FUNCTION get_field_permission(
    p_username text,
    p_entity_type text,
    p_field_name text
) RETURNS text AS $$
DECLARE
    perm text;
BEGIN
    SELECT fp.permission INTO perm
    FROM field_permissions fp
    JOIN app_users u ON u.role = fp.role_name
    WHERE u.username = p_username
      AND fp.entity_type = p_entity_type
      AND fp.field_name = p_field_name
    LIMIT 1;
    RETURN COALESCE(perm, 'write');  -- default = write (backward compat)
END;
$$ LANGUAGE plpgsql STABLE;
