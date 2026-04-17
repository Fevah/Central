# Network Management App — Master Functionality List

**Status:** Specification / future-state capabilities catalogue.
**Scope:** 139 capability groups covering the entire source-of-truth network management app. Every bullet is a concrete capability the engine must deliver for any tenant (not Immunocore-specific). Implementation phasing is in [NETWORKING_BUILDOUT_PLAN.md](NETWORKING_BUILDOUT_PLAN.md).

**Ordering convention:** grouped by domain, dependency-ordered within each group. Numbered sections (1-139) are stable identifiers — refer to capabilities as `MFL §23` ("QoS") etc.

---

## Domain index

| §§ | Domain |
|---|---|
| 1-2 | Tenancy & Organisation, Region |
| 3-4 | Site, Site Profile |
| 5-6 | Building, Building Profile |
| 7-10 | Floor, Floor Profile, Room, Rack |
| 11-16 | Device Role, Device, Module, Port, AE, Loopback |
| 17-19 | ASN Pool / Block, ASN Allocation, IP Pool |
| 20-21 | Subnet, IP Address |
| 22-25 | VLAN Pool, VLAN Block, VLAN, VLAN Template |
| 26-27 | MLAG Domain Pool, MLAG Domain |
| 28-29 | MSTP Priority Rule, MSTP Priority Allocation |
| 30-32 | Link Type, Link, Link Endpoint |
| 33-35 | Server Profile, Server, Server NIC |
| 36 | Naming Convention Engine |
| 37-39 | Lifecycle, Lock System, Reservation Shelf |
| 40-42 | Change Set & Approval, Validation Engine, Audit Trail |
| 43-45 | Access Control (RBAC), Search & Filter, Bulk Operations |
| 46-48 | Import, Export, Config Generation |
| 49-50 | Visualisation & Topology, Dashboards & Reports |
| 51-52 | API, Webhooks & Events |
| 53-56 | Integrations, Notifications, Scenarios, Environment Separation |
| 57-66 | Backup & HA, Tags, External Refs, Security & Compliance, A11y & L10n, UX, Help, Administration, Observability, Extensibility |
| 67-70 | Cabling, Cable Runs, Patch Panels, Cable Testing |
| 71-76 | Power chain (utility feed → generator → ATS → UPS → circuits → PDUs → outlets → device PSUs → budget), Environmental |
| 77-80 | Carriers, WAN Circuits, Demarcs, WAN Overlays |
| 81-85 | External BGP, VRF, Route policy, EVPN/VXLAN, Logical Interfaces |
| 86-88 | Multicast, NAT, QoS |
| 89-91 | Security (zones/objects/policy), Firewall Rules, Load Balancing |
| 92-93 | DHCP, DNS |
| 94-100 | Ops: Maintenance / Incidents / Post-change Verification / DR / Service Catalog / Runbooks / On-call |
| 101-107 | Hardware lifecycle, Support, Warranty, Software, Advisories/CVE, Licences, Compliance |
| 108-111 | Vendor, Cost centre, PO, Asset cost / TCO |
| 112-116 | Credentials, Break-glass, Config backup, Baseline, Drift |
| 117-119 | Discovery, Reconciliation, Live-state |
| 120-124 | Attachments, Comments, Knowledge base, Labels, Mobile / field-tech |
| 125-128 | Data quality, Stewardship, Orphan detection, Legal hold & residency |
| 129-132 | Programmes, Projects, Roadmap, Stakeholders, Decision records |
| 133-139 | Scenarios, Reachability, Failure simulation, Capacity forecast, Refresh dashboard, Anomaly detection, Recommendations |

---

## Principle capabilities (always true across every domain)

