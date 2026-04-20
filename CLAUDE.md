# Central

Enterprise infrastructure platform — network config, service desk, IT operations.
Desktop app builds as `Central.exe`. All namespaces are `Central.*`.
Switches run **PicOS 4.6** (FS brand). Config format is set-style CLI.

## DevExpress Development Instructions

When working with DevExpress components:
- Always use the **`dxdocs` MCP server** to search DevExpress documentation
- Reference specific DevExpress control names
- Verify class names, property names, and enum values before using them in code
- This prevents build errors from incorrect API names (e.g., wrong series type names,
  non-existent properties, incorrect enum values)

Query `dxdocs` for:
- Chart series types and their properties
- Grid column/view settings
- Ribbon control types
- Any DX API you're unsure about

**MCP server URL**: `https://api.devexpress.com/mcp/docs`
**NuGet feed**: `https://nuget.devexpress.com/WfTygbMUTRRTBswwwjXlZCPghcbel7RNgz797YAfgXQJG2FIAR/api/v3/index.json`
**Current version**: 25.2.6

## Architecture & Build Plan

**IMPORTANT**: All new development follows the phased architecture plan in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). Before starting work, check which
phase/step is current and build accordingly. Do not skip phases or change the
project structure without updating the architecture doc first.

| Doc | Purpose |
|-----|---------|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Master build plan — 8 phases, solution structure, module interfaces, RBAC, migration path |
| [docs/ENTERPRISE_CRM_BUILDOUT.md](docs/ENTERPRISE_CRM_BUILDOUT.md) | Enterprise SaaS + CRM — 29 phases: companies, contacts, teams, addresses, profiles, CRM pipeline, billing, sync |
| [docs/SERVER_ARCHITECTURE.md](docs/SERVER_ARCHITECTURE.md) | Multi-user server — API, gRPC, SignalR, K8s deployment, background services |
| [docs/TOTALLINK_PATTERNS.md](docs/TOTALLINK_PATTERNS.md) | TotalLink source patterns — reference implementations for module system, ribbon, CRUD, undo |
| [docs/FEATURE_TEST_CHECKLIST.md](docs/FEATURE_TEST_CHECKLIST.md) | 1,400+ testable items across 180 sections — every feature manually verifiable |
| [docs/GLOBALADMIN_BUILDOUT.md](docs/GLOBALADMIN_BUILDOUT.md) | Global Admin 5-phase buildout — tenant CRUD, licensing, subscriptions, setup wizard, audit |
| [docs/TASKS_BUILDOUT.md](docs/TASKS_BUILDOUT.md) | Task module 11-phase buildout plan (Hansoft/P4 Plan clone) — all phases complete |
| [docs/MERGE_PLAN.md](docs/MERGE_PLAN.md) | Central + Secure merge — 10 phases, unified auth, API gateway, K8s elastic scaling |
| [docs/NETWORKING_BUILDOUT_PLAN.md](docs/NETWORKING_BUILDOUT_PLAN.md) | Networking engine — 23-phase buildout transforming single-customer toolkit into multi-tenant source-of-truth. Phases 1-6 complete; **Phase 10 service-side COMPLETE**; **Phase 10b WPF + Angular operator surface IN PROGRESS** — byte-parity config-gen, turn-up packs, bulk export/import/edit, RBAC, XLSX, search, saved views, audit-stats endpoint, web bulk/search/validation/scope-grants/hierarchy/pools/devices/audit-stats pages (2026-04-19) |
| [docs/NETWORKING_RIBBON_AUDIT.md](docs/NETWORKING_RIBBON_AUDIT.md) | Networking ribbon action inventory — every button, permission, message, handler; placeholder-lambda canary test |
| [docs/CREDENTIALS.md](docs/CREDENTIALS.md) | All login credentials, DSNs, SSH info, service URLs, K8s access |

### All 8 Phases + Task Module COMPLETE — Platform is production-ready

25 projects. .NET 10 / PG 18.3 / Npgsql 10.0.2 / DX 25.2 / Svg.NET 3.4.7 / Elsa 3.5.3. 53 API endpoint files. 96 DB migrations (001-096). 0 build errors. **2,616 tests** across ~180 classes (2026-04-18).

### Networking Engine Buildout — Phases 1-6 COMPLETE (2026-04-18)

Transforms the existing `switches` / `p2p_links` / `servers` single-customer shape into a multi-tenant network source-of-truth with 42 `net.*` tables across:

| Phase | What | Key commits |
|-------|------|-------------|
| 1 | Universal entity base (status / lock / version / audit) | 084 (foundation) |
| 2 | Region → Site → Building → Floor → Room → Rack + profiles | 2a-2e (1b0715c65…8126400fc) |
| 3 | ASN / IP / VLAN / MLAG / MSTP pools + allocation service + shelf + IPv4+IPv6 carver | 3a-3k (f30d760fb…94bd9379b) |
| 4 | device_role catalog + device + module + port + aggregate_ethernet + loopback; dual-write vs legacy switches | 4a-4f (d366969e5…4610ce4df) |
| 5 | Unified `link` + `link_endpoint` + 7 link-type catalog; import of 2,826 legacy P2P/B2B/FW rows with SQL parity tests | 5a-5f (3b2d6da4a…20fc0dd6d) |
| 6 | server_profile + server + server_nic; 4-NIC fan-out allocation service with MLAG-paired cores | 6a-6f (ab866c5a2…d23ce9816) |

Chunks (pull-forward cross-cutting work):

| Chunk | What | Commit |
|-------|------|--------|
| A | Device naming templates — parity with links (`device_role.naming_template` + `DeviceNamingService`) | 3c8da8a6e |
| B | Networking ribbon audit + placeholder wiring + 22 audit tests + `docs/NETWORKING_RIBBON_AUDIT.md` | e1fccd2c6 |
| C | Dialog validation extracted to `HierarchyValidation` / `PoolValidation` / `AllocationValidation` + 44 tests; dialogs rewired to consume them | ce7c1edfa |

Per-phase checklist invariant (from plan amendment 758ccaa98) — every *-type catalog table carries: `naming_template` column, `XNamingService` + `XNamingContext` record with documented tokens, unit tests for happy + edge paths, CRUD REST, ribbon audit with no placeholder lambdas, extracted dialog validation.

**Honest gaps**: Phase 7 (scope-resolution engine + override table + preview API + admin UI) not started. Phases 8, 9, 11-23 not started. MSTP rule editor panel deferred. XAML ribbon coexists with engine-registered ribbon until Phase 11.

### Networking Engine Phase 10 — Config Generation + Turn-up Pack BYTE-PARITY ACHIEVED (2026-04-18)

The config-gen + turn-up-pack halves of Phase 10 shipped as 28 self-contained Rust slices (commits `74d0523b6` through `bcb9ad26d`). **Every section the legacy `ConfigBuilderService` emits now has a Rust counterpart** — once a tenant seeds Gateway / Vrrp / DhcpRelayTarget rows (migration 104 does this for Immunocore) the output matches line-for-line. 230 Rust unit tests + 4 `#[ignore]` live-DB integration tests, 0 failures.

**PicOS renderer pipeline** (legacy-step order): header → hostname (parametric via `net.device_role.naming_template`) → IP routing → QoS preset (55 lines) → per-port QoS bindings → voice-VLAN preset → loopback `lo0` → mgmt SVI `vlan-152` → VLAN catalog (with `l3-interface` binding when SVI present) → L3 VLAN SVIs → BGP scalar + neighbors (from `net.link_endpoint`) → MSTP bridge-priority → MLAG peer-link → VRRP VIPs (from `ip_address.assigned_to_type = 'Vrrp'`) → DHCP relay (from `net.dhcp_relay_target` — migration 103) → merged ports section (description + native-vlan-id + port-mode interleaved by interface) → static default route (from `ip_address.assigned_to_type = 'Gateway'`) → LLDP enable.

**Render lifecycle API** (all under `/api/net/`): `POST devices/:id/render-config` (persists + chains via `previous_render_id`) · `GET devices/:id/renders` (summary list, limit 1-500, default 50) · `GET renders/:id` (full body) · `GET renders/:id/diff` (pure-Rust line-set diff) · `POST buildings/:id/render-configs` · `POST sites/:id/render-configs` · `POST regions/:id/render-configs` (per-device error-tolerant fan-outs).

**DHCP relay target CRUD**: 5 endpoints on `/api/net/dhcp-relay-targets` — list/get/create/update (optimistic concurrency via `version`)/soft-delete. Priority defaults to 10, validated non-negative. Full audit entries in the same transaction as each mutation.

**C# ApiClient**: `NetworkingEngineClient.cs` wraps every Phase 10 endpoint with typed methods + records. WPF modules can call through the standard `CentralApiClient` facade without hand-rolling HttpClient per call-site.

**Migrations shipped:** `102_net_cli_flavors.sql` (tenant CLI flavor state + render history table with SHA-256 chain), `103_net_dhcp_relay_targets.sql` (M:N VLAN × server IP with priority), `104_net_immunocore_seed_gateway_vrrp_dhcp.sql` (imports from `public.vrrp_config` + `public.dhcp_relay` + seeds mgmt-VLAN Gateways).

**Bulk CSV surface (COMPLETE for Phase 10)**:
- **Export** — 9 entities: devices / VLANs / IP addresses / links / servers / subnets / ASN allocations / MLAG domains / DHCP relay targets. RFC 4180 escaping, `Content-Disposition: attachment`, joined display codes for human-readable output.
- **Import** — 6 entities: devices / VLANs / subnets / servers / DHCP relay targets / links. Create-only + transactional all-or-nothing. Links does cross-tab A/B decomposition (1 CSV row → 1 link + 2 endpoints). Validate-then-apply pipeline with per-row outcomes, hand-rolled RFC 4180 parser, dry-run default.
- **Edit** — 5 entities: devices / VLANs / subnets / servers / DHCP relay targets. Same-value-for-all transactional updates with field whitelist + version-checked UPDATE. Dangerous fields (hostname, vlan_id tag, CIDR network) explicitly gated.

**RBAC scoped policy engine (COMPLETE for Phase 10)**: Migration 105 `net.scope_grant` with `(user_id, action, entity_type, scope_type, scope_entity_id)` tuples. Resolver matches Global + EntityId + hierarchy-expanded Region/Site/Building for Device/Server/Building/Site entity types. `GET /api/net/scope-grants/check` dry-runs the resolver for UI feedback. Enforcement wired into every state-changing surface (bulk_edit, bulk_import, DhcpRelayTarget CRUD, ScopeGrant CRUD, config-gen render + history endpoints). Opt-in rollout via `X-User-Id` header presence — service-bypass on missing header preserves backward compat during the rollout window.

**Global search + saved views (COMPLETE)**: `GET /api/net/search` — full-text across 6 entity types via query-time `tsvector` UNION with `plainto_tsquery` stemming + `ts_rank` ordering; optional comma-separated `entityTypes` filter; RBAC post-filter drops hits the caller can't read. Migration 106 `net.saved_view` + per-user CRUD (ownership-as-auth; 404-not-403 on cross-user reads).

**XLSX round-trip (COMPLETE)**: `calamine` (read) + `rust_xlsxwriter` (write) crate deps; `xlsx_codec.rs` wraps the existing CSV pipeline so XLSX is a pure transport adapter with no per-entity XLSX code that could drift from the CSV shape. 12 endpoints (6 entities × export.xlsx + import.xlsx); C# ApiClient gets byte-array transport helpers alongside the existing CSV ones.

**Phase 10 service-side surface is COMPLETE** — every deliverable in the plan (config-gen byte-parity, turn-up pack generator, bulk export/import/edit, RBAC with hierarchy + enforcement, XLSX, global search, saved views) has shipped. Remaining work is WPF/Angular UI integration on top of the completed service surface.

See `docs/NETWORKING_BUILDOUT_PLAN.md` §Phase 10 for the slice-by-slice commit table and acceptance-criteria check.

### Networking Engine Phase 10b — WPF + Angular Operator Surface IN PROGRESS (2026-04-19)

Client-side arc that wires every Phase 10 engine endpoint into both the WPF shell and the Angular web client. Consistent cross-panel drill vocabulary (`selectId:{guid}:{label}` / `selectEntity:{Type}:{Guid}` / `q:{text}` / `filterBy:{Col}:{Val}` / `focus{NodeType}:{code}` / `newGrantForEntity:{Type}:{Guid}`) lets grids + hierarchy + search + audit + scope grants drill into each other without new route plumbing per pair.

**WPF panels shipped**: BulkPanel, SearchPanel + saved-views sidebar, AuditViewerPanel (with drill from Devices / Vlans / Servers / Links / ScopeGrants), ScopeGrantsAdminPanel, HierarchyTreePanel (reverse drill from building → devices/servers), Bulk/Search/Audit/ScopeGrants ribbon groups, tenant indicator in status bar. Two-message pattern `OpenPanelMessage(target)` (shell restore) + `NavigateToPanelMessage(target, payload)` (panel state).

