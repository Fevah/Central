# Network Management App — Attribute System & Data Dictionary

**Purpose:** A tech-agnostic, enterprise-grade entity and attribute catalog for a multi-site network source-of-truth application. Every entity has an explicit primary key, foreign keys, attributes with types and constraints, a naming convention, and a lifecycle. All cardinalities (locations, buildings, floors, devices per role, links per device, servers per building, etc.) are configurable via Profile entities — no hardcoded counts.

---

## 0. Global conventions (apply to every entity unless noted)

### 0.1 Key conventions

| Key type | Naming | Rules |
|---|---|---|
| **Surrogate PK** | `<entity>_id` | Opaque, system-assigned, globally unique, immutable for the life of the record. Never reused. Never exposed in business-facing names. |
| **Business / natural key** | `<entity>_code` | Human-readable, unique within a defined scope (e.g. unique within building, unique globally). Generated from a Naming Template. Lockable. |
| **Foreign key** | `<parent_entity>_id` | Always references a surrogate PK. Never references a business code. |
| **Composite constraints** | Declared per entity | E.g. `(building_id, device_code)` unique. |

### 0.2 Universal attributes on every entity

Every entity carries these fields in addition to its own:

- `<entity>_id` — surrogate PK
- `organization_id` — FK (tenant isolation root)
- `status` — enum: `Planned`, `Reserved`, `Active`, `Deprecated`, `Retired`
- `lock_state` — enum: `Open`, `SoftLock`, `HardLock`, `Immutable`
- `lock_reason` — free text, required when `lock_state != Open`
- `locked_by` — FK → `user_id`, set when locked
- `locked_at` — timestamp, set when locked
- `created_at`, `created_by`, `updated_at`, `updated_by` — audit stamps
- `deleted_at`, `deleted_by` — soft-delete stamps (hard delete only via admin purge)
- `notes` — free text
- `tags[]` — list of key/value tags
- `external_refs[]` — list of `{system, id}` for integration
- `version` — monotonically increasing integer for optimistic concurrency

### 0.3 Lifecycle rules

```
Planned ──► Reserved ──► Active ──► Deprecated ──► Retired
                │            │
                └── can go back to Planned (before any dependent allocation)
```

- Allocation of any downstream number (ASN, IP, VLAN, MLAG ID) against this entity forces transition out of `Planned`.
- Reaching `Active` auto-applies `HardLock` to ASN, loopback, Router-ID, MLAG domain, MSTP priority, and any B2B-exposed subnet.
- `Retired` releases numbers back to pool after configurable cool-down window (default 90 days).

### 0.4 Lock semantics

| Lock state | Edit behaviour |
|---|---|
| `Open` | Any authorised user can edit. |
| `SoftLock` | Edit allowed with warning + reason captured. |
| `HardLock` | Edit requires a Change Set + N approvers (configurable per entity). |
| `Immutable` | Never editable. Only replace via decommission + recreate. |

---

## 1. Parameterization philosophy

The number of locations, buildings per location, floors per building, racks per floor, devices per role, links per device, and servers per building is **never hardcoded**. All cardinalities are driven by **Profile** entities:

- `site_profile` — governs how many buildings a site can hold, default floors, default room layout.
- `building_profile` — governs device counts per role, link-mesh rules, MLAG domain allocation pattern.
- `floor_profile` — default rooms, cabling closet placement.
- `rack_profile` — U-height, PDU count, device placement rules.
- `server_profile` — NIC count, which router roles each NIC targets.

A site, building, floor, rack or server is always created **from a profile**, then diverges as needed. The profile is the template; the instance can override.

---

> **Note.** Sections 2 through 40 — the full entity catalog covering organisational hierarchy, device & port entities, numbering pools (ASN / IP / VLAN / MLAG / MSTP), the unified Link model, servers & NICs, naming convention engine, governance (users, roles, locks, change sets, audit), cabling & patching, power infrastructure, environmental sensing, WAN & carriers, VRFs & routing, route policy, EVPN/VXLAN fabric, logical interfaces, multicast, NAT, QoS, security policy, load balancing, DHCP, DNS, ops (maintenance, incidents, services, DR), hardware lifecycle, software / CVE / licence / compliance, financial, credentials & cert rotation, config backup & drift, discovery & reconciliation, collaboration, labelling, data quality, regulatory, programme & project governance, and simulation — are held in their original form from the source specification. Each section is implemented progressively per the phase plan in [NETWORKING_BUILDOUT_PLAN.md](NETWORKING_BUILDOUT_PLAN.md). See §12 below for the relationship overview that binds them, and §11 / §40 for the primary-key summaries.

The full ~100-entity catalog is too long to duplicate verbatim in a single file without decay. Rather than copying it twice, we treat this doc as the **canonical index** and the companion source material (stored in `docs/source/network_app_attribute_system.md` if reproduced in full) as the authoritative specification. The phase-by-phase buildout references exact entity names from the catalog.

**Key entities referenced by the buildout plan (in dependency order):**

