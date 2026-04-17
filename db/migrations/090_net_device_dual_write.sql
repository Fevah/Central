-- =============================================================================
-- 090 — Networking engine Phase 4e: dual-write trigger switches ↔ device
--
-- During the Phase-4-through-Phase-11 transition the legacy
-- public.switches table and the new net.device table must stay in lock
-- step: WPF panels still reading from public.switches (Switches grid,
-- config generation) must see writes that came in through the new API,
-- and vice versa.
--
-- Approach: two triggers, one per direction, with a per-transaction
-- session variable as a reentrancy guard. When trigger A fires and
-- writes to the other table, it sets the guard; trigger B sees the
-- guard and skips its mirror. Session-local
-- (current_setting(…, true)) rather than advisory lock so multiple
-- transactions can mirror concurrently without contending.
--
-- Fields mirrored both directions:
--   hostname, hardware_model, serial_number, mac_address,
--   management_ip, ssh_username, ssh_port, management_vrf,
--   inband_enabled, last_ping_at, last_ping_ok, last_ping_ms,
--   last_ssh_at, last_ssh_ok
--
-- Fields that only live in one side (legacy-only: source_file,
-- ssh_password, ssh_override_ip, snmp_*, uptime, picos_version→firmware_version
-- map; net-only: device_role_id, building_id, asn_allocation_id, tags)
-- aren't mirrored — they'd be lossy. Those stay as the source-of-truth
-- on their respective tables.
--
-- Links established in 4d via net.device.legacy_switch_id =
-- public.switches.id. New devices created after 4e must set that FK
-- themselves (future repo method / API will do this).
--
-- Rollback: drop the two triggers and the two functions. The underlying
-- rows stay.
-- =============================================================================

-- ─── Direction 1: net.device -> public.switches ───────────────────────────
CREATE OR REPLACE FUNCTION net.sync_device_to_switch()
RETURNS TRIGGER AS $body$
BEGIN
    -- If a switches-side trigger is driving us, don't loop back.
    IF current_setting('net.in_dual_write', true) = 'on' THEN
        RETURN NEW;
    END IF;
    IF NEW.legacy_switch_id IS NULL THEN
        -- Device has no legacy mirror (post-cutover row) — nothing to do.
        RETURN NEW;
    END IF;

    PERFORM set_config('net.in_dual_write', 'on', true);

    UPDATE public.switches SET
        hostname         = NEW.hostname,
        hardware_model   = NEW.hardware_model,
        serial_number    = NEW.serial_number,
        mac_address      = NEW.mac_address::text,
        picos_version    = NEW.firmware_version,
        management_ip    = NEW.management_ip,
        ssh_username     = NEW.ssh_username,
        ssh_port         = NEW.ssh_port,
        management_vrf   = NEW.management_vrf,
        inband_enabled   = NEW.inband_enabled,
        last_ping_at     = NEW.last_ping_at,
        last_ping_ok     = NEW.last_ping_ok,
        last_ping_ms     = NEW.last_ping_ms,
        last_ssh_at      = NEW.last_ssh_at,
        last_ssh_ok      = NEW.last_ssh_ok,
        updated_at       = now()
    WHERE id = NEW.legacy_switch_id;

    PERFORM set_config('net.in_dual_write', 'off', true);
    RETURN NEW;
END
$body$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_sync_device_to_switch ON net.device;
CREATE TRIGGER trg_sync_device_to_switch
    AFTER INSERT OR UPDATE ON net.device
    FOR EACH ROW EXECUTE FUNCTION net.sync_device_to_switch();

-- ─── Direction 2: public.switches -> net.device ───────────────────────────
CREATE OR REPLACE FUNCTION net.sync_switch_to_device()
RETURNS TRIGGER AS $body$
DECLARE
    v_count int;
BEGIN
    IF current_setting('net.in_dual_write', true) = 'on' THEN
        RETURN NEW;
    END IF;

    PERFORM set_config('net.in_dual_write', 'on', true);

    -- Update any net.device whose legacy_switch_id points at this row.
    -- Guid match in both directions — net.device.legacy_switch_id was
    -- set by migration 089 or by the Phase-4c API.
    UPDATE net.device SET
        hostname         = NEW.hostname,
        hardware_model   = NEW.hardware_model,
        serial_number    = NEW.serial_number,
        mac_address      = CASE
            WHEN NEW.mac_address ~ '^([0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}$'
            THEN NEW.mac_address::macaddr ELSE NULL END,
        firmware_version = NEW.picos_version,
        management_ip    = NEW.management_ip,
        ssh_username     = NEW.ssh_username,
        ssh_port         = NEW.ssh_port,
        management_vrf   = COALESCE(NEW.management_vrf, false),
        inband_enabled   = COALESCE(NEW.inband_enabled, false),
        last_ping_at     = NEW.last_ping_at,
        last_ping_ok     = NEW.last_ping_ok,
        last_ping_ms     = NEW.last_ping_ms,
        last_ssh_at      = NEW.last_ssh_at,
        last_ssh_ok      = NEW.last_ssh_ok,
        updated_at       = now()
    WHERE legacy_switch_id = NEW.id AND deleted_at IS NULL;

    GET DIAGNOSTICS v_count = ROW_COUNT;

    -- If there's no matching net.device row yet, don't auto-create one
    -- here — that would bypass the building/role/ASN linkage that
    -- migration 089 does carefully. New switches go through the
    -- import path, not the trigger.

    PERFORM set_config('net.in_dual_write', 'off', true);
    RETURN NEW;
END
$body$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_sync_switch_to_device ON public.switches;
CREATE TRIGGER trg_sync_switch_to_device
    AFTER INSERT OR UPDATE ON public.switches
    FOR EACH ROW EXECUTE FUNCTION net.sync_switch_to_device();