**Web pages shipped** (`apps/web/src/app/modules/network/components/`): `network-search` (with facet chip bar + saved-view CRUD sidebar — create/rename/delete), `network-validation`, `network-data-quality` (runs full validation + groups violations), `network-scope-grants` (list + filter + create + delete + copy id + check permission dry-run), `network-hierarchy` (tree drills to region/site/building/floor detail pages), `network-pools`, `network-bulk` (validate-only dry-run), `network-devices` / `network-vlans` / `network-servers` / `network-links-grid` / `network-subnets` / `network-dhcp-relay` / `network-ports` / `network-aggregate-ethernet` / `network-ip-addresses` / `network-mlag-domains` / `network-modules` / `network-mstp-rules` / `network-reservation-shelf` (thin grids for every net.* entity + supporting tables; dhcp-relay has full create/edit/delete). **Entity detail pages** (every Phase-10 entity covered): `network-device-detail` (Summary/Audit/Renders/Ports), `network-server-detail` (Summary/Audit/NICs), `network-vlan-detail` (Summary/Audit/DHCP relays), `network-link-detail` (Summary/Audit/Endpoints with A/B hero cards), `network-subnet-detail` (Summary/Audit/Addresses), `network-dhcp-relay-detail` (Summary/Audit), `network-ip-address-detail` (Summary/Audit with subnet back-link). **Hierarchy detail pages**: `network-region-detail` (Summary/Sites) + `network-site-detail` (Summary/Buildings) + `network-building-detail` (Summary/Devices/Servers) + `network-floor-detail` — full region → site → building → floor drill chain. Audit + rollup: `network-audit-timeline` (with clickable correlationId drill), `network-audit-search` (free-form filter + CSV/NDJSON export), `network-audit-stats` (DxChart trend + per-entity-type table + top-actors leaderboard). Governance: `network-change-sets` (list + "New change set" dialog) + `network-change-set-detail` (header + items grid + before/after JSON master-detail + Submit/Apply/Cancel action bar + Add-item dialog), `network-locks` (non-Open lock admin view + SetLock PATCH dialog with Immutable-is-terminal warning). Config-gen: `network-naming-preview` (three-mode token-substitution dry-run), `network-naming-overrides` (persisted scope-aware template CRUD), `network-render-history` (per-device renders + body + diff + "Render now"), `network-render-pack` (Building/Site/Region turn-up pack trigger). Admin: `network-pool-utilization` (ASN/VLAN/IP used-vs-capacity with coloured progress bars), `network-cli-flavors` (per-tenant enable + one-default CLI flavor admin). `NetworkingEngineService` is a typed thin wrapper over every Phase 10 endpoint. All pages lazy-loaded via `loadComponent` + gated by `moduleGuard('switches')`; per-page `moduleGuard('links'|'routing')` for fine-grained licensing.

**Legacy ribbon cleanup** (commit `50a83804a`, migration 108): pre-merge Devices + Switches ribbon tabs removed from `apps/desktop/MainWindow.xaml` (~449 lines) plus the seeded `ribbon_pages` / `ribbon_groups` / `ribbon_items` rows for those tabs (FK cascade drops 19 groups + 72 items). Networking module's in-code registrar is now the sole source of Devices + Switches groups under the unified Networking tab.

**Engine thin lists** (commits `73d29f8ca` + `3d003d58a`): `/api/net/devices` / `vlans` / `links` pre-existed; `servers` + `subnets` added so the web grids + WPF pickers have a consistent 5000-row-cap read across every net.* entity type. Each thin list LEFT JOINs the obvious display fields (server's profileCode + buildingCode, subnet's poolCode + vlanTag) so grids render without a second round-trip per row.

**Audit rollups** (commits `7095c47d4` + `e0bcda458` + `0e83e7407`): three read endpoints over `net.audit_entry` — `/api/net/audit/stats` per entity type, `/api/net/audit/trend` time-bucketed (hour/day/week), `/api/net/audit/top-actors` per-user leaderboard. All three share optional window + entity-type narrower; the web audit-stats page combines all three in a single dashboard.

**Search facets** (commit `69991e714`): `/api/net/search/facets` returns per-entity-type COUNT for a given query in one UNION-ALL round trip. Surfaced in the web search page as a clickable chip bar ("Device(12) · Vlan(4) · Subnet(1)") for click-to-narrow UX.

**Validation-rule catalog expansion** (43 batches from `cc4748f5f` through `47ae68a8d`): 54 → 170 rules. Batches 25-42 shipped the lifecycle-resolves family + data-quality advisories. Batch 43 added three more data-quality rules: `server_nic.admin_up_false_on_active_status` (Info — mirror of the port rule), `port.speed_mbps_reasonable_when_set` (Info — flags values outside 100-400000 Mbps), `rack.max_devices_positive_when_set` (Warning — catches the <=0 case the DB has no CHECK for). Every FK in the `net.*` graph has a lifecycle-resolves rule; the catalog now covers structural integrity + uniqueness + reachability + readability + data-quality advisories. Guardrail test `dispatcher_has_arm_for_every_catalog_rule` updated each arc so a catalog row without a matching dispatcher arm fails CI.

**Identity + session banner** (commit `9aca7befe`): `GET /api/net/whoami` returns the caller's identity + grant-count + distinct actions / entity-types across active scope-grants. One round-trip aggregate. Web surface is a blue banner above the network dashboard sub-nav rendering "Signed in as user X · N grants · action chips · entity-type chips".

**IP address thin list + subnet detail** (commit `7c03ae6d5`): `GET /api/net/ip-addresses` with optional subnetId narrower. Backs the new `/network/net-subnet/:id` page's Addresses tab — answers "what's actually allocated from this /24?" without a CSV export.

**Server NIC / link-endpoint / port thin lists + detail-page tabs** (commits `e6471f9b3` + `ff7a3f0fc` + `141f2ad96`): three parallel thin lists (`/api/net/server-nics`, `/api/net/link-endpoints`, `/api/net/ports`) each with an optional parent-entity narrower. Each backs a new tab on the matching detail page (Server NICs / Link Endpoints / Device Ports) + LEFT JOINs resolve display fields so rows render without per-row round-trips. The ports endpoint also backs a tenant-wide `/network/ports` grid (commit `612be5519`).

**Supporting-entity thin lists + grids** (commits `dcb3ff09b` + `78c0054e5` + `f08d71b26` + `56a7e025d` + `98bc53f47` + `c4e02bf46` + `4c09546e3`): `/api/net/mlag-domains`, `/api/net/modules`, `/api/net/mstp-rules`, `/api/net/reservation-shelf`, `/api/net/vlan-blocks` + `/api/net/asn-blocks` (availability), `/api/net/asn-allocations`, `/api/net/rooms`, `/api/net/racks`. Each has a tenant-wide web grid at the matching route with amber row-tints on data-quality edge cases (AE members < min_links, MSTP rule with 0 steps, shelf entries past cooldown, blocks with < 10% available). Backs operator "is anything wrong with the fabric?" scans at a glance.

**Supporting-entity detail pages** (commits `a24448522` + `8948ab95a` + `9ea9b5efb` + `4c09546e3`): `/network/aggregate-ethernet/:id` (Summary + Members filtered by aggregateEthernetId), `/network/module/:id`, `/network/port/:id` (Summary + Audit + Usage — link endpoints + server NICs on this port), `/network/room/:id` + `/network/rack/:id`. Completes the entity-detail chain — every Phase-10 + supporting entity now has a drill target.

**Data-quality dashboard** (commit `7cc590764`): `/network/data-quality` runs every enabled validation rule on page load, shows summary cards (rules run / rules with findings / total violations) + a grouped violations grid with double-click drills to the matching entity detail page. Complements the per-rule validation page with a hero view for tenant-wide scanning.

**Naming-resolve page** (commit `2c7f7cba5`): `/network/naming-resolve` wraps the existing POST /api/net/naming/resolve endpoint. Picks entity type + hierarchy position + subtype + default template; returns the winning template + which tier matched (Default / Global / Region / Site / Building, each with Specific / AnySubtype variants). Tier badge coloured by specificity. Forms a triptych with the existing naming-preview (expand) + naming-overrides (persist) pages.

**Hierarchy + DHCP-relay detail pages** (commits `8fa342397` + `f4974d37f` + `ee0366135` + `e4e6bd5f8` + `daa8eb069`): region / site / building each have `/network/{type}/:id` detail pages with drill-down grids (region → sites → buildings → devices/servers). DHCP-relay detail page closes the entity-detail set. Hierarchy tree rows now drill to the matching detail page (Region/Site/Building) instead of straight to audit.

**Eleventh wave — ApiClient parity + overview dashboard + subnet carver preview + change-set item delete + validation batches 25-26** (commits `82114e518` → `4db6df3bb`): eight-slice arc: (1) C# ApiClient parity — 13 new List*Async methods + DTOs (IpAddresses / Ports / AggregateEthernet / MlagDomains / Modules / MstpRules / ReservationShelf / AsnAllocations / Rooms / Racks / LinkEndpoints / ServerNics / WhoAmI) + naming-override CRUD + ResolveNamingAsync on `libs/api-client/NetworkingEngineClient.cs`. (2) `/network/overview` tile-grid dashboard grouping 21 entity counts into Primary / Secondary / Numbering / Hierarchy / Governance rows + validation summary cards + top-5 violating rules table. (3) `POST /api/net/allocate/subnet/preview` — pure read-only dry-run of the subnet carver returning candidate CIDR without insertion. (4) `/network/carver-preview` UI picking pool + prefix and rendering the candidate in a 28px mono hero card. (5) `DELETE /api/net/change-sets/:setId/items/:itemId` + Remove button on the change-set detail Items grid (Draft-only; optimistic-strip on success, reload on failure). (6)+(7) Validation batches 25 + 26 added six rules completing the "device-child lifecycle resolves" family (room→floor, rack→room, port/module/loopback/aggregate_ethernet→device).

**Twelfth wave — IP pool detail + carver deep-link + ApiClient catchup + batch 27** (commits `b67201433` → `355cb5b1c`): five-slice arc: (1) `GET /api/net/subnets` accepts an optional `poolId` narrower so the pool-detail Subnets tab can drill without a new endpoint. (2) `/network/ip-pool/:id` Angular detail page — Summary + Subnets tabs + "Preview next subnet" button that deep-links to `/network/carver-preview?poolId=…`; carver-preview component honours the query param by pre-selecting the dropdown + running the family-appropriate prefix default. (3) C# ApiClient: new `PreviewSubnetCarveAsync` + `CarvePreviewDto`; `ListSubnetsAsync` gets the optional `Guid? poolId` second parameter. (4) Validation batch 27 — three more lifecycle resolves rules: `server_nic.server_resolves_active`, `link_endpoint.link_resolves_active`, `device.role_resolves_active` (Warning severity for the soft role-deprecation case). (5) Docs refresh.

**Thirteenth wave — numbering parent resolves + cross-drill polish + batch 29** (commits `280cb52bc` → `8e3c6100b`): five-slice arc: (1) Validation batch 28 (122 → 125) — numbering-lifecycle parent resolves: `vlan.block_resolves_active`, `subnet.pool_resolves_active`, `mlag_domain.pool_resolves_active`. (2) Web UX polish — pool-utilization grid double-click drills IP pool rows to `/network/ip-pool/:id`; subnet detail page's Pool field becomes a clickable link via the new `poolId` field on `SubnetListRow` (added alongside the existing `poolCode` for drill targets). (3) C# ApiClient catchup — `DeleteChangeSetItemAsync` wrapper finally mirrors the engine endpoint shipped in wave 11. (4) Validation batch 29 (125 → 128) — block → pool lifecycle: `asn_block.pool_resolves_active`, `vlan_block.pool_resolves_active`, `vlan.template_resolves_active_when_set` (Warning severity for the optional template pointer). (5) Docs refresh.

**Fourteenth wave — nullable-FK resolves + data-quality rules + carver UX + audit entityTypes filter** (commits `87cbab0e0` → `200ed2153`): five-slice arc: (1) Validation batch 30 (128 → 131) — nullable-FK `_when_set` variants: `server_nic.vlan_resolves_active_when_set`, `server_nic.subnet_resolves_active_when_set`, `link_endpoint.vlan_resolves_active_when_set` (all Warning). (2) Validation batch 31 (131 → 134) — non-lifecycle data-quality: `vlan_template.default_unique_per_tenant` (Error), `port.interface_name_starts_with_prefix` (Warning), `device.hardware_model_set_when_active` (Advisory). (3) Carver-preview UX polish — NumberBox min/max bind to the pool's prefix / family so invalid inputs are unreachable; Preview button disables on out-of-range prefix via `prefixLengthValid()`; pool-info bar shows "Requested prefix must be /N or narrower". (4) `/api/net/audit/stats` accepts optional `entityTypes=Device,Server,Vlan` narrower via new `AuditStatsQuery::entity_types_list()` helper + SQL `ANY($4)` branch; three new parser unit tests (audit suite now 21 passing). Angular service + C# ApiClient `AuditStatsAsync` both thread through the new parameter. (5) Docs refresh.

**Fifteenth wave — validation batch 32 + UX/self-serve + my-activity page** (commits `3734df521` → `3719bf53f`): five-slice arc: (1) Validation batch 32 (134 → 137) — more nullable-FK resolves: `device.room_resolves_active_when_set`, `device.rack_resolves_active_when_set`, `subnet.parent_subnet_resolves_active_when_set` (Warning). (2) Validation page gains a severity quick-filter bar (Error / Warning / Info / All buttons with live counts per severity). (3) New `/network/my-activity` page — self-narrowed audit timeline using `/api/net/whoami` → `/api/net/audit?actorUserId=…`; window picker (24h/7d/30d/All) + limit picker (25…500); service-origin callers see a hint banner. (4) C# ApiClient: new `ListMyActivityAsync(organizationId, fromAt?, limit, ct)` convenience that chains WhoAmIAsync + ListAuditAsync for call-sites that want "my activity" in one call. (5) Docs refresh.

**Sixteenth wave — my-grants self-serve + batch 33** (commits `c9887a2ef` → `418ff2793`): five-slice arc: (1) Engine: new `GET /api/net/whoami/grants` — returns the caller's own scope_grant list, bypassing the `read:ScopeGrant` gate so any user can inspect their own access. (2) Web: new `/network/my-grants` page — grid grouped by entity_type, colour-coded action pills (read=blue / write=amber / delete=red / approve=purple / apply=green); service-origin callers see a hint banner. (3) C# ApiClient: new `ListMyGrantsAsync(organizationId, ct)` wrapper reusing the existing `ScopeGrantDto`. (4) Validation batch 33 (137 → 140) — location-FK hierarchy resolves: `device.building_resolves_active_when_set`, `server.room_resolves_active_when_set`, `server.rack_resolves_active_when_set`. (5) Docs refresh.

