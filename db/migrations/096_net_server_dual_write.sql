-- =============================================================================
-- 096 — Networking engine Phase 6f: dual-write trigger public.servers ↔ net.server
--
-- Keeps the legacy public.servers table and net.server in lock step
-- during the transition window (until Phase 11 drops the legacy
-- table). Same reentrancy-guard pattern as migration 090
-- (switches ↔ device): a txn-scoped session variable prevents the
-- trigger-fires-trigger-fires-trigger loop.
--
-- Fields mirrored both directions (server-level only):
--   hostname ↔ server_name
--   status   ↔ status (net.entity_status text ↔ legacy varchar)
--
-- NOT mirrored:
--   building → net.building_id    requires FK lookup; legacy sees the
--                                  authoritative net.building_code
--                                  string, and operators editing
--                                  net.server.building_id directly
--                                  don't need to propagate a guessed
--                                  string back.
--   server_as, loopback_ip        same — FK-backed on net.server, flat
--                                  string on legacy. Resolving across
--                                  makes the trigger lossy.
--   nicN_*                        legacy NIC columns stay on
--                                  public.servers; net.server_nic is
--                                  the authoritative source for any
--                                  NIC data going forward.
--
-- Trigger-created rows are NOT auto-created. A brand-new public.servers
-- row without a matching net.server (no legacy_server_id link) is
-- deliberately left alone — new servers go through the 095 import path
-- or the 6e ServerCreationService, which carefully resolve FKs that a
-- dumb trigger would null out.
-- =============================================================================

-- ─── Direction 1: net.server -> public.servers ────────────────────────────
CREATE OR REPLACE FUNCTION net.sync_server_to_legacy()
RETURNS TRIGGER AS $body$
BEGIN
    IF current_setting('net.in_dual_write', true) = 'on' THEN
        RETURN NEW;
    END IF;
    IF NEW.legacy_server_id IS NULL THEN
        -- Post-cutover server with no legacy mirror — nothing to do.
        RETURN NEW;
    END IF;

    PERFORM set_config('net.in_dual_write', 'on', true);

    UPDATE public.servers SET
        server_name = NEW.hostname,
        status      = NEW.status::text
    WHERE id = NEW.legacy_server_id;

    PERFORM set_config('net.in_dual_write', 'off', true);
    RETURN NEW;
END
$body$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_sync_server_to_legacy ON net.server;
CREATE TRIGGER trg_sync_server_to_legacy
    AFTER INSERT OR UPDATE ON net.server
    FOR EACH ROW EXECUTE FUNCTION net.sync_server_to_legacy();

-- ─── Direction 2: public.servers -> net.server ────────────────────────────
CREATE OR REPLACE FUNCTION net.sync_legacy_to_server()
RETURNS TRIGGER AS $body$
DECLARE
    v_status net.entity_status;
BEGIN
    IF current_setting('net.in_dual_write', true) = 'on' THEN
        RETURN NEW;
    END IF;

    PERFORM set_config('net.in_dual_write', 'on', true);

    -- Map legacy status string to the net.entity_status enum when
    -- possible; otherwise leave the net.server's status unchanged by
    -- using its current value through a COALESCE dance.
    BEGIN
        v_status := NEW.status::net.entity_status;
    EXCEPTION WHEN OTHERS THEN
        v_status := NULL;
    END;

    IF v_status IS NOT NULL THEN
        UPDATE net.server SET
            hostname = NEW.server_name,
            status   = v_status
        WHERE legacy_server_id = NEW.id AND deleted_at IS NULL;
    ELSE
        UPDATE net.server SET
            hostname = NEW.server_name
        WHERE legacy_server_id = NEW.id AND deleted_at IS NULL;
    END IF;

    PERFORM set_config('net.in_dual_write', 'off', true);
    RETURN NEW;
END
$body$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_sync_legacy_to_server ON public.servers;
CREATE TRIGGER trg_sync_legacy_to_server
    AFTER INSERT OR UPDATE ON public.servers
    FOR EACH ROW EXECUTE FUNCTION net.sync_legacy_to_server();
