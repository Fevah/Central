-- =============================================================================
-- 093 — Networking engine Chunk A: device naming templates
--
-- Closes the naming gap flagged during the Phase-5 review: net.link_type
-- already carries a per-tenant naming_template, but net.device_role
-- didn't — so device hostnames were hand-typed with no tenant-
-- configurable convention. This migration brings device_role to
-- parity with link_type.
--
-- Adds net.device_role.naming_template (varchar 255, default
--   '{building_code}-{role_code}{instance}'), then seeds per-role
--   defaults on the Immunocore catalog so the existing 12 rows carry
--   the expected PicOS convention:
--     Core      -> {building_code}-CORE{instance}        ("MEP-91-CORE02")
--     L1Core    -> {building_code}-L1-CORE{instance}     ("MEP-93-L1-CORE02")
--     L2Core    -> {building_code}-L2-CORE{instance}     ("MEP-96-L2-CORE02")
--     MAN       -> {building_code}-MAN{instance}
--     STOR      -> {building_code}-STOR{instance}
--     SW        -> {building_code}-SW{instance}
--     FW        -> {building_code}-FW{instance}
--     DMZ       -> {building_code}-DMZ{instance}
--     L1SW      -> {building_code}-L1-SW{instance}
--     L2SW      -> {building_code}-L2-SW{instance}
--     Res       -> {building_code}-RES{instance}
--     RES-FW    -> {building_code}-RES-FW{instance}
--
-- The recognised token set is shared with DeviceNamingService:
--   {region_code}, {site_code}, {building_code}, {role_code},
--   {instance}, {rack_code}
--
-- Separators live inside the template string as literal text.
-- Customers that use underscores run a bulk update on this column
-- (a single-shot UPDATE is simpler than a schema-level "separator"
-- field that every template would have to thread through).
--
-- Idempotent: ADD COLUMN IF NOT EXISTS; UPDATE ... WHERE naming_template
-- IS NULL OR empty so re-runs don't overwrite edits operators made.
-- =============================================================================

ALTER TABLE net.device_role
    ADD COLUMN IF NOT EXISTS naming_template varchar(255)
        NOT NULL DEFAULT '{building_code}-{role_code}{instance}';

-- Per-role seed — only fill the column when it's still the generic
-- default. If an operator has already customised a row's template we
-- leave it alone.
DO $$
DECLARE
    t_imm CONSTANT uuid := '00000000-0000-0000-0000-000000000000';
    r     record;
    tpl   varchar(255);
BEGIN
    FOR r IN
        SELECT id, role_code FROM net.device_role
         WHERE organization_id = t_imm AND deleted_at IS NULL
    LOOP
        tpl := CASE r.role_code
            WHEN 'Core'   THEN '{building_code}-CORE{instance}'
            WHEN 'L1Core' THEN '{building_code}-L1-CORE{instance}'
            WHEN 'L2Core' THEN '{building_code}-L2-CORE{instance}'
            WHEN 'MAN'    THEN '{building_code}-MAN{instance}'
            WHEN 'STOR'   THEN '{building_code}-STOR{instance}'
            WHEN 'SW'     THEN '{building_code}-SW{instance}'
            WHEN 'FW'     THEN '{building_code}-FW{instance}'
            WHEN 'DMZ'    THEN '{building_code}-DMZ{instance}'
            WHEN 'L1SW'   THEN '{building_code}-L1-SW{instance}'
            WHEN 'L2SW'   THEN '{building_code}-L2-SW{instance}'
            WHEN 'Res'    THEN '{building_code}-RES{instance}'
            WHEN 'RES-FW' THEN '{building_code}-RES-FW{instance}'
            ELSE NULL
        END;

        IF tpl IS NOT NULL THEN
            UPDATE net.device_role
               SET naming_template = tpl
             WHERE id = r.id
               AND (naming_template IS NULL
                    OR naming_template = '{building_code}-{role_code}{instance}');
        END IF;
    END LOOP;
END $$;