**Seventeenth wave — overview recent activity + change-set timeline + batch 34** (commits `4feec7222` → `88d82810b`): five-slice arc: (1) Web: tenant overview dashboard gains a "Recent activity" section rendering the last 10 audit entries across the tenant; click-through to `/network/audit/:entityType/:entityId`. (2) Web: change-set detail page gains a Timeline section showing every audit entry stamped with this set's correlation_id — covers both Set lifecycle events and child-entity audits from apply time. (3) Validation batch 34 (140 → 143) — link lifecycle resolves + server management IP advisory: `link.link_type_resolves_active` (Error), `link.building_resolves_active_when_set` (Warning), `server.management_ip_set_when_active` (Advisory/Warning). (4) C# ApiClient: new `ListChangeSetTimelineAsync(setId, organizationId, limit, ct)` convenience that chains `GetChangeSetAsync` + `ListAuditAsync` to produce the same timeline the web page shows. (5) Docs refresh.

**Eighteenth wave — correlations rollup + batch 35** (commits `1ff5f7b31` → `f6463b1dd`): five-slice arc: (1) Engine: new `GET /api/net/audit/correlations` returns recent distinct correlation ids across the tenant with entry counts + optional change-set metadata (LEFT JOIN `net.change_set` on correlation_id). (2) Web: new `/network/correlations` page — "what bulk operations landed lately?" grid grouped by correlation with drill to change-set detail (when set present) or audit-search (ad-hoc). (3) C# ApiClient: `AuditCorrelationsAsync(organizationId, limit?, ct)` + `RecentCorrelationDto` — sits alongside the other `Audit*Async` rollup wrappers. (4) Validation batch 35 (143 → 146) — three net.port internal-pointer lifecycle resolves: `port.module_resolves_active_when_set`, `port.breakout_parent_resolves_active_when_set`, `port.aggregate_ethernet_resolves_active_when_set`. (5) Docs refresh.

**Nineteenth wave — audit/actions catalog + batch 36** (commits `c5af966ca` → `9cd7baa27`): five-slice arc: (1) Engine: new `GET /api/net/audit/actions` returns the distinct action-string catalog for the tenant, ordered by lastSeenAt DESC with per-action counts. Optional entityType narrower. (2) Web: audit-search page replaces its hard-coded `knownActions` seed list with a live load from the new endpoint — rarely-used actions (ItemRemoved / RolledBack / etc.) now appear in the dropdown when they've actually been emitted. (3) C# ApiClient: `AuditActionsAsync(organizationId, entityType?, limit?, ct)` + `DistinctActionDto` — completes the `Audit*Async` rollup wrapper family. (4) Validation batch 36 (146 → 149) — three more nullable-FK lifecycle resolves on link_endpoint + server_nic: `link_endpoint.device_resolves_active_when_set`, `link_endpoint.ip_address_resolves_active_when_set`, `server_nic.ip_address_resolves_active_when_set`. (5) Docs refresh.

**Twentieth wave — correlations window + overview quick-links + batch 37** (commits `9418c37d1` → `6343bec52`): five-slice arc: (1) Engine: `/api/net/audit/correlations` accepts optional `fromAt` + `toAt` narrowers — a correlation is in-scope if any of its entries land inside the window. (2) Web: correlations page gets a Window select-box (24h / 7d / 30d / All, default 7d); service + C# ApiClient both thread through the new params. (3) Web: overview page gets a chip-bar quick-link section (my-activity / my-grants / correlations / audit-search / data-quality / carver-preview) so operators don't need to memorise the `/network/my-*` paths. (4) Validation batch 37 (149 → 152) — three more nullable-FK resolves on the BGP + provisioning pointers: `device.asn_allocation_resolves_active_when_set`, `server.asn_allocation_resolves_active_when_set`, `server.server_profile_resolves_active_when_set`. (5) Docs refresh.

**Twenty-first wave — detail Summary enrichment + batches 38+39** (commits `c181fdc80` → `fabff188e`): five-slice arc: (1) Validation batch 38 (152 → 155) — closes remaining nullable-FK resolves: `server.loopback_ip_address_resolves_active_when_set`, `link_endpoint.port_resolves_active_when_set`, `server.building_resolves_active_when_set`. (2) Web: device detail Summary tab adds Ports / Modules / Aggregate-ethernet count rows via three parallel thin-list narrower calls on page load. (3) Web: server detail Summary tab adds NIC count row — pre-populates `this.nics` so flipping to the NICs tab doesn't re-fetch. (4) Validation batch 39 (155 → 158) — mixed: `server_nic.target_port_resolves_active_when_set` (Warning), `rack.uheight_within_reason` (Info — Advisory data-quality), `device.firmware_version_set_when_active` (Info — upgrade/CVE tracking). (5) Docs refresh.

**Twenty-second wave — serial uniqueness + detail Summary enrichment + batches 40+41** (commits `13c4466bd` → `da876f26a`): five-slice arc: (1) Validation batch 40 (158 → 161) — three serial-number uniqueness rules (device / server / module, Warning severity; EXISTS-subquery emits every colliding row). (2) Web: VLAN detail Summary adds DHCP-relays count + Linked-subnets count rows (listDhcpRelayTargets(vlanId) narrower + client-side vlanTag filter on listSubnets). (3) Web: Link detail Summary adds Endpoints count + Distinct-devices count (Set-size of deviceHostname from endpoints). (4) Validation batch 41 (161 → 164) — three Advisory Info rules: `device.last_ping_ok_when_active`, `server.last_ping_ok_when_active`, `link.description_set_when_active`. (5) Docs refresh.

**Twenty-third wave — change-sets status summary + subnet Summary + batch 42** (commits `45124d186` → `98b18a0e0`): five-slice arc: (1) Engine: new `GET /api/net/change-sets/summary` returns one row per status (Draft / Submitted / … / Cancelled) with the count in each bucket. Fixed 7-row shape, state-machine order, zero counts filled server-side. (2) Web: /network/change-sets gains a colour-coded pill bar above the filter row — click a pill to filter the grid to that status. (3) Web: subnet detail Summary tab adds IP-addresses count row via `listIpAddresses(subnetId)`; pre-populates `this.addresses` for the Addresses tab cache. Completes the detail-Summary enrichment pass for device / server / vlan / link / subnet. (4) Validation batch 42 (164 → 167) — `device/server.management_ip_unique_per_tenant_when_set` (Warning) + `port.admin_up_false_on_active_status` (Info, Advisory). (5) Docs refresh.

**Twenty-fourth wave — tenant summary endpoint + overview perf win + batch 43** (commits `7b3112c8a` → `47ae68a8d`): five-slice arc: (1) Engine: new `GET /api/net/tenant/summary` returns 21 entity counts in one call via parallel COUNT(*) subselects. (2) Web: overview dashboard drops the 21-parallel forkJoin in favour of the single summary call + a small loadExtraCounts() for the three tiles (VLAN blocks / ASN blocks / Locks) not in the summary payload. (3) C# ApiClient: `TenantSummaryAsync(organizationId, ct)` + `TenantSummaryDto` (21 bigint fields). (4) Validation batch 43 (167 → 170) — `server_nic.admin_up_false_on_active_status` (Info), `port.speed_mbps_reasonable_when_set` (Info — 100-400000 Mbps range), `rack.max_devices_positive_when_set` (Warning — DB has no CHECK). (5) Docs refresh.

**Change-set add-item dialog** (commit `2f35c8897`): Completes the web change-set write flow — operators can create Draft sets (e269ebcf2), add items (this slice), submit / apply / cancel (9a244a1da). Add-item dialog parses free-form JSON for before/after snapshots; conditional form fields based on action (no before on Create, no after on Delete).

**Pool utilization rollup** (commit `e8f333c1d`): `GET /api/net/pools/utilization` — used vs capacity across ASN / VLAN / IP pool families in one round trip. IP pools emit two rows ("IP:Subnets" + "IP:Addresses") so both dimensions surface without a second call. Web surface at `/network/pool-utilization` renders a progress-bar grid with colour thresholds (< 50% green, 50-80% amber, > 80% red).

**CLI flavor admin** (commit `f2aff16a5`): Web page at `/network/cli-flavors` wires `GET /api/net/cli-flavors` + `PUT /api/net/cli-flavors/:code`. Switch-per-row for enabled + default; server enforces one-default-per-tenant by clearing the flag on any other row.

**Device render trigger** (commit `c23213fd4`): "Render now" button on the web render-history page fires `POST /api/net/devices/:id/render-config`. Response body populates the viewer immediately + the grid reloads so the new row appears at the top. 403 ("lacks write:Device") surfaced specifically.

**C# ApiClient parity** (commit `a1c3dafff`): `NetworkingEngineClient.cs` now exposes `ListServersAsync` / `ListSubnetsAsync` / `AuditStatsAsync` / `AuditTrendAsync` / `AuditTopActorsAsync` / `SearchFacetsAsync` / `PoolUtilizationAsync` with matching DTOs. Unblocks WPF modules that consume the new rollup / thin-list endpoints without hand-rolling HttpClient per call-site.

### Central + Secure Merge — ALL 10 PHASES COMPLETE

| Phase | What | Key Deliverables |
|-------|------|-----------------|
| 1 | Auth + RLS | auth-service (Rust), Argon2id re-hash, RLS, PgBouncer, user migration script |
| 2 | API Gateway | Reverse proxy, rate limiting, TLS, WebSocket/SignalR, health aggregation |
| 3 | Task Service | 26 Rust endpoints, SSE stream, batch ops, cursor pagination, Redis pub/sub |
| 4 | Storage + Sync | CAS/MinIO storage, vector clock sync, SQLite offline cache, auto-sync on reconnect |
| 5 | Flutter Mobile | 4 screens (Tasks+Kanban, Network, SD), drift DB, sync client, FCM push, build script |
| 6 | Angular Web | 7 modules (Tasks, Kanban, Network, SD, Audit, Admin, Dashboard), SSE, DxDataGrid/DxTreeList |
| 7 | M365 Audit | audit-service (9 endpoints), GDPR scoring (11 articles), WPF module (5 panels), Angular dashboard |
| 8 | K8s Scaling | Prometheus/Grafana/Jaeger/Loki, PDB, sealed-secrets, read replicas, PgBouncer |
| 9 | IaC + CI/CD | 4 GH Actions workflows, multi-arch builds, backup with retention, env promotion, security-scan job (Trivy, Gitleaks, NuGet audit, npm audit) |
| 10 | Admin Console | Angular 5-tab admin (health, tenants CRUD, users, licenses, infra), WPF 5-panel Global Admin |

7 Rust services (all with source), 3 client surfaces (WPF + Flutter + Angular), 12 K8s manifests.

Phase 3 (Enterprise Desktop) — all complete:
- ✅ Backstage (User Profile + settings, Theme gallery, Connection, About, Switch User, Exit)
- ✅ Splash screen with progress bar
- ✅ Login flow (Windows auto-login → LoginWindow fallback → Offline)
- ✅ Theme gallery (9 installed themes, DX 25.2 RibbonGalleryBarItem + Gallery.ItemClick)
- ✅ Dynamic ribbon per-role (ApplyRibbonPermissions hides tabs/groups/buttons)
- ✅ User info in ribbon header (name + role)
- ✅ Context menus on ALL 10 grids (right-click: CRUD + navigation + export)
- ✅ Print Preview + Column Chooser on all ribbon tabs
- ✅ Column summaries/footers on Devices + Switches grids
- ✅ Cross-panel navigation (Go to Switch A/B, Go to Device from context menu)
- ✅ Set Password dialog (SHA256 + salt)
- ✅ Duplicate record (devices + all link types)
- ✅ DataModifiedMessage auto-refresh (device change → links refresh)
- ✅ DetailDialogService, UndoService, Plugin DLL discovery

Phase 5 (Server-Side SSH):
- ✅ SshOperationsService (ping, config download, BGP sync, batch ping)
- ✅ REST endpoints: /api/ssh/{id}/ping, download-config, sync-bgp, ping-all
- ✅ CredentialEncryptor (AES-256, server-side decrypt only)
- ✅ SignalR SyncProgress streaming during SSH operations

Phase 6 (Background Jobs):
- ✅ JobSchedulerService (checks every 30s, dispatches due jobs)
- ✅ 3 job types: ping_scan (10min), config_backup (24h), bgp_sync (6h)
- ✅ Admin API: /api/jobs (list, enable, disable, interval, run, history)
- ✅ DB migration 029 (job_schedules + job_history)

Phase 7 (Production Ready):
- ✅ Swagger/OpenAPI at /swagger
- ✅ Jobs panel in Admin tab (schedules + history grids)
- ✅ API URL + auto-connect settings in backstage
- ✅ Auto-connect to API server + SignalR on startup when configured

Phase 8 (Icon System + Ribbon Customizer):
- ✅ SVG icon system with Svg.NET rendering + in-memory PNG pre-cache
- ✅ 11,676 Axialist icons (OfficePro + Universal packs) — SVG + PNG 16px + PNG 32px
- ✅ ImagePickerWindow (pack checkboxes, category checkboxes, async batch loading, lazy render)
- ✅ IconService singleton (metadata cache, admin assignments, user overrides, search, bulk import)
- ✅ Local disk PNG cache (%LocalAppData%/Central/icon_cache/)
- ✅ 3-layer ribbon icon override: admin_ribbon_defaults → ribbon_items → user_ribbon_overrides
- ✅ PreloadIconOverridesAsync prevents flash on startup (loads overrides BEFORE ribbon build)
- ✅ Ribbon tree customizer — user tab (RibbonTreePanel) + admin tab (RibbonAdminTreePanel)
- ✅ Admin can push defaults for all users, set display style, link targets
- ✅ Display styles: large (icon+label below), small (icon+label right), smallNoText (icon only)
- ✅ Link targets: panel:PanelName, url:https://..., action:ActionKey, page:PageName
- ✅ Context tabs (Links blue, Switch green, Admin amber)
- ✅ Quick Access Toolbar (Save/Refresh/Undo pinned, user-customizable)
- ✅ DB migrations 030 (icon_library), 032 (ribbon_config) with pg_notify triggers

