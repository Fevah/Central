# Networking Module — Enterprise Engine Buildout Plan

**Status:** Plan — approved for phased execution.
**Goal:** Turn the current Networking module from a single-customer Immunocore toolkit into a multi-tenant network source-of-truth engine delivering ~100 entities and 139 capability groups (see [NETWORKING_ATTRIBUTE_SYSTEM.md](NETWORKING_ATTRIBUTE_SYSTEM.md) and [NETWORKING_MASTER_FUNCTIONALITY.md](NETWORKING_MASTER_FUNCTIONALITY.md)).
**Scope:** 23 phases across roughly 12-18 months of engineering. This document is the operational artefact; the attribute and functionality docs are the reference specs.

---

## 1. Why this plan exists

The current `modules/networking/` assembly is functional but **single-customer-shaped**. Tables like `switches`, `vlans`, `p2p_links`, `b2b_links`, `bgp_neighbors`, `switch_guide` encode Immunocore's network directly. There is:

- no `organization` / `region` / `site` / `building` hierarchy (buildings like MEP-91 are free-text strings)
- no Profile entities driving cardinality (device counts are scattered across rows, not templated)
- no lifecycle (no Planned → Reserved → Active → Retired machinery)
- no lock system (an Active device's ASN can be edited freely)
- no Change Set workflow (every write is immediate)
- no numbering pools as proper entities (ASN allocations are columns, not tracked against a pool)
- no unified Link model (P2P, B2B, FW live in separate tables with slightly different shapes)
- no naming-template engine
- no validation engine beyond ad-hoc triggers
- none of the capabilities beyond the basics — no cabling / power / compliance / CVE / discovery / scenarios / forecast

The attribute and functionality specs describe a different product. The current module is a *single expression* of that product, hard-coded to one customer.

**The buildout turns the module into a proper engine** — a product that can host Immunocore's data as one tenant while serving any other customer's network with the same schema.

---

## 2. Strategic principles

| Principle | What it means in practice |
|---|---|
| **Engine, not customer app** | Schema has zero Immunocore-specific identifiers. "MEP-91" is one building row in one tenant. If we deleted that tenant, the engine still works. |
| **Tenant isolation is sacred** | Every new table gets `organization_id` as a non-null FK. RLS policies enforce per-tenant visibility. Queries without a tenant context are rejected. |
| **Profile-driven cardinality** | No hardcoded "buildings have 2 cores + 2 reserved + 4 management". Those counts live on `building_profile_role_count` rows. |
| **Lifecycle + locks from day one** | Universal status / lock_state / version / audit-stamp fields are added to every new entity before any business logic. Retrofitting these later is 10× harder. |
| **Change Sets are the only mutation path for Active data** | Write-path for unlocked / planned / reserved data = direct writes (fast edits). Write-path for HardLocked / Active data = Change Set (reviewable, rollback-able). Both coexist. |
| **Never break the customer's data** | Immunocore's existing tables keep serving the current WPF panels until the phase that migrates them. Dual-write or shadow-read during transitions if needed. |
| **Every phase ships independently** | Phase N delivers working features at the end. No "big bang" where months of work lands at once. |
| **Validation before UI** | Every phase lands its validation rules before exposing the UI that depends on them. Empty / half-validated states are hidden. |
| **Every entity-type row carries a `naming_template`** | Whenever a phase introduces a new *-type table (catalog rows that describe a family of entities — `link_type`, `device_role`, `server_profile`, future `wan_circuit_type`, etc.), that table carries a `varchar(255) naming_template` column + a matching `XNamingService` expanding the tokens against a context record. Operators edit templates via REST without schema churn; separators are literal text. See Chunk A (`net.device_role.naming_template`, `DeviceNamingService`) and Phase 5a/5e (`net.link_type.naming_template`, `LinkNamingService`) for the reference implementation. |

---

### Per-phase checklist (mandatory for Phase 6 onwards)

Every phase that introduces a new entity-type row MUST tick these before its commit:

- [ ] `naming_template varchar(255)` column on the *-type catalog table, seeded with a sensible per-row default (see Chunk A migration 093 as the reference pattern).
- [ ] `XNamingService` static class + `XNamingContext` record with the recognised-token list documented in the XML summary. Unknown tokens pass through verbatim; empty values collapse to `""`; unmatched braces emit tail unchanged.
- [ ] Unit tests covering happy path + missing tokens + padding/format quirks (zero-pad instance counters, etc.).
- [ ] CRUD REST endpoints — the naming template is editable through the same `POST/PUT` path as the rest of the *-type row.
- [ ] Ribbon + panel action audit: every `BarButtonItem` / `BarCheckItem` the phase adds either has a wired handler with a test, or gets deleted. No `() => { }` placeholders landing in a commit.
- [ ] Dialog factory tests for any new DX window (`ForNew…`, `ForEdit…`) — confirm validation paths + save-success paths against a live DB fixture.

---

## 3. Current vs target — the gap in one table

| Area | Today | Target |
|---|---|---|
| Tenant / customer | Implicit (one customer per DB) | Explicit `organization` root; RLS-enforced |
| Geographic hierarchy | Free-text `site` column | `region` → `site` → `building` → `floor` → `room` → `rack` |
| Device model | `switches` + `switch_guide` rows | `device_role` catalog + `device` rows with profile-driven counts |
| IP management | `ip_ranges` flat table | Hierarchical `ip_pool` tree with `subnet` / `ip_address` allocation |
| ASN tracking | `asn_definitions` flat | `asn_pool` → `asn_block` → `asn_allocation` |
| VLAN tracking | `vlan_inventory` / `vlans` | `vlan_pool` → `vlan_block` → `vlan` + reusable `vlan_template` |
| MLAG / MSTP | `mlag_config` / `mstp_config` columns | `mlag_domain` + `mstp_priority_rule` entities, paired / validated |
| Links | 3 separate tables (P2P / B2B / FW) | Unified `link` + `link_endpoint` with typed variants |
| Naming | Column values + ad-hoc generation | Token-grammar `naming_template` engine with scoped overrides |
| Lifecycle | None | 5-state machine enforced platform-wide |
| Locks | None | 4-level lock system with audit |
| Change control | None | Change Set workflow with approvers |
| Audit | Limited to `audit_log` table for some entities | Universal append-only hash-chained log for every mutation |
| Validation | Trigger-based, scattered | Central named-rule engine, toggleable per rule |
| Cabling / power / environmental | Not modelled | Full entities (phases 13, later) |
| WAN / carriers / BGP peers / VRFs | Partial (`bgp_neighbors`) | Full model (phase 14) |
| Security policy / load balancing / DHCP / DNS | Not modelled | Full entities (phases 15-16) |
| Operations (incidents, DR, services, runbooks) | Not modelled | Full entities (phase 17) |
| Financial / licence / compliance | Not modelled | Full entities (phases 18-19) |
| Discovery / drift / reconciliation | Not modelled | Full entities (phase 20) |
| Scenarios / simulation / forecast | Not modelled | Full entities (phase 23) |

---

## 4. Migration strategy

The hard constraint: **don't lose Immunocore's data**. Their operational network is running against the current schema. The plan:

1. **Build new tables alongside old.** Phase 2 adds `organization`, `region`, `site`, `building` etc. as new tables. Old `switches.site` column keeps working.
2. **Seed Immunocore as tenant-1.** On Phase 2, one `organization` row is created for Immunocore; one `region` (UK); one `site` (Milton Park); `building` rows for each MEP-\* / GBG / etc. — all derived from existing `switches` data.
3. **Dual-write during transitions.** When `device` replaces `switches` (Phase 4), writes go to both tables until cutover. Reads prefer the new source.
4. **Shadow-read validation.** Before cutover, run queries against both sources and diff. Stop when diff is zero for a sustained window.
5. **Cut over per phase.** Phase 4 cutover: `switches` reads redirect to `device`. `switches` becomes read-only. Several phases later, `switches` deleted.
6. **Never touch their running data for reshape-only reasons.** Where the new schema is strictly additive (adds columns / tables), migrate without touching existing rows.

**Coexistence period:** roughly Phase 4 through Phase 12 (old and new tables both present). After Phase 12, old Immunocore-specific tables are dropped.

---

## 5. Phased rollout

> **Note on pull-forward chunks**: some small cross-cutting pieces land mid-phase as lettered chunks (e.g. Chunk A — device naming templates, commit 3c8da8a6e) rather than waiting for their "official" phase. The phase they were pulled from is marked with an **Already landed** callout so the sequencing story stays accurate.


Each phase has: scope, entities delivered, capabilities delivered (by MFL §), DB migrations, API endpoints, UI panels, validation rules, acceptance criteria, risk.

### Phase 0 — Foundation decisions + docs landed

**Scope:** This plan + the two spec docs committed. Agreement on the key architectural bets.

**Deliverables:**
- `docs/NETWORKING_ATTRIBUTE_SYSTEM.md` (entity catalog)
- `docs/NETWORKING_MASTER_FUNCTIONALITY.md` (capability catalog)
- `docs/NETWORKING_BUILDOUT_PLAN.md` (this doc)
- Decisions: Immunocore becomes tenant-1? Y/N (default Y). New tables live in which schema? (default `net.*` to keep separate from current `public.*`). How long is the dual-write window? (default: until zero diff for 14 days.)

**Acceptance:** plan reviewed + signed off.

**Risk:** low.

---

### Phase 1 — Universal entity base

**Scope:** The field pattern every new entity will carry.

**Deliverables:**
- Abstract base schema: `status` enum, `lock_state` enum, `lock_reason`, `locked_by`, `locked_at`, `created_at/by`, `updated_at/by`, `deleted_at/by`, `version` int, `notes`, `tags[]`, `external_refs[]`.
- Reusable SQL composite type + migration generator that adds these to any table.
- Unit tests on the reusable type.
- `Central.Engine.NetEngine` C# project stub with `IEntity` base interface.
- `libs/engine` additions: `EntityStatus` enum, `LockState` enum.

**MFL covered:** §§ 37-38 (lifecycle + lock system primitives, no workflow yet).

**Acceptance:** base type applied to a throwaway test table, audited, versioned correctly on concurrent update.

**Risk:** low.

---

### Phase 2 — Physical hierarchy (organisation → region → site → building → floor → room → rack)

**Scope:** The tenant + geographic backbone.

**Deliverables:**
- Entities: `organization`, `region`, `site`, `site_profile`, `building`, `building_profile` (without device-role counts yet — that's Phase 3), `floor`, `floor_profile`, `room`, `rack`.
- Migration `100_net_hierarchy.sql`: all 10 tables with universal base fields.
- Seed: one `organization` (Immunocore), one `region` (UK), one `site` (Milton Park), one `building` per existing distinct `switches.site` value.
- API: CRUD endpoints `/api/net/organizations`, `/api/net/regions`, `/api/net/sites`, `/api/net/buildings`, `/api/net/floors`, `/api/net/rooms`, `/api/net/racks`. List / get / create / update / delete with version checks.
- WPF UI: a new ribbon group inside Networking tab "Hierarchy" with panels for each level. Tree view navigation.
- Profiles: `site_profile` + `building_profile` + `floor_profile` rows for Immunocore's defaults.

**MFL covered:** §§ 1-10 (tenancy + geographic hierarchy).

**Acceptance:** Immunocore hierarchy visible in tree view, matches production layout (5 sites + expected buildings); new customer can create their own org + hierarchy via API and see it in the UI.

**Risk:** medium. First tenant-scoped data. RLS policies must be right.

---

### Phase 3 — Numbering pools

**Scope:** The "locked" core of the system — ASN / IP / VLAN / MLAG / MSTP.

**Deliverables:**
- Entities: `asn_pool`, `asn_block`, `asn_allocation`; `ip_pool`, `subnet`, `ip_address`; `vlan_pool`, `vlan_block`, `vlan`, `vlan_template`; `mlag_domain_pool`, `mlag_domain`; `mstp_priority_rule`, `mstp_priority_rule_step`, `mstp_priority_allocation`.
- Migration `101_net_pools.sql`.
- Validation rules: subnet overlap detection; pool boundary checks; ASN global uniqueness; VLAN /21 block integrity.
- `Central.NetEngine.Allocation` service: "next free ASN in block", "next free /30 in pool", "next free VLAN in pool", "next free MLAG domain".
- Reservation shelf table `reservation_shelf` so retired numbers come back after cool-down.
- API: pool CRUD + allocation endpoints.
- UI: pool tree panel with utilisation bars per level.

**MFL covered:** §§ 17-29.

**Acceptance:** allocation service returns deterministic next-free values; overlap / boundary / uniqueness invariants enforced; Immunocore's existing numbering loaded into the new tables and appears correctly at the right level of the pool tree.

**Risk:** high. This is where correctness matters most — bad allocation logic locks customers out of their own numbers. Extensive invariant testing required.

---

### Phase 4 — Device catalog + devices + ports

**Scope:** Replace `switches` with the real `device` model.

**Deliverables:**
- Entities: `device_role`, `device`, `module`, `port`, `aggregate_ethernet`, `loopback`.
- Migration `102_net_devices.sql`.
- `building_profile_role_count` table (now we have devices to count).
- Seed: role catalog matching Immunocore's roles (Core, Res, L1Core, L2Core, MAN, STOR, SW, FW, DMZ, L1SW, L2SW, RES-FW).
- Dual-write: `device` writes reflected into `switches` for backwards compatibility during transition.
- API: `/api/net/devices`, `/api/net/device-roles`, `/api/net/ports`, `/api/net/modules`.
- UI: replace current Switches panel with device grid pulling from `device`. Preserve UX (same columns, same filters).
- Migration script: every `switches` row → one `device` row with correct building / role / ASN / loopback references.

**MFL covered:** §§ 11-16.

**Acceptance:** every currently-visible switch appears in the new grid with identical data; ping / SSH / config-download buttons work against the new model; dual-write mirrors to `switches` for Phase 4-through-Phase-11 compat.

**Risk:** high. Most complex entity migration. Needs careful shadow-read validation.

---

### Phase 5 — Unified link model

**Scope:** Merge `p2p_links`, `b2b_links`, `fw_links` into one `link` + `link_endpoint`.

**Deliverables:**
- Entities: `link_type`, `link`, `link_endpoint`.
- Migration `103_net_links.sql`.
- Link types seeded: P2P, B2B, FW, DMZ, MLAG-Peer, Server-NIC, WAN.
- Link-type-specific naming templates wired into the (still rudimentary) naming engine.
- Migration script: each row from the three legacy link tables → one `link` + two `link_endpoint` rows.
- API: `/api/net/links` (with type filter).
- UI: unified Links panel replaces the three separate grids; type tabs preserve the UX; config-builder integrates.

**MFL covered:** §§ 30-32.

**Acceptance:** all three legacy link grids replaced; generated configs identical to pre-migration output byte-for-byte.

**Risk:** medium-high. Config generation is the safety net — if the bytes match, we haven't lost anything.

---

### Phase 6 — Servers + NICs

**Scope:** `server_profile`, `server`, `server_nic` — plus the 4-NIC fan-out pattern.

**Deliverables:**
- Entities + migration `104_net_servers.sql`.
- Seed: Server profile matching Immunocore's 4-NIC pattern.
- Server ASN inheritance from building.
- Loopback allocation from building's server-loopback `/24`.
- Auto-creation of 4 NIC links when a server is created (one per target router with A/B MLAG side routing).
- API + UI panel.

**MFL covered:** §§ 33-35.

**Acceptance:** creating a server in a building auto-creates 4 NIC links to the correct cores with correct IPs, matching the existing manual pattern.

**Risk:** medium.

---

### Phase 7 — Naming template engine

**Scope:** Generalise the per-type `naming_template` pattern (already shipping on `link_type` and `device_role` — see Phase 5a/5e and Chunk A) into a full scope-resolution engine with a UI, and extend it to the tiers that don't yet carry templates.

**Already landed** (in earlier phases, not waiting for Phase 7):
- `net.link_type.naming_template` + `LinkNamingService` — Phase 5a + 5e
- `net.device_role.naming_template` + `DeviceNamingService` — Chunk A (commit 3c8da8a6e)

**Phase 7 deliverables** (remaining):
- Extend the pattern to any *-type tables introduced by Phases 6 / 13 / 14+ that don't yet carry it (per the "Per-phase checklist" invariant above — most should land already compliant).
- Migration `105_net_naming_overrides.sql`: scoped override table `net.naming_template_override` keyed by `(entity_type, scope_level, scope_entity_id)` so a specific building can override its region's default without touching other buildings.
- Scope-resolution service: Role → Building → Site → Region → Global, first match wins. Shared token set: `{organization_code}`, `{region_code}`, `{site_code}`, `{building_code}`, `{building_octet}`, `{role_code}`, `{rack_code}`, `{sequence_number:nn}`, `{instance}`.
- Preview-before-commit REST: `POST /api/net/naming/preview` returns the expanded string for a proposed (type, context) pair without writing.
- Admin UI panel for managing templates + per-scope overrides. Includes a "regenerate" action that re-derives names for existing entities using the latest template (stamps an audit entry each time).
- Per-tenant default separator flag — `net.tenant_naming_config.default_separator` (e.g. `-` vs `_`) applied when a template is first created; existing templates stay untouched.

**MFL covered:** § 36.

**Acceptance:** every entity's business code regenerable identically from its template; override requires audit entry; new tenant's buildings get their own scope without leaking to Immunocore's. Existing link + device naming continues to work byte-for-byte (Phase 5f parity test must stay green).

**Risk:** low. The heavy lifting (per-type templates + services) already shipped; Phase 7 is scope resolution + UI.

---

### Phase 8 — Governance: lifecycle state machine + Change Sets + approval workflow

**Scope:** Make lifecycle and locks actually enforce policy, not just be fields on rows.

**Deliverables:**
- Entities: `change_set`, `change_set_item`, `approval`, `lock` (explicit object separate from the inline lock-state field).
- Migration `106_net_governance.sql`.
- State machine enforcement: Active → HardLock on numbering; Retired → cool-down to reservation_shelf.
- Approver-count-per-entity-type configuration.
- API: `/api/net/change-sets` — draft / submit / approve / reject / apply / rollback.
- UI: Change Set tray, approver inbox, approval UX.
- Change Set readiness checklist: validations + approvals + window.

**MFL covered:** §§ 37-41.

**Acceptance:** cannot edit an Active device's ASN except via Change Set + N approvers; Change Set apply is atomic; rollback works.

**Risk:** high. The governance layer is load-bearing. Needs end-to-end workflow testing.

---

### Phase 9 — Validation engine + audit trail

**Scope:** Centralise all the ad-hoc checks into a named-rule engine; universal hash-chained audit.

**Deliverables:**
- Entities: `audit_entry` (append-only, hash-chained); `validation_rule_definition` (named rules with on/off toggle).
- Migration `107_net_validation_audit.sql`.
- Rule catalog: ~50 named rules covering all the invariants from §41.
- API: `/api/net/validation` (run / report), `/api/net/audit` (query / export).
- UI: validation dashboard, audit log viewer.
- Point-in-time "as-at" query.

**MFL covered:** §§ 41-42.

**Acceptance:** every invariant from MFL §41 is a named toggleable rule; audit log tamper-evident; point-in-time query accurate.

**Risk:** medium.

---

### Phase 10 — RBAC, search, bulk, import/export, config generation

**Scope:** The platform capabilities that make daily use possible.

**Deliverables:**
- RBAC model scoped to `(action, entity_type, scope_type, scope_id)`.
- Global search service + faceted filters + saved views.
- Bulk edit / bulk import / bulk export.
- XLSX import that round-trips the legacy Immunocore workbook.
- Config generation against the new schema (BGP per device, MLAG peer-link, MSTP priority, VLAN trunks, port descriptions, FW zones). Byte-for-byte match with pre-migration output.
- Turn-up pack generator.

**MFL covered:** §§ 43-48.

**Acceptance:** admin can do any day-to-day task via the UI with saved views; legacy workbook imports cleanly; generated configs pass the byte-match test.

**Risk:** medium.

---

### Phase 11 — Data migration: Immunocore legacy tables → engine tables

**Scope:** Cutover. The legacy tables (`switches`, `p2p_links`, `b2b_links`, `fw_links`, `bgp_neighbors`, `vlan_inventory`, `asn_definitions`, `ip_ranges`, `mlag_config`, `mstp_config`, `switch_guide`, `switch_connections`, `interfaces`, `l3_interfaces`, `static_routes`, `dhcp_relay`) become read-only views backed by the new engine tables.

**Deliverables:**
- Migration script that verifies dual-write consistency for N days before cutover.
- Read redirect via views so any remaining legacy queries still work.
- Turn off dual-write; old tables become views.
- Full backup taken before the cutover.

**Acceptance:** every existing panel still renders the same data; no write path still points at legacy tables.

**Risk:** high. Rollback plan required: restore the backup + turn dual-write back on.

---

### Phase 12 — Drop legacy tables; visualisation + dashboards; API & webhooks hardening

**Scope:** Final cleanup + the platform polish that makes the engine sellable.

**Deliverables:**
- Drop the legacy tables after 30+ days of clean post-cutover operation.
- Topology / map / rack elevation / fan-out visualisations.
- Estate dashboard, pool utilisation dashboard, freshness report, capacity forecast.
- API hardening: idempotent bulk endpoints, OpenAPI contract published, webhook subscriptions.
- Scheduled reports; notifications (in-app + email + webhook for Slack/Teams).

**MFL covered:** §§ 49-52, 54.

**Acceptance:** legacy tables gone; no regressions; external integrations can subscribe to events.

**Risk:** medium.

---

### Phases 13-23 — Advanced feature tranches

These come after Phase 12 and can proceed in parallel. Rough sequencing by business value:

| Phase | Scope | MFL §§ | Time (rough) |
|---|---|---|---|
| 13 | Cabling + patching + cable tests | 67-70 | 6-10 weeks |
| 14 | Power chain (utility → generator → ATS → UPS → circuits → PDUs → outlets → PSUs → budget) + environmental | 71-76 | 8-12 weeks |
| 15 | WAN: carriers / circuits / demarcs / overlays / external BGP | 77-81 | 8-12 weeks |
| 16 | VRF / route policy / EVPN / VXLAN / logical interfaces / multicast / NAT / QoS | 82-88 | 10-14 weeks |
| 17 | Security policy (zones / objects / firewall policy / rules) + load balancing | 89-91 | 8-10 weeks |
| 18 | DHCP / DNS integration | 92-93 | 4-6 weeks |
| 19 | Ops layer: maintenance windows, incidents, post-change verification, DR, service catalog, runbooks, on-call | 94-100 | 10-14 weeks |
| 20 | Hardware lifecycle + support + warranty + software / CVE / licence / compliance | 101-107 | 10-14 weeks |
| 21 | Financial: vendors / cost centres / POs / asset cost / TCO | 108-111 | 4-6 weeks |
| 22 | Credentials / certs / config backup / baselines / drift | 112-116 | 6-10 weeks |
| 23 | Discovery + reconciliation + live-state + scenarios + reachability + failure sim + capacity forecast + anomaly + recommendations | 117-119, 133-139 | 14-20 weeks |

These phases assume the Phase 0-12 foundation exists. Each is a standalone project with its own plan.

---

## 6. Risks and mitigations

| Risk | Mitigation |
|---|---|
| **Scope creep.** The spec is huge. | Phase 0-12 is the committed engine core. Phases 13+ are separately scoped when each is ready. Don't mix. |
| **Data loss during migration.** | Dual-write for weeks before cutover; shadow-read validation; full backup before every migration commit. |
| **Tenant isolation regression.** | RLS policies enforced at the DB level with tests that attempt cross-tenant reads. Every API endpoint gets a "pretend to be tenant B, try to read tenant A's data" test. |
| **Allocation logic bugs.** | Phase 3 (pools) has the highest test density. Property-based tests for allocation invariants. Every "next-free" result tested against the full allocation state. |
| **Config generation regression.** | Byte-for-byte match against pre-migration output is the acceptance test for every phase that can affect generated config. |
| **UX churn.** | New UI replaces old UI panel-by-panel, not all-at-once. Each replacement keeps the same columns / filters / shortcuts unless a specific UX improvement is approved. |
| **Profile migrations break existing buildings.** | Profiles are versioned. Existing buildings stay on the profile version they were created against. Re-apply is opt-in with diff preview. |

---

## 7. Decisions needed before Phase 0 starts

1. **Immunocore as tenant-1: Y / N?** Default Y — their data moves into a single tenant organisation.
2. **New-tables schema name: `public.*` or `net.*`?** Default `net.*` to keep the engine separate from the existing `public.*` until Phase 12 drops the legacy tables.
3. **Dual-write window: days / weeks / "until zero diff"?** Default: until zero diff for 14 days before each cutover.
4. **Phase 0-12 target date?** Rough estimate 9-12 months of engineering at one-person throughput; reduces to 5-7 months with two engineers.
5. **Phases 13-23 priority order?** Default as listed above; adjust per business need.

---

## 8. What changes in apps/desktop during the buildout

The existing Networking ribbon tab stays. Panels get replaced one by one:

- Phase 2 adds a "Hierarchy" group (new).
- Phase 4 replaces Switches panel with Device panel (same UX, new backing store).
- Phase 5 replaces the three Link panels with one unified Links panel with a type tab switcher.
- Phase 6 replaces Servers UI.
- Phase 7-9 add new groups (Naming, Change Sets, Validation, Audit) but don't touch existing panels.
- Phase 10 adds a Pool dashboard panel and a Config Generator panel.
- Phase 12 removes any panel tied to a legacy table.

Tenant self-containment still holds: disabling Networking for a tenant removes every group in one step.

---

## 9. References

- [NETWORKING_ATTRIBUTE_SYSTEM.md](NETWORKING_ATTRIBUTE_SYSTEM.md) — entity catalogue
- [NETWORKING_MASTER_FUNCTIONALITY.md](NETWORKING_MASTER_FUNCTIONALITY.md) — capability catalogue (§§ 1-139)
- [REPO_STRUCTURE_PLAN.md](REPO_STRUCTURE_PLAN.md) — where Networking lives in the repo
- [CLAUDE.md](../CLAUDE.md) — platform-wide conventions
- [CREDENTIALS.md](CREDENTIALS.md) — DSNs and local DB access
