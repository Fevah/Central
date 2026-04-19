-- =============================================================================
-- 100 — Lifecycle lock-state enforcement (Phase 8f).
--
-- The net.lock_state column has existed on every entity since Phase 1
-- (Open / SoftLock / HardLock / Immutable) but nothing reads it. This
-- migration wires a trigger function that actually enforces the semantics:
--
--   Open       — no restrictions (explicit default; cheap no-op in the trigger)
--   SoftLock   — no restrictions today; reserved for a future "advisory
--                warning" UX on edit. Treated as Open by this trigger.
--   HardLock   — mutations to business columns are BLOCKED. Only the
--                lock-management columns (lock_state, lock_reason,
--                locked_by, locked_at, updated_at/by, version) may change,
--                which is what lets an admin *unlock* the row. DELETE
--                blocked.
--   Immutable  — terminal. NOTHING may change, not even lock_state.
--                DELETE blocked.
--
-- Enforced as a BEFORE UPDATE / BEFORE DELETE trigger so direct SQL hits
-- the same gate as the Rust service layer — defence in depth.
--
-- Attached to the five numbering tables that matter most for production
-- stability: asn_allocation / vlan / mlag_domain / subnet / ip_address.
-- Hierarchy + devices + servers have the same column shape so can be
-- opted in later by calling the helper with their table name.
--
-- Idempotent; safe to re-run.
-- =============================================================================

BEGIN;

-- ─── Trigger function ────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION net.enforce_lock_state()
RETURNS trigger AS $$
DECLARE
    lock_management_cols CONSTANT text[] := ARRAY[
        'lock_state', 'lock_reason', 'locked_by', 'locked_at',
        'updated_at', 'updated_by', 'version'
    ];
    old_payload jsonb;
    new_payload jsonb;
    col text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        IF OLD.lock_state IN ('HardLock', 'Immutable') THEN
            RAISE EXCEPTION
                'Cannot DELETE row in lock_state % on table %',
                OLD.lock_state, TG_TABLE_NAME
                USING ERRCODE = 'check_violation',
                      HINT = 'Unlock the row (set lock_state to Open) before deleting.';
        END IF;
        RETURN OLD;
    END IF;

    -- UPDATE path.
    IF OLD.lock_state = 'Immutable' THEN
        -- Every field is frozen. Not even lock_state can move off Immutable.
        RAISE EXCEPTION
            'Row on table % is Immutable and cannot be modified',
            TG_TABLE_NAME
            USING ERRCODE = 'check_violation',
                  HINT = 'Immutable rows are terminal by design. Soft-delete instead if the row needs to disappear.';
    END IF;

    IF OLD.lock_state = 'HardLock' THEN
        -- Build sanitised jsonb payloads with the lock-management columns
        -- stripped out, so the only diff that survives is "did a business
        -- field change?". If yes, we block.
        old_payload := to_jsonb(OLD);
        new_payload := to_jsonb(NEW);
        FOREACH col IN ARRAY lock_management_cols LOOP
            old_payload := old_payload - col;
            new_payload := new_payload - col;
        END LOOP;

        IF old_payload IS DISTINCT FROM new_payload THEN
            RAISE EXCEPTION
                'Row on table % is HardLock and only lock-management columns may change',
                TG_TABLE_NAME
                USING ERRCODE = 'check_violation',
                      HINT = 'Set lock_state to Open first, make the edit, then re-apply the lock.';
        END IF;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION net.enforce_lock_state() IS
    'BEFORE UPDATE / DELETE trigger that enforces net.lock_state semantics: '
    'HardLock permits only lock-management column changes; Immutable permits '
    'nothing. Emits PG errcode check_violation (23514) so the service layer '
    'can map to HTTP 409 cleanly.';

-- ─── Helper to attach the trigger to a table ─────────────────────────────
-- Shields the attach points from having to open-code DROP IF EXISTS +
-- CREATE every time. Takes an unqualified table name in the net schema.
CREATE OR REPLACE FUNCTION net.attach_lock_enforcement(tbl text)
RETURNS void AS $$
BEGIN
    EXECUTE format(
        'DROP TRIGGER IF EXISTS trg_enforce_lock_state ON net.%I;',
        tbl);
    EXECUTE format(
        'CREATE TRIGGER trg_enforce_lock_state '
        'BEFORE UPDATE OR DELETE ON net.%I '
        'FOR EACH ROW EXECUTE FUNCTION net.enforce_lock_state();',
        tbl);
END;
$$ LANGUAGE plpgsql;

-- ─── Attach to the five numbering tables ─────────────────────────────────
SELECT net.attach_lock_enforcement('asn_allocation');
SELECT net.attach_lock_enforcement('vlan');
SELECT net.attach_lock_enforcement('mlag_domain');
SELECT net.attach_lock_enforcement('subnet');
SELECT net.attach_lock_enforcement('ip_address');

-- ─── schema_versions record ──────────────────────────────────────────────
INSERT INTO public.schema_versions (version_number, description)
VALUES (100, 'Networking Phase 8f: lock_state enforcement trigger')
ON CONFLICT (version_number) DO NOTHING;

COMMIT;