Engine platform (Core + Shell):
- ✅ ListViewModelBase<T> — Add/Delete/Duplicate/Refresh/Export commands, ContextMenu model, PanelMessageBus
- ✅ ImporterBase<T> — MapRow/ValidateRow/SaveItem with progress, cancel, error navigation
- ✅ ISettingsProvider + SettingsProvider (DB-backed per-user module settings)
- ✅ StartupWorkerManager + StartupWorkerBase (splash pipeline)
- ✅ PanelMessageBus (SelectionChanged, NavigateToPanel, DataModified, RefreshPanel)
- ✅ GridContextMenuBuilder (engine-rendered context menus from model)
- ✅ NotificationService (singleton toast system, Info/Success/Warning/Error, auto-hide)
- ✅ BulkEditWindow (reflection-based, any model, field picker + preview)
- ✅ Toast overlay (bottom-right, color-coded, 4s auto-hide)
- ✅ SignalR DataChanged → toast notifications for real-time multi-user awareness
- ✅ PasswordHasher (Argon2id, legacy SHA256 migration support)
- ✅ CredentialEncryptor (AES-256)
- ✅ SvgHelper (SVG→WPF ImageSource via Svg.NET, currentColor→white, disk cache)
- ✅ IconService (singleton metadata cache, admin/user icon resolution, search, bulk SVG import)
- ✅ RibbonBuilder (DB-backed + fluent API, 3-layer override, separators, split buttons, context tabs)

