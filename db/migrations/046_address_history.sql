-- Address change history + default address management

CREATE TABLE IF NOT EXISTS address_history (
    id              bigserial PRIMARY KEY,
    address_id      int,               -- NULL if address was hard-deleted
    entity_type     text NOT NULL,
    entity_id       int NOT NULL,
    action          text NOT NULL,     -- created, updated, deleted, marked_primary
    old_values      jsonb,
    new_values      jsonb,
    changed_by      int REFERENCES app_users(id),
    changed_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_address_history_entity ON address_history(entity_type, entity_id, changed_at DESC);
CREATE INDEX IF NOT EXISTS idx_address_history_address ON address_history(address_id);

-- Trigger: log address changes automatically
CREATE OR REPLACE FUNCTION log_address_change() RETURNS trigger AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO address_history(address_id, entity_type, entity_id, action, new_values)
        VALUES (NEW.id, NEW.entity_type, NEW.entity_id, 'created', to_jsonb(NEW));
        RETURN NEW;
    ELSIF TG_OP = 'UPDATE' THEN
        INSERT INTO address_history(address_id, entity_type, entity_id, action, old_values, new_values)
        VALUES (NEW.id, NEW.entity_type, NEW.entity_id, 'updated', to_jsonb(OLD), to_jsonb(NEW));
        RETURN NEW;
    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO address_history(address_id, entity_type, entity_id, action, old_values)
        VALUES (OLD.id, OLD.entity_type, OLD.entity_id, 'deleted', to_jsonb(OLD));
        RETURN OLD;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_addresses_history ON addresses;
CREATE TRIGGER trg_addresses_history
    AFTER INSERT OR UPDATE OR DELETE ON addresses
    FOR EACH ROW EXECUTE FUNCTION log_address_change();

-- Default address per user (user preference)
ALTER TABLE user_profiles ADD COLUMN IF NOT EXISTS default_address_id int REFERENCES addresses(id) ON DELETE SET NULL;