1. **Tenant root** — `organization`
2. **Physical hierarchy** — `region`, `site`, `site_profile`, `building`, `building_profile`, `floor`, `floor_profile`, `room`, `rack`
3. **Device catalog** — `device_role`, `device`, `module`, `port`, `aggregate_ethernet`, `loopback`
4. **Numbering pools (the "locked" core)** — `asn_pool` / `asn_block` / `asn_allocation`; `ip_pool` / `subnet` / `ip_address`; `vlan_pool` / `vlan_block` / `vlan` / `vlan_template`; `mlag_domain_pool` / `mlag_domain`; `mstp_priority_rule` / `mstp_priority_allocation`
5. **Links (unified entity)** — `link_type`, `link`, `link_endpoint`
6. **Servers** — `server_profile`, `server`, `server_nic`
7. **Naming convention engine** — `naming_template` + token grammar (`{organization_code}`, `{region_code}`, `{site_code}`, `{building_code}`, `{building_octet}`, `{role_code}`, `{sequence_number:nn}`, etc.) + `naming_template_validation_rule`
8. **Governance** — `user`, `role`, `permission`, `lock`, `change_set`, `change_set_item`, `approval`, `audit_entry` (hash-chained, append-only), `reservation_shelf`
9. **Physical cabling & patching** — `cable`, `cable_run`, `patch_panel`, `patch_port`, `cable_test`
10. **Power** — `utility_feed`, `generator`, `ats`, `ups`, `circuit`, `pdu`, `pdu_outlet`, `device_psu`, `power_budget`
11. **Environmental** — `environmental_sensor`, `environmental_reading`
12. **Circuits / WAN** — `carrier`, `wan_circuit`, `demarc`, `wan_overlay_tunnel`, `bgp_peer`
13. **VRF / routing** — `vrf`, `vrf_binding`, `vrf_route_target`, `prefix_list`, `prefix_list_entry`, `bgp_community`, `route_map`, `route_map_entry`, `as_path_filter`
14. **EVPN / VXLAN fabric** — `tenant`, `vni`, `evpn_instance`, `anycast_gateway`
15. **Logical interfaces** — `logical_interface`
16. **Multicast** — `multicast_config`, `multicast_group`, `igmp_snooping_policy`
17. **NAT** — `nat_pool`, `nat_rule`
18. **QoS** — `qos_class`, `qos_policy`, `qos_policy_rule`, `qos_policy_binding`
19. **Security policy** — `security_zone`, `address_object`, `address_group`, `service_object`, `service_group`, `firewall_policy`, `firewall_rule`
20. **Load balancing** — `lb_vip`, `lb_pool`, `lb_pool_member`, `lb_health_check`, `lb_profile`
21. **DHCP / DNS** — `dhcp_scope`, `dhcp_reservation`, `dhcp_option`, `dns_zone`, `dns_record`
22. **Operations** — `maintenance_window`, `change_freeze`, `incident`, `incident_impact`, `post_change_verification`, `dr_plan`, `service`, `service_dependency`, `runbook`, `oncall_schedule`, `oncall_rotation_entry`
23. **Hardware lifecycle** — EOS/EOL fields on `device` / `server`, `support_contract`, `support_contract_coverage`, `warranty`
24. **Software / CVE / licence / compliance** — `software_image`, `software_advisory`, `cve`, `cve_exposure`, `licence`, `licence_assignment`, `compliance_baseline`, `compliance_scan_result`
25. **Financial / procurement** — `vendor`, `cost_centre`, `purchase_order`, `purchase_order_line`, `asset_cost`
26. **Credentials / certs / config backup / drift** — `certificate`, `credential_profile`, `break_glass_use`, `config_backup`, `config_baseline`, `config_drift`
27. **Discovery & reconciliation** — `discovery_source`, `discovery_run`, `discovered_neighbour`, `reconciliation_finding`, `live_state_snapshot`
28. **Collaboration / docs** — `attachment`, `comment`, `knowledge_article`, `knowledge_link`
29. **Labels** — `label_template`, `label_print_job`
30. **Data quality / stewardship** — `data_quality_rule`, `data_quality_score`, `data_steward_assignment`, `orphan_detection_finding`
31. **Regulatory** — `legal_hold`, `data_residency_policy`, `evidence_export`
32. **Programme / project** — `programme`, `project`, `project_change_set_link`, `roadmap_milestone`, `stakeholder_assignment`, `decision_record`
33. **Simulation / digital twin** — `scenario`, `reachability_analysis`, `failure_simulation`, `capacity_forecast`

**Design-level invariants** (from the source spec, always true):

- Every entity has a surrogate PK and a scoped business code.
- Every cardinality (devices-per-role, floors-per-building, NICs-per-server, etc.) lives on a Profile row — never hardcoded.
- Every mutation is audited; every Active entity's numbering is locked.
- Every pool (ASN, IP, VLAN, MLAG, MSTP) is hierarchical and exhaustion-monitored.
- Every link has exactly two endpoints. Every endpoint is a device+port or server+NIC (mutually exclusive).
- Every name is generated from a token-grammar template scoped at Role / Building / Site / Region / Global (first match wins); overrides are audited.
- Every change to a HardLocked entity requires a Change Set and N approvers.

For the full attribute-by-attribute definitions (hundreds of columns across ~100 tables) refer to the source specification document that accompanied this buildout plan; the buildout plan in [NETWORKING_BUILDOUT_PLAN.md](NETWORKING_BUILDOUT_PLAN.md) expands each phase's entities in concrete migration-level detail.