Phase 4: ✅ COMPLETE (API Server):
- ✅ Central.Api (ASP.NET Core 10 Minimal API, tested with live data)
- ✅ 9 REST endpoint groups (Devices, Switches, Links, VLANs, BGP, Admin, SSH, Jobs, Ribbon)
- ✅ Enterprise endpoint groups: /api/companies (CRUD + /contacts + /addresses sub-routes), /api/contacts (CRUD + /communications), /api/teams (departments, teams, members), /api/addresses (polymorphic by entity_type), /api/profile (user profile), /api/invitations (create/accept/delete), /api/role-templates, /api/groups (CRUD + members + permissions + rules + resources), /api/features (global flags + tenant overrides), /api/security/ip-rules, /api/security/social-providers, /api/user-keys (SSH keys), /api/account/password-reset/*, /verify-email, /tos/*, /api/billing/addons, /discounts, /payment-methods, /quotas, /proration, /invoices
- ✅ CRM endpoint groups: /api/email (accounts, templates, messages, tracking pixel + click redirect), /api/crm/dashboard (revenue, activity, leads, accounts/health, summary, refresh), /api/crm/reports (saved reports + forecasts/live + forecasts/generate), /api/crm/documents (CRUD + /sign + templates), /api/webhooks/event-types + /subscriptions + /deliveries
- ✅ CRM Expansion endpoints:
  - /api/crm/campaigns + /members + /costs + /influence + /refresh-influence
  - /api/crm/marketing/segments + /sequences + /sequences/{id}/enroll + /landing-pages/{slug} (public) + /forms/{slug}/submit (public)
  - /api/crm/salesops/territories + /quotas + /commission-plans + /commission-payouts + /deals/{id}/splits + /accounts/{id}/team + /accounts/{id}/plan + /pipeline-health + /deal-insights
  - /api/crm/cpq/bundles + /pricing-rules + /discount-matrix
  - /api/approvals (generic engine)
  - /api/crm/contracts + /renewals + /clauses + /{id}/milestones + /{id}/sign
  - /api/crm/subscriptions + /mrr-dashboard + /{id}/cancel + /{id}/events
  - /api/crm/revenue/schedules + /entries + /schedules/{id}/generate-entries
  - /api/crm/orders + /{id}/lines
- ✅ Stage 5 endpoints (portals + platform + commerce):
  - /api/portal/users + /magic-link (anonymous auth) + /deal-registrations + /kb/articles (public read + authenticated write) + /kb/categories + /community/threads + /community/threads/{id}/posts (public reads)
  - /api/rules/validation + /rules/workflow (Elsa-integrated) + /rules/execution-log
  - /api/custom-objects/entities + /fields + /records/{entity} + /permissions (field-level security)
  - /api/commerce/import (import wizard) + /cart + /cart/{id}/checkout (cart → order transaction) + /payments (Stripe-compatible)
- ✅ Stage 4 AI endpoints (dual-tier providers + ML + assistant + insights):
  - /api/global-admin/ai (MapAiProviderAdminEndpoints, GlobalAdmin only) — platform providers CRUD, platform models CRUD, feature catalog
  - /api/ai/tenant (MapTenantAiConfigEndpoints, BYOK) — /providers (list/create/update/delete tenant providers), /features (per-feature provider mapping), /resolve/{featureCode} (tenant → platform → none precedence), /usage (logs + aggregates), /providers/{id}/test (round-trip key validation)
  - /api/ai/assistant (MapAiAssistantEndpoints) — /conversations (CRUD), /conversations/{id}/messages (list/post), /templates (prompt templates CRUD), /tools (tool definitions)
  - /api/ai (MapAiInsightsEndpoints) — /scores (ML model scores on leads/deals/accounts), /next-actions, /duplicates + /merge, /enrichment/jobs + /run, /churn-risks, /account-ltv, /calls (recordings + transcripts + sentiment + topics + talk ratio), /ml-models (training jobs)
- ✅ Multi-tenancy sizing endpoints: /api/global-admin/tenants/{id}/sizing (GET sizing + recent provisioning jobs), /api/global-admin/tenants/{id}/provision-dedicated (POST queue provision job), /api/global-admin/tenants/{id}/decommission-dedicated (POST queue decommission job), /api/global-admin/provisioning-jobs (GET platform-wide job queue)
- ✅ SignalR NotificationHub (DataChanged, PingResult, SyncProgress)
- ✅ JWT auth (login → bearer → 25 permission claims)
- ✅ TOTP MFA flow (6 endpoints: /api/auth/mfa/setup, verify, enable, disable, validate, recovery-codes)
- ✅ Auth endpoints: /api/auth/change-password, /api/auth/sessions
- ✅ Batch operations: /api/devices/batch (bulk update/delete)
- ✅ Prometheus metrics: /api/health/metrics
- ✅ Argon2id password hashing (replaces SHA256) with automatic legacy hash migration on login
- ✅ RFC 7807 problem+json error responses on all endpoints
- ✅ Pagination, filtering, and search on list endpoints (cursor + offset)
- ✅ Per-user rate limiting with RFC 6585 429 headers (Retry-After, X-RateLimit-*)
- ✅ Serilog structured JSON logging (console + file + seq sink)
- ✅ Webhook HMAC-SHA256 signature validation (X-Webhook-Signature header)
- ✅ Column whitelist validation preventing SQL injection on sort/filter parameters
- ✅ pg_notify triggers (19+ tables) + ChangeNotifier background service
- ✅ Central.ApiClient (typed HTTP + SignalR client)
- ✅ IDataService abstraction (DirectDb / Api / Offline modes)
- ✅ DirectDbDataService + ApiDataService implementations
- ✅ ConnectivityManager tri-mode + SignalR DataChanged event
- ✅ WPF real-time grid refresh (SignalR → targeted Reload*Async per table)
- ✅ Multi-target Core+Data (net8.0 for API + net8.0-windows for WPF)
- ✅ Dockerfile + pod.yaml (API container ready to enable)

### Enterprise SaaS Engine Buildout — Phases 1-14 + ALL enterprise gaps complete (2026-04-17)

All 66 items from enterprise feature spec delivered:
- User Management: MFA, sessions, profile, invitations, password recovery, email verification, social OAuth (Google/Microsoft/GitHub)
- Address Management: polymorphic addresses + history audit trigger + geocoding columns
- Roles & Permissions: role templates (6 seeded), user-level overrides, inheritance via parent_role_id, effective permissions view
- Teams: departments, team hierarchy (parent_id), team-specific resources, team-based access, team activity tracking
- Groups: entirely new — static + dynamic, assignment rules, group permissions + deny, resource access
- Companies/Tenants: hierarchy, branding, feature flags, cross-company user roles
- Security: IP allowlist, user SSH keys, auto-deprovisioning rules + audit log, domain verification, ToS acceptance
- Registration: self-service signup, invitations, email verification, domain verification, progressive onboarding
- Subscription: trials, grace periods, discounts/coupons, addons (5 seeded), payment methods + POs, proration, annual billing, usage quotas with overage actions

### CRM 29-Phase Buildout — ALL PHASES COMPLETE (2026-04-17)

- Phases 1-5 (Foundation): Done — companies, contacts, teams, addresses, profiles
- Phases 6-10 (Global Admin): Done — onboarding, billing, analytics, search, export
- Phases 11-14 (Admin): Done — invitations, team mgmt, role templates, org chart
- Phases 15-19 (CRM Core): Done — accounts, contacts M:N, deal pipeline, leads, activities
- Phase 20 (Email integration): Done
- Phase 21 (Pipeline viz): Done (data layer)
- Phases 22-23 (Quotes + Products): Done
- Phase 24 (CRM Dashboards): Done
- Phase 25 (CRM Reports + Forecasting): Done
- Phases 26-28 (CRM sync + Email providers + Documents): Done
- Phase 29 (Webhooks + cross-module linking): Done
- **Final: 29/29 phases complete**

### CRM Expansion — Stages 1-3 Complete (2026-04-17)

| Stage | Focus | Migrations | Tables |
|-------|-------|-----------|--------|
| 1 | Marketing Automation | 060-062 | campaigns, segments, email sequences, landing pages, forms, UTM events, attribution touches, campaign influence |
| 2 | Sales Operations | 063-066 | territories, quotas, commissions (plans/tiers/payouts), opportunity splits, account teams, account plans, org charts, forecast adjustments, pipeline health, deal insights |
| 3 | CPQ + Contracts + Revenue | 067-071 | product bundles, pricing rules, discount approval matrix, generic approval engine (reusable), contracts + clauses + templates + milestones + versions, subscriptions + events + MRR dashboard, revenue schedules + entries (ASC 606), orders + order lines |

12 migrations, ~47 new tables, 8 new endpoint files, 35 new permission codes, +42 tests.

### CRM Expansion — Stage 4 Complete (2026-04-17)

AI & Intelligence — dual-tier provider architecture (platform-level providers managed by global_admin, AND per-tenant BYOK so each customer can bring their own Claude/OpenAI API key), ML scoring, AI assistant, dedup + enrichment, churn/LTV, and AI-assisted call capture.

- ✅ **Dual-tier AI providers**: `central_platform.ai_providers` (8 seeded: Anthropic Claude, OpenAI, Azure OpenAI, Vertex, Bedrock, Groq, Ollama, LM Studio) + `ai_models` (9 seeded: Claude Opus 4.7, Sonnet 4.6, Haiku 4.5, GPT-5, GPT-5 Mini, Gemini 2.5 Pro, etc.) + `tenant_ai_providers` (BYOK with AES-256 `api_key_enc`) + `tenant_ai_features` (per-feature provider mapping) + `ai_usage_log` (auto-aggregation trigger) + `resolve_ai_provider()` SQL function (tenant → platform → none precedence)
- ✅ **ML scoring**: `ai_ml_models`, `ai_model_scores`, `ai_next_best_actions`, `ai_training_jobs`, ML score columns on `crm_leads`/`crm_deals`/`crm_accounts`
- ✅ **AI assistant**: `ai_conversations`, `ai_messages`, `ai_prompt_templates` (4 seeded), `ai_tools` (6 seeded)
- ✅ **Dedup + enrichment**: `crm_duplicate_rules`, `crm_duplicates`, `crm_merge_operations`, `crm_enrichment_providers` (5 seeded: Clearbit/Apollo/ZoomInfo/PeopleData/Hunter), `tenant_enrichment_providers` (BYOK), `crm_enrichment_jobs`, `v_contact_duplicate_candidates` view using pg_trgm
- ✅ **Churn + LTV + Calls**: `crm_churn_risks`, `crm_account_ltv`, `crm_call_recordings` (transcript + sentiment + topics + talk ratio), `crm_auto_capture_rules`, `crm_auto_capture_queue`, 8 new AI-related webhook event types
- ✅ **Desktop/API**: `AiModels.cs` (~20 model classes), `ITenantAiProviderResolver` + `TenantAiProviderResolver` implementation (2-min cache, uses `CredentialEncryptor` for BYOK key encryption), `AiEndpoints.cs` with 4 endpoint groups
- ✅ **16 new permission codes**: AiProvidersRead/Admin, AiTenantConfig, AiUse, AiAssistantUse/Admin, AiScoringRead/Train, AiDedupRead/Merge, AiEnrichmentRead/Run, AiChurnRead, AiCallsRead/Admin, AiUsageRead
- **Stage 4 is now complete. All 5 stages of the CRM expansion plan are complete.**

### CRM Expansion — Stage 5 Complete (2026-04-17)

- ✅ Portal infrastructure (customer + partner portal users, separate from `app_users`)
- ✅ Magic-link email authentication (30-min expiry, single-use, SHA256-hashed tokens)
- ✅ Knowledge base with tsvector full-text search + category hierarchy + view/helpful counters
- ✅ Community threads + posts with vote tracking (app_users OR portal_users as author)
- ✅ Generic validation rules engine (JSONLogic, pre-save, per-entity)
- ✅ Workflow rules integrated with **existing Elsa engine** (not a duplicate runtime) — `broadcast_record_change` trigger on 7 CRM tables publishes pg_notify events consumed by the Elsa dispatcher
- ✅ Custom objects framework (metadata-driven `custom_entities` + `custom_fields` + `custom_entity_records` with jsonb storage + GIN index)
- ✅ Field-level security (`field_permissions` per role per entity per field: hidden/read/write) with `get_field_permission()` fallback
- ✅ Import wizard with dedup strategies (create_new/update_existing/skip_duplicates/merge), dry_run, per-row error/warning arrays, `pg_notify('import_queue', id)` for background processing
- ✅ Commerce: shopping_carts + cart_items (auto-recalc trigger), cart → order checkout transaction, payments (Stripe-compatible: `stripe_payment_intent_id`, `stripe_charge_id`, `last4`, `brand`)
- **5/5 stages of CRM expansion plan complete (all stages delivered — Stage 4 AI & Intelligence now complete as of 2026-04-17)**

### CRM WPF Module — NEW (2026-04-17)

`Central.Module.CRM` — registered in Bootstrapper at SortOrder 40, ribbon tab "CRM" with Actions + Data + Panels groups (9 panel toggles):
- **CrmDataService** — async PG queries for accounts / deals / leads / KPIs / pipeline
- **CrmAccountsPanel** — DevExpress grid with filter/group/sort on 10 columns
- **CrmDealsPanel** — DevExpress grid with summary text (open count, pipeline value, weighted)
- **CrmPipelinePanel** — Kanban-style columns per open stage with deal cards (title, account, value, probability, close date, owner) — drag-drop ready
- **CrmDashboardPanel** — KPI tiles (sales + customers) + per-stage progress bars

### Solution Structure

Restructured 2026-04-17 — see [docs/REPO_STRUCTURE_PLAN.md](docs/REPO_STRUCTURE_PLAN.md) for rationale. Root is flat; each top-level folder has one job.

```
/
├── apps/                     End-user apps
│   ├── desktop/              Central.Desktop — WPF shell (DevExpress 25.2)
│   └── web/                  Angular 21 + DevExtreme web client
│
├── services/                 Backend services
│   ├── api/                  Central.Api — ASP.NET Core 10 REST + SignalR
│   └── tenant-provisioner/   Rust — K8s-aware tenant DB provisioning
│
├── libs/                     Shared .NET libraries
│   ├── engine/               Central.Engine — auth, models, widgets, services (was Core)
│   ├── persistence/          Central.Persistence — Npgsql repositories + AppLogger (was Data)
│   ├── api-client/           Central.ApiClient — typed HTTP + SignalR client (was Api.Client)
│   ├── workflows/            Central.Workflows — Elsa 3.5.3 integration
│   ├── security/             Central.Security — ABAC policy engine
│   ├── tenancy/              Central.Tenancy — multi-tenant connection resolution
│   ├── licensing/            Central.Licensing — license keys, subscriptions, module grants
│   ├── observability/        Central.Observability
│   ├── collaboration/        Central.Collaboration — presence
│   ├── protection/           Central.Protection
│   └── update-client/        Central.UpdateClient
│
├── modules/                  WPF feature modules (pluggable into apps/desktop)
│   ├── global/               Always-on core — landing dashboard + per-tenant
│   │                         admin + platform admin. Internal subfolders
│   │                         Admin/, Dashboard/, Platform/. Merged
│   │                         2026-04-17 from former admin + dashboard +
│   │                         global-admin modules.
│   ├── audit/                Audit log viewer (+ Dashboards/GdprDashboardPanel)
│   ├── crm/                  Accounts, deals, pipeline Kanban
│   │                         (+ Dashboards/CrmDashboardPanel)
│   ├── networking/           IPAM devices + switches + routing + VLANs +
│   │                         links — one self-contained tenant-togglable
│   │                         module covering every networking concern.
│   │                         Internal subfolders: Devices/, Switches/,
│   │                         Routing/, Vlans/, Links/, Dashboards/,
│   │                         Hierarchy/ (Phase 2), Pools/ (Phase 3),
│   │                         Servers/ (Phase 6f).
│   │                         Devices folded in 2026-04-17 after earlier
│   │                         four-way merge. Networking engine buildout
│   │                         phases 1-6 complete 2026-04-18.
│   ├── projects/             Project + task management (portfolios,
│   │                         programmes, sprints, Kanban, Gantt — 16 panels)
│   │                         (+ Dashboards/TaskDashboardPanel + QADashboardPanel)
│   ├── service-desk/         ManageEngine sync, teams, groups
│
├── tests/
│   └── dotnet/               Central.Tests — 2,382 unit + integration tests
│
├── db/                       Migrations (001-082+), schema.sql, seed
├── infra/                    Terraform, Terragrunt, K8s, Ansible, Vagrant
├── tools/                    Dev utilities — icons, parser, scripts (not shipped)
├── assets/                   Static assets — icon packs (OfficePro + Universal)
├── config/                   Runtime config (auth-service.toml, gateway.env)
├── docs/                     Architecture, buildout plans, reference docs
├── backups/                  Local-only DB dumps (gitignored by pattern)
├── .github/, .vscode/, .claude/
├── CLAUDE.md                 This file
├── NuGet.config
└── Central.sln               References every .NET project
```

**Planned but not yet built** — these seven Rust services are referenced in architecture docs but do not exist in the current tree. When built, each lands under `services/<name>/`:

| Service | Purpose |
|---------|---------|
| auth | MFA / WebAuthn / SAML / OIDC / JWT issuing |
| admin | Tenant CRUD, setup wizard, license issuing |
| gateway | Reverse proxy + TLS + rate limiting + SignalR passthrough |
| task | Dedicated high-throughput task backend with SSE + Redis pub/sub |
| storage | CAS with MinIO/S3, BLAKE3 dedup, multipart upload |
| sync | Offline-first vector-clock sync for mobile/desktop clients |
| audit | M365 forensics, GDPR scoring, investigation workflows |

### Legacy TotalLink Source — Removed

The `source/` folder (TotalLink WPF client + IntegrationServer + TotalLink server,
97 projects, 1,650 .cs files) was removed on 2026-04-17. Patterns that were borrowed
into Central are documented in [docs/TOTALLINK_PATTERNS.md](docs/TOTALLINK_PATTERNS.md)
(module registration, dynamic ribbon, `ListViewModelBase<T>`, detail dialogs,
entity-VM sync, app context, facade pattern, startup pipeline).

Business logic preserved for future modules lives in:
- [docs/LEGACY_MIGRATION.md](docs/LEGACY_MIGRATION.md) — tree-by-tree audit + what
  was discarded and why
- [docs/REFERENCE_SALES_ORDER_RELEASE.md](docs/REFERENCE_SALES_ORDER_RELEASE.md) —
  atomic release, partial delivery, bin allocation, locking/retry (for a future
  Fulfilment module)
- [docs/REFERENCE_INVENTORY_STOCK_MODEL.md](docs/REFERENCE_INVENTORY_STOCK_MODEL.md) —
  SKU / PhysicalStock / BinLocation data model (for a future Inventory module)
- [docs/REFERENCE_WAREHOUSING.md](docs/REFERENCE_WAREHOUSING.md) — warehouse + bin
  hierarchy, WMS features the legacy lacks, PG schema sketch
- [docs/REFERENCE_SEQUENCE_GENERATION.md](docs/REFERENCE_SEQUENCE_GENERATION.md) —
  app-level lock-safe sequence pattern for formatted codes (e.g. `SO-2026-00012`)

## Project Purpose

Build switch configurations from scratch using an Excel guide (`switch_guide.xlsx`)
as the source of truth, generate PicOS-compatible `set` command output, and store
everything in PostgreSQL for the WPF desktop application to query.

## Infrastructure

- **Runtime**: Kubernetes 1.31 on VMware Workstation (7 nodes, Terraform + Terragrunt IaC)
- **Database**: PostgreSQL 18.3 HA StatefulSet in K8s (primary + streaming replica)
- **Cache**: Redis 7 StatefulSet in K8s
- **Containers**: Podman for builds, K8s for running
- **Load Balancing**: MetalLB L2 (192.168.56.200-220)
- **No Docker** — Podman builds only, K8s for orchestration

### K8s Cluster (primary environment)

```
k8s-master      192.168.56.10   control-plane (Calico + MetalLB)
k8s-worker-01   192.168.56.21   role=database  (4 CPU / 8GB — PG primary)
k8s-worker-02   192.168.56.22   role=database  (4 CPU / 8GB — PG replica)
k8s-worker-03   192.168.56.23   role=general
k8s-worker-04   192.168.56.24   role=general
k8s-worker-05   192.168.56.25   role=general
k8s-worker-06   192.168.56.26   role=general
```

### Service endpoints (via MetalLB)

| Service | External IP | Port |
|---------|------------|------|
| Central API | `http://192.168.56.200` | 5000 |
| PostgreSQL (write) | `192.168.56.201` | 5432 |
| PostgreSQL (read) | `192.168.56.202` | 5432 |
| Container Registry | `192.168.56.10` | 30500 |

### Post-reboot startup

When the machine is rebooted, start **all** services in this order:

```bash
# 1. K8s VMs (Vagrant checks if already running)
cd infra/vagrant && vagrant status
# if any VM is "poweroff": vagrant up

# 2. Refresh kubeconfig if cert errors after reboot
vagrant ssh k8s-master -c "sudo cat /etc/kubernetes/admin.conf" > ~/.kube/central-local.conf

# 3. Verify K8s cluster + services
export KUBECONFIG=~/.kube/central-local.conf
kubectl get nodes
kubectl -n central get pods

# 4. Start Angular dev server (background)
cd web-client && npx ng serve --port 4200 --host 0.0.0.0 &
# Access at http://localhost:4200

# 5. Start FastAPI web app (background)
./run.sh &
# Access at http://localhost:8080

# 6. WPF desktop (GUI — launch manually from bin/x64/Release)
```

**Rule for Claude:** When user says "rebooted" or "start services", run ALL of steps 1-5 above. Verify each reaches a "ready" state before reporting done.

**Rule for versions:** Always check for the **latest stable** before writing a version number. Don't guess image tags or package versions — query the registry:
- Docker: `curl -s "https://hub.docker.com/v2/repositories/<owner>/<image>/tags/?page_size=20"`
- GitHub: `gh release list --repo owner/name --limit 5`
- npm: `npm view <pkg> version`
- Cargo: `cargo search <crate>`
- NuGet: `dotnet package search <pkg>`

A version matrix is maintained in `docs/CREDENTIALS.md` — keep it current when bumping anything.

### Daily commands

```bash
# Set K8s context
export KUBECONFIG=~/.kube/central-local.conf

# Check status
./infra/setup.sh k8s-status

# Connect to DB
./infra/setup.sh k8s-psql central
./infra/setup.sh k8s-psql secure_auth

# View logs
./infra/setup.sh k8s-logs central-api
./infra/setup.sh k8s-logs auth-service

# Build + push container images
podman build -f Central.Api/Containerfile -t central-api:latest .
podman tag central-api:latest 192.168.56.10:30500/central/central-api:latest
podman push 192.168.56.10:30500/central/central-api:latest --tls-verify=false

# Deploy to K8s
./infra/setup.sh k8s-deploy

# Start/stop VMs
cd infra/vagrant && vagrant halt    # stop all
cd infra/vagrant && vagrant up      # start all
```

### IaC Structure (Terraform + Terragrunt)

```
infra/
├── terragrunt.hcl                    # Root config (S3 backend, provider)
├── modules/                          # 11 Terraform modules (33 .tf files)
│   ├── vpc, eks, rds, elasticache    # AWS infrastructure
│   ├── ecr, s3, kms, secrets         # Storage + security
│   ├── monitoring, k8s-service       # Observability + K8s deployments
│   └── local-cluster/                # VMware VMs via Vagrant
├── environments/                     # Terragrunt per-environment configs
│   ├── _envcommon/                   # DRY module templates
│   ├── local/                        # VMware Workstation (current)
│   ├── dev/, staging/, prod/         # AWS environments
├── k8s/base/                         # Kustomize manifests (8 YAML)
├── vagrant/                          # Generated Vagrantfile
├── ansible/                          # K8s bootstrap roles
└── scripts/                          # Migration + utility scripts
```

## Database

**Write DSN**: `postgresql://central:central@192.168.56.201:5432/central`
**Read DSN**: `postgresql://central:central@192.168.56.202:5432/central`
**Auth DSN**: `postgresql://central:central@192.168.56.201:5432/secure_auth`
**Desktop DSN**: Set via `CENTRAL_DSN` environment variable (see docs/CREDENTIALS.md)

Schema file: [db/schema.sql](db/schema.sql)

Key tables:

| Table | Purpose |
|---|---|
| `switches` | One row per switch (hostname, site, role, loopback) |
| `vlans` | VLANs per switch with description and L3 interface |
| `interfaces` | Physical/breakout interfaces (speed, VLAN, mode) |
| `l3_interfaces` | SVIs and loopbacks (IP/prefix) |
| `bgp_config` | BGP AS, router-id, multipath per switch |
| `bgp_neighbors` | eBGP peers with BFD |
| `bgp_networks` | Advertised prefixes |
| `vrrp_config` | VRRP VIPs per interface |
| `static_routes` | Static routes per switch |
| `dhcp_relay` | DHCP relay helpers |
| `firewall_filters` | DSCP/QoS firewall filters (JSONB from/then params) |
| `cos_forwarding_classes` | QoS forwarding classes |
| `switch_guide` | Inventory from Excel import |
| `switch_connections` | Port-level connections from Excel |
| `config_templates` | Generated config snippets for the desktop app |

Useful views: `v_switch_summary`, `v_bgp_peers`, `v_vlan_ip_map`

Apply schema:
```bash
psql $DSN -f db/schema.sql
```

## Python Scripts

Install dependencies:
```bash
pip install psycopg2-binary openpyxl
```

### Parse PicOS config files

```bash
# Parse all .txt files in current directory, dump JSON
python parser/picos_parser.py .

# Parse a single file
python -c "
from parser.picos_parser import PicOSParser
cfg = PicOSParser('MEP-91-CORE02.txt').parse()
print(cfg.hostname, cfg.site, cfg.role)
print('VLANs:', len(cfg.vlans))
print('BGP AS:', cfg.bgp.local_as if cfg.bgp else None)
"
```

### Inspect the Excel guide

```bash
python parser/excel_importer.py switch_guide.xlsx
```

### Load everything into PostgreSQL

```bash
# Load configs + guide
python parser/db_loader.py \
  --dsn "postgresql://central:central@localhost:5432/central" \
  --config-dir . \
  --guide switch_guide.xlsx

# Configs only
python parser/db_loader.py --configs-only

# Guide only
python parser/db_loader.py --guide-only
```

Environment variable shortcut:
```bash
export CENTRAL_DSN="postgresql://central:central@localhost:5432/central"
python parser/db_loader.py
```

## Config File Format (PicOS)

Files are named `{SITE}-{ROLE}.txt`, e.g. `MEP-91-CORE02.txt`.

Config uses `set` CLI syntax:
```
set system hostname "MEP-91-CORE02"
set vlans vlan-id 101 description "IT"
set interface gigabit-ethernet xe-1/1/20 description "Prox01-Trunk"
set interface gigabit-ethernet xe-1/1/20 family ethernet-switching native-vlan-id 120
set interface gigabit-ethernet xe-1/1/20 family ethernet-switching port-mode "trunk"
set interface gigabit-ethernet xe-1/1/20 family ethernet-switching vlan members 1-500
set l3-interface vlan-interface vlan-101 address 10.11.101.2 prefix-length 24
set protocols bgp local-as "65112"
set protocols bgp neighbor 10.5.17.2 remote-as "65121"
set protocols vrrp interface vlan-101 vrid 1 ip 10.11.101.254
```

### Switch naming convention

```
MEP-{site_num}-{ROLE}{instance}
  MEP-91-CORE02   → site=MEP-91, role=core   (FS N-series 25/100G)
  MEP-93-L1-CORE02 → site=MEP-93, role=l1   (distribution)
  MEP-96-L2-CORE02 → site=MEP-96, role=l2   (access, ge- ports)
```

### Interface naming

| Prefix | Type | Speed |
|---|---|---|
| `xe-1/1/N` | 10G SFP+ | up to 100G with speed override |
| `ge-1/1/N` | 1G copper/SFP | 1G |
| `xe-1/1/N.M` | Breakout sub-interface | 10/25G |
| `ae-N` | LACP aggregate | varies |

## Sites in This Project

| Site | Role | Switches |
|---|---|---|
| MEP-91 | Building 91 | CORE02 (eBGP AS 65112) |
| MEP-92 | Building 92 | CORE01 (eBGP AS 65121) |
| MEP-93 | Building 93 | L1-CORE02 (distribution) |
| MEP-94 | Building 94 | CORE01 |
| MEP-96 | Building 96 | L2-CORE02 (access) |

eBGP peerings: 91↔92 over vlan-1017/vlan-262, 91→93 over vlan-1022

## Web App

FastAPI + HTMX + Jinja2. No build step — edit templates and reload.

### Authentication

HTTP Basic Auth is enabled. Default credentials: **admin / admin**

Note: SSH password for the switches (admin / admin123) is separate — enter it in the connectivity panel or save it via Edit Switch.

Override via environment variables:
```bash
export CENTRAL_USER=admin
export CENTRAL_PASS=admin123
```

### Start

```bash
./run.sh              # starts Podman pod + web server on :7472 (default)
./run.sh 9000         # custom port
```

Or manually:
```bash
pip install fastapi uvicorn[standard] psycopg2-binary jinja2 python-multipart paramiko openpyxl
export CENTRAL_DSN="postgresql://central:central@localhost:5432/central"
python -m uvicorn web.app:app --host 127.0.0.1 --port 8080 --reload
```

To restart after code changes (uvicorn --reload may not auto-pick-up on Windows):
```powershell
# Kill all Python processes bound to 8080, then restart run.sh
Get-Process python | Stop-Process -Force
```

### Pages

| URL | Description |
|---|---|
| `/` | Dashboard — all switches with stats |
| `/ipam` | IPAM grid — all 987 devices, sortable/filterable/groupable, links to config |
| `/switches/{hostname}` | Switch detail — VLANs, interfaces, BGP, routing, QoS, connectivity |
| `/switches/{hostname}/running-configs` | Downloaded config history |
| `/guide` | Switch guide from Excel — filterable |
| `/guide/{id}` | Per-switch connections — toggle enabled/disabled with one click |
| `/switches/{hostname}/preview` | Generated PicOS config — copy or download |
| `/import` | Upload .txt config files or .xlsx guide |

### Connectivity panel (switch detail page)

Each switch detail page has a connectivity bar at the top:
- **Ping** — pings `management_ip` (VLAN-152), shows latency, updates DB
- **Test SSH** — opens SSH connection to verify credentials
- **Download Running Config** — SSHs and pulls `show configuration`, stores in `running_configs` table, diffs vs previous

SSH uses `management_ip` (VLAN-152 address), `ssh_username` (default: root), `ssh_port` (default: 22).
Password can be entered inline or saved via Edit Switch form.

### Management IPs (VLAN-152)

| Switch | Management IP |
|---|---|
| MEP-91-CORE02 | 10.11.152.2 |
| MEP-92-CORE01 | 10.12.152.1 |
| MEP-93-L1-CORE02 | 10.13.152.2 |
| MEP-94-CORE01 | 10.14.152.1 |
| MEP-96-L2-CORE02 | 10.16.152.2 |

### How config generation works

1. Load parsed configs into DB via `parser/db_loader.py`
2. Import `switch_guide.xlsx` to get port connections
3. In `/guide/{id}`, toggle connections on/off (HTMX, no page reload)
4. Preview/Export builds PicOS `set` commands:
   - Infrastructure/trunk ports always included
   - Access ports only included if `enabled = true` in `switch_connections`
5. Download or copy the raw config text

### Web files

```
web/
├── app.py                      FastAPI routes + config generator + Basic Auth middleware
├── ssh_utils.py                ping_host(), ssh_download_config(), diff_configs()
├── requirements.txt
├── templates/
│   ├── base.html               Nav + layout
│   ├── dashboard.html          Switch card grid
│   ├── switch_detail.html      Tabbed detail + connectivity panel (HTMX)
│   ├── switch_form.html        Create/Edit switch (includes management IP + SSH fields)
│   ├── guide.html              Guide table with filters
│   ├── guide_detail.html       Per-switch connections + toggles
│   ├── preview.html            Config output + copy/download
│   ├── import.html             File upload forms
│   ├── running_configs.html    Downloaded config history list
│   ├── running_config_detail.html  Full config + coloured diff
│   ├── running_config_compare.html Side-by-side version compare
│   ├── generate.html           Config generation page
│   ├── ipam.html               IPAM device grid
│   ├── _connection_row.html    HTMX partial for connection toggle
│   ├── _ping_status.html       HTMX partial — ping result badge
│   ├── _ssh_status.html        HTMX partial — SSH test result badge
│   └── _ssh_download_result.html  HTMX partial — download result
└── static/
    ├── css/app.css             Dark theme styles
    └── js/app.js               Tabs, copy, drag-drop
```

### Database tables added by migrations

| Migration | Adds |
|---|---|
| 002_builder.sql | `vlan_templates`, extra columns on `switch_connections` and `bgp_config` |
| 003_connectivity.sql | `management_ip`, `ssh_*`, `last_ping_*`, `last_ssh_*` on `switches`; `running_configs` table |
| 004_ipam_fields.sql | Additional IPAM device fields |
| 009_config_ranges.sql | Config range definitions |
| 010_excel_sheets.sql | Excel sheet import tracking |
| 011_view_reserved.sql | Reserved device view |
| 012_config_versions.sql | Config version history for compare |
| 023_bgp_sync.sql | `fast_external_failover`, `bestpath_multipath_relax`, `last_synced` on `bgp_config` |

## VLAN Standard (across sites)

| VLAN | Description |
|---|---|
| 82 | Switch Management |
| 101 | IT |
| 105 | Conference Rooms |
| 106 | Printers/MFD |
| 112 | Data Staff |
| 120 | Servers |
| 128 | Data Staff WiFi |
| 136 | VoIP Staff |
| 152 | Devices (management trunk native) |
| 176 | Backup Replication |
| 235–239 | DMZ zones |
| 248 | DMZ Staff Patching |
| 254 | DMZ Cyber Isolation |
| 1017+ | eBGP interconnect VLANs |

## QoS / CoS (consistent across all switches)

| Class | Priority | Traffic |
|---|---|---|
| Teams-Voice | 6 | UDP 50000-50019 DSCP 46 |
| Teams-Video | 6 | UDP 50020-50039, 3478-3481 DSCP 34 |
| Teams-Screen-Share | 6 | UDP/TCP 50040-50059 DSCP 18 |
| Call-Sig-NetCtrl | 4 | SIP 5060/5061, H.323 1720 DSCP 24 |
| Telepresence | 5 | — |
| O365-FileShare-AV | — | SMB, SMTP, IMAP DSCP 16 |
| Low-priority | 1 | Default |

## Desktop App (WPF)

Location: `Central.sln`

**Stack:** C# / .NET 10 / WPF · DevExpress 25.2 (WPF subscription) · Npgsql 10.0.2 · Svg.NET 3.4.7

**Build:**
```powershell
cd desktop
dotnet build Central.sln --configuration Release -p:Platform=x64
# Output: apps/bin/x64/Release/net10.0-windows/Central.exe
```

**Run:**
```powershell
cd apps/bin/x64/Release/net10.0-windows
./Central.exe
```

**DevExpress license:** Place `DevExpress_License.txt` in `%AppData%\DevExpress` or run the DX Unified Installer. Without license = evaluation mode (watermark).

**DB connection:** Reads `CENTRAL_DSN` env var or falls back to `Host=localhost;Port=5432;Database=central;Username=central;Password=central`. ConnectivityManager handles offline mode with 5s connection timeout and 10s background retry — app starts in offline mode if DB unreachable, auto-loads data when connection restores.

### Environment Variables

| Variable | Required | Description |
|---|---|---|
| `CENTRAL_DSN` | No | PostgreSQL connection string (desktop + API) |
| `CENTRAL_JWT_SECRET` | **Yes** (API) | JWT signing key — no default, must be set |
| `AUTH_SERVICE_JWT_SECRET` | **Yes** (auth-service) | Rust auth-service JWT signing key — no default |
| `CENTRAL_CREDENTIAL_KEY` | No | AES-256 key for SSH credential encryption |
| `CENTRAL_CORS_ORIGINS` | No | Comma-separated allowed CORS origins |
| `CENTRAL_WEBHOOK_SECRET` | No | HMAC-SHA256 secret for webhook signature validation |
| `CENTRAL_PG_PASSWORD` | No | DB password override (desktop fallback when DSN not set) |
| `CENTRAL_USER` | No | Web app HTTP Basic Auth username (default: admin) |
| `CENTRAL_PASS` | No | Web app HTTP Basic Auth password (default: admin123) |

### Ribbon Tabs

| Tab | Groups |
|---|---|
| Home | Connection, Export, Web App, Layout (Save/Restore Default via cog) |
| Devices | Actions (New/Edit/Delete), Group By, Filter, Panels (IPAM/Switches/Details), Connectivity (Sync BGP/Sync All BGP) |
| Switches | Actions, Connectivity (Ping All/Ping Selected), Panels, Data |
| Links | (context tab, blue) — Actions, Panels |
| Routing | (context tab) — BGP actions |
| VLANs | (context tab) — VLAN actions |
| Tasks | (context tab) — Task actions |
| Admin | Actions (New/Edit/Delete — routes by active panel), Panels (Roles/Users/Lookups/Details/Ribbon Config/Ribbon Admin), Data |

Context tabs (Links blue, Switch green, Admin amber) appear/disappear based on the active panel.

Quick Access Toolbar: Save/Refresh/Undo pinned, user-customizable.

### Ribbon Customization (3-layer override)

| Layer | Table | Priority | Who |
|---|---|---|---|
| 1 | `admin_ribbon_defaults` | Lowest | Admin pushes defaults for all users |
| 2 | `ribbon_items` (DB seed) | Middle | Module registration / system defaults |
| 3 | `user_ribbon_overrides` | Highest | Per-user icon, text, visibility |

`PreloadIconOverridesAsync` runs BEFORE ribbon build to prevent icon flash on startup.

### Document Panels

| Panel | Content | Closeable |
|---|---|---|
| IPAM | DeviceRecord grid — inline editing, dropdown columns, RESERVED row highlighting | Yes |
| Configured Switches | SwitchRecord grid — ping/SSH status icons (green/red/grey circles), latency | Yes |
| Roles & Permissions | Split: roles grid left, permissions tree + site access checkboxes right | Yes |
| Users | User grid with role dropdown bound to DB roles | Yes |
| Lookup Values | Category/Value grid for dropdown options | Yes |
| BGP | Master-detail: top grid (BGP config per switch), bottom tabs (Neighbors, Advertised Networks). SSH sync from live switches | Yes |
| Config Compare | Side-by-side diff with line numbers, pink highlighting, synced scroll — toggled via Compare button in Details > Config tab | Yes |
| Asset Details | Right-docked detail panel, updates on grid row selection | Yes |
| Ribbon Config | User ribbon customizer tree — icon picker, custom text, hide/show, reorder, apply/reset | Yes |
| Ribbon Admin | Admin ribbon customizer tree — set defaults for all users, display style, link targets, push all | Yes |
| Dashboard | KPI cards, notification center, recent activity — landing page after login | Yes |

### Authentication & RBAC

- **Auto-login** by Windows username match to `app_users` table
- **Roles** (Admin/Operator/Viewer + custom): per-module permissions (View/Edit/Delete) for ipam, switches, admin
- **Site-level access**: `role_sites` table controls which buildings each role can see — data is filtered at SQL level (`WHERE building = ANY(@sites)`) so unauthorized data never leaves the DB
- Hidden UI: ribbon buttons use `IsVisible`, panels use `DockController.Close()`, grid editing disabled per role

### Key Architecture Patterns

- **ItemClick events** instead of Command binding for DevExpress BarButtonItem (Command binding is unreliable in DX ribbon)
- **ComboBoxEditSettings** wired in code-behind via `BindComboSources()` (XAML binding fails — EditSettings are not in the visual tree)
- **DockController.Close()/Restore()** for panel toggle (not Visibility.Collapsed — that breaks DX tab close buttons)
- **INotifyPropertyChanged** on all models for live grid updates (e.g., ping status icons updating in real-time)
- **ValidateRow** event on TableView for auto-save on row commit
- **_deletePending flag** + capture CurrentItem before CancelRowEdit() for delete-during-edit
- **LayoutService** saves/restores grid layouts, dock layout, window bounds, panel states, preferences to `user_settings` table per user
- **Settings cog** in ribbon page header (BarSubItem via code-behind) for Save Layout / Restore Default
- **NavigationStyle="Cell"** required for inline editing (Row navigation prevents cell editing)
- **ShownEditor** event must be wired in code-behind constructor, not XAML attributes
- **Saved layouts override XAML** — skip layout restore for newly-editable grids until user re-saves
- **ConnectivityManager** handles DB connection with offline mode, background retry, ConnectionChanged event
- **Multi-column port dropdowns** via ComboBoxEdit ItemTemplate (Interface, Admin, Link, Speed, Description, LLDP)
- **Global ComboBox clear** via EventManager.RegisterClassHandler for Delete/Backspace on ComboBoxEdit
- **Natural numeric sort** for interface names (xe-1/1/2 before xe-1/1/10)
- **3-layer ribbon icon override** — PreloadIconOverridesAsync loads admin_ribbon_defaults + user_ribbon_overrides BEFORE ribbon build (no flash)
- **SVG icon rendering** via Svg.NET — SvgHelper replaces "currentColor" with white, caches in-memory + disk (%LocalAppData%/Central/icon_cache/)
- **IconService singleton** — metadata cache on startup, on-demand PNG byte loading, admin/user resolution chain
- **ImagePickerWindow** — pack + category checkboxes, async batch load from DB (png_32 preferred), generation counter cancels stale loads
- **RibbonTreeItem** — flat tree node model with Id/ParentId for TreeListControl, NodeType discriminator (page/group/item/separator)
- **Context tabs** — Links (blue), Switch (green), Admin (amber) — appear/disappear by active panel
- **Quick Access Toolbar** — Save/Refresh/Undo pinned, user-customizable
- **Argon2id password hashing** — replaces SHA256+salt; legacy hashes auto-migrated on successful login
- **TOTP MFA** — 6 API endpoints (setup/verify/enable/disable/validate/recovery-codes), QR code provisioning
- **RFC 7807 problem+json** — all API error responses use standard ProblemDetails format
- **Per-user rate limiting** — sliding window, RFC 6585 headers (Retry-After, X-RateLimit-Limit/Remaining/Reset)
- **Column whitelist validation** — sort/filter/search parameters validated against allowed column names (prevents SQL injection)
- **Webhook HMAC-SHA256** — inbound webhooks validated via X-Webhook-Signature header using CENTRAL_WEBHOOK_SECRET
- **Serilog structured logging** — JSON output to console + rolling file + Seq sink, correlation IDs on all requests
- **Prometheus /api/health/metrics** — request counts, latencies, active connections, GC stats for Grafana dashboards
- **Excel/PDF/CSV export** — available on all grids via ribbon Export group (DX XlsxExportOptions, PdfExportOptions)

### Desktop Files

```
apps/
├── App.xaml.cs                     Startup, auto-login, session init
├── MainWindow.xaml                 Ribbon + DockLayoutManager + all grids
├── MainWindow.xaml.cs              Event handlers, panel routing, ping, layout, config compare, ribbon overrides
├── ImagePickerWindow.xaml/.cs      Icon picker dialog — pack/category checkboxes, async batch PNG load, search
├── BulkEditWindow.xaml/.cs         Reflection-based bulk edit (any model, field picker + preview)
├── LoginWindow.xaml/.cs            Login dialog (fallback from Windows auto-login)
├── SetPasswordWindow.xaml/.cs      Change password dialog (SHA256 + salt)
├── SplashWindow.xaml/.cs           Splash screen with startup worker progress
├── Converters/
│   ├── NullToBoolConverter.cs
│   └── StringToBrushConverter.cs
├── Data/
│   └── DbRepository.cs            All DB CRUD (devices, switches, users, roles, sites, lookups, settings, config versions)
├── Models/
│   ├── (models now in libs/engine/Models/ — see below)
├── Services/
│   ├── UserSession.cs              Static singleton — CurrentUser, Permissions, AllowedSites
│   ├── PingService.cs              Parallel ping via System.Net.NetworkInformation
│   ├── SshService.cs               SSH connectivity, config download, BGP config parsing
│   ├── LayoutService.cs            Save/restore grids, dock, window bounds, preferences
│   ├── ConnectivityManager.cs      DB connection management — 5s timeout, 10s retry, offline mode
│   ├── SvgHelper.cs                SVG→WPF ImageSource via Svg.NET, currentColor→white, disk + memory cache
│   ├── GridContextMenuBuilder.cs   One-line right-click menus for any grid
│   ├── ConfigCompareHelper.cs      Side-by-side diff panel helper
│   ├── BackstageHelper.cs          Backstage tab wiring
│   ├── DetailDialogService.cs      Nested entity detail dialogs
│   ├── SettingsProvider.cs         DB-backed per-user module settings
│   ├── ConfigBuilderService.cs     PicOS config generation
│   ├── LinkEditorHelper.cs         Link editing helpers
│   ├── DirectDbDataService.cs      IDataService → direct PostgreSQL
│   ├── SshProxy.cs                 SSH proxy service
│   ├── SwitchSyncService.cs        Switch config sync
│   └── InputPrompt.cs              Simple text input dialog
├── ViewModels/
│   ├── MainViewModel.cs            All collections, CRUD methods, permission properties
│   └── RelayCommand.cs             ICommand implementation
└── NuGet.config                    Licensed DevExpress feed

libs/engine/Models/
├── AppUser.cs, DeviceRecord.cs, SwitchRecord.cs  — INotifyPropertyChanged
├── P2PLink.cs, B2BLink.cs, FWLink.cs             — Network link models
├── BgpRecord.cs, BgpNeighborRecord.cs, BgpNetworkRecord.cs
├── RibbonConfig.cs                — RibbonPageConfig, RibbonGroupConfig, RibbonItemConfig, UserRibbonOverride
├── RibbonTreeItem.cs              — Flat tree node (Id/ParentId), display style, link target, icon preview
├── SavedFilter.cs                 — DB-backed saved filters per panel per user
├── TaskItem.cs                    — Task model with tree hierarchy
├── (+ 20 more: VlanEntry, Server, RoleRecord, LookupItem, MlagConfig, MstpConfig, etc.)

libs/persistence/
├── DbRepository.cs                — Main DB repo (partial class)
├── DbRepository.Ribbon.cs         — Ribbon CRUD: pages, groups, items, user overrides, admin defaults, saved filters
├── IconService.cs                 — Singleton: metadata cache, admin/user icon resolution, search, bulk SVG import
├── AppLogger.cs                   — Application logging to DB

modules/admin/Views/
├── RibbonConfigPanel.xaml/.cs     — Ribbon config grid panel (flat list)
├── RibbonTreePanel.xaml/.cs       — User ribbon customizer tree (icon picker, hide/show, reorder, apply/reset)
├── RibbonAdminTreePanel.xaml/.cs  — Admin ribbon customizer tree (push defaults, display style, link targets)
```

### Database Migrations

| Migration | Adds |
|---|---|
| 002_builder.sql | `vlan_templates`, extra columns on `switch_connections` and `bgp_config` |
| 003_connectivity.sql | `management_ip`, `ssh_*`, `last_ping_*`, `last_ssh_*` on `switches`; `running_configs` table |
| 004_ipam_fields.sql | Additional IPAM device fields |
| 005_lookup_values.sql | `lookup_values` table for dropdown options |
| 006_users_roles.sql | `app_users`, `role_permissions`, `user_settings` tables |
| 007_roles_table.sql | `roles` table for role management UI |
| 008_role_sites.sql | `role_sites` table for per-role site/building access control |
| 009_config_ranges.sql | Config range definitions |
| 010_excel_sheets.sql | Excel sheet import tracking |
| 011_view_reserved.sql | Reserved device view |
| 012_config_versions.sql | Config version history for compare |
| 023_bgp_sync.sql | `fast_external_failover`, `bestpath_multipath_relax`, `last_synced` on `bgp_config` |
| 024_permissions_v2.sql | `permissions` table (25 module:action codes), `role_permission_grants`, role priorities |
| 025_audit_log_v2.sql | `audit_log` (append-only, JSONB), soft delete columns on link + device tables |
| 026_pg_notify.sql | pg_notify triggers on 19 tables for real-time SignalR |
| 027_user_auth.sql | `password_hash`, `salt`, `user_type`, `last_login` on `app_users` |
| 028_default_settings.sql | `default_user_settings` table + auto-seed trigger |
| 029_job_schedules.sql | `job_schedules` + `job_history` tables for background jobs |
| 030_icon_library.sql | `icon_library` (PNG/SVG), `ribbon_icon_assignments`, `user_icon_overrides` |
| 031_tasks.sql | `tasks` table for task management module |
| 032_ribbon_config.sql | `ribbon_pages`, `ribbon_groups`, `ribbon_items` + pg_notify triggers |
| 033_service_desk_incremental.sql | SD tables (`sd_requests`, `sd_requesters`, `sd_technicians`), `integrations`, `integration_credentials`, permissions, ME seed |
| 034_sd_teams.sql | `sd_teams` + `sd_team_members` for tech team grouping |
| 035_sd_resolved_at.sql | `resolved_at` + `me_completed_time` columns on `sd_requests` |
| 035_api_key_salt.sql | `salt` column on `api_keys` for per-key salted SHA256 hashing |
| 036_sd_groups.sql | `sd_groups` lookup + `sd_group_categories` + `sd_group_category_members` for nested group filtering |
| 039_extended_user_fields.sql | department, title, phone, mobile, company, ad_guid, last_ad_sync on `app_users` |
| 040_ad_config.sql | AD integration config in `integrations`, `admin:ad` permission |
| 041_migration_history.sql | `migration_history` table for schema migration tracking |
| 042_backup_history.sql | `backup_history` table, `db_backup` job schedule |
| 043_location_tables.sql | `countries`, `regions`, `postcodes` with seed data |
| 044_reference_numbers.sql | `reference_config` table, `next_reference()` PG function |
| 045_panel_customizations.sql | `panel_customizations` (per-user JSONB settings) |
| 046_appointments.sql | `appointments`, `appointment_resources` for scheduler |
| 047_identity_providers.sql | `identity_providers`, `idp_domain_mappings`, `user_external_identities`, `claim_mappings`, `auth_events`, `social_providers`, `magic_link_tokens`, `mfa_recovery_codes`, `revoked_tokens` + app_users auth extensions |
| 048_pg_notify_new_tables.sql | pg_notify triggers for all tables added in migrations 029-047 |
| 049_sync_engine.sql | `sync_configs`, `sync_entity_maps`, `sync_field_maps`, `sync_status`, `sync_log` — pluggable integration sync engine |
| 050_webhook_log.sql | `webhook_log` — inbound webhook payload storage with pg_notify |
| 051_job_cron.sql | `schedule_cron` column on `job_schedules` for cron expression scheduling |
| 052_audit_log.sql | `audit_log` (before/after JSONB snapshots) + `password_history` for reuse prevention |
| 053_api_keys.sql | `api_keys` — service-to-service API key auth with SHA256 hash, role, usage tracking |
| 054_notification_prefs.sql | `notification_preferences` + `active_sessions` — per-user alert channels + session management |
| 055_saved_filters.sql | `saved_filters` — per-user per-panel filter presets (was referenced but table missing) |
| 060_tasks_v2.sql | `portfolios`, `programmes`, `task_projects`, `sprints`, `releases`, `task_links`, `task_dependencies` + 23 new columns on `tasks` |
| 061_sprint_planning.sql | `sprint_allocations`, `sprint_burndown` + `snapshot_sprint_burndown()` function |
| 062_kanban_board.sql | `board_columns`, `board_lanes` with default 5-column seed |
| 063_workflows.sql | `workflow_assignments`, `workflow_approvals`, `workflow_execution_log` (Elsa tracking) |
| 064_baselines.sql | `task_baselines` + `save_project_baseline()` function |
| 065_custom_columns.sql | `custom_columns`, `custom_column_permissions`, `task_custom_values` |
| 066_reports_dashboards.sql | `saved_reports`, `dashboards`, `dashboard_snapshots` |
| 067_time_activity.sql | `time_entries`, `activity_feed`, `task_views` + `log_task_activity()` auto-trigger |
| 032_global_admin_audit.sql | `central_platform.global_admin_audit_log` — platform-level audit trail |
| 033_global_admin_permissions.sql | `global_admin:read/write/delete/provision` permissions |
| 034_tenant_addresses_contacts.sql | `tenant_addresses` (many-to-one), `contacts` + `tenant_contacts` (many-to-many) |
| 036_companies.sql | `companies` table with hierarchy |
| 037_contacts_v2.sql | Full CRM contacts + addresses + communications |
| 038_teams_departments.sql | `departments`, `teams`, `team_members` |
| 039_addresses_unified.sql | Polymorphic addresses |
| 040_user_profiles.sql | `user_profiles` + `user_invitations` + `role_templates` |
| 041_global_admin_v2.sql | `tenant_onboarding`, `billing_accounts`, `invoices`, usage metrics, FTS indexes |
| 042_groups.sql | `user_groups`, `group_members`, `group_permissions`, `group_resource_access`, `group_assignment_rules` |
| 043_feature_flags.sql | `feature_flags`, `tenant_feature_flags` (9 seeded flags) |
| 044_security_enhancements.sql | `ip_access_rules`, `user_ssh_keys`, `deprovisioning_rules`/`log`, `terms_of_service`, `domain_verifications` |
| 045_team_hierarchy.sql | `teams.parent_id`, `team_resources`, `team_permissions`, `company_user_roles`, `team_activity` |
| 046_address_history.sql | `address_history` + auto-audit trigger |
| 047_permission_inheritance.sql | `roles.parent_role_id`, `user_permission_overrides`, `v_user_effective_permissions` view |
| 048_social_providers.sql | `social_providers` (Google/Microsoft/GitHub seeded), `user_social_logins`, `oauth_states` |
| 049_billing_extended.sql | Annual pricing, trials, grace periods, addons (5 seeded), discount codes, payment methods, proration, quotas |
| 050_password_recovery.sql | `password_reset_tokens`, `email_verification_tokens`, `app_users.email_verified_at` + `must_change_password` |
| 052_tenant_sizing.sql | `tenant_connection_map` + dedicated DB tracking + provisioning jobs + auto-upgrade trigger |
| 053_rls_timescale_citus.sql | Per-op RLS policies, TimescaleDB hypertables + compression + continuous aggregates, Citus scaffolding, logical replication publications, sharding threshold function |
| 056_email_integration.sql | `email_accounts`, `email_templates` (4 seeded), `email_messages`, `email_tracking_events`, `email_attachments` |
| 057_crm_dashboards_reports.sql | `saved_reports`, `forecast_snapshots`, 4 materialized views (revenue/activity/lead_source_roi/account_health), `refresh_crm_dashboards()` + hourly job |
| 058_crm_integrations.sql | Salesforce/HubSpot/Dynamics/Exchange/Gmail/Pipedrive integrations seeded, `sync_configs` for bidirectional sync, `crm_external_ids`, `crm_sync_conflicts`, `crm_documents`, `crm_document_templates`, `crm_document_approvals` |
| 059_crm_webhooks_polish.sql | `webhook_event_types` (28 seeded), `webhook_subscriptions`, `webhook_deliveries`, deal_won trigger, contact↔sd_requester auto-link, CRM↔Infra (`switch_guide.crm_account_id`), CRM↔Tasks (`task_projects.crm_deal_id`) |
| 060_crm_campaigns.sql | `crm_campaigns` + members + costs with auto-cost-recalc trigger, links campaigns to deals and leads |
| 061_crm_segments_sequences.sql | `crm_segments` (static + dynamic rule_expression), `crm_email_sequences` + steps + enrollments, `crm_landing_pages`, `crm_forms` + form_submissions |
| 062_crm_attribution.sql | `crm_utm_events`, `crm_attribution_touches` (first/last/linear/position/time_decay weights), `generate_attribution()` fn, `crm_campaign_influence` matview, hourly refresh job |
| 063_crm_territories.sql | `crm_territories` + territory_members + territory_rules, territory FK on accounts and leads |
| 064_crm_quotas_commissions.sql | `crm_quotas`, `crm_commission_plans` + tiers + user assignment + payouts |
| 065_crm_account_teams.sql | `crm_opportunity_splits` (with 100% validation trigger), `crm_account_teams`, `crm_account_plans` + stakeholders, `crm_org_chart_edges` |
| 066_crm_forecast_hierarchies.sql | `crm_forecast_adjustments`, `crm_pipeline_health` matview, `crm_deal_insights` + `generate_deal_insights()` fn |
| 067_crm_cpq.sql | `crm_product_bundles` + components, `crm_pricing_rules`, `crm_discount_approval_matrix` (4 seeded tiers) |
| 068_approval_engine.sql | `approval_requests` + steps + actions + auto-resolve trigger (generic, reusable across modules) |
| 069_crm_contracts.sql | `crm_contract_clauses` library, `crm_contract_templates`, `crm_contracts`, `crm_contract_versions`, `crm_contract_clause_usage`, `crm_contract_milestones`, `crm_contract_renewals` view |
| 070_crm_subscriptions_revenue.sql | `crm_subscriptions` + events with auto-log trigger, `crm_mrr_dashboard` matview, `crm_revenue_schedules` + entries (ASC 606), `generate_revenue_entries()` fn |
| 071_crm_orders.sql | `crm_orders` + order_lines with auto-recalc-totals + auto-create-subscription-from-order-line triggers, 16 new webhook event types |
| 072_portals_community.sql | `portal_users` (customer/partner), `magic_links`, `portal_sessions`, `partner_deal_registrations`, `kb_categories`, `kb_articles` (with tsvector full-text search), `community_threads` + `community_posts` + `community_votes`, auto-update triggers |
| 073_rule_engines.sql | `validation_rules` (JSONLogic), `workflow_rules` (integrates with Elsa workflow engine), `rule_execution_log`, universal `broadcast_record_change` trigger attached to 7 CRM tables for pg_notify |
| 074_custom_objects_field_security.sql | `custom_entities`, `custom_fields` (text/number/date/picklist/lookup/etc.), `custom_entity_records` (jsonb storage), `custom_field_values` (on built-in entities), `custom_relationships`, `field_permissions` (hidden/read/write per role per entity per field), `get_field_permission()` function |
| 075_import_commerce.sql | `import_jobs` + `import_job_rows` with dedup strategies, `shopping_carts` + `cart_items` with auto-recalc trigger, `payments` with Stripe-compatible fields |
| 076_ai_providers.sql | `central_platform.ai_providers` (8 seeded), `ai_models` (9 seeded), `tenant_ai_providers` (BYOK with AES-256 `api_key_enc`), `tenant_ai_features` (per-feature provider mapping), `ai_usage_log` with auto-aggregation trigger, `resolve_ai_provider()` SQL function (tenant → platform precedence) |
| 077_ai_ml_scoring.sql | `ai_ml_models`, `ai_model_scores`, `ai_next_best_actions`, `ai_training_jobs`, ML score columns on `crm_leads`/`crm_deals`/`crm_accounts` |
| 078_ai_assistant.sql | `ai_conversations`, `ai_messages`, `ai_prompt_templates` (4 seeded), `ai_tools` (6 seeded) |
| 079_ai_dedup_enrichment.sql | `crm_duplicate_rules`, `crm_duplicates`, `crm_merge_operations`, `crm_enrichment_providers` (5 seeded: Clearbit/Apollo/ZoomInfo/PeopleData/Hunter), `tenant_enrichment_providers` (BYOK), `crm_enrichment_jobs`, `v_contact_duplicate_candidates` view using pg_trgm |
| 080_ai_churn_calls.sql | `crm_churn_risks`, `crm_account_ltv`, `crm_call_recordings` (transcript + sentiment + topics + talk ratio), `crm_auto_capture_rules`, `crm_auto_capture_queue`, 8 new AI-related webhook event types |
| 081_desktop_missing_tables.sql | 6 tables the WPF startup health check was flagging as missing (identity_providers, auth_events, sync_configs, etc.) |
| 082_app_users_auth_columns.sql | `password_changed_at` + `mfa_secret_enc` on `app_users` (fixes silent login failure where the query referenced non-existent columns) |
| 083_module_catalog_reconcile.sql | Module catalog reconcile after the Networking module folded Devices |
| 084_net_schema_foundation.sql | `net` schema, `net.entity_status` + `net.lock_state` enums, `public.schema_versions` table |
| 085_net_hierarchy.sql | 9 tables: `net.region` / `site_profile` / `site` / `building_profile` / `building` / `floor_profile` / `floor` / `room` / `rack` with 17 universal base columns each; Immunocore seed |
| 086_net_pools.sql | 16 pool tables — `asn_pool` / `asn_block` / `asn_allocation`; `ip_pool` / `subnet` (with GIST EXCLUDE for no-overlap) / `ip_address`; `vlan_pool` / `vlan_block` / `vlan` / `vlan_template`; `mlag_domain_pool` / `mlag_domain`; `mstp_priority_rule` / `rule_step` / `allocation`; `reservation_shelf` |
| 087_net_immunocore_import.sql | Immunocore numbering import: 1 ASN pool + 5 per-site blocks + 5 allocations; 1 IP pool + 5 loopback subnets + 5 /32s; 1 VLAN pool + 63 distinct VLANs |
| 088_net_devices.sql | 7 tables: `net.device_role` (12 Immunocore roles) / `device` / `module` / `port` / `aggregate_ethernet` / `loopback` / `building_profile_role_count` |
| 089_net_device_import.sql | `public.switches` → `net.device` import with role-prefix disambiguation via hostname hint (l1 + CORE → L1Core, not L1SW) |
| 090_net_device_dual_write.sql | Bidirectional trigger `public.switches` ↔ `net.device` with txn-scoped reentrancy guard |
| 091_net_links.sql | 3 tables: `net.link_type` (7 seeded: P2P/B2B/FW/DMZ/MLAG-Peer/Server-NIC/WAN) / `link` / `link_endpoint` |
| 092_net_link_import.sql | 2,826 legacy link rows imported (2,310 P2P + 180 B2B + 336 FW) with 5,652 endpoints |
| 093_net_device_naming.sql | `naming_template` column on `net.device_role` + per-role seeds (Chunk A — parity with `net.link_type.naming_template`) |
| 094_net_servers.sql | 3 tables: `net.server_profile` (Server4NIC seeded) / `server` / `server_nic` with MlagSide A/B |
| 095_net_server_import.sql | `public.servers` → `net.server` import; 160 legacy rows → 31 distinct hostnames (data-quality-correct collapse via UNIQUE hostname) |
| 096_net_server_dual_write.sql | Bidirectional trigger `public.servers` ↔ `net.server`; mirrors hostname + status (FK fields stay authoritative on `net.*`) |

## Multi-Tenancy Sizing Model

- **Normal (zoned)**: schema-per-tenant in shared `central` DB, schema `tenant_<slug>`, default sizing
- **Enterprise (dedicated)**: own database `central_<slug>` + own K8s namespace `central-<slug>`, auto-provisioned on tier upgrade via DB trigger + Rust `tenant-provisioner` service
- Connection routing: `tenant_connection_map` lookup → `TenantConnectionResolver` (5-min cache) → falls back to zoned
- Provisioning: pg_dump source schema → CREATE DATABASE → restore → run migrations → create K8s namespace + NetworkPolicy + ResourceQuota
- TimescaleDB hypertables on `audit_log`, `activity_feed`, `auth_events` (70% compression savings)
- Citus extension for horizontal scaling at 50TB / 1M users
- PgBouncer tier-based pools: Free=2, Normal=20, Pro=50, Enterprise=100

## Development Notes

- PicOS uses `set` format (similar to Junos flat format)
- Interfaces on core switches: `xe-1/1/N` (10G/25G/40G/100G)
- Breakout ports (`xe-1/1/31`, `xe-1/1/32`) split into .1/.2/.3/.4 sub-interfaces
- VRRP VRID 1 = site MEP-91, VRID 12 = site MEP-92
- BGP ECMP max-paths 4 on all core switches
- MSTP bridge-priority: CORE02=6000 (master), CORE01=12288 (secondary)
- DHCP relay servers: 10.11.120.10 and 10.11.120.11 (both sites)
- Config generation: P2P, B2B, and FW links do NOT emit `port-mode "trunk"` — not needed for point-to-point links
- BGP neighbor commands in B2B BuildConfig: `set protocols bgp neighbor ... remote-as ... bfd`

### Icon System

Two icon sources available:

**Axialist Icons** (custom, stored in DB):
- **11,676 icons** in two packs: OfficePro + Universal
- Stored in `icon_library` table as SVG source + pre-rendered PNG 16px + PNG 32px
- `SvgHelper.RenderSvgToImageSource()` — Svg.NET renders SVG to WPF BitmapImage, replaces `currentColor` with `#FFFFFF`
- In-memory cache (hash-keyed) + local disk cache (`%LocalAppData%/Central/icon_cache/`)
- `IconService` singleton loads metadata on startup, loads PNG bytes on demand
- `ImagePickerWindow` — DXDialogWindow with pack checkboxes (OfficePro/Universal), category checkboxes, search, async batch PNG load

**DevExpress SVG Gallery** (built-in, theme-aware):
- **34 categories**, thousands of vector icons in color + grayscale
- Ships with `DevExpress.Images.v25.2.dll` — no DB storage needed
- Auto-adapts to app theme (Win11Dark, Office2019Colorful, etc.)
- XAML: `Glyph="{dx:DXImage 'SvgImages/Actions/Open2.svg'}"`
- Code: `DxSvgGallery.GetSvgImage("Actions", "Open2")` returns `SvgImage`
- `DxSvgGallery.cs` has category list + common icon reference
- Categories: Actions, Business Objects, Chart, Content, Data, Edit, Filter, Grid, Navigation, Reports, Setup, Zoom, etc.
- Use the DX Image Picker (Visual Studio designer) for visual browsing

### Ribbon Customization

- **3-layer override**: `admin_ribbon_defaults` (lowest) → `ribbon_items` (middle) → `user_ribbon_overrides` (highest)
- `PreloadIconOverridesAsync` runs at startup BEFORE ribbon build — loads all 3 layers into `_preloadedOverrides` dictionary
- `RibbonTreePanel` (user) — TreeListControl with Id/ParentId hierarchy, icon picker button, hide/show toggle, custom text, reorder (Move Up/Down), Apply/Reset All
- `RibbonAdminTreePanel` (admin) — same tree + display style dropdown (large/small/smallNoText), item type dropdown, link target picker, Push All Defaults button
- Link targets: `panel:PanelName`, `url:https://...`, `action:ActionKey`, `page:PageName`
- Auto-save on icon pick / hide toggle / rename (SaveSingleOverride delegate)
- DB tables: `ribbon_pages`, `ribbon_groups`, `ribbon_items`, `admin_ribbon_defaults`, `user_ribbon_overrides`
