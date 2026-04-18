-- =============================================================================
-- 103 — Networking engine Phase 10: DHCP relay targets
--
-- A small M:N table linking client VLANs to the DHCP server IPs the
-- switch should forward requests to. Needed by config generation so
-- the PicOS renderer can emit `set protocols dhcp relay interface
-- vlan-N dhcp-server-address X` lines without hard-coding the
-- customer's 10.11.120.10 / .11 pair.
--
-- Design notes:
--
--   - One row per (VLAN, server_ip). Multiple rows per VLAN when a
--     tenant has redundant DHCP servers (Immunocore runs two for
--     every client VLAN — hence the `priority` column, lowest-first).
--
--   - `server_ip inet` is the source of truth; `ip_address_id uuid`
--     optionally links to the `net.ip_address` row that owns the
--     address (if the server IP is already modelled under
--     `assigned_to_type = 'Server'`). Keeping the inet separate means
--     tenants can seed DHCP targets for IPs that aren't yet in
--     ip_address (e.g. external DHCP appliances outside the building
--     inventory) without forcing them to model those IPs first.
--
--   - Same 17-column universal base as every other net.* table
--     (organization_id, status / lock_state, created/updated/deleted
--     audit, notes, tags, external_refs, version) so the standard
--     lock + soft-delete + audit patterns apply.
--
-- Scope: schema-only. Tenants populate rows via CRUD API or seed
-- migration. No Immunocore seed in this migration — that lands
-- separately when the existing `public.dhcp_relay` rows get imported.
-- =============================================================================

CREATE TABLE IF NOT EXISTS net.dhcp_relay_target (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    vlan_id           uuid               NOT NULL REFERENCES net.vlan(id) ON DELETE CASCADE,
    server_ip         inet               NOT NULL,
    -- Optional FK to the net.ip_address row that models this server
    -- IP (if any). SET NULL on delete so a tenant pruning their
    -- ip_address catalog doesn't cascade through and delete live
    -- relay config.
    ip_address_id     uuid               REFERENCES net.ip_address(id) ON DELETE SET NULL,
    -- Priority (lowest-first). Same VLAN with priorities 10 / 20
    -- means "try server_ip@10 first, fall back to server_ip@20".
    -- Rendered in priority order so the generated config expresses
    -- the tenant's intent.
    priority          int                NOT NULL DEFAULT 10,
    status            net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state        net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason       text,
    locked_by         int,
    locked_at         timestamptz,
    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        int,
    deleted_at        timestamptz,
    deleted_by        int,
    notes             text,
    tags              jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs     jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version           int                NOT NULL DEFAULT 1,
    -- One target-per-vlan-per-server pair. Two rows for the same
    -- (vlan_id, server_ip) with different priorities makes no sense.
    UNIQUE (organization_id, vlan_id, server_ip)
);

CREATE INDEX IF NOT EXISTS ix_dhcp_relay_vlan
    ON net.dhcp_relay_target (organization_id, vlan_id, priority)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_dhcp_relay_ip_address
    ON net.dhcp_relay_target (organization_id, ip_address_id)
    WHERE deleted_at IS NULL AND ip_address_id IS NOT NULL;

COMMENT ON TABLE net.dhcp_relay_target IS
    'M:N link between client VLANs and the DHCP server IPs the switch should forward to. Emitted by the PicOS renderer as ''set protocols dhcp relay interface vlan-N dhcp-server-address X'' lines; one row per (vlan, server_ip) pair.';
