-- Migration 059: File management — versioned file storage with metadata
-- Ported from TotalLink's Repository module (chunked upload, versioning, MD5 integrity).

CREATE TABLE IF NOT EXISTS file_store (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    filename        varchar(256) NOT NULL,
    description     text DEFAULT '',
    mime_type       varchar(128) DEFAULT 'application/octet-stream',
    file_size       bigint,
    entity_type     varchar(64),                     -- 'device', 'switch', 'ticket', 'task', etc.
    entity_id       varchar(64),                     -- FK to the entity this file is attached to
    uploaded_by     integer REFERENCES app_users(id),
    md5_hash        varchar(32),
    tags            text DEFAULT '',
    is_deleted      boolean DEFAULT false,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS file_versions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    file_id         uuid NOT NULL REFERENCES file_store(id) ON DELETE CASCADE,
    version_number  integer NOT NULL DEFAULT 1,
    file_data       bytea,                           -- actual file content (for small files <10MB)
    storage_path    text,                            -- filesystem path (for large files)
    file_size       bigint,
    md5_hash        varchar(32),
    uploaded_by     integer REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(file_id, version_number)
);

CREATE INDEX IF NOT EXISTS idx_file_store_entity ON file_store(entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_file_versions_file ON file_versions(file_id);

-- pg_notify
DO $$ BEGIN
    EXECUTE 'DROP TRIGGER IF EXISTS trg_notify_file_store ON file_store; CREATE TRIGGER trg_notify_file_store AFTER INSERT OR UPDATE OR DELETE ON file_store FOR EACH ROW EXECUTE FUNCTION notify_data_change();';
END; $$;