- **Tenant isolation.** Every operation is scoped to a single tenant. No cross-tenant data access except by platform-global admin.
- **Profile-driven cardinality.** No hardcoded counts; adding a site / building / floor / device uses a Profile as the template.
- **Lifecycle enforcement.** `Planned → Reserved → Active → Deprecated → Retired` drives what edits are allowed. `Active` auto-applies HardLock to numbering.
- **Lock discipline.** Four lock states (`Open` / `SoftLock` / `HardLock` / `Immutable`). HardLocked entities require a Change Set to edit.
- **Change Set workflow.** Every multi-entity mutation groups into a Change Set with pre-apply validation, N-approver routing, scheduled / atomic apply, and rollback.
- **Audit trail.** Every create/update/delete/lock-change/status-transition is written to the append-only, hash-chained audit log with before/after JSON.
- **Validation engine.** Named, toggleable rules run on save and on demand. ASN / loopback / Router-ID / MAC / subnet overlap / pool bounds / MLAG symmetry / MSTP ladder / server NIC topology / B2B symmetry / VLAN block integrity all enforced centrally.
- **Dry-run everything.** Every mutation path (single, bulk, import, Change Set apply) supports a dry-run preview before commit.
- **Reversibility by default.** Every Change Set is reversible unless explicitly marked otherwise with justification. Every retirement releases numbering to a cool-down shelf rather than immediately returning to the pool.

---

## Cross-cutting CRUD (every entity unless noted)

- Create with validation + auto-generated business code.
- Read with full detail incl. version for optimistic concurrency.
- List with pagination / filter / sort / column-selection / saved views.
- Update with version check and audit log.
- Soft-delete with cascade policy; hard-delete via admin purge only.
- Clone, tag / untag, add external ref, add note.
- Subscribe (notifications on change).
- View history (all audit entries) and view related entities.
- Single-entity export.

---

## Cross-cutting non-functionals

- Every write is logged. Every list endpoint is stable-cursor paginated. Every list endpoint has filter/sort/paginate.
- Every pool (ASN / IP / VLAN / MLAG / MSTP / loopback / port) has utilisation reporting and exhaustion alerts.
- Every entity supports soft-delete with configurable retention.
- Every validation rule is named and individually toggleable.
- Every API endpoint has a documented SLA and is monitored.

---

## Capability group detail

The 139 capability sections covering the full functional surface are reproduced in the source specification document accompanying this plan. Rather than duplicate that text, this document lists the capability groups by reference (§§ index above) and ties each group to an implementation phase in [NETWORKING_BUILDOUT_PLAN.md](NETWORKING_BUILDOUT_PLAN.md). Each phase commits against the specific §§ it fulfils.

**Quick mapping of capability §§ to build phases:**

| Capability §§ | Build phase (see plan) |
|---|---|
| §§ 1-10 (tenancy, hierarchy) | Phase 2 |
| §§ 11-16 (device catalog, device, ports) | Phase 4 |
| §§ 17-29 (pools + numbering + MLAG + MSTP) | Phase 3 |
| §§ 30-32 (unified link) | Phase 5 |
| §§ 33-35 (servers / NICs) | Phase 6 |
| §§ 36 (naming engine) | Phase 7 |
| §§ 37-42 (lifecycle / locks / change sets / validation / audit) | Phase 8 |
| §§ 43-45 (RBAC / search / bulk) | Phase 9 |
| §§ 46-48 (import / export / config gen) | Phase 10 |
| §§ 49-50 (viz / dashboards) | Phase 11 |
| §§ 51-52 (API / webhooks) | Continuous; hardened in Phase 10 |
| §§ 53-56, 60-66 (integrations, security, UX, admin, observability, extensibility) | Phase 12 |
| §§ 67-76 (cabling, patching, power, environmental) | Phase 13 |
| §§ 77-88 (WAN, carriers, BGP, VRF, policy, EVPN, logical IF, multicast, NAT, QoS) | Phase 14 |
| §§ 89-91 (security policy, load balancing) | Phase 15 |
| §§ 92-93 (DHCP, DNS) | Phase 16 |
| §§ 94-100 (ops, incidents, DR, services, runbooks, on-call) | Phase 17 |
| §§ 101-107 (lifecycle, support, software, CVE, licence, compliance) | Phase 18 |
| §§ 108-116 (finance, credentials, config backup, drift) | Phase 19 |
| §§ 117-119 (discovery, reconciliation) | Phase 20 |
| §§ 120-124 (collab, labels, mobile) | Phase 21 |
| §§ 125-132 (data quality, regulatory, programme/project) | Phase 22 |
| §§ 133-139 (scenarios, reachability, simulation, forecasting, recommendations) | Phase 23 |

**Reading order.** A new contributor should read: this doc (for capability catalog), the attribute system doc (for entities & attributes), the buildout plan (for phased delivery). The phase plan is the operational artefact; these two are the reference it builds against.
