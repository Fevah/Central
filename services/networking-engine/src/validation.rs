//! Validation rule engine — Phase 9a.
//!
//! Code-owned rule catalog. Each rule has a stable `code` (used as the
//! external identifier in UIs + the per-tenant config row), a default
//! severity, and an executor function that queries the DB for violations.
//!
//! Architecture call: rule SQL lives in Rust, not the DB. Giving admins an
//! edit box that becomes a runtime SELECT is an injection shape no matter
//! how carefully it's validated — so we own the queries, admins toggle +
//! see results.
//!
//! This slice ships 6 rules across the three most common categories
//! (Consistency / Integrity / Safety). Extending is one entry in `RULES`
//! plus one executor arm; no migration needed.

use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use std::collections::HashMap;
use uuid::Uuid;

use crate::error::EngineError;

// ─── Catalog ──────────────────────────────────────────────────────────────

#[derive(Debug, Copy, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub enum Severity { Error, Warning, Info }

impl Severity {
    /// Text form used by the DB config table + test assertions.
    /// Callers outside this module are coming once the WPF admin panel wires up.
    #[allow(dead_code)]
    pub fn as_str(&self) -> &'static str {
        match self { Self::Error => "Error", Self::Warning => "Warning", Self::Info => "Info" }
    }

    pub fn from_db(s: &str) -> Result<Self, EngineError> {
        match s {
            "Error" => Ok(Self::Error),
            "Warning" => Ok(Self::Warning),
            "Info" => Ok(Self::Info),
            other => Err(EngineError::bad_request(format!(
                "Unknown severity '{other}' — must be Error / Warning / Info."))),
        }
    }
}

#[derive(Debug, Copy, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RuleMeta {
    pub code: &'static str,
    pub name: &'static str,
    pub description: &'static str,
    pub category: &'static str,
    pub default_severity: Severity,
    pub default_enabled: bool,
}

/// Canonical rule catalog. Extending: add a `RuleMeta` entry here AND a
/// matching arm in `dispatch()`. `code` must be unique (enforced by a
/// test below) and stable across releases — tenant_rule_config rows key
/// off it.
pub const RULES: &[RuleMeta] = &[
    RuleMeta {
        code: "device.hostname_required",
        name: "Device hostname is required",
        description: "Every device must have a non-empty hostname so it can be \
                      referenced by links, config generators, and audit entries.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.hostname_required",
        name: "Server hostname is required",
        description: "Every server must have a non-empty hostname (same reasoning \
                      as device.hostname_required).",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.endpoint_count",
        name: "Link must have exactly 2 endpoints",
        description: "Every active link must carry endpoints at endpoint_order 0 \
                      and 1. Three-way (hub-and-spoke) links are not yet supported.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.nic_count_matches_profile",
        name: "Server NIC count matches its profile",
        description: "A server's number of active NICs should equal its server \
                      profile's nic_count. Mismatches usually mean a fan-out run \
                      partially failed — worth investigating.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "loopback.ipv4_slash_32",
        name: "Loopback IPv4 addresses must be /32",
        description: "Loopback subnets should be /32 per device to keep routing \
                      tables predictable. Non-/32 loopbacks are usually a \
                      mis-entered prefix.",
        category: "Safety",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan.unique_per_block",
        name: "VLAN IDs unique within their block",
        description: "Two non-deleted VLAN rows must not share both block_id and \
                      vlan_id. The allocation service enforces this on create, \
                      but manual imports can bypass it.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "hierarchy.building_requires_site",
        name: "Building must have a site",
        description: "Every non-deleted building must carry a site_id. A \
                      building orphaned from its site breaks region / site-code \
                      naming templates and scope-based RBAC.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "hierarchy.site_requires_region",
        name: "Site must have a region",
        description: "Every non-deleted site must carry a region_id so \
                      downstream buildings can resolve their region_code token.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "asn_allocation.value_in_block_range",
        name: "ASN allocations lie within their block's range",
        description: "Every allocation's asn column must fall inside the \
                      [asn_first, asn_last] of its containing block. The \
                      AllocationService enforces this on create, but legacy \
                      imports may not.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan.value_in_block_range",
        name: "VLANs lie within their block's range",
        description: "Every VLAN's vlan_id must fall inside its containing \
                      block's [vlan_first, vlan_last] range.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.active_requires_building",
        name: "Active device must have a building",
        description: "Any device in status='Active' must carry a building_id. \
                      Unsited Active devices bypass scope-based RBAC and \
                      cannot feed into naming templates.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.endpoints_distinct_devices",
        name: "Link endpoints must be different devices",
        description: "A link with both endpoints on the same device_id is \
                      almost always a data-entry error. Loopback-style \
                      self-peering links should use a dedicated type, not \
                      reuse the standard link types.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "ip_address.contained_in_subnet",
        name: "IP address is inside its subnet's CIDR",
        description: "Every ip_address.address must be a host inside \
                      subnet.network. Mismatches usually mean a subnet was \
                      resized after addresses were allocated.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.scope_entity_present_when_non_free",
        name: "Scoped subnets must carry scope_entity_id",
        description: "Subnets with scope_level other than 'Free' must point \
                      at a hierarchy entity (region / site / building / etc.). \
                      'Free' means the subnet exists but hasn't been bound \
                      yet — scope_entity_id should be NULL in that case.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.unique_hostname",
        name: "Device hostnames unique within tenant",
        description: "Two non-deleted devices must not share a hostname. \
                      The table has a UNIQUE constraint, so this rule is \
                      the belt-and-braces check for imports that bypass it.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.unique_hostname",
        name: "Server hostnames unique within tenant",
        description: "Two non-deleted servers must not share a hostname (same \
                      reasoning as device.unique_hostname).",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "ip_address.unique_per_subnet",
        name: "IP addresses unique within their subnet",
        description: "Two non-deleted ip_address rows must not share both \
                      subnet_id and address. Catches double-allocation that \
                      bypasses the allocation service.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.parent_same_pool",
        name: "Subnet parent must be in same pool",
        description: "When parent_subnet_id is set, the parent and child must \
                      live in the same ip_pool — cross-pool hierarchy is a \
                      data-entry error.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.parent_contains_child",
        name: "Subnet parent CIDR must contain child CIDR",
        description: "parent_subnet.network must be a supernet of child \
                      subnet.network. A /24 can't be parent of a /22 by \
                      definition.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_nic.index_unique_per_server",
        name: "Server NIC indexes unique per server",
        description: "Two non-deleted NIC rows on one server must not share \
                      nic_index. The fan-out service enforces dense 0..N-1 \
                      on create; this catches manual double-inserts.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_nic.index_dense_sequence",
        name: "Server NIC indexes form dense 0..N-1 sequence",
        description: "Active NIC rows on one server should be numbered 0, 1, \
                      …, count-1 with no gaps. Sparse indexes usually mean \
                      a fan-out partially failed and partial-retry drifted.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.building_matches_endpoints",
        name: "Link's building matches at least one endpoint",
        description: "If a link carries a building_id, at least one of its \
                      endpoint devices should sit in the same building. A \
                      link tagged for Building A with both endpoints in \
                      Building B is suspicious.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "loopback.ipv6_slash_128",
        name: "Loopback IPv6 subnets must be /128",
        description: "Loopback subnets on IPv6 should be /128 per device, \
                      same reasoning as the /32 rule for IPv4.",
        category: "Safety",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "mlag_domain.unique_per_tenant",
        name: "MLAG domain IDs unique per tenant",
        description: "MLAG uniqueness is tenant-wide (not pool-wide): two \
                      non-deleted mlag_domain rows must not share domain_id \
                      within an organization.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "asn_allocation.unique_per_tenant",
        name: "ASN values unique per tenant",
        description: "An ASN number must be allocated only once per tenant. \
                      A duplicate would mean two devices are advertising the \
                      same AS, which is an operational hazard.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.management_ip_for_active",
        name: "Active devices should carry management_ip",
        description: "Devices in status='Active' are expected to be reachable, \
                      which requires a management_ip. Advisory — a device \
                      legitimately out-of-band managed may fail this; toggle \
                      off per-tenant if that's your norm.",
        category: "Advisory",
        default_severity: Severity::Info,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.has_device_role",
        name: "Active devices must carry a device role",
        description: "Devices in status='Active' must reference a device_role. \
                      Without one, naming templates fall back to catalog defaults \
                      and role-scoped naming overrides don't apply.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.vlan_in_tenant",
        name: "Link VLAN references resolve",
        description: "If a link carries vlan_id, it must resolve to a non-deleted \
                      VLAN in the same tenant. Dangling references are usually a \
                      VLAN hard-delete that left links orphaned.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.subnet_in_tenant",
        name: "Link subnet references resolve",
        description: "Mirrors link.vlan_in_tenant for subnet_id: a link's subnet \
                      reference must resolve to a non-deleted subnet in the same \
                      tenant.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "hierarchy.floor_requires_building",
        name: "Floors must have a building",
        description: "Every non-deleted floor must carry a building_id. Orphan \
                      floors break the site→building→floor walk the naming \
                      resolver uses.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "hierarchy.rack_requires_room",
        name: "Racks must have a room",
        description: "Every non-deleted rack must carry a room_id (or the parent \
                      room must carry a building_id fallback). Orphan racks \
                      can't be addressed by scope-level naming rules.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_nic.target_device_resolves",
        name: "Server NIC target_device resolves",
        description: "If a NIC carries target_device_id, that id must resolve to \
                      a non-deleted device. Dangling NIC→device edges usually \
                      mean a core was decommissioned and its NIC fan-out rows \
                      weren't updated.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.loopback_in_loopback_subnet",
        name: "Server loopback IP comes from a LOOPBACK subnet",
        description: "Servers with loopback_ip_address_id set should have that IP \
                      in a subnet whose subnet_code starts with 'LOOPBACK'. A \
                      loopback drawn from a regular fabric subnet usually means \
                      the fan-out picked from the wrong pool.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "asn_block.range_not_empty",
        name: "ASN block ranges must be non-empty",
        description: "net.asn_block.asn_first must be ≤ asn_last. An inverted \
                      range silently exhausts allocation attempts — nothing fits \
                      an empty range.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan_block.range_not_empty",
        name: "VLAN block ranges must be non-empty",
        description: "Same shape as asn_block.range_not_empty — \
                      net.vlan_block.vlan_first must be ≤ vlan_last. An inverted \
                      range exhausts allocation silently.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan.vlan_id_valid_range",
        name: "VLAN IDs within 1-4094",
        description: "IEEE 802.1Q allocates VLAN ids 1-4094 (0 is 'untagged', \
                      4095 is reserved). A vlan_id outside that range is the \
                      switch's config generator waiting to fail at deploy time.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "mlag_domain.domain_id_valid_range",
        name: "MLAG domain IDs within 1-4094",
        description: "PicOS (FS) MLAG domain ids must sit in 1-4094. Out-of-range \
                      values produce config the switch rejects at commit.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.matches_pool_family",
        name: "Subnet family matches its pool",
        description: "A subnet carved from an IP pool must share that pool's \
                      address family — IPv4 subnet from an IPv4 pool, IPv6 from \
                      IPv6. Mismatches are import / migration errors.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_profile.nic_count_positive",
        name: "Server profile NIC count > 0",
        description: "ServerCreationService.CreateWithFanOutAsync refuses to run \
                      on a zero-NIC profile, but the row itself isn't constrained. \
                      A NicCount of 0 silently blocks every server using the \
                      profile.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_nic.mlag_side_valid",
        name: "Server NIC mlag_side is 'A' or 'B'",
        description: "MlagSide is a small A/B discriminator driving the fan-out \
                      target. Any other value is corrupt import data; config \
                      generation skips the NIC silently.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.unique_mac_address",
        name: "Device MAC addresses unique within tenant",
        description: "Two non-deleted devices must not share a mac_address. A \
                      duplicate is either a clone of a replacement device the \
                      original wasn't retired from, or a genuine MAC collision \
                      that needs operator attention.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.unique_mac_address",
        name: "Server MAC addresses unique within tenant",
        description: "Same as device.unique_mac_address but for servers.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "device_role.naming_template_not_empty",
        name: "Device roles must carry a naming template",
        description: "Every role should have a naming_template so the resolver \
                      produces deterministic hostnames. Empty templates force the \
                      resolver to the global fallback, which may produce \
                      ambiguous names across buildings.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "asn_allocation.allocated_to_set",
        name: "ASN allocations should carry allocated_to info",
        description: "allocated_to_type + allocated_to_id together identify what \
                      the ASN was issued for. Missing either leaves the \
                      allocation orphaned for audit purposes (no way to trace \
                      which device / server / building is using it).",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "asn_pool.range_not_empty",
        name: "ASN pool ranges must be non-empty",
        description: "Parallel to asn_block.range_not_empty — net.asn_pool's \
                      asn_first must be ≤ asn_last. An inverted pool range \
                      makes every block carved from it empty-by-construction.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "mlag_domain_pool.range_not_empty",
        name: "MLAG domain pool ranges must be non-empty",
        description: "Parallel to asn_pool / vlan_block range checks for the \
                      MLAG pool tier (MLAG is pool-direct — no block layer). \
                      domain_first must be ≤ domain_last; an inverted range \
                      exhausts allocation silently.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "link_type.naming_template_not_empty",
        name: "Link types must carry a naming template",
        description: "Parallel to device_role.naming_template_not_empty — every \
                      link_type should have a naming_template so the resolver \
                      produces deterministic link_codes. Empty templates fall \
                      back to global defaults which may not disambiguate \
                      across buildings.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_profile.naming_template_not_empty",
        name: "Server profiles must carry a naming template",
        description: "Completes the naming-template-required trio (device_role \
                      / link_type / server_profile). Server profiles without a \
                      template fall back to global resolver defaults.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.endpoint_interface_unique_per_device",
        name: "A device's interface is used by at most one active link",
        description: "Two non-deleted endpoints sharing the same device_id + \
                      interface_name is a physical config error — you can't \
                      plug two cables into one switch port. Catches the classic \
                      'operator typo'd the port and linked A to the same port \
                      twice' bug that bulk import is silent about.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "dhcp_relay_target.unique_per_vlan_ip",
        name: "DHCP relay (vlan, server_ip) pairs unique per tenant",
        description: "Two non-deleted dhcp_relay_target rows sharing both \
                      vlan_id and server_ip add no value but double every \
                      config-gen emission + break the bulk-import dup-check \
                      if the CRUD path was used to insert a duplicate.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "device_role.display_name_not_empty",
        name: "Device role display names must be non-empty",
        description: "Role with an empty display_name renders as a blank row \
                      in device pickers + naming-override dialogs. Integrity \
                      twin of device_role.naming_template_not_empty.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "dhcp_relay_target.priority_non_negative",
        name: "DHCP relay target priority must be non-negative",
        description: "The bulk import rejects negative priority at parse \
                      time; this rule catches the same condition on rows \
                      that came in via the CRUD endpoint or raw SQL where \
                      the validator didn't run. Negative priorities sort \
                      in unexpected positions inside the rendered DHCP \
                      relay ordered list.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan.display_name_not_empty",
        name: "VLAN display name must be non-empty",
        description: "An active VLAN with a blank display_name shows up \
                      as 'VLAN 120' in pickers instead of the operator-\
                      recognised name ('Servers', 'VoIP'), which hurts \
                      troubleshooting when a tenant reuses the same \
                      vlan_id across blocks.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.display_name_not_empty",
        name: "Subnet display name must be non-empty",
        description: "Parallel to vlan.display_name_not_empty — a subnet \
                      with blank display_name renders as a CIDR in \
                      pickers. Usually harmless but the render in Config \
                      Gen comments reads as 'subnet  10.11.1.0/24' \
                      (double space) which is a give-away for a missing \
                      description.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.endpoint_devices_resolve",
        name: "Link endpoint devices must resolve",
        description: "Every active net.link_endpoint must point at a \
                      non-deleted device. Dangling endpoint → device \
                      edges usually mean a device was hard-deleted while \
                      links still referenced it; config-gen silently \
                      skips the affected links.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.active_subnet_has_pool",
        name: "Active subnet must reference an IP pool",
        description: "An Active net.subnet without a pool_id is an \
                      orphan — can't be part of the IP-allocation \
                      lifecycle. The FK is nullable in the schema to \
                      allow bootstrap rows; this rule catches the ones \
                      that should have been linked up.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.active_has_building",
        name: "Active server should carry a building",
        description: "Mirrors device.active_requires_building. Active \
                      servers without a building_id bypass scope-based \
                      RBAC + can't feed into naming templates that \
                      reference {building_code}. Advisory because a \
                      few tenants legitimately run portable servers \
                      with no fixed home.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.within_parent_pool_cidr",
        name: "Subnet CIDR must be contained by its parent pool",
        description: "Active net.subnet rows must sit inside their \
                      referenced net.ip_pool's network CIDR. A subnet \
                      routed outside the pool leaks allocations past \
                      the pool's carver invariants; the GIST overlap \
                      check only enforces no-overlap between subnets, \
                      not containment within the pool CIDR.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.unique_link_code_active",
        name: "Link code must be unique among active links",
        description: "Two active net.link rows sharing link_code would \
                      make cross-panel drill ambiguous and break any \
                      audit cross-reference that keys on the code. \
                      The DB doesn't carry a unique constraint because \
                      soft-deleted rows retain their original code; \
                      this rule catches the live-collision case.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "device_role.unique_role_code_per_tenant",
        name: "Device role code must be unique per tenant",
        description: "Two net.device_role rows sharing role_code within \
                      a tenant make naming-template resolution + the \
                      role picker non-deterministic. Active + \
                      soft-deleted collisions both count because the \
                      picker queries across the deleted_at filter \
                      during 'show deleted' audits.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan.scope_entity_resolves",
        name: "VLAN scope_entity_id must resolve when scope is non-Free",
        description: "A VLAN with scope_level != 'Free' must carry a \
                      scope_entity_id that points at a live row in \
                      the matching hierarchy table (region / site / \
                      building / device). Orphaned scope_entity_id \
                      breaks the scope resolver + makes the 'show \
                      VLANs for building X' drill return the wrong set.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "dhcp_relay_target.vlan_active",
        name: "DHCP relay target must point at an active VLAN",
        description: "A net.dhcp_relay_target row whose vlan_id \
                      resolves to a soft-deleted or Decommissioned \
                      VLAN generates DHCP helper config for a VLAN \
                      that no longer exists — config-gen emits it \
                      silently. Keeps the target in sync with the \
                      VLAN's lifecycle.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.vlan_link_is_active",
        name: "Subnet's linked VLAN should be active",
        description: "A net.subnet with a non-null vlan_id pointing \
                      at a soft-deleted or Decommissioned VLAN is a \
                      sign the VLAN was retired without cleaning up \
                      its allocated subnet. Warning rather than \
                      error — IP ranges often outlive the VLAN tag.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "ip_address.assigned_to_id_when_typed",
        name: "Typed IP assignment must carry an assigned_to_id",
        description: "An IP address row with a non-null assigned_to_type \
                      (Device / Server / ServerNic / Vrrp) should also \
                      have assigned_to_id filled in — the type without \
                      the id is an orphan and breaks the 'who owns this \
                      IP?' drill in audit. Gateway / Broadcast / \
                      Reserved are exempt (they're policy roles, not \
                      pointers to an entity row).",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "rack.position_positive",
        name: "Rack position should be a positive integer",
        description: "net.rack.position is nullable (not every tenant \
                      tracks layout), but a stored 0 or negative value \
                      is almost always a stale default. Advisory — a \
                      few deployments use 0 legitimately as a reserved \
                      slot marker.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "building_profile.role_counts_non_empty",
        name: "Building profile should define at least one role count",
        description: "A net.building_profile with zero matching \
                      building_profile_role_count rows renders an \
                      empty template when the operator tries to \
                      expand it into actual devices. Catches profiles \
                      that were created but never populated.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "rendered_config.chain_integrity",
        name: "Rendered-config chain must resolve",
        description: "A net.rendered_config row with a non-null \
                      previous_render_id that doesn't resolve to an \
                      existing render breaks the 'what changed since \
                      last render' diff — diff_render short-circuits \
                      to 'first render' silently. LEFT JOIN catches \
                      the orphaned-pointer case.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "scope_grant.no_duplicate_tuple",
        name: "Scope grants shouldn't duplicate (user, action, entity, scope) tuples",
        description: "Two Active net.scope_grant rows for the same \
                      (user_id, action, entity_type, scope_type, \
                      scope_entity_id) tuple are redundant — the \
                      second one can never change the resolver's \
                      answer. Typically the result of a bulk-import \
                      running twice or a manual duplicate.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "change_set.applied_has_no_pending_items",
        name: "Applied change-sets should have every item applied",
        description: "A net.change_set in status 'Applied' with any \
                      item whose applied_at is NULL is a partial \
                      apply — the Set looks done from the dashboard \
                      but part of its payload never ran. Apply path \
                      normally blocks this transition, but raw SQL \
                      edits or a crashed apply can leave the state.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "naming_template_override.scope_entity_resolves",
        name: "Naming override non-Global scope must resolve",
        description: "A net.naming_template_override with scope_level \
                      != 'Global' must carry a scope_entity_id that \
                      points at a live row in the matching hierarchy \
                      table (region / site / building). Dangling \
                      scope_entity_id makes the naming resolver \
                      silently fall through to parent scope — hard \
                      to diagnose from the output alone.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "asn_allocation.unique_allocated_to",
        name: "Entity shouldn't have more than one active ASN allocation",
        description: "Two active net.asn_allocation rows with the \
                      same (allocated_to_type, allocated_to_id) mean \
                      one entity has two ASNs — the config-gen picks \
                      one non-deterministically. Expected pattern is \
                      one ASN per device / building.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "port.interface_name_not_empty",
        name: "Port interface_name must be non-empty",
        description: "A net.port row with empty interface_name is \
                      un-renderable — config-gen emits malformed \
                      `set interface '' ...` lines. NOT NULL in the \
                      schema but doesn't catch the blank-string case.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "ip_address.reserved_type_is_marked_reserved",
        name: "Reserved-role IP addresses should carry is_reserved=true",
        description: "A net.ip_address with assigned_to_type in \
                      (Gateway / Broadcast / Reserved) should have \
                      is_reserved=true. These aren't hand-allocated \
                      to a live entity — they're policy markers at the \
                      subnet edges. Warning because a few tenants run \
                      with is_reserved tracked separately, but the \
                      coupling is strong enough the audit picks it up.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.network_is_network_address",
        name: "Subnet CIDR should be stored as the network address",
        description: "A net.subnet.network stored with host bits set \
                      (e.g. 10.11.0.5/24 instead of 10.11.0.0/24) \
                      violates the carver's assumptions — GIST \
                      containment queries can miss it. PG's \
                      `set_masklen(network, masklen(network))` flips \
                      the address to canonical; this rule surfaces \
                      rows that drifted.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link_endpoint.port_resolves",
        name: "Link endpoint's port_id must resolve to a live net.port",
        description: "A net.link_endpoint row with a non-null port_id \
                      that points at a deleted or non-existent \
                      net.port breaks config-gen's endpoint-to-port \
                      interface resolution — it emits an empty \
                      `set interface ... ...` slot. Warnings for \
                      NULL port_id rows are fine (some link types \
                      don't need port specificity).",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_nic.target_port_resolves",
        name: "Server NIC's target_port_id must resolve to a live port",
        description: "A net.server_nic row with a non-null \
                      target_port_id pointing at a deleted port is \
                      a dangling pointer — the fan-out allocator \
                      walks this chain on renders. NULL target_port_id \
                      is fine (a server profile may not have wired \
                      its NICs yet); only checks the non-null case.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "asn_allocation.block_resolves_active",
        name: "ASN allocation's block_id must resolve to an active block",
        description: "A net.asn_allocation whose block_id doesn't \
                      resolve, or resolves to a soft-deleted or \
                      non-Active block, breaks the pool-utilization \
                      rollup + the 'which pool did this ASN come \
                      from' audit trail.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_nic.target_device_matches_port_device",
        name: "Server NIC target_device_id should match its target_port_id's device",
        description: "server_nic.target_device_id is denormalised \
                      from target_port_id.device_id for cheap 'all \
                      NICs on this core' filtering. The two should \
                      stay consistent — drift means one side was \
                      updated without the other. Warning because \
                      the query layer uses target_device_id + NIC \
                      list rarely needs rebuilding; the drift \
                      surfaces as missing rows in filter queries.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "port.breakout_parent_on_same_device",
        name: "Port's breakout parent must be on the same device",
        description: "A net.port row with breakout_parent_id \
                      pointing at a port on a different device is \
                      nonsensical — breakout is a physical \
                      operation on a single switch's port module. \
                      FK is schema-wide by design (ON DELETE \
                      CASCADE) so the constraint must be checked \
                      explicitly.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "port.aggregate_on_same_device",
        name: "Port's aggregate_ethernet must be on the same device",
        description: "Similar to breakout_parent: an aggregate \
                      ethernet (LACP bundle) is a single-device \
                      construct. A member port whose \
                      aggregate_ethernet_id points at an ae on a \
                      different device won't config-gen correctly.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "aggregate_ethernet.name_unique_per_device",
        name: "Aggregate ethernet name must be unique per device",
        description: "Two net.aggregate_ethernet rows with the same \
                      (device_id, ae_name) make config-gen emit \
                      duplicate `set interface ae-N ...` stanzas. \
                      The DB doesn't carry a UNIQUE constraint \
                      because name swaps during migrations could \
                      temporarily violate it; this rule flags the \
                      steady-state case.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "change_set.submitted_has_items",
        name: "Submitted / Approved / Applied change-sets should carry items",
        description: "A net.change_set past Draft status with zero \
                      net.change_set_item rows is nonsensical — \
                      there's nothing to approve or apply. Typically \
                      the result of a submit-then-empty flow or a \
                      bulk import that failed before items landed.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "reservation_shelf.cooldown_respected",
        name: "Shelf entries shouldn't be re-used before available_after",
        description: "A net.reservation_shelf row with status='Active' \
                      and available_after in the past means the \
                      cooldown window has elapsed but nothing \
                      recycled the resource yet — the shelf grows \
                      unbounded. Advisory: a background job typically \
                      promotes these; this rule surfaces cases where \
                      the job isn't running.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan_block.range_within_pool",
        name: "VLAN block range must sit within its parent pool's range",
        description: "A net.vlan_block's (vlan_first, vlan_last) must \
                      be contained by its parent net.vlan_pool's \
                      (vlan_first, vlan_last). Carver stamps fresh \
                      blocks within-range; this rule catches manual \
                      inserts or migrations that stored a block \
                      straddling the pool edge.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "floor.floor_code_unique_per_building",
        name: "Floor code must be unique within a building",
        description: "Two net.floor rows in the same building sharing \
                      floor_code break the hierarchy picker + make \
                      scope-level resolution ambiguous for floor-\
                      scoped resources. No DB UNIQUE constraint \
                      because floor_code is free-form across tenants; \
                      this rule flags the per-building collision.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "room.room_code_unique_per_floor",
        name: "Room code must be unique within a floor",
        description: "Parallel to the floor rule. Two net.room rows \
                      on the same floor sharing room_code make the \
                      rack picker + hierarchy drill ambiguous.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "rack.rack_code_unique_per_room",
        name: "Rack code must be unique within a room",
        description: "Bottom of the hierarchy uniqueness check. Two \
                      net.rack rows in the same room sharing \
                      rack_code break device-placement pickers + \
                      the physical-layout naming template's {rack_code} \
                      token expansion.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "ip_address.vrrp_has_peer_on_other_device",
        name: "VRRP IPs should be advertised from at least two devices",
        description: "A VRRP VIP (net.ip_address with \
                      assigned_to_type='Vrrp') that's only \
                      referenced by one device indicates a broken \
                      pair — VRRP is a redundancy protocol, single-\
                      speaker VIPs are a config-gen smell. Warning \
                      because stand-alone VIPs during a migration \
                      window are legitimate.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "site_profile.display_name_not_empty",
        name: "Site profile display_name must be non-empty",
        description: "Schema NOT NULL constraint doesn't catch blank \
                      strings. Empty display_name renders as an empty \
                      row in the hierarchy + profile pickers — admins \
                      can't pick what they can't see.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "floor_profile.display_name_not_empty",
        name: "Floor profile display_name must be non-empty",
        description: "Parallel to site_profile. Blank display_name \
                      breaks the floor profile picker + audit rows \
                      display empty values.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "region.display_name_not_empty",
        name: "Region display_name must be non-empty",
        description: "Empty region display_name renders as a blank \
                      row in the hierarchy picker + tree — can't be \
                      selected without knowing its uuid. Parallel to \
                      the *_profile rules (batch 17).",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "site.display_name_not_empty",
        name: "Site display_name must be non-empty",
        description: "Parallel to region. Blank site display_name \
                      breaks the site picker on the device + server \
                      create forms.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "building.display_name_not_empty",
        name: "Building display_name must be non-empty",
        description: "Parallel to region + site. The building is \
                      the most frequently-drilled-into hierarchy \
                      level, so an empty display_name is the most \
                      painful variant.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "module.slot_unique_per_device",
        name: "Module slot must be unique within a device",
        description: "Two net.module rows on the same device \
                      sharing `slot` (e.g. two rows claiming 'fpc0') \
                      is a data-quality bug — slots are physical \
                      positions and only one card fits each.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "mstp_priority_rule.has_steps",
        name: "Active MSTP rules should have at least one step",
        description: "A net.mstp_priority_rule with zero steps \
                      emits nothing at config-gen — the rule is a \
                      name with no behaviour. Warning because the \
                      rule may be mid-edit; operators ship with \
                      steps defined in practice.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "reservation_shelf.resource_key_not_empty",
        name: "Reservation shelf resource_key must be non-empty",
        description: "A shelf entry with a blank resource_key can't \
                      be matched by the recycler against the pool \
                      tables. Rare (schema NOT NULL + usually \
                      populated by the retire path) but worth the \
                      guard.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "mlag_domain.scope_entity_present_when_non_global",
        name: "MLAG domain non-Global scope must carry a scope_entity_id",
        description: "A net.mlag_domain with scope_level != 'Global' \
                      must have scope_entity_id populated pointing \
                      at a live row in the matching hierarchy table. \
                      Mirrors the shape of the earlier \
                      vlan.scope_entity_resolves rule (batch 8).",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "change_set_item.entity_id_required_for_mutations",
        name: "Non-Create change-set items must carry an entity_id",
        description: "A net.change_set_item with action in \
                      (Update / Delete / Rename) must have entity_id \
                      set — there's nothing to mutate without a \
                      target. Only Create allows NULL entity_id (the \
                      entity doesn't exist yet).",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "port.speed_mbps_positive_when_set",
        name: "Port speed_mbps should be positive when populated",
        description: "NULL speed_mbps means unknown / auto-negotiate \
                      (fine). Zero or negative speed is nonsensical + \
                      breaks config-gen's speed-override stanza \
                      emission. Warning because it's recoverable by \
                      clearing the column to NULL.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "naming_template_override.template_not_empty",
        name: "Naming override template must be non-empty",
        description: "A net.naming_template_override with a blank \
                      naming_template column short-circuits the \
                      resolver — the override wins but produces an \
                      empty hostname. Always a bug.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "ip_pool.network_is_network_address",
        name: "IP pool CIDR should be stored as the network address",
        description: "Parallel to subnet.network_is_network_address \
                      (batch 12) but for net.ip_pool.network. Host \
                      bits set violate the carver's assumptions when \
                      picking subnets.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "loopback.active_has_ip_address",
        name: "Active loopback should carry an ip_address_id",
        description: "A net.loopback in status 'Active' without an \
                      ip_address_id set is a broken allocation — the \
                      loopback is meant to back an assigned /32 or \
                      /128. NULL is fine during staging (status \
                      Planned); this rule only flags Active rows.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "loopback.number_unique_per_device",
        name: "Loopback number must be unique per device",
        description: "Two net.loopback rows on the same device with \
                      duplicate loopback_number render ambiguously \
                      in config-gen (two 'lo0' stanzas). GROUP BY + \
                      HAVING across active rows.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan_template.code_unique_per_tenant",
        name: "VLAN template code must be unique per tenant",
        description: "net.vlan_template rows sharing template_code \
                      break the template picker at VLAN creation. \
                      GROUP BY + HAVING guard for the steady state.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.active_scope_entity_resolves",
        name: "Active subnet non-Free scope must resolve",
        description: "Parallel to vlan.scope_entity_resolves (batch \
                      8) but for net.subnet. Active subnets with \
                      scope_level != 'Free' need a scope_entity_id \
                      resolvable to a live hierarchy row.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "rack.uheight_positive",
        name: "Rack u_height must be positive",
        description: "net.rack.u_height defaults to 42 but admin \
                      edits can store 0 or negative. A rack with \
                      non-positive height can't place any device + \
                      breaks the placement UI.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "room.max_racks_positive_when_set",
        name: "Room max_racks should be positive when populated",
        description: "NULL is fine (uncapped). Zero or negative is \
                      nonsense — no racks fit. Mirrors the shape of \
                      port.speed_mbps_positive_when_set (batch 20).",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan_template.display_name_not_empty",
        name: "VLAN template display_name must be non-empty",
        description: "Parallel to the *_profile display_name rules \
                      (batches 17-18). Blank display_name breaks \
                      the template picker at VLAN creation.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "ip_address.subnet_resolves_active",
        name: "IP address subnet must resolve + be Active",
        description: "An ip_address pointing at a soft-deleted or \
                      Decommissioned subnet is an orphan — the \
                      allocation lifecycle broke. Parallel to \
                      subnet.vlan_link_is_active (batch 8) but for \
                      IP → subnet.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.active_has_endpoints",
        name: "Active link should carry at least two endpoints",
        description: "An Active net.link with fewer than two \
                      net.link_endpoint rows renders as a half-link \
                      at config-gen. Existing link.endpoint_count \
                      catches the > 2 case; this parallels for the \
                      < 2 underflow.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "floor.building_resolves_active",
        name: "Floor building_id must resolve to an Active building",
        description: "A floor under a soft-deleted or Decommissioned \
                      building is orphaned — the hierarchy drill \
                      breaks at the floor level. Parallel to \
                      hierarchy.floor_requires_building but checks \
                      the parent's lifecycle state too.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "room.floor_resolves_active",
        name: "Room floor_id must resolve to an Active floor",
        description: "Rooms under soft-deleted or non-Active floors \
                      are orphaned — the hierarchy drill to racks + \
                      devices breaks at the room level. Parallel to \
                      floor.building_resolves_active one step lower.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "rack.room_resolves_active",
        name: "Rack room_id must resolve to an Active room",
        description: "Racks under soft-deleted or non-Active rooms \
                      are orphaned — devices placed in them can't \
                      be found by hierarchy-scoped scope-grants. \
                      Parallel to room.floor_resolves_active one \
                      step lower.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "port.device_resolves_active",
        name: "Port device_id must resolve to an Active device",
        description: "A port whose device is soft-deleted or \
                      Decommissioned is an orphan — the port shows \
                      up in reports but its device has moved on. \
                      Parallels floor/room/rack lifecycle resolves \
                      one level lower in the phys-stack.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "module.device_resolves_active",
        name: "Module device_id must resolve to an Active device",
        description: "A module (linecard / transceiver / PSU) whose \
                      device is soft-deleted or Decommissioned \
                      leaks into inventory reports. Parallel to \
                      port.device_resolves_active.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "loopback.device_resolves_active",
        name: "Loopback device_id must resolve to an Active device",
        description: "A loopback whose device is soft-deleted or \
                      Decommissioned becomes a stranded /32 reservation. \
                      Parallel to port/module lifecycle resolves.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "aggregate_ethernet.device_resolves_active",
        name: "Aggregate-ethernet device_id must resolve to an Active device",
        description: "An AE bundle whose device is soft-deleted or \
                      Decommissioned can't hold member ports. Pair \
                      to port.device_resolves_active — an orphan AE \
                      leaks LAG config on the wire.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_nic.server_resolves_active",
        name: "Server NIC server_id must resolve to an Active server",
        description: "A NIC whose server is soft-deleted or \
                      Decommissioned is an orphan — the NIC row \
                      leaks into reports even though its host is \
                      gone. Parallel to the device-child lifecycle \
                      resolves family, on the server branch.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "link_endpoint.link_resolves_active",
        name: "Link endpoint link_id must resolve to an Active link",
        description: "An endpoint whose parent link is soft-deleted \
                      or non-Active is an orphan — the endpoint \
                      survives if the link was soft-deleted without \
                      cascade (ON DELETE CASCADE guards the hard-\
                      delete path, but status changes don't cascade \
                      by design so this rule catches the gap).",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.role_resolves_active",
        name: "Device device_role_id must resolve to an Active role",
        description: "Devices whose device_role is soft-deleted or \
                      non-Active lose their naming template + \
                      default ASN kind — config-gen falls back to \
                      'Unknown' prefixes. Surfaces role-deprecation \
                      migrations that left devices stranded on the \
                      retired role.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan.block_resolves_active",
        name: "VLAN block_id must resolve to an Active VLAN block",
        description: "A VLAN whose parent vlan_block is soft-deleted \
                      or non-Active is orphaned from pool-availability \
                      calculations. Parallels asn_allocation.block_\
                      resolves_active one entity over.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.pool_resolves_active",
        name: "Subnet pool_id must resolve to an Active IP pool",
        description: "A subnet whose parent ip_pool is soft-deleted \
                      or non-Active misreports in pool-utilization. \
                      subnet.active_subnet_has_pool catches a NULL \
                      pool_id; this rule catches the present-but-\
                      decommissioned case.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "mlag_domain.pool_resolves_active",
        name: "MLAG domain pool_id must resolve to an Active pool",
        description: "An MLAG domain whose parent pool is soft-deleted \
                      or non-Active loses its numbering lineage. \
                      Parallels vlan.block_resolves_active + \
                      subnet.pool_resolves_active.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "asn_block.pool_resolves_active",
        name: "ASN block pool_id must resolve to an Active ASN pool",
        description: "An ASN block whose parent asn_pool is soft-deleted \
                      or non-Active misreports in pool-utilization. \
                      Parallels subnet.pool_resolves_active / \
                      vlan.block_resolves_active on the ASN tree.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan_block.pool_resolves_active",
        name: "VLAN block pool_id must resolve to an Active VLAN pool",
        description: "A VLAN block whose parent vlan_pool is soft-deleted \
                      or non-Active breaks pool-aware allocation + \
                      availability counts.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan.template_resolves_active_when_set",
        name: "VLAN template_id must resolve to an Active template when set",
        description: "Optional template pointer — when set, must resolve \
                      to an Active vlan_template. Warning-severity \
                      because a missing template falls back to the \
                      catalog default rather than failing config-gen.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_nic.vlan_resolves_active_when_set",
        name: "Server NIC vlan_id must resolve to an Active VLAN when set",
        description: "Optional VLAN pointer on server_nic — when \
                      set, must resolve to an Active vlan. Config-\
                      gen just omits VLAN tagging for NICs with a \
                      dangling vlan pointer, so Warning severity.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_nic.subnet_resolves_active_when_set",
        name: "Server NIC subnet_id must resolve to an Active subnet when set",
        description: "Optional subnet pointer on server_nic — when \
                      set, must resolve to an Active subnet. Warning \
                      severity: NIC still ships without subnet \
                      context, just loses the drill path.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link_endpoint.vlan_resolves_active_when_set",
        name: "Link endpoint vlan_id must resolve to an Active VLAN when set",
        description: "Optional VLAN pointer on link_endpoint — when \
                      set, must resolve to an Active vlan. Surfaces \
                      VLAN decommissions that left endpoints \
                      stranded on retired VLAN ids.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan_template.default_unique_per_tenant",
        name: "At most one vlan_template may have is_default=true per tenant",
        description: "Two default templates within a tenant leave \
                      the fall-back path ambiguous — whichever one \
                      the resolver picks first wins, which is \
                      insertion-order and not reproducible. Guards \
                      against an operator forgetting to flip the \
                      old default off before marking the new one.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "port.interface_name_starts_with_prefix",
        name: "Port interface_name must begin with its interface_prefix",
        description: "Ports are categorised by prefix (xe- / ge- / \
                      et- / fe-) but the name is free-form. If the \
                      two disagree the device editor's filter / \
                      group-by behaviour gets confused. Catches \
                      typos + legacy imports that stamped the \
                      wrong prefix.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.hardware_model_set_when_active",
        name: "Active device should have hardware_model set",
        description: "An Active device with NULL hardware_model is \
                      a data-quality gap — config-gen can still \
                      render, but inventory reports + hardware-\
                      compatibility scans miss the row. Warning \
                      severity so existing tenants aren't flooded \
                      with errors at the point this rule ships.",
        category: "Advisory",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.room_resolves_active_when_set",
        name: "Device room_id must resolve to an Active room when set",
        description: "Device's optional room_id, when set, must \
                      resolve to an Active room. Hierarchy-scoped \
                      scope-grants expand through the room row; a \
                      decommissioned room hides the device from \
                      room-scoped grants unexpectedly.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.rack_resolves_active_when_set",
        name: "Device rack_id must resolve to an Active rack when set",
        description: "Device's optional rack_id, when set, must \
                      resolve to an Active rack. Mirror of \
                      device.room_resolves_active_when_set one \
                      hierarchy step lower.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.parent_subnet_resolves_active_when_set",
        name: "Subnet parent_subnet_id must resolve to an Active subnet when set",
        description: "Nested subnet chains (/16 → /24 → /30) \
                      depend on every ancestor being live for \
                      rollup counts. An orphaned parent breaks \
                      pool-utilization hierarchy views.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.building_resolves_active_when_set",
        name: "Device building_id must resolve to an Active building when set",
        description: "Nullable-FK sibling of device.active_requires_\
                      building (which catches the NULL case on Active \
                      devices). Building-scoped scope-grants expand \
                      through the building row; a decommissioned \
                      building silently hides the device from scoped \
                      reads.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.room_resolves_active_when_set",
        name: "Server room_id must resolve to an Active room when set",
        description: "Server's optional room_id, when set, must \
                      resolve to an Active room. Mirror of \
                      device.room_resolves_active_when_set on the \
                      server branch.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.rack_resolves_active_when_set",
        name: "Server rack_id must resolve to an Active rack when set",
        description: "Server's optional rack_id, when set, must \
                      resolve to an Active rack. Mirror of \
                      device.rack_resolves_active_when_set on the \
                      server branch.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.link_type_resolves_active",
        name: "Link link_type_id must resolve to an Active link_type",
        description: "A link whose parent link_type is soft-deleted \
                      or non-Active loses its naming template + \
                      config-gen hints. Error severity because the \
                      link won't render correctly if the type row \
                      is gone.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.building_resolves_active_when_set",
        name: "Link building_id must resolve to an Active building when set",
        description: "Links with an optional building_id pointer \
                      for location scoping — when set, it must \
                      resolve to an Active building. Warning: \
                      config-gen still renders, but building-scoped \
                      scope-grants can't reach the link.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.management_ip_set_when_active",
        name: "Active server should carry management_ip",
        description: "Servers in status='Active' are expected to \
                      be reachable, which requires a management_ip. \
                      Advisory — a server without one still ships, \
                      but probe / SSH automation can't exercise it. \
                      Parallels device.management_ip_for_active.",
        category: "Advisory",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "port.module_resolves_active_when_set",
        name: "Port module_id must resolve to an Active module when set",
        description: "Optional module pointer — when set, must \
                      resolve to an Active net.module. Port cleanly \
                      ships without a module (software-only / \
                      virtual / logical ports), so Warning rather \
                      than Error.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "port.breakout_parent_resolves_active_when_set",
        name: "Port breakout_parent_id must resolve to an Active port when set",
        description: "Breakout child ports reference their physical \
                      parent via breakout_parent_id. If the parent \
                      goes non-Active the child is stranded without \
                      a backing connector. Companion to \
                      port.breakout_parent_on_same_device which \
                      cross-checks that the parent lives on the \
                      same device.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "port.aggregate_ethernet_resolves_active_when_set",
        name: "Port aggregate_ethernet_id must resolve to an Active AE when set",
        description: "A port's membership in an AE bundle uses \
                      aggregate_ethernet_id. If the bundle goes \
                      non-Active the port's membership is a dangling \
                      pointer — config-gen would still render the \
                      port but omit the LAG config, which surprises \
                      operators.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link_endpoint.device_resolves_active_when_set",
        name: "Link endpoint device_id must resolve to an Active device when set",
        description: "Endpoints that don't yet have port_id populated \
                      still carry a device_id pointer for quick \
                      hierarchy drills. When that pointer is set, \
                      it must resolve to an Active device — otherwise \
                      the endpoint appears to belong to a retired box.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link_endpoint.ip_address_resolves_active_when_set",
        name: "Link endpoint ip_address_id must resolve to an Active address when set",
        description: "Endpoint's optional ip_address_id pointer, \
                      when set, must resolve to an Active net.ip_\
                      address row. An orphaned IP pointer leaks \
                      into config-gen's address binding.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_nic.ip_address_resolves_active_when_set",
        name: "Server NIC ip_address_id must resolve to an Active address when set",
        description: "NIC's optional ip_address_id pointer, when \
                      set, must resolve to an Active net.ip_address \
                      row. Mirrors link_endpoint.ip_address_resolves_\
                      active_when_set on the server branch.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.asn_allocation_resolves_active_when_set",
        name: "Device asn_allocation_id must resolve to an Active allocation when set",
        description: "Device's optional asn_allocation_id pointer, \
                      when set, must resolve to an Active net.asn_\
                      allocation row. An orphaned pointer would let \
                      config-gen emit a BGP local-as against a \
                      decommissioned ASN.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.asn_allocation_resolves_active_when_set",
        name: "Server asn_allocation_id must resolve to an Active allocation when set",
        description: "Mirror of device.asn_allocation_resolves_\
                      active_when_set on the server branch. Servers \
                      that run BGP-to-ToR carry an ASN allocation; \
                      this rule catches stale pointers.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.server_profile_resolves_active_when_set",
        name: "Server server_profile_id must resolve to an Active profile when set",
        description: "Server's optional server_profile_id pointer, \
                      when set, must resolve to an Active net.server_\
                      profile. The profile drives NIC fan-out + \
                      naming template defaults — a decommissioned \
                      profile leaves the server's provisioning \
                      context stale.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.loopback_ip_address_resolves_active_when_set",
        name: "Server loopback_ip_address_id must resolve to an Active IP when set",
        description: "Server's optional loopback_ip_address_id pointer, \
                      when set, must resolve to an Active net.ip_\
                      address row. An orphan pointer surfaces as a \
                      dangling /32 reservation in pool-utilization.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link_endpoint.port_resolves_active_when_set",
        name: "Link endpoint port_id must resolve to an Active port when set",
        description: "Endpoint's optional port_id, when set, must \
                      resolve to an Active net.port. A dangling port \
                      pointer breaks the config-gen interface-\
                      binding step silently.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.building_resolves_active_when_set",
        name: "Server building_id must resolve to an Active building when set",
        description: "Sibling to server.active_has_building (which \
                      catches the NULL case on Active servers) — \
                      this rule catches the present-but-non-Active \
                      case. Decommissioned-building hierarchy \
                      resolves silently exclude the server from \
                      building-scoped scope-grants otherwise.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_nic.target_port_resolves_active_when_set",
        name: "Server NIC target_port_id must resolve to an Active port when set",
        description: "NIC's optional target_port_id pointer, when \
                      set, must resolve to an Active net.port. \
                      Sibling to link_endpoint.port_resolves_\
                      active_when_set on the server NIC branch. \
                      Complements the existing server_nic.target_\
                      port_resolves rule which only checks \
                      existence, not lifecycle state.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "rack.uheight_within_reason",
        name: "Rack u_height should be in a realistic range",
        description: "Rack u_height > 60 is almost certainly a \
                      data-entry typo — most racks are 42U, \
                      enterprise-high racks cap at 48U. Advisory \
                      — the row is still valid, but surfaces it \
                      for operator review.",
        category: "Advisory",
        default_severity: Severity::Info,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.firmware_version_set_when_active",
        name: "Active device should have firmware_version set",
        description: "Active devices without a recorded firmware_\
                      version leave upgrade-planning + CVE-scan \
                      reports incomplete. Advisory — config-gen \
                      still works, but the hardware-compat pipeline \
                      needs this field.",
        category: "Advisory",
        default_severity: Severity::Info,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.serial_number_unique_per_tenant_when_set",
        name: "Device serial_number should be unique per tenant when set",
        description: "Two devices with the same serial_number in \
                      the same tenant almost always indicate a \
                      duplicate-import or copy-paste data-entry \
                      mistake. Warning-severity: the rows are \
                      still valid, but inventory sync to upstream \
                      systems (Zabbix, CMDB) breaks on the \
                      collision.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.serial_number_unique_per_tenant_when_set",
        name: "Server serial_number should be unique per tenant when set",
        description: "Mirror of device.serial_number_unique_per_\
                      tenant_when_set on the server branch. Same \
                      inventory-sync concern applies.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "module.serial_number_unique_per_tenant_when_set",
        name: "Module serial_number should be unique per tenant when set",
        description: "Linecards + transceivers carry unique serial \
                      numbers from the factory. Duplicates within \
                      a tenant point at bad data entry — the \
                      module inventory + RMA workflow needs one-\
                      to-one serial mapping.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.last_ping_ok_when_active",
        name: "Active device should have last_ping_ok = true",
        description: "Active devices that have never answered a \
                      probe (last_ping_ok != true) are either \
                      mis-commissioned or unreachable. Advisory — \
                      surfaces cases where the device row exists \
                      but the physical device is dark.",
        category: "Advisory",
        default_severity: Severity::Info,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.last_ping_ok_when_active",
        name: "Active server should have last_ping_ok = true",
        description: "Mirror of device.last_ping_ok_when_active \
                      on the server branch.",
        category: "Advisory",
        default_severity: Severity::Info,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.description_set_when_active",
        name: "Active link should carry a description",
        description: "Active links without a description are hard \
                      to audit — operators opening a legacy link \
                      need the description to understand its \
                      purpose. Advisory; not all link types warrant \
                      a description but the network dashboard's \
                      human readability degrades with empties.",
        category: "Advisory",
        default_severity: Severity::Info,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.management_ip_unique_per_tenant_when_set",
        name: "Device management_ip should be unique per tenant when set",
        description: "Two devices sharing a management_ip in the \
                      same tenant collide on SSH / probe / SNMP \
                      automation. Warning severity — the rows \
                      remain valid but the probe subsystem will \
                      double-ping or target the wrong box.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "port.admin_up_false_on_active_status",
        name: "Active port with admin_up=false is suspicious",
        description: "A port in entity status='Active' but with \
                      admin_up=false signals an in-progress planned \
                      downtime, a stale import, or a misconfiguration. \
                      Advisory — surfaces the condition for operator \
                      review without blocking config-gen.",
        category: "Advisory",
        default_severity: Severity::Info,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.management_ip_unique_per_tenant_when_set",
        name: "Server management_ip should be unique per tenant when set",
        description: "Mirror of device.management_ip_unique_per_\
                      tenant_when_set on the server branch. Same \
                      probe / SSH / SNMP collision risk.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_nic.admin_up_false_on_active_status",
        name: "Active NIC with admin_up=false is suspicious",
        description: "A NIC in entity status='Active' but with \
                      admin_up=false signals a planned downtime, \
                      a stale import, or a misconfiguration. Mirror \
                      of port.admin_up_false_on_active_status on \
                      the server-NIC branch.",
        category: "Advisory",
        default_severity: Severity::Info,
        default_enabled: true,
    },
    RuleMeta {
        code: "port.speed_mbps_reasonable_when_set",
        name: "Port speed_mbps should be within realistic range when set",
        description: "Port speed_mbps outside 100-400000 Mbps is \
                      almost always a typo — 100 Mbps is the low \
                      end of modern gear, 400 Gbps is the current \
                      high-end cap. Advisory — the row remains \
                      valid, but surfaces data-entry anomalies.",
        category: "Advisory",
        default_severity: Severity::Info,
        default_enabled: true,
    },
    RuleMeta {
        code: "rack.max_devices_positive_when_set",
        name: "Rack max_devices should be positive when set",
        description: "A non-null max_devices <= 0 is a data-entry \
                      mistake — you can't plan any devices into a \
                      rack whose max is zero or negative. DB has no \
                      CHECK on this column so the rule catches it \
                      at validation time.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "port.native_vlan_requires_access_or_trunk",
        name: "Port native_vlan_id is only valid on access or trunk mode",
        description: "native_vlan_id set on a port_mode='routed' or \
                      'unset' port misconfigures the L2 posture — \
                      config-gen would emit a VLAN assignment on an \
                      L3 interface. Warning: surfaces the mismatch \
                      without blocking the render.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "aggregate_ethernet.member_count_meets_min_links",
        name: "Aggregate-ethernet member port count should meet min_links",
        description: "An AE bundle whose member-port count is less \
                      than min_links will never come up — the LAG \
                      requires at least `min_links` active members \
                      to enter forwarding. Counts only non-deleted \
                      ports pointing at the bundle.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "change_set_item.expected_version_set_for_update",
        name: "Change-set Update items should carry expected_version",
        description: "An Update action without expected_version \
                      can't guard against stale writes — the apply \
                      step silently overwrites any concurrent \
                      changes. Advisory: some update flows don't \
                      need the guard, but explicit stale-version \
                      detection is strongly recommended.",
        category: "Advisory",
        default_severity: Severity::Info,
        default_enabled: true,
    },
    RuleMeta {
        code: "port.speed_mbps_set_when_active",
        name: "Active port should have speed_mbps populated",
        description: "Active ports without speed_mbps leave capacity \
                      planning incomplete — LACP load-balancing + \
                      link-aggregation reporting rely on the field. \
                      Advisory: config-gen still works but the \
                      capacity pipeline needs this value.",
        category: "Advisory",
        default_severity: Severity::Info,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_nic.nic_index_in_range",
        name: "Server NIC index should be within a realistic range",
        description: "nic_index outside 0..=63 is almost always a \
                      data-entry typo — 64 NICs per server is an \
                      extreme upper bound for current hardware. \
                      Warning — flags the value for operator review.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.hostname_no_leading_trailing_whitespace",
        name: "Device hostname should not have leading or trailing whitespace",
        description: "Whitespace in hostnames breaks SSH + DNS \
                      lookups in subtle ways. Trimmed vs untrimmed \
                      values compare unequal everywhere. Warning — \
                      trim the value to fix.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.hostname_no_leading_trailing_whitespace",
        name: "Server hostname should not have leading or trailing whitespace",
        description: "Mirror of device.hostname_no_leading_trailing_\
                      whitespace on the server branch. Same trim-\
                      equal compare-unequal concern.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "port.description_set_when_active",
        name: "Active port should carry a description",
        description: "Active ports without a description are harder \
                      to audit — operators debugging a link trace \
                      through the port description to identify the \
                      remote end. Advisory; config-gen still works \
                      but reports degrade.",
        category: "Advisory",
        default_severity: Severity::Info,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.link_code_no_leading_trailing_whitespace",
        name: "Link link_code should not have leading or trailing whitespace",
        description: "link_code is referenced from legacy tables + \
                      imports by exact-string match; whitespace in \
                      the value breaks the lookup silently. \
                      Warning — trim the value to fix.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "ip_address.gateway_unique_per_subnet",
        name: "At most one Gateway IP per subnet",
        description: "Two net.ip_address rows in the same subnet \
                      both marked `assigned_to_type='Gateway'` is \
                      almost always a data-entry mistake — a subnet \
                      only has one default gateway. Config-gen picks \
                      whichever the resolver finds first, which is \
                      insertion-order and not deterministic.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.display_name_no_leading_trailing_whitespace",
        name: "Subnet display_name should not have leading or trailing whitespace",
        description: "Mirror of device.hostname_no_leading_trailing_\
                      whitespace / link.link_code_no_leading_\
                      trailing_whitespace on subnet display_name.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan.display_name_no_leading_trailing_whitespace",
        name: "VLAN display_name should not have leading or trailing whitespace",
        description: "Mirror on VLAN display_name. Trim hygiene \
                      matters for search + sort + cross-referencing.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.display_name_no_leading_trailing_whitespace",
        name: "Server display_name should not have leading or trailing whitespace",
        description: "Trim-hygiene mirror on server display_name. \
                      server.display_name is optional — rule only \
                      fires when it's set.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.display_name_no_leading_trailing_whitespace",
        name: "Link display_name should not have leading or trailing whitespace",
        description: "Trim-hygiene mirror on link display_name. \
                      link.display_name is optional — rule only \
                      fires when it's set.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "ip_pool.display_name_no_leading_trailing_whitespace",
        name: "IP pool display_name should not have leading or trailing whitespace",
        description: "Trim-hygiene mirror on ip_pool display_name. \
                      Pool display names drive pool-utilization \
                      search + filter so stray whitespace breaks \
                      drill-downs.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "port.breakout_parent_not_self_loop",
        name: "Port breakout_parent_id must not reference the port itself",
        description: "A port whose breakout_parent_id equals its \
                      own id is a self-loop — both config-gen + \
                      the breakout-child queries will infinite-\
                      recurse. DB has no CHECK for this so the \
                      rule catches it at validation time.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.parent_subnet_not_self_loop",
        name: "Subnet parent_subnet_id must not reference the subnet itself",
        description: "Self-referential parent_subnet_id breaks \
                      nested-pool rollup views + infinite-recurses \
                      the ancestor-walk query.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.rack_implies_room",
        name: "Device with rack_id should also carry room_id",
        description: "Rack belongs to a room; setting rack_id on a \
                      device without also setting room_id leaves the \
                      hierarchy drill broken at the room level. \
                      Warning: config-gen still renders but room-\
                      scoped scope-grants skip the device.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.rack_implies_room",
        name: "Server with rack_id should also carry room_id",
        description: "Mirror of device.rack_implies_room on the \
                      server branch. Same hierarchy-drill + scope-\
                      grant-expansion concern.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "mlag_domain.display_name_no_leading_trailing_whitespace",
        name: "MLAG domain display_name should not have leading or trailing whitespace",
        description: "Trim-hygiene mirror on mlag_domain display_\
                      name. Domain names drive config-gen's LAG \
                      labels — stray whitespace makes the rendered \
                      output differ from what operators expect.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "rack.rack_code_no_leading_trailing_whitespace",
        name: "Rack rack_code should not have leading or trailing whitespace",
        description: "Trim-hygiene mirror on rack rack_code. \
                      rack_code is UNIQUE per room + referenced \
                      from imports by exact-string match so \
                      whitespace would break lookups silently.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "room.room_code_no_leading_trailing_whitespace",
        name: "Room room_code should not have leading or trailing whitespace",
        description: "Trim-hygiene on room_code. UNIQUE per floor; \
                      whitespace breaks lookups + hierarchy drills \
                      that match by code.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "floor.floor_code_no_leading_trailing_whitespace",
        name: "Floor floor_code should not have leading or trailing whitespace",
        description: "Trim-hygiene on floor_code. UNIQUE per \
                      building; same concern as room + rack codes.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "building.building_code_no_leading_trailing_whitespace",
        name: "Building building_code should not have leading or trailing whitespace",
        description: "Trim-hygiene on building_code. UNIQUE per \
                      site; device naming templates interpolate \
                      building_code so whitespace leaks into the \
                      rendered hostname.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "site.site_code_no_leading_trailing_whitespace",
        name: "Site site_code should not have leading or trailing whitespace",
        description: "Trim-hygiene on site_code. Site codes often \
                      interpolate into naming templates + are the \
                      primary hierarchy handle — whitespace breaks \
                      cross-referencing silently.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "region.region_code_no_leading_trailing_whitespace",
        name: "Region region_code should not have leading or trailing whitespace",
        description: "Trim-hygiene on region_code — the top of the \
                      hierarchy. Same whitespace concern as the \
                      lower-level code columns.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.device_code_no_leading_trailing_whitespace",
        name: "Device device_code should not have leading or trailing whitespace",
        description: "Trim-hygiene on device_code (optional short \
                      code alongside hostname). Whitespace breaks \
                      imports that match by exact device_code.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "asn_pool.display_name_no_leading_trailing_whitespace",
        name: "ASN pool display_name should not have leading or trailing whitespace",
        description: "Trim-hygiene on asn_pool display_name. Pool \
                      names drive allocation-picker UIs + dashboard \
                      search — whitespace breaks exact-match filters.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan_pool.display_name_no_leading_trailing_whitespace",
        name: "VLAN pool display_name should not have leading or trailing whitespace",
        description: "Trim-hygiene mirror on vlan_pool display_name.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "mlag_domain_pool.display_name_no_leading_trailing_whitespace",
        name: "MLAG domain pool display_name should not have leading or trailing whitespace",
        description: "Trim-hygiene mirror on mlag_domain_pool \
                      display_name. Completes the pool-name trim \
                      sweep (asn_pool + vlan_pool + ip_pool + \
                      mlag_domain_pool).",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "asn_pool.pool_code_no_leading_trailing_whitespace",
        name: "ASN pool pool_code should not have leading or trailing whitespace",
        description: "Trim-hygiene on asn_pool pool_code. pool_code \
                      is UNIQUE per tenant + referenced by imports \
                      so whitespace breaks exact-match lookups.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan_pool.pool_code_no_leading_trailing_whitespace",
        name: "VLAN pool pool_code should not have leading or trailing whitespace",
        description: "Trim-hygiene mirror on vlan_pool pool_code.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "ip_pool.pool_code_no_leading_trailing_whitespace",
        name: "IP pool pool_code should not have leading or trailing whitespace",
        description: "Trim-hygiene mirror on ip_pool pool_code. \
                      Completes the pool_code trim family.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "device_role.role_code_no_leading_trailing_whitespace",
        name: "Device role role_code should not have leading or trailing whitespace",
        description: "Trim-hygiene on device_role.role_code. \
                      role_code is UNIQUE per tenant + interpolated \
                      into hostname naming templates so whitespace \
                      leaks into rendered device names.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link_type.type_code_no_leading_trailing_whitespace",
        name: "Link type type_code should not have leading or trailing whitespace",
        description: "Trim-hygiene on link_type.type_code. UNIQUE \
                      per tenant; referenced from imports by exact \
                      match so whitespace breaks lookups silently.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_profile.profile_code_no_leading_trailing_whitespace",
        name: "Server profile profile_code should not have leading or trailing whitespace",
        description: "Trim-hygiene on server_profile.profile_code. \
                      profile_code interpolates into hostname \
                      naming templates (e.g. {profile_code}-srv-01) \
                      so whitespace leaks into rendered names.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "asn_block.block_code_no_leading_trailing_whitespace",
        name: "ASN block block_code should not have leading or trailing whitespace",
        description: "Trim-hygiene on asn_block.block_code. \
                      block_code is UNIQUE per tenant + referenced \
                      by imports + allocation flows so stray \
                      whitespace breaks exact-match lookups.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan_block.block_code_no_leading_trailing_whitespace",
        name: "VLAN block block_code should not have leading or trailing whitespace",
        description: "Trim-hygiene mirror on vlan_block.block_code.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "reservation_shelf.resource_key_no_leading_trailing_whitespace",
        name: "Reservation shelf resource_key should not have leading or trailing whitespace",
        description: "Trim-hygiene on reservation_shelf.resource_key. \
                      Resource keys are looked up by exact match \
                      when checking shelf cooldown — stray \
                      whitespace creates phantom duplicates.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "device_role.display_name_no_leading_trailing_whitespace",
        name: "Device role display_name should not have leading or trailing whitespace",
        description: "Trim-hygiene on device_role.display_name. \
                      Companion to the existing role_code trim \
                      rule + the display_name_not_empty rule.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link_type.display_name_no_leading_trailing_whitespace",
        name: "Link type display_name should not have leading or trailing whitespace",
        description: "Trim-hygiene mirror on link_type.display_name.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_profile.display_name_no_leading_trailing_whitespace",
        name: "Server profile display_name should not have leading or trailing whitespace",
        description: "Trim-hygiene mirror on server_profile.display_\
                      name. Completes the catalog-display trim \
                      family across device_role / link_type / \
                      server_profile.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "change_set.title_not_empty",
        name: "Change-set title must not be empty or whitespace-only",
        description: "change_set.title is NOT NULL but the DB \
                      allows empty-string + whitespace-only values. \
                      Empty titles break the change-sets list UX — \
                      operators can't identify the set without \
                      opening it.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "change_set.title_no_leading_trailing_whitespace",
        name: "Change-set title should not have leading or trailing whitespace",
        description: "Trim-hygiene on change_set.title. Whitespace \
                      breaks search + sort ordering on the change-\
                      sets list page.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan_template.template_code_no_leading_trailing_whitespace",
        name: "VLAN template template_code should not have leading or trailing whitespace",
        description: "Trim-hygiene on vlan_template.template_code. \
                      UNIQUE per tenant + referenced by exact match \
                      during VLAN creation flows; whitespace breaks \
                      template picker lookups.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "region.display_name_no_leading_trailing_whitespace",
        name: "Region display_name should not have leading or trailing whitespace",
        description: "Trim-hygiene on region display_name. Companion \
                      to the existing display_name_not_empty rule \
                      + the region_code trim check.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "site.display_name_no_leading_trailing_whitespace",
        name: "Site display_name should not have leading or trailing whitespace",
        description: "Trim-hygiene mirror on site display_name.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "building.display_name_no_leading_trailing_whitespace",
        name: "Building display_name should not have leading or trailing whitespace",
        description: "Trim-hygiene mirror on building display_name.",
        category: "Integrity",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
];

/// Find a rule by code. Returns `None` for unknown codes — callers surface
/// that as a `bad_request` rather than hitting the DB.
pub fn find_rule(code: &str) -> Option<&'static RuleMeta> {
    RULES.iter().find(|r| r.code == code)
}

// ─── Resolved rule + per-tenant merge ────────────────────────────────────

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ResolvedRule {
    #[serde(flatten)]
    pub meta: RuleMeta,
    pub effective_severity: Severity,
    pub effective_enabled: bool,
    pub has_tenant_override: bool,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListRulesQuery { pub organization_id: Uuid }

pub async fn list_rules(
    pool: &PgPool,
    q: &ListRulesQuery,
) -> Result<Vec<ResolvedRule>, EngineError> {
    let overrides: Vec<(String, Option<bool>, Option<String>)> = sqlx::query_as(
        "SELECT rule_code, enabled, severity_override
           FROM net.tenant_rule_config
          WHERE organization_id = $1")
        .bind(q.organization_id)
        .fetch_all(pool)
        .await?;
    let overrides: HashMap<String, (Option<bool>, Option<String>)> = overrides
        .into_iter().map(|(k, e, s)| (k, (e, s))).collect();

    let mut out = Vec::with_capacity(RULES.len());
    for meta in RULES {
        let ov = overrides.get(meta.code);
        let (enabled_override, severity_override) = ov
            .map(|(e, s)| (*e, s.clone()))
            .unwrap_or((None, None));
        let effective_enabled = enabled_override.unwrap_or(meta.default_enabled);
        let effective_severity = match severity_override.as_deref() {
            Some(s) => Severity::from_db(s)?,
            None => meta.default_severity,
        };
        out.push(ResolvedRule {
            meta: *meta,
            effective_enabled,
            effective_severity,
            has_tenant_override: ov.is_some(),
        });
    }
    Ok(out)
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SetRuleConfigBody {
    pub enabled: Option<bool>,
    pub severity_override: Option<String>,
    pub notes: Option<String>,
}

pub async fn set_rule_config(
    pool: &PgPool,
    org_id: Uuid,
    rule_code: &str,
    body: &SetRuleConfigBody,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    if find_rule(rule_code).is_none() {
        return Err(EngineError::bad_request(format!(
            "Unknown rule code '{rule_code}'. Check /api/net/validation/rules for the catalog.")));
    }
    if let Some(sev) = body.severity_override.as_deref() {
        Severity::from_db(sev)?; // validate early
    }
    sqlx::query(
        "INSERT INTO net.tenant_rule_config
            (organization_id, rule_code, enabled, severity_override, notes,
             created_by, updated_by)
         VALUES ($1, $2, $3, $4, $5, $6, $6)
         ON CONFLICT (organization_id, rule_code) DO UPDATE
           SET enabled           = EXCLUDED.enabled,
               severity_override = EXCLUDED.severity_override,
               notes             = EXCLUDED.notes,
               updated_at        = now(),
               updated_by        = EXCLUDED.updated_by,
               version           = net.tenant_rule_config.version + 1")
        .bind(org_id)
        .bind(rule_code)
        .bind(body.enabled)
        .bind(body.severity_override.as_deref())
        .bind(body.notes.as_deref())
        .bind(user_id)
        .execute(pool)
        .await?;
    Ok(())
}

// ─── Execution ───────────────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Violation {
    pub rule_code: String,
    pub severity: Severity,
    pub entity_type: String,
    pub entity_id: Option<Uuid>,
    pub message: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RunValidationBody {
    pub organization_id: Uuid,
    /// Optional — run one specific rule. Useful for "fix it, re-run that
    /// rule to confirm" UX without the overhead of every rule.
    pub rule_code: Option<String>,
    /// Optional — run only rules matching this category string
    /// ("Integrity" / "Consistency" / "Safety" / "Advisory"). Lets
    /// dashboards drive a "run only integrity rules" workflow
    /// without materialising the per-code list client-side.
    pub category: Option<String>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ValidationRunResult {
    pub violations: Vec<Violation>,
    pub rules_run: usize,
    pub rules_with_findings: usize,
    pub total_violations: usize,
}

pub async fn run_validation(
    pool: &PgPool,
    body: &RunValidationBody,
) -> Result<ValidationRunResult, EngineError> {
    // Gather the effective rule set first — merging catalog + tenant
    // overrides — and filter down to (a) enabled rules and (b) the
    // specific rule if one was asked for.
    let resolved = list_rules(pool, &ListRulesQuery {
        organization_id: body.organization_id,
    }).await?;

    let target: Option<&str> = body.rule_code.as_deref();
    if let Some(code) = target {
        if find_rule(code).is_none() {
            return Err(EngineError::bad_request(format!(
                "Unknown rule code '{code}'.")));
        }
    }

    let mut violations = Vec::new();
    let mut rules_run = 0usize;
    let mut rules_with_findings = 0usize;

    let category: Option<&str> = body.category.as_deref();

    for r in resolved {
        if !r.effective_enabled { continue; }
        if let Some(code) = target {
            if r.meta.code != code { continue; }
        }
        if let Some(cat) = category {
            if r.meta.category != cat { continue; }
        }
        rules_run += 1;
        let before_len = violations.len();
        dispatch(pool, body.organization_id, r.meta.code, r.effective_severity, &mut violations).await?;
        if violations.len() > before_len { rules_with_findings += 1; }
    }

    let total_violations = violations.len();
    Ok(ValidationRunResult { violations, rules_run, rules_with_findings, total_violations })
}

/// Route a rule code to its executor. Each executor pushes any found
/// violations into `out` — the `match` is the one place every new rule
/// has to appear. A rule code the catalog knows but the dispatcher
/// doesn't is caught by the `unreachable!` — a missed arm is a compile
/// error at test time via `RULES` iteration.
async fn dispatch(
    pool: &PgPool,
    org_id: Uuid,
    code: &str,
    severity: Severity,
    out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    match code {
        "device.hostname_required"         => run_device_hostname_required(pool, org_id, severity, out).await,
        "server.hostname_required"         => run_server_hostname_required(pool, org_id, severity, out).await,
        "link.endpoint_count"              => run_link_endpoint_count(pool, org_id, severity, out).await,
        "server.nic_count_matches_profile" => run_server_nic_count(pool, org_id, severity, out).await,
        "loopback.ipv4_slash_32"           => run_loopback_v4_slash_32(pool, org_id, severity, out).await,
        "vlan.unique_per_block"            => run_vlan_unique_per_block(pool, org_id, severity, out).await,
        "hierarchy.building_requires_site"    => run_building_requires_site(pool, org_id, severity, out).await,
        "hierarchy.site_requires_region"      => run_site_requires_region(pool, org_id, severity, out).await,
        "asn_allocation.value_in_block_range" => run_asn_in_block_range(pool, org_id, severity, out).await,
        "vlan.value_in_block_range"           => run_vlan_in_block_range(pool, org_id, severity, out).await,
        "device.active_requires_building"     => run_active_device_requires_building(pool, org_id, severity, out).await,
        "link.endpoints_distinct_devices"     => run_link_endpoints_distinct(pool, org_id, severity, out).await,
        "ip_address.contained_in_subnet"      => run_ip_contained_in_subnet(pool, org_id, severity, out).await,
        "subnet.scope_entity_present_when_non_free" => run_subnet_scope_entity_present(pool, org_id, severity, out).await,
        "device.unique_hostname"               => run_device_unique_hostname(pool, org_id, severity, out).await,
        "server.unique_hostname"               => run_server_unique_hostname(pool, org_id, severity, out).await,
        "ip_address.unique_per_subnet"         => run_ip_unique_per_subnet(pool, org_id, severity, out).await,
        "subnet.parent_same_pool"              => run_subnet_parent_same_pool(pool, org_id, severity, out).await,
        "subnet.parent_contains_child"         => run_subnet_parent_contains_child(pool, org_id, severity, out).await,
        "server_nic.index_unique_per_server"   => run_server_nic_index_unique(pool, org_id, severity, out).await,
        "server_nic.index_dense_sequence"      => run_server_nic_index_dense(pool, org_id, severity, out).await,
        "link.building_matches_endpoints"      => run_link_building_matches_endpoints(pool, org_id, severity, out).await,
        "loopback.ipv6_slash_128"              => run_loopback_v6_slash_128(pool, org_id, severity, out).await,
        "mlag_domain.unique_per_tenant"        => run_mlag_unique_per_tenant(pool, org_id, severity, out).await,
        "asn_allocation.unique_per_tenant"     => run_asn_unique_per_tenant(pool, org_id, severity, out).await,
        "device.management_ip_for_active"      => run_active_device_has_management_ip(pool, org_id, severity, out).await,
        "device.has_device_role"               => run_device_has_role(pool, org_id, severity, out).await,
        "link.vlan_in_tenant"                  => run_link_vlan_resolves(pool, org_id, severity, out).await,
        "link.subnet_in_tenant"                => run_link_subnet_resolves(pool, org_id, severity, out).await,
        "hierarchy.floor_requires_building"    => run_floor_requires_building(pool, org_id, severity, out).await,
        "hierarchy.rack_requires_room"         => run_rack_requires_room(pool, org_id, severity, out).await,
        "server_nic.target_device_resolves"    => run_server_nic_target_resolves(pool, org_id, severity, out).await,
        "server.loopback_in_loopback_subnet"   => run_server_loopback_in_loopback_subnet(pool, org_id, severity, out).await,
        "asn_block.range_not_empty"            => run_asn_block_range(pool, org_id, severity, out).await,
        "vlan_block.range_not_empty"           => run_vlan_block_range(pool, org_id, severity, out).await,
        "vlan.vlan_id_valid_range"             => run_vlan_id_range(pool, org_id, severity, out).await,
        "mlag_domain.domain_id_valid_range"    => run_mlag_domain_id_range(pool, org_id, severity, out).await,
        "subnet.matches_pool_family"           => run_subnet_matches_pool_family(pool, org_id, severity, out).await,
        "server_profile.nic_count_positive"    => run_server_profile_nic_count_positive(pool, org_id, severity, out).await,
        "server_nic.mlag_side_valid"           => run_server_nic_mlag_side(pool, org_id, severity, out).await,
        "device.unique_mac_address"            => run_device_unique_mac(pool, org_id, severity, out).await,
        "server.unique_mac_address"            => run_server_unique_mac(pool, org_id, severity, out).await,
        "device_role.naming_template_not_empty" => run_device_role_template_set(pool, org_id, severity, out).await,
        "asn_allocation.allocated_to_set"      => run_asn_allocation_target_set(pool, org_id, severity, out).await,
        "asn_pool.range_not_empty"             => run_asn_pool_range(pool, org_id, severity, out).await,
        "mlag_domain_pool.range_not_empty"     => run_mlag_pool_range(pool, org_id, severity, out).await,
        "link_type.naming_template_not_empty"  => run_link_type_template_set(pool, org_id, severity, out).await,
        "link.endpoint_interface_unique_per_device" => run_link_endpoint_interface_unique(pool, org_id, severity, out).await,
        "dhcp_relay_target.unique_per_vlan_ip"  => run_dhcp_relay_target_unique(pool, org_id, severity, out).await,
        "device_role.display_name_not_empty"    => run_device_role_display_name_set(pool, org_id, severity, out).await,
        "dhcp_relay_target.priority_non_negative" => run_dhcp_relay_priority_non_negative(pool, org_id, severity, out).await,
        "vlan.display_name_not_empty"            => run_vlan_display_name_set(pool, org_id, severity, out).await,
        "subnet.display_name_not_empty"          => run_subnet_display_name_set(pool, org_id, severity, out).await,
        "link.endpoint_devices_resolve"          => run_link_endpoint_devices_resolve(pool, org_id, severity, out).await,
        "subnet.active_subnet_has_pool"          => run_active_subnet_has_pool(pool, org_id, severity, out).await,
        "server.active_has_building"             => run_active_server_has_building(pool, org_id, severity, out).await,
        "subnet.within_parent_pool_cidr"         => run_subnet_within_pool_cidr(pool, org_id, severity, out).await,
        "link.unique_link_code_active"           => run_link_unique_code_active(pool, org_id, severity, out).await,
        "device_role.unique_role_code_per_tenant" => run_device_role_unique_code(pool, org_id, severity, out).await,
        "vlan.scope_entity_resolves"              => run_vlan_scope_entity_resolves(pool, org_id, severity, out).await,
        "dhcp_relay_target.vlan_active"           => run_dhcp_relay_vlan_active(pool, org_id, severity, out).await,
        "subnet.vlan_link_is_active"              => run_subnet_vlan_link_active(pool, org_id, severity, out).await,
        "ip_address.assigned_to_id_when_typed"    => run_ip_assigned_to_id_when_typed(pool, org_id, severity, out).await,
        "rack.position_positive"                  => run_rack_position_positive(pool, org_id, severity, out).await,
        "building_profile.role_counts_non_empty"  => run_building_profile_role_counts(pool, org_id, severity, out).await,
        "rendered_config.chain_integrity"         => run_rendered_config_chain(pool, org_id, severity, out).await,
        "scope_grant.no_duplicate_tuple"          => run_scope_grant_no_duplicates(pool, org_id, severity, out).await,
        "change_set.applied_has_no_pending_items" => run_change_set_applied_complete(pool, org_id, severity, out).await,
        "naming_template_override.scope_entity_resolves" => run_naming_override_scope_resolves(pool, org_id, severity, out).await,
        "asn_allocation.unique_allocated_to"      => run_asn_unique_allocated_to(pool, org_id, severity, out).await,
        "port.interface_name_not_empty"           => run_port_name_not_empty(pool, org_id, severity, out).await,
        "ip_address.reserved_type_is_marked_reserved" => run_ip_reserved_type_marked(pool, org_id, severity, out).await,
        "subnet.network_is_network_address"       => run_subnet_network_is_network(pool, org_id, severity, out).await,
        "link_endpoint.port_resolves"             => run_link_endpoint_port_resolves(pool, org_id, severity, out).await,
        "server_nic.target_port_resolves"         => run_server_nic_port_resolves(pool, org_id, severity, out).await,
        "asn_allocation.block_resolves_active"    => run_asn_alloc_block_active(pool, org_id, severity, out).await,
        "server_nic.target_device_matches_port_device" => run_server_nic_device_matches(pool, org_id, severity, out).await,
        "port.breakout_parent_on_same_device"     => run_port_breakout_same_device(pool, org_id, severity, out).await,
        "port.aggregate_on_same_device"           => run_port_aggregate_same_device(pool, org_id, severity, out).await,
        "aggregate_ethernet.name_unique_per_device" => run_ae_name_unique(pool, org_id, severity, out).await,
        "change_set.submitted_has_items"          => run_change_set_submitted_has_items(pool, org_id, severity, out).await,
        "reservation_shelf.cooldown_respected"    => run_shelf_cooldown_respected(pool, org_id, severity, out).await,
        "vlan_block.range_within_pool"            => run_vlan_block_range_within_pool(pool, org_id, severity, out).await,
        "floor.floor_code_unique_per_building"    => run_floor_code_unique(pool, org_id, severity, out).await,
        "room.room_code_unique_per_floor"         => run_room_code_unique(pool, org_id, severity, out).await,
        "rack.rack_code_unique_per_room"          => run_rack_code_unique(pool, org_id, severity, out).await,
        "ip_address.vrrp_has_peer_on_other_device" => run_vrrp_has_peer(pool, org_id, severity, out).await,
        "site_profile.display_name_not_empty"     => run_site_profile_display_name(pool, org_id, severity, out).await,
        "floor_profile.display_name_not_empty"    => run_floor_profile_display_name(pool, org_id, severity, out).await,
        "region.display_name_not_empty"           => run_region_display_name(pool, org_id, severity, out).await,
        "site.display_name_not_empty"             => run_site_display_name(pool, org_id, severity, out).await,
        "building.display_name_not_empty"         => run_building_display_name(pool, org_id, severity, out).await,
        "module.slot_unique_per_device"           => run_module_slot_unique(pool, org_id, severity, out).await,
        "mstp_priority_rule.has_steps"            => run_mstp_rule_has_steps(pool, org_id, severity, out).await,
        "reservation_shelf.resource_key_not_empty" => run_shelf_resource_key_not_empty(pool, org_id, severity, out).await,
        "mlag_domain.scope_entity_present_when_non_global" => run_mlag_scope_entity_present(pool, org_id, severity, out).await,
        "change_set_item.entity_id_required_for_mutations" => run_cs_item_entity_id_required(pool, org_id, severity, out).await,
        "port.speed_mbps_positive_when_set"       => run_port_speed_positive(pool, org_id, severity, out).await,
        "naming_template_override.template_not_empty" => run_naming_override_template_not_empty(pool, org_id, severity, out).await,
        "ip_pool.network_is_network_address"      => run_ip_pool_network_canonical(pool, org_id, severity, out).await,
        "loopback.active_has_ip_address"          => run_loopback_active_has_ip(pool, org_id, severity, out).await,
        "loopback.number_unique_per_device"       => run_loopback_number_unique(pool, org_id, severity, out).await,
        "vlan_template.code_unique_per_tenant"    => run_vlan_template_code_unique(pool, org_id, severity, out).await,
        "subnet.active_scope_entity_resolves"     => run_subnet_scope_entity_resolves_active(pool, org_id, severity, out).await,
        "rack.uheight_positive"                   => run_rack_uheight_positive(pool, org_id, severity, out).await,
        "room.max_racks_positive_when_set"        => run_room_max_racks_positive(pool, org_id, severity, out).await,
        "vlan_template.display_name_not_empty"    => run_vlan_template_display_name(pool, org_id, severity, out).await,
        "ip_address.subnet_resolves_active"       => run_ip_subnet_resolves_active(pool, org_id, severity, out).await,
        "link.active_has_endpoints"               => run_link_active_has_endpoints(pool, org_id, severity, out).await,
        "floor.building_resolves_active"          => run_floor_building_active(pool, org_id, severity, out).await,
        "room.floor_resolves_active"              => run_room_floor_active(pool, org_id, severity, out).await,
        "rack.room_resolves_active"               => run_rack_room_active(pool, org_id, severity, out).await,
        "port.device_resolves_active"             => run_port_device_active(pool, org_id, severity, out).await,
        "module.device_resolves_active"           => run_module_device_active(pool, org_id, severity, out).await,
        "loopback.device_resolves_active"         => run_loopback_device_active(pool, org_id, severity, out).await,
        "aggregate_ethernet.device_resolves_active" => run_ae_device_active(pool, org_id, severity, out).await,
        "server_nic.server_resolves_active"       => run_server_nic_server_active(pool, org_id, severity, out).await,
        "link_endpoint.link_resolves_active"      => run_link_endpoint_link_active(pool, org_id, severity, out).await,
        "device.role_resolves_active"             => run_device_role_active(pool, org_id, severity, out).await,
        "vlan.block_resolves_active"              => run_vlan_block_active(pool, org_id, severity, out).await,
        "subnet.pool_resolves_active"             => run_subnet_pool_active(pool, org_id, severity, out).await,
        "mlag_domain.pool_resolves_active"        => run_mlag_domain_pool_active(pool, org_id, severity, out).await,
        "asn_block.pool_resolves_active"          => run_asn_block_pool_active(pool, org_id, severity, out).await,
        "vlan_block.pool_resolves_active"         => run_vlan_block_pool_active(pool, org_id, severity, out).await,
        "vlan.template_resolves_active_when_set"  => run_vlan_template_active(pool, org_id, severity, out).await,
        "server_nic.vlan_resolves_active_when_set" => run_server_nic_vlan_active(pool, org_id, severity, out).await,
        "server_nic.subnet_resolves_active_when_set" => run_server_nic_subnet_active(pool, org_id, severity, out).await,
        "link_endpoint.vlan_resolves_active_when_set" => run_link_endpoint_vlan_active(pool, org_id, severity, out).await,
        "vlan_template.default_unique_per_tenant" => run_vlan_template_default_unique(pool, org_id, severity, out).await,
        "port.interface_name_starts_with_prefix"  => run_port_name_prefix(pool, org_id, severity, out).await,
        "device.hardware_model_set_when_active"   => run_device_model_set(pool, org_id, severity, out).await,
        "device.room_resolves_active_when_set"    => run_device_room_active(pool, org_id, severity, out).await,
        "device.rack_resolves_active_when_set"    => run_device_rack_active(pool, org_id, severity, out).await,
        "subnet.parent_subnet_resolves_active_when_set" => run_subnet_parent_active(pool, org_id, severity, out).await,
        "device.building_resolves_active_when_set" => run_device_building_active(pool, org_id, severity, out).await,
        "server.room_resolves_active_when_set"    => run_server_room_active(pool, org_id, severity, out).await,
        "server.rack_resolves_active_when_set"    => run_server_rack_active(pool, org_id, severity, out).await,
        "link.link_type_resolves_active"          => run_link_type_active(pool, org_id, severity, out).await,
        "link.building_resolves_active_when_set"  => run_link_building_active(pool, org_id, severity, out).await,
        "server.management_ip_set_when_active"    => run_server_management_ip(pool, org_id, severity, out).await,
        "port.module_resolves_active_when_set"    => run_port_module_active(pool, org_id, severity, out).await,
        "port.breakout_parent_resolves_active_when_set" => run_port_breakout_active(pool, org_id, severity, out).await,
        "port.aggregate_ethernet_resolves_active_when_set" => run_port_ae_active(pool, org_id, severity, out).await,
        "link_endpoint.device_resolves_active_when_set" => run_link_endpoint_device_active(pool, org_id, severity, out).await,
        "link_endpoint.ip_address_resolves_active_when_set" => run_link_endpoint_ip_active(pool, org_id, severity, out).await,
        "server_nic.ip_address_resolves_active_when_set" => run_server_nic_ip_active(pool, org_id, severity, out).await,
        "device.asn_allocation_resolves_active_when_set" => run_device_asn_alloc_active(pool, org_id, severity, out).await,
        "server.asn_allocation_resolves_active_when_set" => run_server_asn_alloc_active(pool, org_id, severity, out).await,
        "server.server_profile_resolves_active_when_set" => run_server_profile_active(pool, org_id, severity, out).await,
        "server.loopback_ip_address_resolves_active_when_set" => run_server_loopback_ip_active(pool, org_id, severity, out).await,
        "link_endpoint.port_resolves_active_when_set" => run_link_endpoint_port_active(pool, org_id, severity, out).await,
        "server.building_resolves_active_when_set" => run_server_building_active(pool, org_id, severity, out).await,
        "server_nic.target_port_resolves_active_when_set" => run_server_nic_target_port_active(pool, org_id, severity, out).await,
        "rack.uheight_within_reason"              => run_rack_uheight_reason(pool, org_id, severity, out).await,
        "device.firmware_version_set_when_active" => run_device_firmware_set(pool, org_id, severity, out).await,
        "device.serial_number_unique_per_tenant_when_set" => run_device_serial_unique(pool, org_id, severity, out).await,
        "server.serial_number_unique_per_tenant_when_set" => run_server_serial_unique(pool, org_id, severity, out).await,
        "module.serial_number_unique_per_tenant_when_set" => run_module_serial_unique(pool, org_id, severity, out).await,
        "device.last_ping_ok_when_active"         => run_device_ping_ok(pool, org_id, severity, out).await,
        "server.last_ping_ok_when_active"         => run_server_ping_ok(pool, org_id, severity, out).await,
        "link.description_set_when_active"        => run_link_description_set(pool, org_id, severity, out).await,
        "device.management_ip_unique_per_tenant_when_set" => run_device_mgmt_ip_unique(pool, org_id, severity, out).await,
        "port.admin_up_false_on_active_status"    => run_port_admin_up_active(pool, org_id, severity, out).await,
        "server.management_ip_unique_per_tenant_when_set" => run_server_mgmt_ip_unique(pool, org_id, severity, out).await,
        "server_nic.admin_up_false_on_active_status" => run_nic_admin_up_active(pool, org_id, severity, out).await,
        "port.speed_mbps_reasonable_when_set"     => run_port_speed_reasonable(pool, org_id, severity, out).await,
        "rack.max_devices_positive_when_set"      => run_rack_max_devices_positive(pool, org_id, severity, out).await,
        "port.native_vlan_requires_access_or_trunk" => run_port_native_vlan_mode(pool, org_id, severity, out).await,
        "aggregate_ethernet.member_count_meets_min_links" => run_ae_member_count(pool, org_id, severity, out).await,
        "change_set_item.expected_version_set_for_update" => run_change_set_item_expected_version(pool, org_id, severity, out).await,
        "port.speed_mbps_set_when_active"         => run_port_speed_set(pool, org_id, severity, out).await,
        "server_nic.nic_index_in_range"           => run_nic_index_range(pool, org_id, severity, out).await,
        "device.hostname_no_leading_trailing_whitespace" => run_device_hostname_ws(pool, org_id, severity, out).await,
        "server.hostname_no_leading_trailing_whitespace" => run_server_hostname_ws(pool, org_id, severity, out).await,
        "port.description_set_when_active"        => run_port_description_set(pool, org_id, severity, out).await,
        "link.link_code_no_leading_trailing_whitespace" => run_link_code_ws(pool, org_id, severity, out).await,
        "ip_address.gateway_unique_per_subnet"    => run_ip_gateway_unique(pool, org_id, severity, out).await,
        "subnet.display_name_no_leading_trailing_whitespace" => run_subnet_name_ws(pool, org_id, severity, out).await,
        "vlan.display_name_no_leading_trailing_whitespace" => run_vlan_name_ws(pool, org_id, severity, out).await,
        "server.display_name_no_leading_trailing_whitespace" => run_server_name_ws(pool, org_id, severity, out).await,
        "link.display_name_no_leading_trailing_whitespace" => run_link_name_ws(pool, org_id, severity, out).await,
        "ip_pool.display_name_no_leading_trailing_whitespace" => run_ip_pool_name_ws(pool, org_id, severity, out).await,
        "port.breakout_parent_not_self_loop"      => run_port_breakout_self_loop(pool, org_id, severity, out).await,
        "subnet.parent_subnet_not_self_loop"      => run_subnet_parent_self_loop(pool, org_id, severity, out).await,
        "device.rack_implies_room"                => run_device_rack_implies_room(pool, org_id, severity, out).await,
        "server.rack_implies_room"                => run_server_rack_implies_room(pool, org_id, severity, out).await,
        "mlag_domain.display_name_no_leading_trailing_whitespace" => run_mlag_domain_name_ws(pool, org_id, severity, out).await,
        "rack.rack_code_no_leading_trailing_whitespace" => run_rack_code_ws(pool, org_id, severity, out).await,
        "room.room_code_no_leading_trailing_whitespace" => run_room_code_ws(pool, org_id, severity, out).await,
        "floor.floor_code_no_leading_trailing_whitespace" => run_floor_code_ws(pool, org_id, severity, out).await,
        "building.building_code_no_leading_trailing_whitespace" => run_building_code_ws(pool, org_id, severity, out).await,
        "site.site_code_no_leading_trailing_whitespace" => run_site_code_ws(pool, org_id, severity, out).await,
        "region.region_code_no_leading_trailing_whitespace" => run_region_code_ws(pool, org_id, severity, out).await,
        "device.device_code_no_leading_trailing_whitespace" => run_device_code_ws(pool, org_id, severity, out).await,
        "asn_pool.display_name_no_leading_trailing_whitespace" => run_asn_pool_name_ws(pool, org_id, severity, out).await,
        "vlan_pool.display_name_no_leading_trailing_whitespace" => run_vlan_pool_name_ws(pool, org_id, severity, out).await,
        "mlag_domain_pool.display_name_no_leading_trailing_whitespace" => run_mlag_pool_name_ws(pool, org_id, severity, out).await,
        "asn_pool.pool_code_no_leading_trailing_whitespace" => run_asn_pool_code_ws(pool, org_id, severity, out).await,
        "vlan_pool.pool_code_no_leading_trailing_whitespace" => run_vlan_pool_code_ws(pool, org_id, severity, out).await,
        "ip_pool.pool_code_no_leading_trailing_whitespace" => run_ip_pool_code_ws(pool, org_id, severity, out).await,
        "device_role.role_code_no_leading_trailing_whitespace" => run_device_role_code_ws(pool, org_id, severity, out).await,
        "link_type.type_code_no_leading_trailing_whitespace" => run_link_type_code_ws(pool, org_id, severity, out).await,
        "server_profile.profile_code_no_leading_trailing_whitespace" => run_server_profile_code_ws(pool, org_id, severity, out).await,
        "asn_block.block_code_no_leading_trailing_whitespace" => run_asn_block_code_ws(pool, org_id, severity, out).await,
        "vlan_block.block_code_no_leading_trailing_whitespace" => run_vlan_block_code_ws(pool, org_id, severity, out).await,
        "reservation_shelf.resource_key_no_leading_trailing_whitespace" => run_shelf_key_ws(pool, org_id, severity, out).await,
        "device_role.display_name_no_leading_trailing_whitespace" => run_device_role_name_ws(pool, org_id, severity, out).await,
        "link_type.display_name_no_leading_trailing_whitespace" => run_link_type_name_ws(pool, org_id, severity, out).await,
        "server_profile.display_name_no_leading_trailing_whitespace" => run_server_profile_name_ws(pool, org_id, severity, out).await,
        "change_set.title_not_empty"              => run_change_set_title_empty(pool, org_id, severity, out).await,
        "change_set.title_no_leading_trailing_whitespace" => run_change_set_title_ws(pool, org_id, severity, out).await,
        "vlan_template.template_code_no_leading_trailing_whitespace" => run_vlan_template_code_ws(pool, org_id, severity, out).await,
        "region.display_name_no_leading_trailing_whitespace" => run_region_name_ws(pool, org_id, severity, out).await,
        "site.display_name_no_leading_trailing_whitespace" => run_site_name_ws(pool, org_id, severity, out).await,
        "building.display_name_no_leading_trailing_whitespace" => run_building_name_ws(pool, org_id, severity, out).await,
        "server_profile.naming_template_not_empty" => run_server_profile_template_set(pool, org_id, severity, out).await,
        other => Err(EngineError::bad_request(format!(
            "Rule '{other}' in catalog but dispatcher has no executor — codebase bug."))),
    }
}

async fn run_device_hostname_required(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid,)> = sqlx::query_as(
        "SELECT id FROM net.device
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (hostname IS NULL OR btrim(hostname) = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id,) in rows {
        out.push(Violation {
            rule_code: "device.hostname_required".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: "Device has no hostname — every device must be named.".into(),
        });
    }
    Ok(())
}

async fn run_server_hostname_required(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid,)> = sqlx::query_as(
        "SELECT id FROM net.server
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (hostname IS NULL OR btrim(hostname) = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id,) in rows {
        out.push(Violation {
            rule_code: "server.hostname_required".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: "Server has no hostname.".into(),
        });
    }
    Ok(())
}

async fn run_link_endpoint_count(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // Two failure modes: wrong count, or both endpoints present but the
    // ordering isn't exactly {0, 1}. Reporting the id + actual count is
    // enough for an admin to jump to the right row.
    let rows: Vec<(Uuid, i64, Option<i32>, Option<i32>)> = sqlx::query_as(
        "SELECT l.id,
                COUNT(e.id)                                          AS cnt,
                MIN(e.endpoint_order)::int                           AS min_order,
                MAX(e.endpoint_order)::int                           AS max_order
           FROM net.link l
           LEFT JOIN net.link_endpoint e
             ON e.link_id = l.id AND e.deleted_at IS NULL
          WHERE l.organization_id = $1 AND l.deleted_at IS NULL
          GROUP BY l.id
         HAVING COUNT(e.id) <> 2
             OR MIN(e.endpoint_order) <> 0
             OR MAX(e.endpoint_order) <> 1")
        .bind(org_id).fetch_all(pool).await?;
    for (id, cnt, min_o, max_o) in rows {
        out.push(Violation {
            rule_code: "link.endpoint_count".into(),
            severity, entity_type: "Link".into(), entity_id: Some(id),
            message: format!(
                "Link has {cnt} endpoint(s) (min_order={:?}, max_order={:?}); expected exactly 2 at orders 0 and 1.",
                min_o, max_o),
        });
    }
    Ok(())
}

async fn run_server_nic_count(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i32, i64)> = sqlx::query_as(
        "SELECT s.id, s.hostname, sp.nic_count,
                COUNT(nic.id) FILTER (WHERE nic.deleted_at IS NULL)
           FROM net.server s
           LEFT JOIN net.server_profile sp ON sp.id = s.server_profile_id
           LEFT JOIN net.server_nic nic    ON nic.server_id = s.id
          WHERE s.organization_id = $1 AND s.deleted_at IS NULL
            AND sp.nic_count IS NOT NULL
          GROUP BY s.id, s.hostname, sp.nic_count
         HAVING sp.nic_count <> COUNT(nic.id) FILTER (WHERE nic.deleted_at IS NULL)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, expected, actual) in rows {
        out.push(Violation {
            rule_code: "server.nic_count_matches_profile".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server '{hostname}' has {actual} NIC(s), profile expects {expected}."),
        });
    }
    Ok(())
}

async fn run_loopback_v4_slash_32(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // net.subnet.network is cidr. Loopback subnets are the ones referenced
    // by device.asn_allocation_id's pool scope, but that's a deep join —
    // a cheaper heuristic is the subnet_code convention ('LOOPBACK' prefix).
    // If admins use a different convention, they either rename or disable
    // this rule.
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, subnet_code, network::text
           FROM net.subnet
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND subnet_code ILIKE 'LOOPBACK%'
            AND family(network) = 4
            AND masklen(network) <> 32")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, cidr) in rows {
        out.push(Violation {
            rule_code: "loopback.ipv4_slash_32".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Loopback subnet '{code}' is {cidr}; expected /32 (check prefix length)."),
        });
    }
    Ok(())
}

async fn run_vlan_unique_per_block(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // Any non-deleted rows that share (block_id, vlan_id). Report one
    // violation per colliding row so the admin sees all involved parties.
    let rows: Vec<(Uuid, Option<Uuid>, i32)> = sqlx::query_as(
        "SELECT v.id, v.block_id, v.vlan_id
           FROM net.vlan v
           JOIN (
             SELECT block_id, vlan_id
               FROM net.vlan
              WHERE organization_id = $1 AND deleted_at IS NULL
              GROUP BY block_id, vlan_id
             HAVING COUNT(*) > 1
           ) d ON d.block_id IS NOT DISTINCT FROM v.block_id
              AND d.vlan_id = v.vlan_id
          WHERE v.organization_id = $1 AND v.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, block_id, vlan_id) in rows {
        out.push(Violation {
            rule_code: "vlan.unique_per_block".into(),
            severity, entity_type: "Vlan".into(), entity_id: Some(id),
            message: format!(
                "VLAN id {vlan_id} duplicated in block {:?}.", block_id),
        });
    }
    Ok(())
}

async fn run_building_requires_site(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, building_code
           FROM net.building
          WHERE organization_id = $1 AND deleted_at IS NULL AND site_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "hierarchy.building_requires_site".into(),
            severity, entity_type: "Building".into(), entity_id: Some(id),
            message: format!("Building '{code}' has no site_id."),
        });
    }
    Ok(())
}

async fn run_site_requires_region(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, site_code
           FROM net.site
          WHERE organization_id = $1 AND deleted_at IS NULL AND region_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "hierarchy.site_requires_region".into(),
            severity, entity_type: "Site".into(), entity_id: Some(id),
            message: format!("Site '{code}' has no region_id."),
        });
    }
    Ok(())
}

async fn run_asn_in_block_range(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i64, i64, i64)> = sqlx::query_as(
        "SELECT a.id, a.asn, b.asn_first, b.asn_last
           FROM net.asn_allocation a
           JOIN net.asn_block b ON b.id = a.block_id
          WHERE a.organization_id = $1
            AND a.deleted_at IS NULL
            AND b.deleted_at IS NULL
            AND (a.asn < b.asn_first OR a.asn > b.asn_last)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, asn, first, last) in rows {
        out.push(Violation {
            rule_code: "asn_allocation.value_in_block_range".into(),
            severity, entity_type: "AsnAllocation".into(), entity_id: Some(id),
            message: format!("ASN {asn} is outside block range [{first}, {last}]."),
        });
    }
    Ok(())
}

async fn run_vlan_in_block_range(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, i32, i32)> = sqlx::query_as(
        "SELECT v.id, v.vlan_id, b.vlan_first, b.vlan_last
           FROM net.vlan v
           JOIN net.vlan_block b ON b.id = v.block_id
          WHERE v.organization_id = $1
            AND v.deleted_at IS NULL
            AND b.deleted_at IS NULL
            AND (v.vlan_id < b.vlan_first OR v.vlan_id > b.vlan_last)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, vlan_id, first, last) in rows {
        out.push(Violation {
            rule_code: "vlan.value_in_block_range".into(),
            severity, entity_type: "Vlan".into(), entity_id: Some(id),
            message: format!("VLAN id {vlan_id} is outside block range [{first}, {last}]."),
        });
    }
    Ok(())
}

async fn run_active_device_requires_building(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // status is a net.entity_status enum — compare via ::text so we don't
    // have to teach sqlx about the custom type binding.
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname
           FROM net.device
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status::text = 'Active'
            AND building_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "device.active_requires_building".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!("Active device '{hostname}' has no building_id."),
        });
    }
    Ok(())
}

async fn run_link_endpoints_distinct(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // A link is offending if its two endpoints point at the same
    // device_id. The COUNT DISTINCT + NOT NULL filter keeps us from
    // flagging links where both endpoints are still unresolved
    // (device_id NULL on both sides).
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT l.id, l.link_code
           FROM net.link l
          WHERE l.organization_id = $1 AND l.deleted_at IS NULL
            AND (SELECT COUNT(DISTINCT e.device_id)
                   FROM net.link_endpoint e
                  WHERE e.link_id = l.id
                    AND e.device_id IS NOT NULL
                    AND e.deleted_at IS NULL) = 1
            AND (SELECT COUNT(e.id)
                   FROM net.link_endpoint e
                  WHERE e.link_id = l.id
                    AND e.device_id IS NOT NULL
                    AND e.deleted_at IS NULL) = 2")
        .bind(org_id).fetch_all(pool).await?;
    for (id, link_code) in rows {
        out.push(Violation {
            rule_code: "link.endpoints_distinct_devices".into(),
            severity, entity_type: "Link".into(), entity_id: Some(id),
            message: format!("Link '{link_code}' has both endpoints on the same device."),
        });
    }
    Ok(())
}

async fn run_ip_contained_in_subnet(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // Postgres's inet / cidr <<= operator is "is contained by". The
    // address cast forces the comparison into the inet space even if
    // the ip_address.address column is stored as varchar.
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT ip.id, ip.address::text, s.network::text
           FROM net.ip_address ip
           JOIN net.subnet s ON s.id = ip.subnet_id
          WHERE ip.organization_id = $1
            AND ip.deleted_at IS NULL
            AND s.deleted_at IS NULL
            AND NOT (ip.address <<= s.network)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, addr, network) in rows {
        out.push(Violation {
            rule_code: "ip_address.contained_in_subnet".into(),
            severity, entity_type: "IpAddress".into(), entity_id: Some(id),
            message: format!("Address {addr} is not inside subnet {network}."),
        });
    }
    Ok(())
}

async fn run_subnet_scope_entity_present(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // Warning — non-Free scope but no scope_entity_id. The reverse
    // (Free scope but scope_entity_id set) is treated as benign staleness.
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, subnet_code, scope_level
           FROM net.subnet
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND scope_level <> 'Free'
            AND scope_entity_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, level) in rows {
        out.push(Violation {
            rule_code: "subnet.scope_entity_present_when_non_free".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Subnet '{code}' claims scope_level '{level}' but has no scope_entity_id."),
        });
    }
    Ok(())
}

async fn run_device_unique_hostname(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT d.id, d.hostname
           FROM net.device d
           JOIN (
             SELECT hostname
               FROM net.device
              WHERE organization_id = $1 AND deleted_at IS NULL
              GROUP BY hostname
             HAVING COUNT(*) > 1
           ) dup ON dup.hostname = d.hostname
          WHERE d.organization_id = $1 AND d.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "device.unique_hostname".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!("Device hostname '{hostname}' is duplicated within the tenant."),
        });
    }
    Ok(())
}

async fn run_server_unique_hostname(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT s.id, s.hostname
           FROM net.server s
           JOIN (
             SELECT hostname
               FROM net.server
              WHERE organization_id = $1 AND deleted_at IS NULL
              GROUP BY hostname
             HAVING COUNT(*) > 1
           ) dup ON dup.hostname = s.hostname
          WHERE s.organization_id = $1 AND s.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "server.unique_hostname".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!("Server hostname '{hostname}' is duplicated within the tenant."),
        });
    }
    Ok(())
}

async fn run_ip_unique_per_subnet(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, Uuid)> = sqlx::query_as(
        "SELECT ip.id, ip.address::text, ip.subnet_id
           FROM net.ip_address ip
           JOIN (
             SELECT subnet_id, address
               FROM net.ip_address
              WHERE organization_id = $1 AND deleted_at IS NULL
              GROUP BY subnet_id, address
             HAVING COUNT(*) > 1
           ) dup ON dup.subnet_id = ip.subnet_id AND dup.address = ip.address
          WHERE ip.organization_id = $1 AND ip.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, addr, subnet_id) in rows {
        out.push(Violation {
            rule_code: "ip_address.unique_per_subnet".into(),
            severity, entity_type: "IpAddress".into(), entity_id: Some(id),
            message: format!("Address {addr} duplicated within subnet {subnet_id}."),
        });
    }
    Ok(())
}

async fn run_subnet_parent_same_pool(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, Uuid, Uuid)> = sqlx::query_as(
        "SELECT child.id, child.subnet_code, child.pool_id, parent.pool_id
           FROM net.subnet child
           JOIN net.subnet parent ON parent.id = child.parent_subnet_id
          WHERE child.organization_id = $1
            AND child.deleted_at IS NULL
            AND parent.deleted_at IS NULL
            AND child.pool_id <> parent.pool_id")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, child_pool, parent_pool) in rows {
        out.push(Violation {
            rule_code: "subnet.parent_same_pool".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Subnet '{code}' is in pool {child_pool} but its parent is in pool {parent_pool}."),
        });
    }
    Ok(())
}

async fn run_subnet_parent_contains_child(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String, String)> = sqlx::query_as(
        "SELECT child.id, child.subnet_code, child.network::text, parent.network::text
           FROM net.subnet child
           JOIN net.subnet parent ON parent.id = child.parent_subnet_id
          WHERE child.organization_id = $1
            AND child.deleted_at IS NULL
            AND parent.deleted_at IS NULL
            AND NOT (child.network <<= parent.network)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, child_net, parent_net) in rows {
        out.push(Violation {
            rule_code: "subnet.parent_contains_child".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Subnet '{code}' {child_net} is not contained by parent {parent_net}."),
        });
    }
    Ok(())
}

async fn run_server_nic_index_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, Uuid, i32)> = sqlx::query_as(
        "SELECT n.id, n.server_id, n.nic_index
           FROM net.server_nic n
           JOIN (
             SELECT server_id, nic_index
               FROM net.server_nic
              WHERE organization_id = $1 AND deleted_at IS NULL
              GROUP BY server_id, nic_index
             HAVING COUNT(*) > 1
           ) dup ON dup.server_id = n.server_id AND dup.nic_index = n.nic_index
          WHERE n.organization_id = $1 AND n.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, server_id, idx) in rows {
        out.push(Violation {
            rule_code: "server_nic.index_unique_per_server".into(),
            severity, entity_type: "ServerNic".into(), entity_id: Some(id),
            message: format!("Server {server_id} has duplicate NIC index {idx}."),
        });
    }
    Ok(())
}

async fn run_server_nic_index_dense(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // Per-server: if the set of active nic_index values isn't {0, 1, …, count-1}
    // we've got a gap. max(index) != count - 1 is a cheap detector.
    let rows: Vec<(Uuid, String, i32, i64)> = sqlx::query_as(
        "SELECT s.id, s.hostname, MAX(n.nic_index), COUNT(n.id)
           FROM net.server s
           JOIN net.server_nic n ON n.server_id = s.id AND n.deleted_at IS NULL
          WHERE s.organization_id = $1 AND s.deleted_at IS NULL
          GROUP BY s.id, s.hostname
         HAVING MAX(n.nic_index) <> COUNT(n.id) - 1")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, max_idx, count) in rows {
        out.push(Violation {
            rule_code: "server_nic.index_dense_sequence".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server '{hostname}' has {count} NIC(s) but max nic_index is {max_idx}; \
                 sequence has gaps."),
        });
    }
    Ok(())
}

async fn run_link_building_matches_endpoints(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // Flag only when the link has a building_id AND at least one endpoint
    // resolves to a device with a building_id AND neither endpoint device
    // is in that building. This keeps WAN / cross-site links (legitimately
    // spanning buildings) quiet — they don't carry a building_id.
    let rows: Vec<(Uuid, String, Uuid)> = sqlx::query_as(
        "SELECT l.id, l.link_code, l.building_id
           FROM net.link l
          WHERE l.organization_id = $1
            AND l.deleted_at IS NULL
            AND l.building_id IS NOT NULL
            AND NOT EXISTS (
              SELECT 1
                FROM net.link_endpoint e
                JOIN net.device d ON d.id = e.device_id
               WHERE e.link_id = l.id
                 AND e.deleted_at IS NULL
                 AND d.building_id = l.building_id)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, building_id) in rows {
        out.push(Violation {
            rule_code: "link.building_matches_endpoints".into(),
            severity, entity_type: "Link".into(), entity_id: Some(id),
            message: format!(
                "Link '{code}' is tagged for building {building_id} but neither \
                 endpoint device is in that building."),
        });
    }
    Ok(())
}

async fn run_loopback_v6_slash_128(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, subnet_code, network::text
           FROM net.subnet
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND subnet_code ILIKE 'LOOPBACK%'
            AND family(network) = 6
            AND masklen(network) <> 128")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, cidr) in rows {
        out.push(Violation {
            rule_code: "loopback.ipv6_slash_128".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Loopback IPv6 subnet '{code}' is {cidr}; expected /128."),
        });
    }
    Ok(())
}

async fn run_mlag_unique_per_tenant(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32)> = sqlx::query_as(
        "SELECT m.id, m.domain_id
           FROM net.mlag_domain m
           JOIN (
             SELECT domain_id
               FROM net.mlag_domain
              WHERE organization_id = $1 AND deleted_at IS NULL
              GROUP BY domain_id
             HAVING COUNT(*) > 1
           ) dup ON dup.domain_id = m.domain_id
          WHERE m.organization_id = $1 AND m.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, did) in rows {
        out.push(Violation {
            rule_code: "mlag_domain.unique_per_tenant".into(),
            severity, entity_type: "MlagDomain".into(), entity_id: Some(id),
            message: format!("MLAG domain ID {did} is duplicated within the tenant."),
        });
    }
    Ok(())
}

async fn run_asn_unique_per_tenant(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i64)> = sqlx::query_as(
        "SELECT a.id, a.asn
           FROM net.asn_allocation a
           JOIN (
             SELECT asn
               FROM net.asn_allocation
              WHERE organization_id = $1 AND deleted_at IS NULL
              GROUP BY asn
             HAVING COUNT(*) > 1
           ) dup ON dup.asn = a.asn
          WHERE a.organization_id = $1 AND a.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, asn) in rows {
        out.push(Violation {
            rule_code: "asn_allocation.unique_per_tenant".into(),
            severity, entity_type: "AsnAllocation".into(), entity_id: Some(id),
            message: format!("ASN {asn} is allocated more than once in this tenant."),
        });
    }
    Ok(())
}

async fn run_active_device_has_management_ip(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname
           FROM net.device
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status::text = 'Active'
            AND management_ip IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "device.management_ip_for_active".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!("Active device '{hostname}' has no management_ip."),
        });
    }
    Ok(())
}

async fn run_device_has_role(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname
           FROM net.device
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status::text = 'Active'
            AND device_role_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "device.has_device_role".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!("Active device '{hostname}' has no device_role_id."),
        });
    }
    Ok(())
}

async fn run_link_vlan_resolves(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // Flag links whose vlan_id points at a deleted or cross-tenant vlan.
    // NOT EXISTS on the happy-path join — any link with vlan_id set must
    // match a non-deleted vlan in the same tenant.
    let rows: Vec<(Uuid, String, Option<Uuid>)> = sqlx::query_as(
        "SELECT l.id, l.link_code, l.vlan_id
           FROM net.link l
          WHERE l.organization_id = $1 AND l.deleted_at IS NULL
            AND l.vlan_id IS NOT NULL
            AND NOT EXISTS (
              SELECT 1 FROM net.vlan v
               WHERE v.id = l.vlan_id
                 AND v.organization_id = l.organization_id
                 AND v.deleted_at IS NULL)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, vlan_id) in rows {
        out.push(Violation {
            rule_code: "link.vlan_in_tenant".into(),
            severity, entity_type: "Link".into(), entity_id: Some(id),
            message: format!("Link '{code}' references dangling vlan {vlan_id:?}."),
        });
    }
    Ok(())
}

async fn run_link_subnet_resolves(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, Option<Uuid>)> = sqlx::query_as(
        "SELECT l.id, l.link_code, l.subnet_id
           FROM net.link l
          WHERE l.organization_id = $1 AND l.deleted_at IS NULL
            AND l.subnet_id IS NOT NULL
            AND NOT EXISTS (
              SELECT 1 FROM net.subnet s
               WHERE s.id = l.subnet_id
                 AND s.organization_id = l.organization_id
                 AND s.deleted_at IS NULL)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, subnet_id) in rows {
        out.push(Violation {
            rule_code: "link.subnet_in_tenant".into(),
            severity, entity_type: "Link".into(), entity_id: Some(id),
            message: format!("Link '{code}' references dangling subnet {subnet_id:?}."),
        });
    }
    Ok(())
}

async fn run_floor_requires_building(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, floor_code
           FROM net.floor
          WHERE organization_id = $1 AND deleted_at IS NULL AND building_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "hierarchy.floor_requires_building".into(),
            severity, entity_type: "Floor".into(), entity_id: Some(id),
            message: format!("Floor '{code}' has no building_id."),
        });
    }
    Ok(())
}

async fn run_rack_requires_room(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, rack_code
           FROM net.rack
          WHERE organization_id = $1 AND deleted_at IS NULL AND room_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "hierarchy.rack_requires_room".into(),
            severity, entity_type: "Rack".into(), entity_id: Some(id),
            message: format!("Rack '{code}' has no room_id."),
        });
    }
    Ok(())
}

async fn run_server_nic_target_resolves(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, Uuid, Option<Uuid>)> = sqlx::query_as(
        "SELECT n.id, n.server_id, n.target_device_id
           FROM net.server_nic n
          WHERE n.organization_id = $1 AND n.deleted_at IS NULL
            AND n.target_device_id IS NOT NULL
            AND NOT EXISTS (
              SELECT 1 FROM net.device d
               WHERE d.id = n.target_device_id
                 AND d.organization_id = n.organization_id
                 AND d.deleted_at IS NULL)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, server_id, target) in rows {
        out.push(Violation {
            rule_code: "server_nic.target_device_resolves".into(),
            severity, entity_type: "ServerNic".into(), entity_id: Some(id),
            message: format!(
                "Server {server_id} NIC points at device {target:?} which is \
                 missing / deleted / cross-tenant."),
        });
    }
    Ok(())
}

async fn run_server_loopback_in_loopback_subnet(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT srv.id, srv.hostname, s.subnet_code
           FROM net.server srv
           JOIN net.ip_address ip ON ip.id = srv.loopback_ip_address_id
           JOIN net.subnet s      ON s.id  = ip.subnet_id
          WHERE srv.organization_id = $1
            AND srv.deleted_at IS NULL
            AND srv.loopback_ip_address_id IS NOT NULL
            AND s.subnet_code NOT ILIKE 'LOOPBACK%'")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, subnet_code) in rows {
        out.push(Violation {
            rule_code: "server.loopback_in_loopback_subnet".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server '{hostname}' loopback is from subnet '{subnet_code}' — \
                 expected subnet_code to start with 'LOOPBACK'."),
        });
    }
    Ok(())
}

async fn run_asn_block_range(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i64, i64)> = sqlx::query_as(
        "SELECT id, block_code, asn_first, asn_last
           FROM net.asn_block
          WHERE organization_id = $1 AND deleted_at IS NULL
            AND asn_first > asn_last")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, first, last) in rows {
        out.push(Violation {
            rule_code: "asn_block.range_not_empty".into(),
            severity, entity_type: "AsnBlock".into(), entity_id: Some(id),
            message: format!(
                "Block '{code}' has inverted range [{first}, {last}] — allocation will exhaust silently."),
        });
    }
    Ok(())
}

async fn run_vlan_block_range(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i32, i32)> = sqlx::query_as(
        "SELECT id, block_code, vlan_first, vlan_last
           FROM net.vlan_block
          WHERE organization_id = $1 AND deleted_at IS NULL
            AND vlan_first > vlan_last")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, first, last) in rows {
        out.push(Violation {
            rule_code: "vlan_block.range_not_empty".into(),
            severity, entity_type: "VlanBlock".into(), entity_id: Some(id),
            message: format!(
                "Block '{code}' has inverted range [{first}, {last}] — allocation will exhaust silently."),
        });
    }
    Ok(())
}

async fn run_vlan_id_range(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32)> = sqlx::query_as(
        "SELECT id, vlan_id FROM net.vlan
          WHERE organization_id = $1 AND deleted_at IS NULL
            AND (vlan_id < 1 OR vlan_id > 4094)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, vlan_id) in rows {
        out.push(Violation {
            rule_code: "vlan.vlan_id_valid_range".into(),
            severity, entity_type: "Vlan".into(), entity_id: Some(id),
            message: format!("VLAN id {vlan_id} is outside IEEE 802.1Q range 1-4094."),
        });
    }
    Ok(())
}

async fn run_mlag_domain_id_range(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32)> = sqlx::query_as(
        "SELECT id, domain_id FROM net.mlag_domain
          WHERE organization_id = $1 AND deleted_at IS NULL
            AND (domain_id < 1 OR domain_id > 4094)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, did) in rows {
        out.push(Violation {
            rule_code: "mlag_domain.domain_id_valid_range".into(),
            severity, entity_type: "MlagDomain".into(), entity_id: Some(id),
            message: format!("MLAG domain_id {did} outside PicOS range 1-4094."),
        });
    }
    Ok(())
}

async fn run_subnet_matches_pool_family(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // family(inet) returns 4 or 6. Mismatched families usually surface
    // as a GIST EXCLUDE error at INSERT, but a pre-existing row predating
    // the constraint could slip through.
    let rows: Vec<(Uuid, String, i32, String, i32)> = sqlx::query_as(
        "SELECT s.id, s.subnet_code, family(s.network), p.pool_code, family(p.network)
           FROM net.subnet s
           JOIN net.ip_pool p ON p.id = s.pool_id
          WHERE s.organization_id = $1
            AND s.deleted_at IS NULL
            AND p.deleted_at IS NULL
            AND family(s.network) <> family(p.network)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, sfam, pool_code, pfam) in rows {
        out.push(Violation {
            rule_code: "subnet.matches_pool_family".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Subnet '{code}' is IPv{sfam} but pool '{pool_code}' is IPv{pfam}."),
        });
    }
    Ok(())
}

async fn run_server_profile_nic_count_positive(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i32)> = sqlx::query_as(
        "SELECT id, profile_code, nic_count
           FROM net.server_profile
          WHERE organization_id = $1 AND deleted_at IS NULL AND nic_count <= 0")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, nic_count) in rows {
        out.push(Violation {
            rule_code: "server_profile.nic_count_positive".into(),
            severity, entity_type: "ServerProfile".into(), entity_id: Some(id),
            message: format!(
                "Profile '{code}' has nic_count={nic_count} — fan-out refuses zero/negative."),
        });
    }
    Ok(())
}

async fn run_server_nic_mlag_side(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT id, nic_index, mlag_side
           FROM net.server_nic
          WHERE organization_id = $1 AND deleted_at IS NULL
            AND mlag_side NOT IN ('A','B')")
        .bind(org_id).fetch_all(pool).await?;
    for (id, idx, side) in rows {
        out.push(Violation {
            rule_code: "server_nic.mlag_side_valid".into(),
            severity, entity_type: "ServerNic".into(), entity_id: Some(id),
            message: format!("NIC index {idx}: mlag_side='{side}' — expected 'A' or 'B'."),
        });
    }
    Ok(())
}

async fn run_device_unique_mac(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // macaddr comparison — ::text for portability through sqlx + joining
    // on the dup CTE.
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT d.id, d.hostname, d.mac_address::text
           FROM net.device d
           JOIN (
             SELECT mac_address
               FROM net.device
              WHERE organization_id = $1 AND deleted_at IS NULL AND mac_address IS NOT NULL
              GROUP BY mac_address
             HAVING COUNT(*) > 1
           ) dup ON dup.mac_address = d.mac_address
          WHERE d.organization_id = $1 AND d.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, mac) in rows {
        out.push(Violation {
            rule_code: "device.unique_mac_address".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!("Device '{hostname}' has duplicate MAC {mac}."),
        });
    }
    Ok(())
}

async fn run_server_unique_mac(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT s.id, s.hostname, s.mac_address::text
           FROM net.server s
           JOIN (
             SELECT mac_address
               FROM net.server
              WHERE organization_id = $1 AND deleted_at IS NULL AND mac_address IS NOT NULL
              GROUP BY mac_address
             HAVING COUNT(*) > 1
           ) dup ON dup.mac_address = s.mac_address
          WHERE s.organization_id = $1 AND s.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, mac) in rows {
        out.push(Violation {
            rule_code: "server.unique_mac_address".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!("Server '{hostname}' has duplicate MAC {mac}."),
        });
    }
    Ok(())
}

async fn run_device_role_template_set(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, role_code
           FROM net.device_role
          WHERE organization_id = $1 AND deleted_at IS NULL
            AND (naming_template IS NULL OR btrim(naming_template) = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "device_role.naming_template_not_empty".into(),
            severity, entity_type: "DeviceRole".into(), entity_id: Some(id),
            message: format!("Role '{code}' has no naming_template — naming resolver falls back to default."),
        });
    }
    Ok(())
}

async fn run_asn_allocation_target_set(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i64)> = sqlx::query_as(
        "SELECT id, asn
           FROM net.asn_allocation
          WHERE organization_id = $1 AND deleted_at IS NULL
            AND (allocated_to_type IS NULL OR allocated_to_id IS NULL
                 OR btrim(allocated_to_type) = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id, asn) in rows {
        out.push(Violation {
            rule_code: "asn_allocation.allocated_to_set".into(),
            severity, entity_type: "AsnAllocation".into(), entity_id: Some(id),
            message: format!("ASN {asn} has no allocated_to_type/id — orphaned for audit."),
        });
    }
    Ok(())
}

async fn run_asn_pool_range(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i64, i64)> = sqlx::query_as(
        "SELECT id, pool_code, asn_first, asn_last
           FROM net.asn_pool
          WHERE organization_id = $1 AND deleted_at IS NULL
            AND asn_first > asn_last")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, first, last) in rows {
        out.push(Violation {
            rule_code: "asn_pool.range_not_empty".into(),
            severity, entity_type: "AsnPool".into(), entity_id: Some(id),
            message: format!(
                "Pool '{code}' has inverted range [{first}, {last}] — every block carved from it is empty."),
        });
    }
    Ok(())
}

async fn run_mlag_pool_range(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i32, i32)> = sqlx::query_as(
        "SELECT id, pool_code, domain_first, domain_last
           FROM net.mlag_domain_pool
          WHERE organization_id = $1 AND deleted_at IS NULL
            AND domain_first > domain_last")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, first, last) in rows {
        out.push(Violation {
            rule_code: "mlag_domain_pool.range_not_empty".into(),
            severity, entity_type: "MlagDomainPool".into(), entity_id: Some(id),
            message: format!(
                "Pool '{code}' has inverted range [{first}, {last}] — no domain IDs available."),
        });
    }
    Ok(())
}

async fn run_link_type_template_set(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, type_code
           FROM net.link_type
          WHERE organization_id = $1 AND deleted_at IS NULL
            AND (naming_template IS NULL OR btrim(naming_template) = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "link_type.naming_template_not_empty".into(),
            severity, entity_type: "LinkType".into(), entity_id: Some(id),
            message: format!(
                "Link type '{code}' has no naming_template — resolver falls back to global defaults."),
        });
    }
    Ok(())
}

async fn run_server_profile_template_set(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, profile_code
           FROM net.server_profile
          WHERE organization_id = $1 AND deleted_at IS NULL
            AND (naming_template IS NULL OR btrim(naming_template) = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "server_profile.naming_template_not_empty".into(),
            severity, entity_type: "ServerProfile".into(), entity_id: Some(id),
            message: format!(
                "Profile '{code}' has no naming_template — resolver falls back to global defaults."),
        });
    }
    Ok(())
}

/// `link.endpoint_interface_unique_per_device` — detects two non-deleted
/// endpoints on the same device sharing an interface_name. Physical
/// reality: one port = one cable = one active link. GROUP BY on
/// (device_id, interface_name) with HAVING count > 1 finds every
/// duplicated tuple; we surface the link that was inserted second
/// (MIN returns the earliest id, so the 'offending' one is whatever
/// isn't the min — report the latest so operators fix forward).
async fn run_link_endpoint_interface_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, Option<String>, i64)> = sqlx::query_as(
        "SELECT e.link_id,
                d.hostname,
                e.interface_name,
                COUNT(*) OVER (PARTITION BY e.device_id, e.interface_name) AS dupes
           FROM net.link_endpoint e
           JOIN net.device d ON d.id = e.device_id AND d.deleted_at IS NULL
          WHERE e.organization_id = $1
            AND e.deleted_at IS NULL
            AND e.interface_name IS NOT NULL
            AND btrim(e.interface_name) <> ''
          ORDER BY e.device_id, e.interface_name, e.created_at")
        .bind(org_id).fetch_all(pool).await?;
    // Report only once per (device, interface) — the COUNT() window
    // gives every row its group size, but we only want to flag each
    // duplicate group, not every member.
    let mut seen: std::collections::HashSet<(String, String)> = std::collections::HashSet::new();
    for (link_id, host, iface, dupes) in rows {
        if dupes < 2 { continue; }
        let iface_s = iface.unwrap_or_default();
        if !seen.insert((host.clone(), iface_s.clone())) { continue; }
        out.push(Violation {
            rule_code: "link.endpoint_interface_unique_per_device".into(),
            severity, entity_type: "Link".into(), entity_id: Some(link_id),
            message: format!(
                "{dupes} active links share interface '{iface_s}' on device '{host}' — one port, one cable."),
        });
    }
    Ok(())
}

/// `dhcp_relay_target.unique_per_vlan_ip` — detects duplicate
/// (vlan_id, server_ip) rows. The bulk importer checks this on
/// create in upsert mode, but the CRUD endpoint / direct SQL can
/// slip duplicates past. Reports each pair once.
async fn run_dhcp_relay_target_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, Option<i32>, String, i64)> = sqlx::query_as(
        "SELECT drt.id,
                v.vlan_id,
                host(drt.server_ip) AS server_ip,
                COUNT(*) OVER (PARTITION BY drt.vlan_id, drt.server_ip) AS dupes
           FROM net.dhcp_relay_target drt
           LEFT JOIN net.vlan v ON v.id = drt.vlan_id AND v.deleted_at IS NULL
          WHERE drt.organization_id = $1 AND drt.deleted_at IS NULL
          ORDER BY drt.vlan_id, drt.server_ip, drt.created_at")
        .bind(org_id).fetch_all(pool).await?;
    let mut seen: std::collections::HashSet<(Option<i32>, String)> = std::collections::HashSet::new();
    for (id, vid, ip, dupes) in rows {
        if dupes < 2 { continue; }
        if !seen.insert((vid, ip.clone())) { continue; }
        let vid_s = vid.map(|n| n.to_string()).unwrap_or_else(|| "?".into());
        out.push(Violation {
            rule_code: "dhcp_relay_target.unique_per_vlan_ip".into(),
            severity, entity_type: "DhcpRelayTarget".into(), entity_id: Some(id),
            message: format!(
                "{dupes} dhcp_relay_target rows share vlan {vid_s} + server_ip {ip}."),
        });
    }
    Ok(())
}

/// `device_role.display_name_not_empty` — a role with a blank
/// display_name still renders in pickers but as an empty row, which
/// operators reach for but can't tell apart. Integrity twin of the
/// naming_template_not_empty rule shipped earlier.
async fn run_device_role_display_name_set(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, role_code
           FROM net.device_role
          WHERE organization_id = $1 AND deleted_at IS NULL
            AND (display_name IS NULL OR btrim(display_name) = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "device_role.display_name_not_empty".into(),
            severity, entity_type: "DeviceRole".into(), entity_id: Some(id),
            message: format!(
                "Role '{code}' has no display_name — renders as a blank row in pickers."),
        });
    }
    Ok(())
}

/// `dhcp_relay_target.priority_non_negative` — guard against CRUD /
/// raw-SQL inserts that bypass the bulk-import parser's non-negative
/// check. Negative priorities break the ordered-list render in
/// Config Gen (sort puts them first instead of where the operator
/// expected).
async fn run_dhcp_relay_priority_non_negative(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, Option<i32>, String, i32)> = sqlx::query_as(
        "SELECT drt.id, v.vlan_id, host(drt.server_ip), drt.priority
           FROM net.dhcp_relay_target drt
           LEFT JOIN net.vlan v ON v.id = drt.vlan_id AND v.deleted_at IS NULL
          WHERE drt.organization_id = $1 AND drt.deleted_at IS NULL
            AND drt.priority < 0")
        .bind(org_id).fetch_all(pool).await?;
    for (id, vid, ip, prio) in rows {
        let vid_s = vid.map(|n| n.to_string()).unwrap_or_else(|| "?".into());
        out.push(Violation {
            rule_code: "dhcp_relay_target.priority_non_negative".into(),
            severity, entity_type: "DhcpRelayTarget".into(), entity_id: Some(id),
            message: format!(
                "DHCP relay vlan {vid_s} → {ip} has negative priority {prio}."),
        });
    }
    Ok(())
}

/// `vlan.display_name_not_empty` — active VLAN with blank display_name.
/// Pickers then show "VLAN 120" which collides across blocks that
/// reuse the same numeric tag.
async fn run_vlan_display_name_set(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, Option<String>)> = sqlx::query_as(
        "SELECT v.id, v.vlan_id, b.block_code
           FROM net.vlan v
           LEFT JOIN net.vlan_block b ON b.id = v.block_id
          WHERE v.organization_id = $1 AND v.deleted_at IS NULL
            AND v.status = 'Active'::net.entity_status
            AND (v.display_name IS NULL OR btrim(v.display_name) = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id, vlan_id, block_code) in rows {
        let block = block_code.unwrap_or_else(|| "?".into());
        out.push(Violation {
            rule_code: "vlan.display_name_not_empty".into(),
            severity, entity_type: "Vlan".into(), entity_id: Some(id),
            message: format!(
                "VLAN {vlan_id} (block '{block}') has no display_name — pickers show bare vlan_id."),
        });
    }
    Ok(())
}

/// `subnet.display_name_not_empty` — parallel to vlan version.
async fn run_subnet_display_name_set(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, subnet_code, network::text
           FROM net.subnet
          WHERE organization_id = $1 AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
            AND (display_name IS NULL OR btrim(display_name) = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, network) in rows {
        out.push(Violation {
            rule_code: "subnet.display_name_not_empty".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Subnet '{code}' ({network}) has no display_name."),
        });
    }
    Ok(())
}

/// `link.endpoint_devices_resolve` — dangling endpoint → device
/// edges. LEFT JOIN to net.device + ensure it exists. Reports the
/// link (not the endpoint) since operators act on whole links.
async fn run_link_endpoint_devices_resolve(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i32)> = sqlx::query_as(
        "SELECT DISTINCT l.id, l.link_code, e.endpoint_order
           FROM net.link_endpoint e
           JOIN net.link l ON l.id = e.link_id AND l.deleted_at IS NULL
           LEFT JOIN net.device d ON d.id = e.device_id AND d.deleted_at IS NULL
          WHERE e.organization_id = $1 AND e.deleted_at IS NULL
            AND (e.device_id IS NULL OR d.id IS NULL)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, side) in rows {
        out.push(Violation {
            rule_code: "link.endpoint_devices_resolve".into(),
            severity, entity_type: "Link".into(), entity_id: Some(id),
            message: format!(
                "Link '{code}' endpoint order {side} does not resolve to a live device."),
        });
    }
    Ok(())
}

/// `subnet.active_subnet_has_pool` — active subnets without a
/// pool_id. Not every subnet is required to have a pool (the FK is
/// nullable for bootstrap / imported rows), but Active ones should.
async fn run_active_subnet_has_pool(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, subnet_code, network::text
           FROM net.subnet
          WHERE organization_id = $1 AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
            AND pool_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, network) in rows {
        out.push(Violation {
            rule_code: "subnet.active_subnet_has_pool".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Active subnet '{code}' ({network}) has no pool_id — orphaned from IP allocation lifecycle."),
        });
    }
    Ok(())
}

/// `server.active_has_building` — parallel to the device version.
async fn run_active_server_has_building(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname
           FROM net.server
          WHERE organization_id = $1 AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
            AND building_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "server.active_has_building".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Active server '{hostname}' has no building_id."),
        });
    }
    Ok(())
}

/// `subnet.within_parent_pool_cidr` — Active subnets must be
/// contained by their parent pool's pool_cidr. Uses the `<<=`
/// inet operator ("is contained by or equal to") so a subnet
/// equal to the pool CIDR is allowed (a pool of one subnet).
async fn run_subnet_within_pool_cidr(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String, String)> = sqlx::query_as(
        "SELECT sn.id, sn.subnet_code, sn.network::text, p.network::text
           FROM net.subnet sn
           JOIN net.ip_pool p ON p.id = sn.pool_id
          WHERE sn.organization_id = $1
            AND sn.deleted_at IS NULL
            AND sn.status = 'Active'::net.entity_status
            AND NOT (sn.network <<= p.network)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, network, pool_cidr) in rows {
        out.push(Violation {
            rule_code: "subnet.within_parent_pool_cidr".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Subnet '{code}' ({network}) is not contained by its pool CIDR {pool_cidr}."),
        });
    }
    Ok(())
}

/// `link.unique_link_code_active` — GROUP BY link_code HAVING count>1
/// across active links only. Collapses multi-row collisions into one
/// violation per code with a comma-joined id list in the message so
/// operators can jump straight to the duplicates.
async fn run_link_unique_code_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(String, i64, Vec<Uuid>)> = sqlx::query_as(
        "SELECT link_code, COUNT(*) AS n, array_agg(id) AS ids
           FROM net.link
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
       GROUP BY link_code
         HAVING COUNT(*) > 1")
        .bind(org_id).fetch_all(pool).await?;
    for (code, n, ids) in rows {
        // Surface the first id as the violation's entity_id so drill
        // lands somewhere; all ids go in the message.
        let primary = ids.first().copied();
        let id_list = ids.iter()
            .map(|id| id.to_string()).collect::<Vec<_>>().join(", ");
        out.push(Violation {
            rule_code: "link.unique_link_code_active".into(),
            severity, entity_type: "Link".into(), entity_id: primary,
            message: format!(
                "Link code '{code}' is shared by {n} active links: {id_list}."),
        });
    }
    Ok(())
}

/// `device_role.unique_role_code_per_tenant` — GROUP BY role_code
/// HAVING count>1, ignoring deleted_at (collisions across
/// active/deleted still break the role picker during 'show deleted'
/// audits).
async fn run_device_role_unique_code(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(String, i64, Vec<Uuid>)> = sqlx::query_as(
        "SELECT role_code, COUNT(*) AS n, array_agg(id) AS ids
           FROM net.device_role
          WHERE organization_id = $1
       GROUP BY role_code
         HAVING COUNT(*) > 1")
        .bind(org_id).fetch_all(pool).await?;
    for (code, n, ids) in rows {
        let primary = ids.first().copied();
        let id_list = ids.iter()
            .map(|id| id.to_string()).collect::<Vec<_>>().join(", ");
        out.push(Violation {
            rule_code: "device_role.unique_role_code_per_tenant".into(),
            severity, entity_type: "DeviceRole".into(), entity_id: primary,
            message: format!(
                "Role code '{code}' is shared by {n} device_role rows: {id_list}."),
        });
    }
    Ok(())
}

/// `vlan.scope_entity_resolves` — VLAN with scope_level != 'Free'
/// must point at a live row in the matching hierarchy table. One
/// UNION-ALL per scope level so each branch can use the right
/// join target (region/site/building/device).
async fn run_vlan_scope_entity_resolves(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String, String)> = sqlx::query_as(
        "SELECT v.id, v.vlan_id, v.scope_level, v.display_name
           FROM net.vlan v
          WHERE v.organization_id = $1
            AND v.deleted_at IS NULL
            AND v.scope_level != 'Free'
            AND (
                  v.scope_entity_id IS NULL
               OR (v.scope_level = 'Region'   AND NOT EXISTS
                     (SELECT 1 FROM net.region   x WHERE x.id = v.scope_entity_id AND x.deleted_at IS NULL))
               OR (v.scope_level = 'Site'     AND NOT EXISTS
                     (SELECT 1 FROM net.site     x WHERE x.id = v.scope_entity_id AND x.deleted_at IS NULL))
               OR (v.scope_level = 'Building' AND NOT EXISTS
                     (SELECT 1 FROM net.building x WHERE x.id = v.scope_entity_id AND x.deleted_at IS NULL))
               OR (v.scope_level = 'Device'   AND NOT EXISTS
                     (SELECT 1 FROM net.device   x WHERE x.id = v.scope_entity_id AND x.deleted_at IS NULL))
            )")
        .bind(org_id).fetch_all(pool).await?;
    for (id, vid, scope, name) in rows {
        out.push(Violation {
            rule_code: "vlan.scope_entity_resolves".into(),
            severity, entity_type: "Vlan".into(), entity_id: Some(id),
            message: format!(
                "VLAN {vid} '{name}' with scope_level='{scope}' has no resolvable scope_entity_id."),
        });
    }
    Ok(())
}

/// `dhcp_relay_target.vlan_active` — DHCP relay rows whose vlan_id
/// resolves to a deleted or non-Active VLAN. Left-joins so the
/// failure case is either (a) VLAN row missing entirely (shouldn't
/// happen with FK, but cheap to check) or (b) VLAN present but
/// deleted / Decommissioned.
async fn run_dhcp_relay_vlan_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT drt.id, host(drt.server_ip), COALESCE(v.status::text, '(missing)')
           FROM net.dhcp_relay_target drt
           LEFT JOIN net.vlan v ON v.id = drt.vlan_id
          WHERE drt.organization_id = $1
            AND drt.deleted_at IS NULL
            AND (v.id IS NULL
                 OR v.deleted_at IS NOT NULL
                 OR v.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, server_ip, status) in rows {
        out.push(Violation {
            rule_code: "dhcp_relay_target.vlan_active".into(),
            severity, entity_type: "DhcpRelayTarget".into(), entity_id: Some(id),
            message: format!(
                "DHCP relay target → {server_ip} points at a VLAN with status '{status}' — config-gen will emit a stale helper."),
        });
    }
    Ok(())
}

/// `ip_address.assigned_to_id_when_typed` — non-exempt typed
/// assignments must carry an assigned_to_id. Gateway / Broadcast /
/// Reserved are policy markers, not pointers, so they're skipped
/// in the filter. (NULL type = unassigned, also skipped.)
async fn run_ip_assigned_to_id_when_typed(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, address::text, COALESCE(assigned_to_type, '')
           FROM net.ip_address
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND assigned_to_type IS NOT NULL
            AND assigned_to_type NOT IN ('Gateway','Broadcast','Reserved')
            AND assigned_to_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, addr, typ) in rows {
        out.push(Violation {
            rule_code: "ip_address.assigned_to_id_when_typed".into(),
            severity, entity_type: "IpAddress".into(), entity_id: Some(id),
            message: format!(
                "IP {addr} has assigned_to_type='{typ}' but no assigned_to_id — orphaned assignment."),
        });
    }
    Ok(())
}

/// `rack.position_positive` — net.rack.position stored as 0 or
/// negative is almost always a stale default. NULL is allowed (not
/// every tenant tracks layout).
async fn run_rack_position_positive(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i32)> = sqlx::query_as(
        "SELECT id, rack_code, position
           FROM net.rack
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND position IS NOT NULL
            AND position <= 0")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, pos) in rows {
        out.push(Violation {
            rule_code: "rack.position_positive".into(),
            severity, entity_type: "Rack".into(), entity_id: Some(id),
            message: format!(
                "Rack '{code}' has position={pos} — expected a positive integer or NULL."),
        });
    }
    Ok(())
}

/// `rendered_config.chain_integrity` — LEFT JOIN on the chained
/// previous_render_id catches orphaned pointers (row pointed at
/// was deleted, or never existed). Only active (non-deleted) rows
/// are checked.
async fn run_rendered_config_chain(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, Uuid)> = sqlx::query_as(
        "SELECT r.id, r.previous_render_id
           FROM net.rendered_config r
           LEFT JOIN net.rendered_config p ON p.id = r.previous_render_id
          WHERE r.organization_id = $1
            AND r.previous_render_id IS NOT NULL
            AND p.id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, prev_id) in rows {
        out.push(Violation {
            rule_code: "rendered_config.chain_integrity".into(),
            severity, entity_type: "RenderedConfig".into(), entity_id: Some(id),
            message: format!(
                "Render {id} references previous_render_id {prev_id} which no longer exists — diff chain broken."),
        });
    }
    Ok(())
}

/// `scope_grant.no_duplicate_tuple` — GROUP BY the effective resolver
/// key HAVING count>1 across Active rows only. COALESCE on
/// scope_entity_id so a Global grant (NULL scope_entity_id) vs. an
/// EntityId grant with a specific uuid compare cleanly.
async fn run_scope_grant_no_duplicates(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(i32, String, String, String, i64, Vec<Uuid>)> = sqlx::query_as(
        "SELECT user_id,
                action,
                entity_type,
                scope_type,
                COUNT(*) AS n,
                array_agg(id) AS ids
           FROM net.scope_grant
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
          GROUP BY user_id, action, entity_type, scope_type,
                   COALESCE(scope_entity_id, '00000000-0000-0000-0000-000000000000'::uuid)
         HAVING COUNT(*) > 1")
        .bind(org_id).fetch_all(pool).await?;
    for (user_id, action, entity_type, scope_type, n, ids) in rows {
        let primary = ids.first().copied();
        let id_list = ids.iter().map(|id| id.to_string())
            .collect::<Vec<_>>().join(", ");
        out.push(Violation {
            rule_code: "scope_grant.no_duplicate_tuple".into(),
            severity, entity_type: "ScopeGrant".into(), entity_id: primary,
            message: format!(
                "{n} active grants duplicate (user {user_id}, action '{action}', entity '{entity_type}', scope '{scope_type}'): {id_list}."),
        });
    }
    Ok(())
}

/// `naming_template_override.scope_entity_resolves` — non-Global
/// overrides must point at a live row in region / site / building.
/// Same shape as the VLAN scope-resolution rule.
async fn run_naming_override_scope_resolves(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT n.id, n.scope_level, n.entity_type
           FROM net.naming_template_override n
          WHERE n.organization_id = $1
            AND n.deleted_at IS NULL
            AND n.scope_level != 'Global'
            AND (
                  n.scope_entity_id IS NULL
               OR (n.scope_level = 'Region'   AND NOT EXISTS
                     (SELECT 1 FROM net.region   x WHERE x.id = n.scope_entity_id AND x.deleted_at IS NULL))
               OR (n.scope_level = 'Site'     AND NOT EXISTS
                     (SELECT 1 FROM net.site     x WHERE x.id = n.scope_entity_id AND x.deleted_at IS NULL))
               OR (n.scope_level = 'Building' AND NOT EXISTS
                     (SELECT 1 FROM net.building x WHERE x.id = n.scope_entity_id AND x.deleted_at IS NULL))
            )")
        .bind(org_id).fetch_all(pool).await?;
    for (id, scope_level, entity_type) in rows {
        out.push(Violation {
            rule_code: "naming_template_override.scope_entity_resolves".into(),
            severity, entity_type: "NamingTemplateOverride".into(), entity_id: Some(id),
            message: format!(
                "Naming override for {entity_type} at scope '{scope_level}' has no resolvable scope_entity_id."),
        });
    }
    Ok(())
}

/// `asn_allocation.unique_allocated_to` — GROUP BY
/// (allocated_to_type, allocated_to_id) HAVING count>1 across active
/// rows. Multi-ASN per entity = config-gen picks one arbitrarily.
async fn run_asn_unique_allocated_to(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(String, Uuid, i64, Vec<Uuid>)> = sqlx::query_as(
        "SELECT allocated_to_type,
                allocated_to_id,
                COUNT(*) AS n,
                array_agg(id) AS ids
           FROM net.asn_allocation
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
          GROUP BY allocated_to_type, allocated_to_id
         HAVING COUNT(*) > 1")
        .bind(org_id).fetch_all(pool).await?;
    for (target_type, target_id, n, ids) in rows {
        let primary = ids.first().copied();
        let id_list = ids.iter().map(|id| id.to_string())
            .collect::<Vec<_>>().join(", ");
        out.push(Violation {
            rule_code: "asn_allocation.unique_allocated_to".into(),
            severity, entity_type: "AsnAllocation".into(), entity_id: primary,
            message: format!(
                "{n} active ASN allocations target the same {target_type} {target_id}: {id_list}."),
        });
    }
    Ok(())
}

/// `ip_address.reserved_type_is_marked_reserved` — typed-reserved
/// roles (Gateway / Broadcast / Reserved) should have is_reserved
/// flipped to true. Catches rows where the type was set but the
/// boolean wasn't.
async fn run_ip_reserved_type_marked(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, host(address), assigned_to_type
           FROM net.ip_address
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND assigned_to_type IN ('Gateway','Broadcast','Reserved')
            AND is_reserved = false")
        .bind(org_id).fetch_all(pool).await?;
    for (id, addr, typ) in rows {
        out.push(Violation {
            rule_code: "ip_address.reserved_type_is_marked_reserved".into(),
            severity, entity_type: "IpAddress".into(), entity_id: Some(id),
            message: format!(
                "IP {addr} has assigned_to_type='{typ}' but is_reserved=false — flag the policy role on is_reserved."),
        });
    }
    Ok(())
}

/// `subnet.network_is_network_address` — stored CIDR should have no
/// host bits set (should be the network address). `set_masklen(network,
/// masklen(network)) != network` flags rows that drifted (e.g.
/// 10.0.0.5/24 stored instead of 10.0.0.0/24).
async fn run_subnet_network_is_network(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, subnet_code, network::text
           FROM net.subnet
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND network != set_masklen(network, masklen(network))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, network) in rows {
        out.push(Violation {
            rule_code: "subnet.network_is_network_address".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Subnet '{code}' stored as '{network}' has host bits set — canonical form is the network address."),
        });
    }
    Ok(())
}

/// `link_endpoint.port_resolves` — non-null port_id must point at
/// a live net.port. LEFT JOIN catches orphaned references.
async fn run_link_endpoint_port_resolves(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, Uuid, Uuid, i32)> = sqlx::query_as(
        "SELECT e.id, e.link_id, e.port_id, e.endpoint_order
           FROM net.link_endpoint e
           LEFT JOIN net.port p ON p.id = e.port_id AND p.deleted_at IS NULL
          WHERE e.organization_id = $1
            AND e.port_id IS NOT NULL
            AND p.id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, link_id, port_id, order) in rows {
        out.push(Violation {
            rule_code: "link_endpoint.port_resolves".into(),
            severity, entity_type: "LinkEndpoint".into(), entity_id: Some(id),
            message: format!(
                "Link {link_id} endpoint order {order} references port {port_id} which doesn't resolve — config-gen will emit an empty interface slot."),
        });
    }
    Ok(())
}

/// `server_nic.target_port_resolves` — non-null target_port_id
/// must point at a live net.port.
async fn run_server_nic_port_resolves(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, Uuid, Uuid, i32)> = sqlx::query_as(
        "SELECT n.id, n.server_id, n.target_port_id, n.nic_index
           FROM net.server_nic n
           LEFT JOIN net.port p ON p.id = n.target_port_id AND p.deleted_at IS NULL
          WHERE n.organization_id = $1
            AND n.deleted_at IS NULL
            AND n.target_port_id IS NOT NULL
            AND p.id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, server_id, port_id, idx) in rows {
        out.push(Violation {
            rule_code: "server_nic.target_port_resolves".into(),
            severity, entity_type: "ServerNic".into(), entity_id: Some(id),
            message: format!(
                "Server {server_id} NIC {idx} target_port_id {port_id} doesn't resolve to a live port."),
        });
    }
    Ok(())
}

/// `asn_allocation.block_resolves_active` — block_id must resolve
/// + be Active. JOIN catches both missing rows + Decommissioned blocks.
async fn run_asn_alloc_block_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i64, Uuid, String)> = sqlx::query_as(
        "SELECT a.id, a.asn, a.block_id, COALESCE(b.status::text, '(missing)')
           FROM net.asn_allocation a
           LEFT JOIN net.asn_block b ON b.id = a.block_id
          WHERE a.organization_id = $1
            AND a.deleted_at IS NULL
            AND (b.id IS NULL
                 OR b.deleted_at IS NOT NULL
                 OR b.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, asn, block_id, status) in rows {
        out.push(Violation {
            rule_code: "asn_allocation.block_resolves_active".into(),
            severity, entity_type: "AsnAllocation".into(), entity_id: Some(id),
            message: format!(
                "ASN {asn} (allocation {id}) references block {block_id} with status '{status}'."),
        });
    }
    Ok(())
}

/// `server_nic.target_device_matches_port_device` — denorm check.
/// target_device_id should equal target_port_id.device_id when both
/// are populated. GROUP BY for drift between the two columns.
async fn run_server_nic_device_matches(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, Uuid, i32, Uuid, Uuid)> = sqlx::query_as(
        "SELECT n.id, n.server_id, n.nic_index, n.target_device_id, p.device_id
           FROM net.server_nic n
           JOIN net.port p ON p.id = n.target_port_id AND p.deleted_at IS NULL
          WHERE n.organization_id = $1
            AND n.deleted_at IS NULL
            AND n.target_port_id IS NOT NULL
            AND n.target_device_id IS NOT NULL
            AND n.target_device_id != p.device_id")
        .bind(org_id).fetch_all(pool).await?;
    for (id, server_id, idx, target_device, port_device) in rows {
        out.push(Violation {
            rule_code: "server_nic.target_device_matches_port_device".into(),
            severity, entity_type: "ServerNic".into(), entity_id: Some(id),
            message: format!(
                "Server {server_id} NIC {idx} target_device_id {target_device} != port's device_id {port_device} — denorm drift."),
        });
    }
    Ok(())
}

/// `port.breakout_parent_on_same_device` — breakout is a physical
/// operation on one switch's port module, so parent must be on the
/// same device. Self-join on net.port to compare device_id.
async fn run_port_breakout_same_device(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, Uuid, Uuid)> = sqlx::query_as(
        "SELECT child.id, child.interface_name, child.device_id, parent.device_id
           FROM net.port child
           JOIN net.port parent ON parent.id = child.breakout_parent_id
          WHERE child.organization_id = $1
            AND child.deleted_at IS NULL
            AND child.breakout_parent_id IS NOT NULL
            AND parent.device_id != child.device_id")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name, child_dev, parent_dev) in rows {
        out.push(Violation {
            rule_code: "port.breakout_parent_on_same_device".into(),
            severity, entity_type: "Port".into(), entity_id: Some(id),
            message: format!(
                "Port '{name}' on device {child_dev} has breakout_parent on device {parent_dev}."),
        });
    }
    Ok(())
}

/// `port.aggregate_on_same_device` — an ae (LACP bundle) is a
/// single-device construct; members must be on the same device.
async fn run_port_aggregate_same_device(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, Uuid, Uuid)> = sqlx::query_as(
        "SELECT p.id, p.interface_name, p.device_id, ae.device_id
           FROM net.port p
           JOIN net.aggregate_ethernet ae ON ae.id = p.aggregate_ethernet_id
          WHERE p.organization_id = $1
            AND p.deleted_at IS NULL
            AND p.aggregate_ethernet_id IS NOT NULL
            AND ae.device_id != p.device_id")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name, port_dev, ae_dev) in rows {
        out.push(Violation {
            rule_code: "port.aggregate_on_same_device".into(),
            severity, entity_type: "Port".into(), entity_id: Some(id),
            message: format!(
                "Port '{name}' on device {port_dev} bundled into aggregate_ethernet on device {ae_dev}."),
        });
    }
    Ok(())
}

/// `aggregate_ethernet.name_unique_per_device` — GROUP BY
/// (device_id, ae_name) HAVING count>1 catches collisions that
/// would make config-gen emit duplicate `set interface ae-N` lines.
async fn run_ae_name_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i64, Vec<Uuid>)> = sqlx::query_as(
        "SELECT device_id, ae_name, COUNT(*) AS n, array_agg(id) AS ids
           FROM net.aggregate_ethernet
          WHERE organization_id = $1
            AND deleted_at IS NULL
          GROUP BY device_id, ae_name
         HAVING COUNT(*) > 1")
        .bind(org_id).fetch_all(pool).await?;
    for (device_id, name, n, ids) in rows {
        let primary = ids.first().copied();
        let id_list = ids.iter().map(|id| id.to_string())
            .collect::<Vec<_>>().join(", ");
        out.push(Violation {
            rule_code: "aggregate_ethernet.name_unique_per_device".into(),
            severity, entity_type: "AggregateEthernet".into(), entity_id: primary,
            message: format!(
                "{n} aggregate ethernet rows on device {device_id} share name '{name}': {id_list}."),
        });
    }
    Ok(())
}

/// `change_set.submitted_has_items` — sets past Draft status
/// with zero items. Normal apply path guards against this, but
/// raw SQL INSERT into net.change_set without matching
/// change_set_item rows can produce the state.
async fn run_change_set_submitted_has_items(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT cs.id, cs.title, cs.status
           FROM net.change_set cs
          WHERE cs.organization_id = $1
            AND cs.deleted_at IS NULL
            AND cs.status IN ('Submitted','Approved','Applied')
            AND NOT EXISTS (
                SELECT 1 FROM net.change_set_item i
                 WHERE i.change_set_id = cs.id
            )")
        .bind(org_id).fetch_all(pool).await?;
    for (id, title, status) in rows {
        out.push(Violation {
            rule_code: "change_set.submitted_has_items".into(),
            severity, entity_type: "ChangeSet".into(), entity_id: Some(id),
            message: format!(
                "Change set '{title}' in status '{status}' has zero items — nothing to approve / apply."),
        });
    }
    Ok(())
}

/// `reservation_shelf.cooldown_respected` — Active shelf rows with
/// `available_after` in the past aren't "bad" per se, but mean no
/// background job has recycled them. Surfaces the case cleanly so
/// admins know the recycler isn't running.
async fn run_shelf_cooldown_respected(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, resource_type, resource_key
           FROM net.reservation_shelf
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
            AND available_after < now()")
        .bind(org_id).fetch_all(pool).await?;
    for (id, resource_type, resource_key) in rows {
        out.push(Violation {
            rule_code: "reservation_shelf.cooldown_respected".into(),
            severity, entity_type: "ReservationShelf".into(), entity_id: Some(id),
            message: format!(
                "Shelf entry {resource_type}/{resource_key} past its cooldown — recycler hasn't run."),
        });
    }
    Ok(())
}

/// `vlan_block.range_within_pool` — JOIN net.vlan_block →
/// net.vlan_pool + compare (vlan_first, vlan_last) ranges. Flag
/// rows where block range isn't fully contained by pool range.
async fn run_vlan_block_range_within_pool(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i32, i32, i32, i32)> = sqlx::query_as(
        "SELECT b.id,
                b.block_code,
                b.vlan_first,
                b.vlan_last,
                p.vlan_first,
                p.vlan_last
           FROM net.vlan_block b
           JOIN net.vlan_pool p ON p.id = b.pool_id
          WHERE b.organization_id = $1
            AND b.deleted_at IS NULL
            AND (b.vlan_first < p.vlan_first
                 OR b.vlan_last > p.vlan_last)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, bfirst, blast, pfirst, plast) in rows {
        out.push(Violation {
            rule_code: "vlan_block.range_within_pool".into(),
            severity, entity_type: "VlanBlock".into(), entity_id: Some(id),
            message: format!(
                "VLAN block '{code}' range {bfirst}-{blast} straddles parent pool range {pfirst}-{plast}."),
        });
    }
    Ok(())
}

/// `port.interface_name_not_empty` — net.port.interface_name is
/// NOT NULL but the CHECK doesn't cover the blank-string case.
/// Empty name breaks config-gen's `set interface ... ...` stanza.
async fn run_port_name_not_empty(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, Uuid)> = sqlx::query_as(
        "SELECT id, device_id
           FROM net.port
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND btrim(interface_name) = ''")
        .bind(org_id).fetch_all(pool).await?;
    for (id, device_id) in rows {
        out.push(Violation {
            rule_code: "port.interface_name_not_empty".into(),
            severity, entity_type: "Port".into(), entity_id: Some(id),
            message: format!(
                "Port {id} on device {device_id} has an empty interface_name — config-gen will emit malformed lines."),
        });
    }
    Ok(())
}

/// `change_set.applied_has_no_pending_items` — sets marked Applied
/// shouldn't have items with applied_at IS NULL. Surfaces the partial-
/// apply state that normally can't happen (guarded by the transition
/// path) but raw SQL / crashed apply can produce.
async fn run_change_set_applied_complete(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i64)> = sqlx::query_as(
        "SELECT cs.id, cs.title, COUNT(i.id) AS pending
           FROM net.change_set cs
           JOIN net.change_set_item i ON i.change_set_id = cs.id
          WHERE cs.organization_id = $1
            AND cs.deleted_at IS NULL
            AND cs.status = 'Applied'
            AND i.applied_at IS NULL
          GROUP BY cs.id, cs.title")
        .bind(org_id).fetch_all(pool).await?;
    for (id, title, pending) in rows {
        out.push(Violation {
            rule_code: "change_set.applied_has_no_pending_items".into(),
            severity, entity_type: "ChangeSet".into(), entity_id: Some(id),
            message: format!(
                "Applied change set '{title}' has {pending} item(s) with applied_at=NULL — partial apply."),
        });
    }
    Ok(())
}

/// `building_profile.role_counts_non_empty` — a profile with zero
/// role-count rows expands to an empty template. Finds every
/// profile whose COUNT(*) in building_profile_role_count is 0.
async fn run_building_profile_role_counts(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT bp.id, bp.profile_code
           FROM net.building_profile bp
          WHERE bp.organization_id = $1
            AND bp.deleted_at IS NULL
            AND NOT EXISTS (
                  SELECT 1 FROM net.building_profile_role_count rc
                   WHERE rc.building_profile_id = bp.id)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "building_profile.role_counts_non_empty".into(),
            severity, entity_type: "BuildingProfile".into(), entity_id: Some(id),
            message: format!(
                "Building profile '{code}' has no role-count rows — template expands to nothing."),
        });
    }
    Ok(())
}

/// `subnet.vlan_link_is_active` — subnets with non-null vlan_id
/// pointing at a deleted / Decommissioned VLAN. Warning not error —
/// IP ranges often outlive VLAN tags, but an operator should still
/// know the link is stale before running config-gen.
async fn run_subnet_vlan_link_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT sn.id, sn.subnet_code, COALESCE(v.status::text, '(missing)')
           FROM net.subnet sn
           LEFT JOIN net.vlan v ON v.id = sn.vlan_id
          WHERE sn.organization_id = $1
            AND sn.deleted_at IS NULL
            AND sn.vlan_id IS NOT NULL
            AND (v.id IS NULL
                 OR v.deleted_at IS NOT NULL
                 OR v.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, status) in rows {
        out.push(Violation {
            rule_code: "subnet.vlan_link_is_active".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Subnet '{code}' links to a VLAN with status '{status}' — consider clearing vlan_id or reactivating the VLAN."),
        });
    }
    Ok(())
}

/// `floor.floor_code_unique_per_building` — GROUP BY
/// (building_id, floor_code) HAVING count>1. Active rows only.
async fn run_floor_code_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i64, Vec<Uuid>)> = sqlx::query_as(
        "SELECT building_id, floor_code, COUNT(*) AS n, array_agg(id) AS ids
           FROM net.floor
          WHERE organization_id = $1
            AND deleted_at IS NULL
          GROUP BY building_id, floor_code
         HAVING COUNT(*) > 1")
        .bind(org_id).fetch_all(pool).await?;
    for (building_id, code, n, ids) in rows {
        let primary = ids.first().copied();
        let id_list = ids.iter().map(|id| id.to_string())
            .collect::<Vec<_>>().join(", ");
        out.push(Violation {
            rule_code: "floor.floor_code_unique_per_building".into(),
            severity, entity_type: "Floor".into(), entity_id: primary,
            message: format!(
                "{n} floors in building {building_id} share floor_code '{code}': {id_list}."),
        });
    }
    Ok(())
}

/// `room.room_code_unique_per_floor` — parallel to floor rule.
async fn run_room_code_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i64, Vec<Uuid>)> = sqlx::query_as(
        "SELECT floor_id, room_code, COUNT(*) AS n, array_agg(id) AS ids
           FROM net.room
          WHERE organization_id = $1
            AND deleted_at IS NULL
          GROUP BY floor_id, room_code
         HAVING COUNT(*) > 1")
        .bind(org_id).fetch_all(pool).await?;
    for (floor_id, code, n, ids) in rows {
        let primary = ids.first().copied();
        let id_list = ids.iter().map(|id| id.to_string())
            .collect::<Vec<_>>().join(", ");
        out.push(Violation {
            rule_code: "room.room_code_unique_per_floor".into(),
            severity, entity_type: "Room".into(), entity_id: primary,
            message: format!(
                "{n} rooms on floor {floor_id} share room_code '{code}': {id_list}."),
        });
    }
    Ok(())
}

/// `ip_address.vrrp_has_peer_on_other_device` — VRRP VIPs should
/// appear on at least two distinct devices (the VRRP master +
/// backup speakers). Single-device VIPs flag a broken pair.
async fn run_vrrp_has_peer(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // Heuristic: a VRRP IP row has assigned_to_id pointing at one
    // device. The "pair" shows up when a SECOND ip_address row
    // exists in the same subnet with the same assigned_to_type +
    // a different assigned_to_id. This query finds VIP rows with
    // no sibling in the subnet.
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT ip.id, host(ip.address)
           FROM net.ip_address ip
          WHERE ip.organization_id = $1
            AND ip.deleted_at IS NULL
            AND ip.assigned_to_type = 'Vrrp'
            AND NOT EXISTS (
                SELECT 1 FROM net.ip_address peer
                 WHERE peer.organization_id = ip.organization_id
                   AND peer.subnet_id       = ip.subnet_id
                   AND peer.deleted_at IS NULL
                   AND peer.id != ip.id
                   AND peer.assigned_to_type = 'Vrrp'
            )")
        .bind(org_id).fetch_all(pool).await?;
    for (id, addr) in rows {
        out.push(Violation {
            rule_code: "ip_address.vrrp_has_peer_on_other_device".into(),
            severity, entity_type: "IpAddress".into(), entity_id: Some(id),
            message: format!(
                "VRRP VIP {addr} has no peer VIP in the same subnet — single-speaker VRRP is a broken pair."),
        });
    }
    Ok(())
}

/// `site_profile.display_name_not_empty` — schema NOT NULL doesn't
/// reject blank strings. Empty display_name renders as empty row
/// in pickers.
async fn run_site_profile_display_name(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid,)> = sqlx::query_as(
        "SELECT id FROM net.site_profile
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND btrim(display_name) = ''")
        .bind(org_id).fetch_all(pool).await?;
    for (id,) in rows {
        out.push(Violation {
            rule_code: "site_profile.display_name_not_empty".into(),
            severity, entity_type: "SiteProfile".into(), entity_id: Some(id),
            message: format!("Site profile {id} has an empty display_name — fix before shipping to pickers."),
        });
    }
    Ok(())
}

/// `region.display_name_not_empty` — schema NOT NULL doesn't
/// reject blank strings. Hierarchy picker rows rendered blank
/// when this drifts.
async fn run_region_display_name(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, region_code FROM net.region
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND btrim(display_name) = ''")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "region.display_name_not_empty".into(),
            severity, entity_type: "Region".into(), entity_id: Some(id),
            message: format!("Region '{code}' has an empty display_name."),
        });
    }
    Ok(())
}

/// `site.display_name_not_empty` — parallel to region.
async fn run_site_display_name(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, site_code FROM net.site
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND btrim(display_name) = ''")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "site.display_name_not_empty".into(),
            severity, entity_type: "Site".into(), entity_id: Some(id),
            message: format!("Site '{code}' has an empty display_name."),
        });
    }
    Ok(())
}

/// `mlag_domain.scope_entity_present_when_non_global` — parallel
/// to vlan.scope_entity_resolves. Non-Global scope_level needs a
/// populated scope_entity_id.
async fn run_mlag_scope_entity_present(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT id, domain_id, scope_level
           FROM net.mlag_domain
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND scope_level != 'Global'
            AND scope_entity_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, domain_id, scope_level) in rows {
        out.push(Violation {
            rule_code: "mlag_domain.scope_entity_present_when_non_global".into(),
            severity, entity_type: "MlagDomain".into(), entity_id: Some(id),
            message: format!(
                "MLAG domain {domain_id} with scope_level='{scope_level}' has no scope_entity_id."),
        });
    }
    Ok(())
}

/// `change_set_item.entity_id_required_for_mutations` — Update /
/// Delete / Rename need entity_id; Create may be NULL (entity
/// doesn't exist yet).
async fn run_cs_item_entity_id_required(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, Uuid, String)> = sqlx::query_as(
        "SELECT id, change_set_id, action::text
           FROM net.change_set_item
          WHERE organization_id = $1
            AND action IN ('Update','Delete','Rename')
            AND entity_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, cs_id, action) in rows {
        out.push(Violation {
            rule_code: "change_set_item.entity_id_required_for_mutations".into(),
            severity, entity_type: "ChangeSetItem".into(), entity_id: Some(id),
            message: format!(
                "Item in change set {cs_id} has action '{action}' but null entity_id — nothing to mutate."),
        });
    }
    Ok(())
}

/// `naming_template_override.template_not_empty` — blank template
/// shortcut-wins the resolver + produces empty hostnames.
async fn run_naming_override_template_not_empty(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, entity_type, scope_level
           FROM net.naming_template_override
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND btrim(naming_template) = ''")
        .bind(org_id).fetch_all(pool).await?;
    for (id, entity_type, scope_level) in rows {
        out.push(Violation {
            rule_code: "naming_template_override.template_not_empty".into(),
            severity, entity_type: "NamingTemplateOverride".into(), entity_id: Some(id),
            message: format!(
                "Override for {entity_type} at scope '{scope_level}' has an empty template — resolver shorts-out producing blank hostnames."),
        });
    }
    Ok(())
}

/// `ip_pool.network_is_network_address` — parallel to the subnet
/// rule. `network != set_masklen(network, masklen(network))` flags
/// non-canonical storage.
async fn run_ip_pool_network_canonical(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, pool_code, network::text
           FROM net.ip_pool
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND network != set_masklen(network, masklen(network))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, network) in rows {
        out.push(Violation {
            rule_code: "ip_pool.network_is_network_address".into(),
            severity, entity_type: "IpPool".into(), entity_id: Some(id),
            message: format!(
                "IP pool '{code}' stored as '{network}' has host bits set — canonical form is the network address."),
        });
    }
    Ok(())
}

/// `ip_address.subnet_resolves_active` — IP's subnet must be
/// live + Active. LEFT JOIN catches missing subnet + deleted
/// subnet + non-Active subnet.
async fn run_ip_subnet_resolves_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT ip.id, host(ip.address), COALESCE(sn.status::text, '(missing)')
           FROM net.ip_address ip
           LEFT JOIN net.subnet sn ON sn.id = ip.subnet_id
          WHERE ip.organization_id = $1
            AND ip.deleted_at IS NULL
            AND (sn.id IS NULL
                 OR sn.deleted_at IS NOT NULL
                 OR sn.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, addr, status) in rows {
        out.push(Violation {
            rule_code: "ip_address.subnet_resolves_active".into(),
            severity, entity_type: "IpAddress".into(), entity_id: Some(id),
            message: format!(
                "IP {addr} references a subnet with status '{status}' — orphaned allocation."),
        });
    }
    Ok(())
}

/// `link.active_has_endpoints` — Active link with < 2 endpoints
/// is half-formed. Parallel to link.endpoint_count which catches > 2.
async fn run_link_active_has_endpoints(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i64)> = sqlx::query_as(
        "SELECT l.id, l.link_code, COUNT(e.id) AS n
           FROM net.link l
           LEFT JOIN net.link_endpoint e ON e.link_id = l.id
          WHERE l.organization_id = $1
            AND l.deleted_at IS NULL
            AND l.status = 'Active'::net.entity_status
          GROUP BY l.id, l.link_code
         HAVING COUNT(e.id) < 2")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, n) in rows {
        out.push(Violation {
            rule_code: "link.active_has_endpoints".into(),
            severity, entity_type: "Link".into(), entity_id: Some(id),
            message: format!(
                "Active link '{code}' has only {n} endpoint(s) — needs at least 2."),
        });
    }
    Ok(())
}

/// `floor.building_resolves_active` — floor's building must be live.
async fn run_floor_building_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT f.id, f.floor_code, COALESCE(b.status::text, '(missing)')
           FROM net.floor f
           LEFT JOIN net.building b ON b.id = f.building_id
          WHERE f.organization_id = $1
            AND f.deleted_at IS NULL
            AND (b.id IS NULL
                 OR b.deleted_at IS NOT NULL
                 OR b.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, status) in rows {
        out.push(Violation {
            rule_code: "floor.building_resolves_active".into(),
            severity, entity_type: "Floor".into(), entity_id: Some(id),
            message: format!(
                "Floor '{code}' references a building with status '{status}'."),
        });
    }
    Ok(())
}

/// `room.floor_resolves_active` — room's floor must be live.
async fn run_room_floor_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT r.id, r.room_code, COALESCE(f.status::text, '(missing)')
           FROM net.room r
           LEFT JOIN net.floor f ON f.id = r.floor_id
          WHERE r.organization_id = $1
            AND r.deleted_at IS NULL
            AND (f.id IS NULL
                 OR f.deleted_at IS NOT NULL
                 OR f.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, status) in rows {
        out.push(Violation {
            rule_code: "room.floor_resolves_active".into(),
            severity, entity_type: "Room".into(), entity_id: Some(id),
            message: format!(
                "Room '{code}' references a floor with status '{status}'."),
        });
    }
    Ok(())
}

/// `rack.room_resolves_active` — rack's room must be live.
async fn run_rack_room_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT rk.id, rk.rack_code, COALESCE(rm.status::text, '(missing)')
           FROM net.rack rk
           LEFT JOIN net.room rm ON rm.id = rk.room_id
          WHERE rk.organization_id = $1
            AND rk.deleted_at IS NULL
            AND (rm.id IS NULL
                 OR rm.deleted_at IS NOT NULL
                 OR rm.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, status) in rows {
        out.push(Violation {
            rule_code: "rack.room_resolves_active".into(),
            severity, entity_type: "Rack".into(), entity_id: Some(id),
            message: format!(
                "Rack '{code}' references a room with status '{status}'."),
        });
    }
    Ok(())
}

/// `port.device_resolves_active` — port's device must be live.
async fn run_port_device_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT p.id, p.interface_name, COALESCE(d.status::text, '(missing)')
           FROM net.port p
           LEFT JOIN net.device d ON d.id = p.device_id
          WHERE p.organization_id = $1
            AND p.deleted_at IS NULL
            AND (d.id IS NULL
                 OR d.deleted_at IS NOT NULL
                 OR d.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, iface, status) in rows {
        out.push(Violation {
            rule_code: "port.device_resolves_active".into(),
            severity, entity_type: "Port".into(), entity_id: Some(id),
            message: format!(
                "Port '{iface}' references a device with status '{status}'."),
        });
    }
    Ok(())
}

/// `module.device_resolves_active` — module's device must be live.
async fn run_module_device_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT m.id, m.slot, COALESCE(d.status::text, '(missing)')
           FROM net.module m
           LEFT JOIN net.device d ON d.id = m.device_id
          WHERE m.organization_id = $1
            AND m.deleted_at IS NULL
            AND (d.id IS NULL
                 OR d.deleted_at IS NOT NULL
                 OR d.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, slot, status) in rows {
        out.push(Violation {
            rule_code: "module.device_resolves_active".into(),
            severity, entity_type: "Module".into(), entity_id: Some(id),
            message: format!(
                "Module in slot '{slot}' references a device with status '{status}'."),
        });
    }
    Ok(())
}

/// `loopback.device_resolves_active` — loopback's device must be live.
async fn run_loopback_device_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT l.id, l.loopback_number, COALESCE(d.status::text, '(missing)')
           FROM net.loopback l
           LEFT JOIN net.device d ON d.id = l.device_id
          WHERE l.organization_id = $1
            AND l.deleted_at IS NULL
            AND (d.id IS NULL
                 OR d.deleted_at IS NOT NULL
                 OR d.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, num, status) in rows {
        out.push(Violation {
            rule_code: "loopback.device_resolves_active".into(),
            severity, entity_type: "Loopback".into(), entity_id: Some(id),
            message: format!(
                "Loopback lo{num} references a device with status '{status}'."),
        });
    }
    Ok(())
}

/// `aggregate_ethernet.device_resolves_active` — AE bundle's device must be live.
async fn run_ae_device_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT a.id, a.ae_name, COALESCE(d.status::text, '(missing)')
           FROM net.aggregate_ethernet a
           LEFT JOIN net.device d ON d.id = a.device_id
          WHERE a.organization_id = $1
            AND a.deleted_at IS NULL
            AND (d.id IS NULL
                 OR d.deleted_at IS NOT NULL
                 OR d.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name, status) in rows {
        out.push(Violation {
            rule_code: "aggregate_ethernet.device_resolves_active".into(),
            severity, entity_type: "AggregateEthernet".into(), entity_id: Some(id),
            message: format!(
                "Aggregate-ethernet '{name}' references a device with status '{status}'."),
        });
    }
    Ok(())
}

/// `server_nic.server_resolves_active` — NIC's server must be live.
async fn run_server_nic_server_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT n.id, n.nic_index, COALESCE(s.status::text, '(missing)')
           FROM net.server_nic n
           LEFT JOIN net.server s ON s.id = n.server_id
          WHERE n.organization_id = $1
            AND n.deleted_at IS NULL
            AND (s.id IS NULL
                 OR s.deleted_at IS NOT NULL
                 OR s.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, idx, status) in rows {
        out.push(Violation {
            rule_code: "server_nic.server_resolves_active".into(),
            severity, entity_type: "ServerNic".into(), entity_id: Some(id),
            message: format!(
                "NIC {idx} references a server with status '{status}'."),
        });
    }
    Ok(())
}

/// `link_endpoint.link_resolves_active` — endpoint's parent link must be live.
async fn run_link_endpoint_link_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT e.id, e.endpoint_order, COALESCE(l.status::text, '(missing)')
           FROM net.link_endpoint e
           LEFT JOIN net.link l ON l.id = e.link_id
          WHERE e.organization_id = $1
            AND e.deleted_at IS NULL
            AND (l.id IS NULL
                 OR l.deleted_at IS NOT NULL
                 OR l.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, order, status) in rows {
        out.push(Violation {
            rule_code: "link_endpoint.link_resolves_active".into(),
            severity, entity_type: "LinkEndpoint".into(), entity_id: Some(id),
            message: format!(
                "Link endpoint #{order} references a link with status '{status}'."),
        });
    }
    Ok(())
}

/// `device.role_resolves_active` — device's role, when set, must be live.
/// Warning-severity: a device with a decommissioned role still renders
/// config, it just falls back to the catalog defaults.
async fn run_device_role_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT d.id, d.hostname, COALESCE(r.status::text, '(missing)')
           FROM net.device d
           LEFT JOIN net.device_role r ON r.id = d.device_role_id
          WHERE d.organization_id = $1
            AND d.deleted_at IS NULL
            AND d.device_role_id IS NOT NULL
            AND (r.id IS NULL
                 OR r.deleted_at IS NOT NULL
                 OR r.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, status) in rows {
        out.push(Violation {
            rule_code: "device.role_resolves_active".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!(
                "Device '{hostname}' references a device role with status '{status}'."),
        });
    }
    Ok(())
}

/// `vlan.block_resolves_active` — VLAN's parent vlan_block must be live.
async fn run_vlan_block_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT v.id, v.vlan_id, COALESCE(b.status::text, '(missing)')
           FROM net.vlan v
           LEFT JOIN net.vlan_block b ON b.id = v.block_id
          WHERE v.organization_id = $1
            AND v.deleted_at IS NULL
            AND (b.id IS NULL
                 OR b.deleted_at IS NOT NULL
                 OR b.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, vlan_id, status) in rows {
        out.push(Violation {
            rule_code: "vlan.block_resolves_active".into(),
            severity, entity_type: "Vlan".into(), entity_id: Some(id),
            message: format!(
                "VLAN {vlan_id} references a vlan_block with status '{status}'."),
        });
    }
    Ok(())
}

/// `subnet.pool_resolves_active` — subnet's parent ip_pool must be live.
async fn run_subnet_pool_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT sn.id, sn.subnet_code, COALESCE(p.status::text, '(missing)')
           FROM net.subnet sn
           LEFT JOIN net.ip_pool p ON p.id = sn.pool_id
          WHERE sn.organization_id = $1
            AND sn.deleted_at IS NULL
            AND (p.id IS NULL
                 OR p.deleted_at IS NOT NULL
                 OR p.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, status) in rows {
        out.push(Violation {
            rule_code: "subnet.pool_resolves_active".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Subnet '{code}' references an ip_pool with status '{status}'."),
        });
    }
    Ok(())
}

/// `mlag_domain.pool_resolves_active` — MLAG domain's parent pool must be live.
async fn run_mlag_domain_pool_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT m.id, m.domain_id, COALESCE(p.status::text, '(missing)')
           FROM net.mlag_domain m
           LEFT JOIN net.mlag_domain_pool p ON p.id = m.pool_id
          WHERE m.organization_id = $1
            AND m.deleted_at IS NULL
            AND (p.id IS NULL
                 OR p.deleted_at IS NOT NULL
                 OR p.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, domain_id, status) in rows {
        out.push(Violation {
            rule_code: "mlag_domain.pool_resolves_active".into(),
            severity, entity_type: "MlagDomain".into(), entity_id: Some(id),
            message: format!(
                "MLAG domain {domain_id} references a pool with status '{status}'."),
        });
    }
    Ok(())
}

/// `asn_block.pool_resolves_active` — ASN block's parent asn_pool must be live.
async fn run_asn_block_pool_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT b.id, b.block_code, COALESCE(p.status::text, '(missing)')
           FROM net.asn_block b
           LEFT JOIN net.asn_pool p ON p.id = b.pool_id
          WHERE b.organization_id = $1
            AND b.deleted_at IS NULL
            AND (p.id IS NULL
                 OR p.deleted_at IS NOT NULL
                 OR p.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, status) in rows {
        out.push(Violation {
            rule_code: "asn_block.pool_resolves_active".into(),
            severity, entity_type: "AsnBlock".into(), entity_id: Some(id),
            message: format!(
                "ASN block '{code}' references an asn_pool with status '{status}'."),
        });
    }
    Ok(())
}

/// `vlan_block.pool_resolves_active` — VLAN block's parent vlan_pool must be live.
async fn run_vlan_block_pool_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT b.id, b.block_code, COALESCE(p.status::text, '(missing)')
           FROM net.vlan_block b
           LEFT JOIN net.vlan_pool p ON p.id = b.pool_id
          WHERE b.organization_id = $1
            AND b.deleted_at IS NULL
            AND (p.id IS NULL
                 OR p.deleted_at IS NOT NULL
                 OR p.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, status) in rows {
        out.push(Violation {
            rule_code: "vlan_block.pool_resolves_active".into(),
            severity, entity_type: "VlanBlock".into(), entity_id: Some(id),
            message: format!(
                "VLAN block '{code}' references a vlan_pool with status '{status}'."),
        });
    }
    Ok(())
}

/// `vlan.template_resolves_active_when_set` — VLAN's optional template_id,
/// when set, must resolve to an Active vlan_template. Warning severity
/// because config-gen falls back to the catalog default on a missing
/// template rather than failing outright.
async fn run_vlan_template_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT v.id, v.vlan_id, COALESCE(t.status::text, '(missing)')
           FROM net.vlan v
           LEFT JOIN net.vlan_template t ON t.id = v.template_id
          WHERE v.organization_id = $1
            AND v.deleted_at IS NULL
            AND v.template_id IS NOT NULL
            AND (t.id IS NULL
                 OR t.deleted_at IS NOT NULL
                 OR t.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, vlan_id, status) in rows {
        out.push(Violation {
            rule_code: "vlan.template_resolves_active_when_set".into(),
            severity, entity_type: "Vlan".into(), entity_id: Some(id),
            message: format!(
                "VLAN {vlan_id} references a vlan_template with status '{status}'."),
        });
    }
    Ok(())
}

/// `server_nic.vlan_resolves_active_when_set` — NIC's optional VLAN must be live.
async fn run_server_nic_vlan_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT n.id, n.nic_index, COALESCE(v.status::text, '(missing)')
           FROM net.server_nic n
           LEFT JOIN net.vlan v ON v.id = n.vlan_id
          WHERE n.organization_id = $1
            AND n.deleted_at IS NULL
            AND n.vlan_id IS NOT NULL
            AND (v.id IS NULL
                 OR v.deleted_at IS NOT NULL
                 OR v.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, idx, status) in rows {
        out.push(Violation {
            rule_code: "server_nic.vlan_resolves_active_when_set".into(),
            severity, entity_type: "ServerNic".into(), entity_id: Some(id),
            message: format!(
                "NIC {idx} references a VLAN with status '{status}'."),
        });
    }
    Ok(())
}

/// `server_nic.subnet_resolves_active_when_set` — NIC's optional subnet must be live.
async fn run_server_nic_subnet_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT n.id, n.nic_index, COALESCE(sn.status::text, '(missing)')
           FROM net.server_nic n
           LEFT JOIN net.subnet sn ON sn.id = n.subnet_id
          WHERE n.organization_id = $1
            AND n.deleted_at IS NULL
            AND n.subnet_id IS NOT NULL
            AND (sn.id IS NULL
                 OR sn.deleted_at IS NOT NULL
                 OR sn.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, idx, status) in rows {
        out.push(Violation {
            rule_code: "server_nic.subnet_resolves_active_when_set".into(),
            severity, entity_type: "ServerNic".into(), entity_id: Some(id),
            message: format!(
                "NIC {idx} references a subnet with status '{status}'."),
        });
    }
    Ok(())
}

/// `link_endpoint.vlan_resolves_active_when_set` — endpoint's optional VLAN must be live.
async fn run_link_endpoint_vlan_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT e.id, e.endpoint_order, COALESCE(v.status::text, '(missing)')
           FROM net.link_endpoint e
           LEFT JOIN net.vlan v ON v.id = e.vlan_id
          WHERE e.organization_id = $1
            AND e.deleted_at IS NULL
            AND e.vlan_id IS NOT NULL
            AND (v.id IS NULL
                 OR v.deleted_at IS NOT NULL
                 OR v.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, order, status) in rows {
        out.push(Violation {
            rule_code: "link_endpoint.vlan_resolves_active_when_set".into(),
            severity, entity_type: "LinkEndpoint".into(), entity_id: Some(id),
            message: format!(
                "Link endpoint #{order} references a VLAN with status '{status}'."),
        });
    }
    Ok(())
}

/// `vlan_template.default_unique_per_tenant` — at most one
/// is_default=true vlan_template per tenant.
async fn run_vlan_template_default_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // If more than one is_default row exists, every such row is a
    // violation (they're all ambiguous — pick-by-insertion-order
    // doesn't privilege any of them).
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, template_code
           FROM net.vlan_template
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND is_default = true
            AND (SELECT COUNT(*) FROM net.vlan_template
                  WHERE organization_id = $1
                    AND deleted_at IS NULL
                    AND is_default = true) > 1")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "vlan_template.default_unique_per_tenant".into(),
            severity, entity_type: "VlanTemplate".into(), entity_id: Some(id),
            message: format!(
                "VLAN template '{code}' is marked is_default but so is at \
                 least one other template in the same tenant — fall-back \
                 resolver becomes ambiguous."),
        });
    }
    Ok(())
}

/// `port.interface_name_starts_with_prefix` — port's interface_name
/// should begin with its interface_prefix + a '-'.
async fn run_port_name_prefix(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, interface_name, interface_prefix
           FROM net.port
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND interface_name NOT LIKE interface_prefix || '-%'")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name, prefix) in rows {
        out.push(Violation {
            rule_code: "port.interface_name_starts_with_prefix".into(),
            severity, entity_type: "Port".into(), entity_id: Some(id),
            message: format!(
                "Port interface_name '{name}' does not start with \
                 interface_prefix '{prefix}-' — filter/group-by by \
                 prefix will misclassify this port."),
        });
    }
    Ok(())
}

/// `device.hardware_model_set_when_active` — Active devices
/// should carry a hardware_model. Warning severity.
async fn run_device_model_set(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname
           FROM net.device
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
            AND (hardware_model IS NULL OR hardware_model = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "device.hardware_model_set_when_active".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!(
                "Active device '{hostname}' has no hardware_model — \
                 inventory + compatibility reports won't include it."),
        });
    }
    Ok(())
}

/// `device.room_resolves_active_when_set` — device's optional room must be live.
async fn run_device_room_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT d.id, d.hostname, COALESCE(r.status::text, '(missing)')
           FROM net.device d
           LEFT JOIN net.room r ON r.id = d.room_id
          WHERE d.organization_id = $1
            AND d.deleted_at IS NULL
            AND d.room_id IS NOT NULL
            AND (r.id IS NULL
                 OR r.deleted_at IS NOT NULL
                 OR r.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, status) in rows {
        out.push(Violation {
            rule_code: "device.room_resolves_active_when_set".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!(
                "Device '{hostname}' references a room with status '{status}'."),
        });
    }
    Ok(())
}

/// `device.rack_resolves_active_when_set` — device's optional rack must be live.
async fn run_device_rack_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT d.id, d.hostname, COALESCE(rk.status::text, '(missing)')
           FROM net.device d
           LEFT JOIN net.rack rk ON rk.id = d.rack_id
          WHERE d.organization_id = $1
            AND d.deleted_at IS NULL
            AND d.rack_id IS NOT NULL
            AND (rk.id IS NULL
                 OR rk.deleted_at IS NOT NULL
                 OR rk.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, status) in rows {
        out.push(Violation {
            rule_code: "device.rack_resolves_active_when_set".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!(
                "Device '{hostname}' references a rack with status '{status}'."),
        });
    }
    Ok(())
}

/// `subnet.parent_subnet_resolves_active_when_set` — subnet's
/// optional parent_subnet_id must resolve to an Active subnet.
async fn run_subnet_parent_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT c.id, c.subnet_code, COALESCE(p.status::text, '(missing)')
           FROM net.subnet c
           LEFT JOIN net.subnet p ON p.id = c.parent_subnet_id
          WHERE c.organization_id = $1
            AND c.deleted_at IS NULL
            AND c.parent_subnet_id IS NOT NULL
            AND (p.id IS NULL
                 OR p.deleted_at IS NOT NULL
                 OR p.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, status) in rows {
        out.push(Violation {
            rule_code: "subnet.parent_subnet_resolves_active_when_set".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Subnet '{code}' references a parent subnet with status '{status}'."),
        });
    }
    Ok(())
}

/// `device.building_resolves_active_when_set` — device's optional
/// building must be live. Companion to device.active_requires_building.
async fn run_device_building_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT d.id, d.hostname, COALESCE(b.status::text, '(missing)')
           FROM net.device d
           LEFT JOIN net.building b ON b.id = d.building_id
          WHERE d.organization_id = $1
            AND d.deleted_at IS NULL
            AND d.building_id IS NOT NULL
            AND (b.id IS NULL
                 OR b.deleted_at IS NOT NULL
                 OR b.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, status) in rows {
        out.push(Violation {
            rule_code: "device.building_resolves_active_when_set".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!(
                "Device '{hostname}' references a building with status '{status}'."),
        });
    }
    Ok(())
}

/// `server.room_resolves_active_when_set` — server's optional room must be live.
async fn run_server_room_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT s.id, s.hostname, COALESCE(r.status::text, '(missing)')
           FROM net.server s
           LEFT JOIN net.room r ON r.id = s.room_id
          WHERE s.organization_id = $1
            AND s.deleted_at IS NULL
            AND s.room_id IS NOT NULL
            AND (r.id IS NULL
                 OR r.deleted_at IS NOT NULL
                 OR r.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, status) in rows {
        out.push(Violation {
            rule_code: "server.room_resolves_active_when_set".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server '{hostname}' references a room with status '{status}'."),
        });
    }
    Ok(())
}

/// `server.rack_resolves_active_when_set` — server's optional rack must be live.
async fn run_server_rack_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT s.id, s.hostname, COALESCE(rk.status::text, '(missing)')
           FROM net.server s
           LEFT JOIN net.rack rk ON rk.id = s.rack_id
          WHERE s.organization_id = $1
            AND s.deleted_at IS NULL
            AND s.rack_id IS NOT NULL
            AND (rk.id IS NULL
                 OR rk.deleted_at IS NOT NULL
                 OR rk.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, status) in rows {
        out.push(Violation {
            rule_code: "server.rack_resolves_active_when_set".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server '{hostname}' references a rack with status '{status}'."),
        });
    }
    Ok(())
}

/// `link.link_type_resolves_active` — link's parent link_type must be live.
async fn run_link_type_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT l.id, l.link_code, COALESCE(t.status::text, '(missing)')
           FROM net.link l
           LEFT JOIN net.link_type t ON t.id = l.link_type_id
          WHERE l.organization_id = $1
            AND l.deleted_at IS NULL
            AND (t.id IS NULL
                 OR t.deleted_at IS NOT NULL
                 OR t.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, status) in rows {
        out.push(Violation {
            rule_code: "link.link_type_resolves_active".into(),
            severity, entity_type: "Link".into(), entity_id: Some(id),
            message: format!(
                "Link '{code}' references a link_type with status '{status}'."),
        });
    }
    Ok(())
}

/// `link.building_resolves_active_when_set` — link's optional building must be live.
async fn run_link_building_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT l.id, l.link_code, COALESCE(b.status::text, '(missing)')
           FROM net.link l
           LEFT JOIN net.building b ON b.id = l.building_id
          WHERE l.organization_id = $1
            AND l.deleted_at IS NULL
            AND l.building_id IS NOT NULL
            AND (b.id IS NULL
                 OR b.deleted_at IS NOT NULL
                 OR b.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, status) in rows {
        out.push(Violation {
            rule_code: "link.building_resolves_active_when_set".into(),
            severity, entity_type: "Link".into(), entity_id: Some(id),
            message: format!(
                "Link '{code}' references a building with status '{status}'."),
        });
    }
    Ok(())
}

/// `server.management_ip_set_when_active` — active server should have management_ip.
async fn run_server_management_ip(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname
           FROM net.server
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
            AND management_ip IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "server.management_ip_set_when_active".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Active server '{hostname}' has no management_ip — \
                 probe + SSH automation can't reach it."),
        });
    }
    Ok(())
}

/// `port.module_resolves_active_when_set` — port's optional module must be live.
async fn run_port_module_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT p.id, p.interface_name, COALESCE(m.status::text, '(missing)')
           FROM net.port p
           LEFT JOIN net.module m ON m.id = p.module_id
          WHERE p.organization_id = $1
            AND p.deleted_at IS NULL
            AND p.module_id IS NOT NULL
            AND (m.id IS NULL
                 OR m.deleted_at IS NOT NULL
                 OR m.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, iface, status) in rows {
        out.push(Violation {
            rule_code: "port.module_resolves_active_when_set".into(),
            severity, entity_type: "Port".into(), entity_id: Some(id),
            message: format!(
                "Port '{iface}' references a module with status '{status}'."),
        });
    }
    Ok(())
}

/// `port.breakout_parent_resolves_active_when_set` — parent port must be live.
async fn run_port_breakout_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT c.id, c.interface_name, COALESCE(p.status::text, '(missing)')
           FROM net.port c
           LEFT JOIN net.port p ON p.id = c.breakout_parent_id
          WHERE c.organization_id = $1
            AND c.deleted_at IS NULL
            AND c.breakout_parent_id IS NOT NULL
            AND (p.id IS NULL
                 OR p.deleted_at IS NOT NULL
                 OR p.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, iface, status) in rows {
        out.push(Violation {
            rule_code: "port.breakout_parent_resolves_active_when_set".into(),
            severity, entity_type: "Port".into(), entity_id: Some(id),
            message: format!(
                "Breakout child port '{iface}' references a parent port with status '{status}'."),
        });
    }
    Ok(())
}

/// `port.aggregate_ethernet_resolves_active_when_set` — AE bundle must be live.
async fn run_port_ae_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT p.id, p.interface_name, COALESCE(a.status::text, '(missing)')
           FROM net.port p
           LEFT JOIN net.aggregate_ethernet a ON a.id = p.aggregate_ethernet_id
          WHERE p.organization_id = $1
            AND p.deleted_at IS NULL
            AND p.aggregate_ethernet_id IS NOT NULL
            AND (a.id IS NULL
                 OR a.deleted_at IS NOT NULL
                 OR a.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, iface, status) in rows {
        out.push(Violation {
            rule_code: "port.aggregate_ethernet_resolves_active_when_set".into(),
            severity, entity_type: "Port".into(), entity_id: Some(id),
            message: format!(
                "Port '{iface}' references an aggregate-ethernet bundle with status '{status}'."),
        });
    }
    Ok(())
}

/// `link_endpoint.device_resolves_active_when_set` — endpoint's optional device must be live.
async fn run_link_endpoint_device_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT e.id, e.endpoint_order, COALESCE(d.status::text, '(missing)')
           FROM net.link_endpoint e
           LEFT JOIN net.device d ON d.id = e.device_id
          WHERE e.organization_id = $1
            AND e.deleted_at IS NULL
            AND e.device_id IS NOT NULL
            AND (d.id IS NULL
                 OR d.deleted_at IS NOT NULL
                 OR d.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, order, status) in rows {
        out.push(Violation {
            rule_code: "link_endpoint.device_resolves_active_when_set".into(),
            severity, entity_type: "LinkEndpoint".into(), entity_id: Some(id),
            message: format!(
                "Link endpoint #{order} references a device with status '{status}'."),
        });
    }
    Ok(())
}

/// `link_endpoint.ip_address_resolves_active_when_set` — endpoint's optional IP must be live.
async fn run_link_endpoint_ip_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT e.id, e.endpoint_order, COALESCE(ip.status::text, '(missing)')
           FROM net.link_endpoint e
           LEFT JOIN net.ip_address ip ON ip.id = e.ip_address_id
          WHERE e.organization_id = $1
            AND e.deleted_at IS NULL
            AND e.ip_address_id IS NOT NULL
            AND (ip.id IS NULL
                 OR ip.deleted_at IS NOT NULL
                 OR ip.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, order, status) in rows {
        out.push(Violation {
            rule_code: "link_endpoint.ip_address_resolves_active_when_set".into(),
            severity, entity_type: "LinkEndpoint".into(), entity_id: Some(id),
            message: format!(
                "Link endpoint #{order} references an IP address with status '{status}'."),
        });
    }
    Ok(())
}

/// `server_nic.ip_address_resolves_active_when_set` — NIC's optional IP must be live.
async fn run_server_nic_ip_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT n.id, n.nic_index, COALESCE(ip.status::text, '(missing)')
           FROM net.server_nic n
           LEFT JOIN net.ip_address ip ON ip.id = n.ip_address_id
          WHERE n.organization_id = $1
            AND n.deleted_at IS NULL
            AND n.ip_address_id IS NOT NULL
            AND (ip.id IS NULL
                 OR ip.deleted_at IS NOT NULL
                 OR ip.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, idx, status) in rows {
        out.push(Violation {
            rule_code: "server_nic.ip_address_resolves_active_when_set".into(),
            severity, entity_type: "ServerNic".into(), entity_id: Some(id),
            message: format!(
                "NIC {idx} references an IP address with status '{status}'."),
        });
    }
    Ok(())
}

/// `device.asn_allocation_resolves_active_when_set` — device's
/// optional ASN allocation must be live when set.
async fn run_device_asn_alloc_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT d.id, d.hostname, COALESCE(a.status::text, '(missing)')
           FROM net.device d
           LEFT JOIN net.asn_allocation a ON a.id = d.asn_allocation_id
          WHERE d.organization_id = $1
            AND d.deleted_at IS NULL
            AND d.asn_allocation_id IS NOT NULL
            AND (a.id IS NULL
                 OR a.deleted_at IS NOT NULL
                 OR a.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, status) in rows {
        out.push(Violation {
            rule_code: "device.asn_allocation_resolves_active_when_set".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!(
                "Device '{hostname}' references an ASN allocation with status '{status}'."),
        });
    }
    Ok(())
}

/// `server.asn_allocation_resolves_active_when_set` — server's
/// optional ASN allocation must be live when set.
async fn run_server_asn_alloc_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT s.id, s.hostname, COALESCE(a.status::text, '(missing)')
           FROM net.server s
           LEFT JOIN net.asn_allocation a ON a.id = s.asn_allocation_id
          WHERE s.organization_id = $1
            AND s.deleted_at IS NULL
            AND s.asn_allocation_id IS NOT NULL
            AND (a.id IS NULL
                 OR a.deleted_at IS NOT NULL
                 OR a.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, status) in rows {
        out.push(Violation {
            rule_code: "server.asn_allocation_resolves_active_when_set".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server '{hostname}' references an ASN allocation with status '{status}'."),
        });
    }
    Ok(())
}

/// `server.server_profile_resolves_active_when_set` — server's
/// optional server_profile must be live when set.
async fn run_server_profile_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT s.id, s.hostname, COALESCE(p.status::text, '(missing)')
           FROM net.server s
           LEFT JOIN net.server_profile p ON p.id = s.server_profile_id
          WHERE s.organization_id = $1
            AND s.deleted_at IS NULL
            AND s.server_profile_id IS NOT NULL
            AND (p.id IS NULL
                 OR p.deleted_at IS NOT NULL
                 OR p.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, status) in rows {
        out.push(Violation {
            rule_code: "server.server_profile_resolves_active_when_set".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server '{hostname}' references a server_profile with status '{status}'."),
        });
    }
    Ok(())
}

/// `server.loopback_ip_address_resolves_active_when_set` — server's
/// optional loopback IP must be live when set.
async fn run_server_loopback_ip_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT s.id, s.hostname, COALESCE(ip.status::text, '(missing)')
           FROM net.server s
           LEFT JOIN net.ip_address ip ON ip.id = s.loopback_ip_address_id
          WHERE s.organization_id = $1
            AND s.deleted_at IS NULL
            AND s.loopback_ip_address_id IS NOT NULL
            AND (ip.id IS NULL
                 OR ip.deleted_at IS NOT NULL
                 OR ip.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, status) in rows {
        out.push(Violation {
            rule_code: "server.loopback_ip_address_resolves_active_when_set".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server '{hostname}' references a loopback IP with status '{status}'."),
        });
    }
    Ok(())
}

/// `link_endpoint.port_resolves_active_when_set` — endpoint's optional port must be live.
async fn run_link_endpoint_port_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT e.id, e.endpoint_order, COALESCE(p.status::text, '(missing)')
           FROM net.link_endpoint e
           LEFT JOIN net.port p ON p.id = e.port_id
          WHERE e.organization_id = $1
            AND e.deleted_at IS NULL
            AND e.port_id IS NOT NULL
            AND (p.id IS NULL
                 OR p.deleted_at IS NOT NULL
                 OR p.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, order, status) in rows {
        out.push(Violation {
            rule_code: "link_endpoint.port_resolves_active_when_set".into(),
            severity, entity_type: "LinkEndpoint".into(), entity_id: Some(id),
            message: format!(
                "Link endpoint #{order} references a port with status '{status}'."),
        });
    }
    Ok(())
}

/// `server.building_resolves_active_when_set` — server's optional
/// building must be live when set. Sibling to server.active_has_building.
async fn run_server_building_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT s.id, s.hostname, COALESCE(b.status::text, '(missing)')
           FROM net.server s
           LEFT JOIN net.building b ON b.id = s.building_id
          WHERE s.organization_id = $1
            AND s.deleted_at IS NULL
            AND s.building_id IS NOT NULL
            AND (b.id IS NULL
                 OR b.deleted_at IS NOT NULL
                 OR b.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, status) in rows {
        out.push(Violation {
            rule_code: "server.building_resolves_active_when_set".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server '{hostname}' references a building with status '{status}'."),
        });
    }
    Ok(())
}

/// `server_nic.target_port_resolves_active_when_set` — NIC's optional target port must be live.
async fn run_server_nic_target_port_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT n.id, n.nic_index, COALESCE(p.status::text, '(missing)')
           FROM net.server_nic n
           LEFT JOIN net.port p ON p.id = n.target_port_id
          WHERE n.organization_id = $1
            AND n.deleted_at IS NULL
            AND n.target_port_id IS NOT NULL
            AND (p.id IS NULL
                 OR p.deleted_at IS NOT NULL
                 OR p.status != 'Active'::net.entity_status)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, idx, status) in rows {
        out.push(Violation {
            rule_code: "server_nic.target_port_resolves_active_when_set".into(),
            severity, entity_type: "ServerNic".into(), entity_id: Some(id),
            message: format!(
                "NIC {idx} references a target port with status '{status}'."),
        });
    }
    Ok(())
}

/// `rack.uheight_within_reason` — flag racks with u_height > 60.
async fn run_rack_uheight_reason(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i32)> = sqlx::query_as(
        "SELECT id, rack_code, u_height
           FROM net.rack
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND u_height > 60")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, u_height) in rows {
        out.push(Violation {
            rule_code: "rack.uheight_within_reason".into(),
            severity, entity_type: "Rack".into(), entity_id: Some(id),
            message: format!(
                "Rack '{code}' has u_height={u_height} — likely a data-entry typo."),
        });
    }
    Ok(())
}

/// `device.firmware_version_set_when_active` — Active device
/// should record its firmware version for upgrade + CVE tracking.
async fn run_device_firmware_set(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname
           FROM net.device
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
            AND (firmware_version IS NULL OR firmware_version = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "device.firmware_version_set_when_active".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!(
                "Active device '{hostname}' has no firmware_version — \
                 upgrade + CVE-scan reports miss it."),
        });
    }
    Ok(())
}

/// `device.serial_number_unique_per_tenant_when_set` — flag any
/// device with a serial_number that collides with another device
/// in the same tenant. Every colliding row emits a violation so
/// UI filters surface the whole collision group.
async fn run_device_serial_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT d.id, d.hostname, d.serial_number
           FROM net.device d
          WHERE d.organization_id = $1
            AND d.deleted_at IS NULL
            AND d.serial_number IS NOT NULL
            AND d.serial_number <> ''
            AND EXISTS (
                SELECT 1 FROM net.device d2
                 WHERE d2.organization_id = d.organization_id
                   AND d2.deleted_at IS NULL
                   AND d2.serial_number = d.serial_number
                   AND d2.id <> d.id)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, serial) in rows {
        out.push(Violation {
            rule_code: "device.serial_number_unique_per_tenant_when_set".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!(
                "Device '{hostname}' has serial_number '{serial}' which is not unique in the tenant."),
        });
    }
    Ok(())
}

/// `server.serial_number_unique_per_tenant_when_set` — mirror on server branch.
async fn run_server_serial_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT s.id, s.hostname, s.serial_number
           FROM net.server s
          WHERE s.organization_id = $1
            AND s.deleted_at IS NULL
            AND s.serial_number IS NOT NULL
            AND s.serial_number <> ''
            AND EXISTS (
                SELECT 1 FROM net.server s2
                 WHERE s2.organization_id = s.organization_id
                   AND s2.deleted_at IS NULL
                   AND s2.serial_number = s.serial_number
                   AND s2.id <> s.id)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, serial) in rows {
        out.push(Violation {
            rule_code: "server.serial_number_unique_per_tenant_when_set".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server '{hostname}' has serial_number '{serial}' which is not unique in the tenant."),
        });
    }
    Ok(())
}

/// `module.serial_number_unique_per_tenant_when_set` — mirror on module branch.
async fn run_module_serial_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT m.id, m.slot, m.serial_number
           FROM net.module m
          WHERE m.organization_id = $1
            AND m.deleted_at IS NULL
            AND m.serial_number IS NOT NULL
            AND m.serial_number <> ''
            AND EXISTS (
                SELECT 1 FROM net.module m2
                 WHERE m2.organization_id = m.organization_id
                   AND m2.deleted_at IS NULL
                   AND m2.serial_number = m.serial_number
                   AND m2.id <> m.id)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, slot, serial) in rows {
        out.push(Violation {
            rule_code: "module.serial_number_unique_per_tenant_when_set".into(),
            severity, entity_type: "Module".into(), entity_id: Some(id),
            message: format!(
                "Module in slot '{slot}' has serial_number '{serial}' which is not unique in the tenant."),
        });
    }
    Ok(())
}

/// `device.last_ping_ok_when_active` — Active device should have
/// answered at least one probe (last_ping_ok = true).
async fn run_device_ping_ok(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname
           FROM net.device
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
            AND (last_ping_ok IS NULL OR last_ping_ok = false)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "device.last_ping_ok_when_active".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!(
                "Active device '{hostname}' has not answered a probe (last_ping_ok != true)."),
        });
    }
    Ok(())
}

/// `server.last_ping_ok_when_active` — mirror on server branch.
async fn run_server_ping_ok(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname
           FROM net.server
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
            AND (last_ping_ok IS NULL OR last_ping_ok = false)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "server.last_ping_ok_when_active".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Active server '{hostname}' has not answered a probe (last_ping_ok != true)."),
        });
    }
    Ok(())
}

/// `link.description_set_when_active` — Active links should have
/// a description for audit readability.
async fn run_link_description_set(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, link_code
           FROM net.link
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
            AND (description IS NULL OR description = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id, link_code) in rows {
        out.push(Violation {
            rule_code: "link.description_set_when_active".into(),
            severity, entity_type: "Link".into(), entity_id: Some(id),
            message: format!(
                "Active link '{link_code}' has no description — audit \
                 readers can't tell the link's purpose at a glance."),
        });
    }
    Ok(())
}

/// `device.management_ip_unique_per_tenant_when_set` — flag
/// devices sharing a management_ip. Every colliding row emits.
async fn run_device_mgmt_ip_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT d.id, d.hostname, host(d.management_ip)
           FROM net.device d
          WHERE d.organization_id = $1
            AND d.deleted_at IS NULL
            AND d.management_ip IS NOT NULL
            AND EXISTS (
                SELECT 1 FROM net.device d2
                 WHERE d2.organization_id = d.organization_id
                   AND d2.deleted_at IS NULL
                   AND d2.management_ip = d.management_ip
                   AND d2.id <> d.id)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, ip) in rows {
        out.push(Violation {
            rule_code: "device.management_ip_unique_per_tenant_when_set".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!(
                "Device '{hostname}' management_ip {ip} collides with another device in the tenant."),
        });
    }
    Ok(())
}

/// `server.management_ip_unique_per_tenant_when_set` — mirror on server.
async fn run_server_mgmt_ip_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT s.id, s.hostname, host(s.management_ip)
           FROM net.server s
          WHERE s.organization_id = $1
            AND s.deleted_at IS NULL
            AND s.management_ip IS NOT NULL
            AND EXISTS (
                SELECT 1 FROM net.server s2
                 WHERE s2.organization_id = s.organization_id
                   AND s2.deleted_at IS NULL
                   AND s2.management_ip = s.management_ip
                   AND s2.id <> s.id)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, ip) in rows {
        out.push(Violation {
            rule_code: "server.management_ip_unique_per_tenant_when_set".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server '{hostname}' management_ip {ip} collides with another server in the tenant."),
        });
    }
    Ok(())
}

/// `port.admin_up_false_on_active_status` — Active port should
/// have admin_up=true.
async fn run_port_admin_up_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, interface_name
           FROM net.port
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
            AND admin_up = false")
        .bind(org_id).fetch_all(pool).await?;
    for (id, iface) in rows {
        out.push(Violation {
            rule_code: "port.admin_up_false_on_active_status".into(),
            severity, entity_type: "Port".into(), entity_id: Some(id),
            message: format!(
                "Port '{iface}' is Active but admin_up=false — planned downtime or stale import."),
        });
    }
    Ok(())
}

/// `server_nic.admin_up_false_on_active_status` — Active NIC with admin_up=false.
async fn run_nic_admin_up_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32)> = sqlx::query_as(
        "SELECT id, nic_index
           FROM net.server_nic
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
            AND admin_up = false")
        .bind(org_id).fetch_all(pool).await?;
    for (id, idx) in rows {
        out.push(Violation {
            rule_code: "server_nic.admin_up_false_on_active_status".into(),
            severity, entity_type: "ServerNic".into(), entity_id: Some(id),
            message: format!(
                "NIC {idx} is Active but admin_up=false — planned downtime or stale import."),
        });
    }
    Ok(())
}

/// `port.speed_mbps_reasonable_when_set` — flag speed_mbps
/// outside 100..=400000 Mbps when set.
async fn run_port_speed_reasonable(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i64)> = sqlx::query_as(
        "SELECT id, interface_name, speed_mbps
           FROM net.port
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND speed_mbps IS NOT NULL
            AND (speed_mbps < 100 OR speed_mbps > 400000)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, iface, speed) in rows {
        out.push(Violation {
            rule_code: "port.speed_mbps_reasonable_when_set".into(),
            severity, entity_type: "Port".into(), entity_id: Some(id),
            message: format!(
                "Port '{iface}' speed_mbps={speed} is outside the realistic 100-400000 Mbps range."),
        });
    }
    Ok(())
}

/// `rack.max_devices_positive_when_set` — flag max_devices <= 0.
async fn run_rack_max_devices_positive(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i32)> = sqlx::query_as(
        "SELECT id, rack_code, max_devices
           FROM net.rack
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND max_devices IS NOT NULL
            AND max_devices <= 0")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, max_devices) in rows {
        out.push(Violation {
            rule_code: "rack.max_devices_positive_when_set".into(),
            severity, entity_type: "Rack".into(), entity_id: Some(id),
            message: format!(
                "Rack '{code}' has max_devices={max_devices} — must be > 0 to plan devices into it."),
        });
    }
    Ok(())
}

/// `port.native_vlan_requires_access_or_trunk` — flag ports with
/// native_vlan_id set but port_mode='routed'|'unset'.
async fn run_port_native_vlan_mode(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, interface_name, port_mode
           FROM net.port
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND native_vlan_id IS NOT NULL
            AND port_mode NOT IN ('access', 'trunk')")
        .bind(org_id).fetch_all(pool).await?;
    for (id, iface, mode) in rows {
        out.push(Violation {
            rule_code: "port.native_vlan_requires_access_or_trunk".into(),
            severity, entity_type: "Port".into(), entity_id: Some(id),
            message: format!(
                "Port '{iface}' has native_vlan_id set but port_mode='{mode}' — VLAN tag won't take effect."),
        });
    }
    Ok(())
}

/// `aggregate_ethernet.member_count_meets_min_links` — AE bundle
/// whose current member count < min_links.
async fn run_ae_member_count(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i32, i64)> = sqlx::query_as(
        "SELECT a.id, a.ae_name, a.min_links,
                (SELECT COUNT(*)::bigint FROM net.port p
                  WHERE p.aggregate_ethernet_id = a.id
                    AND p.deleted_at IS NULL) AS member_count
           FROM net.aggregate_ethernet a
          WHERE a.organization_id = $1
            AND a.deleted_at IS NULL
            AND (SELECT COUNT(*) FROM net.port p
                  WHERE p.aggregate_ethernet_id = a.id
                    AND p.deleted_at IS NULL) < a.min_links")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name, min_links, members) in rows {
        out.push(Violation {
            rule_code: "aggregate_ethernet.member_count_meets_min_links".into(),
            severity, entity_type: "AggregateEthernet".into(), entity_id: Some(id),
            message: format!(
                "AE '{name}' has {members} member port(s) but min_links={min_links} — bundle won't come up."),
        });
    }
    Ok(())
}

/// `change_set_item.expected_version_set_for_update` — flag
/// Update items that don't carry expected_version.
async fn run_change_set_item_expected_version(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, String)> = sqlx::query_as(
        "SELECT item.id, item.item_order, item.entity_type
           FROM net.change_set_item item
          WHERE item.organization_id = $1
            AND item.deleted_at IS NULL
            AND item.action = 'Update'::net.change_set_action
            AND item.expected_version IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, order, entity_type) in rows {
        out.push(Violation {
            rule_code: "change_set_item.expected_version_set_for_update".into(),
            severity, entity_type: "ChangeSetItem".into(), entity_id: Some(id),
            message: format!(
                "Change-set item #{order} on {entity_type} is an Update but has no expected_version — \
                 stale-write guard is disabled."),
        });
    }
    Ok(())
}

/// `port.speed_mbps_set_when_active` — Active port should have
/// speed_mbps populated for capacity planning.
async fn run_port_speed_set(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, interface_name
           FROM net.port
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
            AND speed_mbps IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, iface) in rows {
        out.push(Violation {
            rule_code: "port.speed_mbps_set_when_active".into(),
            severity, entity_type: "Port".into(), entity_id: Some(id),
            message: format!(
                "Active port '{iface}' has no speed_mbps — capacity-planning reports miss it."),
        });
    }
    Ok(())
}

/// `server_nic.nic_index_in_range` — flag NICs with index > 63.
async fn run_nic_index_range(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32)> = sqlx::query_as(
        "SELECT id, nic_index
           FROM net.server_nic
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (nic_index < 0 OR nic_index > 63)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, idx) in rows {
        out.push(Violation {
            rule_code: "server_nic.nic_index_in_range".into(),
            severity, entity_type: "ServerNic".into(), entity_id: Some(id),
            message: format!(
                "NIC nic_index={idx} is outside the realistic 0..=63 range."),
        });
    }
    Ok(())
}

/// `device.hostname_no_leading_trailing_whitespace` — trim-equal check.
async fn run_device_hostname_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname
           FROM net.device
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (hostname <> btrim(hostname))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "device.hostname_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!(
                "Device hostname '{hostname}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `server.hostname_no_leading_trailing_whitespace` — mirror on server.
async fn run_server_hostname_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname
           FROM net.server
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (hostname <> btrim(hostname))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "server.hostname_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server hostname '{hostname}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `port.description_set_when_active` — Active ports should have a description.
async fn run_port_description_set(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, interface_name
           FROM net.port
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
            AND (description IS NULL OR description = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id, iface) in rows {
        out.push(Violation {
            rule_code: "port.description_set_when_active".into(),
            severity, entity_type: "Port".into(), entity_id: Some(id),
            message: format!(
                "Active port '{iface}' has no description — link-trace reports lose the remote-end hint."),
        });
    }
    Ok(())
}

/// `link.link_code_no_leading_trailing_whitespace` — trim-equal.
async fn run_link_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, link_code
           FROM net.link
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (link_code <> btrim(link_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "link.link_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Link".into(), entity_id: Some(id),
            message: format!(
                "Link link_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `ip_address.gateway_unique_per_subnet` — one Gateway per subnet.
/// Every colliding row emits so UI filters show the whole group.
async fn run_ip_gateway_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT ip.id, host(ip.address)
           FROM net.ip_address ip
          WHERE ip.organization_id = $1
            AND ip.deleted_at IS NULL
            AND ip.assigned_to_type = 'Gateway'
            AND EXISTS (
                SELECT 1 FROM net.ip_address ip2
                 WHERE ip2.organization_id = ip.organization_id
                   AND ip2.deleted_at IS NULL
                   AND ip2.assigned_to_type = 'Gateway'
                   AND ip2.subnet_id = ip.subnet_id
                   AND ip2.id <> ip.id)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, addr) in rows {
        out.push(Violation {
            rule_code: "ip_address.gateway_unique_per_subnet".into(),
            severity, entity_type: "IpAddress".into(), entity_id: Some(id),
            message: format!(
                "Gateway IP {addr} shares its subnet with another Gateway — non-deterministic pick."),
        });
    }
    Ok(())
}

/// `subnet.display_name_no_leading_trailing_whitespace` — trim-equal.
async fn run_subnet_name_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, display_name
           FROM net.subnet
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (display_name <> btrim(display_name))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name) in rows {
        out.push(Violation {
            rule_code: "subnet.display_name_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Subnet display_name '{name}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `vlan.display_name_no_leading_trailing_whitespace` — trim-equal.
async fn run_vlan_name_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, display_name
           FROM net.vlan
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (display_name <> btrim(display_name))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name) in rows {
        out.push(Violation {
            rule_code: "vlan.display_name_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Vlan".into(), entity_id: Some(id),
            message: format!(
                "VLAN display_name '{name}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `server.display_name_no_leading_trailing_whitespace` — trim on optional display_name.
async fn run_server_name_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, display_name
           FROM net.server
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND display_name IS NOT NULL
            AND (display_name <> btrim(display_name))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name) in rows {
        out.push(Violation {
            rule_code: "server.display_name_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server display_name '{name}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `link.display_name_no_leading_trailing_whitespace` — trim on optional display_name.
async fn run_link_name_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, display_name
           FROM net.link
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND display_name IS NOT NULL
            AND (display_name <> btrim(display_name))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name) in rows {
        out.push(Violation {
            rule_code: "link.display_name_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Link".into(), entity_id: Some(id),
            message: format!(
                "Link display_name '{name}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `ip_pool.display_name_no_leading_trailing_whitespace` — trim on pool display_name.
async fn run_ip_pool_name_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, display_name
           FROM net.ip_pool
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (display_name <> btrim(display_name))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name) in rows {
        out.push(Violation {
            rule_code: "ip_pool.display_name_no_leading_trailing_whitespace".into(),
            severity, entity_type: "IpPool".into(), entity_id: Some(id),
            message: format!(
                "IP pool display_name '{name}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `port.breakout_parent_not_self_loop` — flag self-referencing ports.
async fn run_port_breakout_self_loop(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, interface_name
           FROM net.port
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND breakout_parent_id = id")
        .bind(org_id).fetch_all(pool).await?;
    for (id, iface) in rows {
        out.push(Violation {
            rule_code: "port.breakout_parent_not_self_loop".into(),
            severity, entity_type: "Port".into(), entity_id: Some(id),
            message: format!(
                "Port '{iface}' breakout_parent_id references itself — self-loop."),
        });
    }
    Ok(())
}

/// `subnet.parent_subnet_not_self_loop` — flag self-referencing subnets.
async fn run_subnet_parent_self_loop(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, subnet_code
           FROM net.subnet
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND parent_subnet_id = id")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "subnet.parent_subnet_not_self_loop".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Subnet '{code}' parent_subnet_id references itself — self-loop."),
        });
    }
    Ok(())
}

/// `device.rack_implies_room` — rack_id set without room_id.
async fn run_device_rack_implies_room(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname
           FROM net.device
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND rack_id IS NOT NULL
            AND room_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "device.rack_implies_room".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!(
                "Device '{hostname}' has rack_id but no room_id — hierarchy drill breaks at the room level."),
        });
    }
    Ok(())
}

/// `server.rack_implies_room` — mirror on server branch.
async fn run_server_rack_implies_room(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname
           FROM net.server
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND rack_id IS NOT NULL
            AND room_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "server.rack_implies_room".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server '{hostname}' has rack_id but no room_id — hierarchy drill breaks at the room level."),
        });
    }
    Ok(())
}

/// `mlag_domain.display_name_no_leading_trailing_whitespace` — trim-equal.
async fn run_mlag_domain_name_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, display_name
           FROM net.mlag_domain
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (display_name <> btrim(display_name))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name) in rows {
        out.push(Violation {
            rule_code: "mlag_domain.display_name_no_leading_trailing_whitespace".into(),
            severity, entity_type: "MlagDomain".into(), entity_id: Some(id),
            message: format!(
                "MLAG domain display_name '{name}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `rack.rack_code_no_leading_trailing_whitespace` — trim-equal on rack_code.
async fn run_rack_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, rack_code
           FROM net.rack
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (rack_code <> btrim(rack_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "rack.rack_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Rack".into(), entity_id: Some(id),
            message: format!(
                "Rack rack_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `room.room_code_no_leading_trailing_whitespace` — trim-equal on room_code.
async fn run_room_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, room_code
           FROM net.room
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (room_code <> btrim(room_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "room.room_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Room".into(), entity_id: Some(id),
            message: format!(
                "Room room_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `floor.floor_code_no_leading_trailing_whitespace` — trim-equal on floor_code.
async fn run_floor_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, floor_code
           FROM net.floor
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (floor_code <> btrim(floor_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "floor.floor_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Floor".into(), entity_id: Some(id),
            message: format!(
                "Floor floor_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `building.building_code_no_leading_trailing_whitespace` — trim-equal on building_code.
async fn run_building_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, building_code
           FROM net.building
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (building_code <> btrim(building_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "building.building_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Building".into(), entity_id: Some(id),
            message: format!(
                "Building building_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `site.site_code_no_leading_trailing_whitespace` — trim-equal on site_code.
async fn run_site_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, site_code
           FROM net.site
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (site_code <> btrim(site_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "site.site_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Site".into(), entity_id: Some(id),
            message: format!(
                "Site site_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `region.region_code_no_leading_trailing_whitespace` — trim-equal on region_code.
async fn run_region_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, region_code
           FROM net.region
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (region_code <> btrim(region_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "region.region_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Region".into(), entity_id: Some(id),
            message: format!(
                "Region region_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `device.device_code_no_leading_trailing_whitespace` — optional device_code.
async fn run_device_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, device_code
           FROM net.device
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND device_code IS NOT NULL
            AND (device_code <> btrim(device_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "device.device_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!(
                "Device device_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `asn_pool.display_name_no_leading_trailing_whitespace` — trim-equal.
async fn run_asn_pool_name_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, display_name
           FROM net.asn_pool
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (display_name <> btrim(display_name))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name) in rows {
        out.push(Violation {
            rule_code: "asn_pool.display_name_no_leading_trailing_whitespace".into(),
            severity, entity_type: "AsnPool".into(), entity_id: Some(id),
            message: format!(
                "ASN pool display_name '{name}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `vlan_pool.display_name_no_leading_trailing_whitespace` — trim-equal.
async fn run_vlan_pool_name_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, display_name
           FROM net.vlan_pool
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (display_name <> btrim(display_name))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name) in rows {
        out.push(Violation {
            rule_code: "vlan_pool.display_name_no_leading_trailing_whitespace".into(),
            severity, entity_type: "VlanPool".into(), entity_id: Some(id),
            message: format!(
                "VLAN pool display_name '{name}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `mlag_domain_pool.display_name_no_leading_trailing_whitespace` — trim-equal.
async fn run_mlag_pool_name_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, display_name
           FROM net.mlag_domain_pool
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (display_name <> btrim(display_name))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name) in rows {
        out.push(Violation {
            rule_code: "mlag_domain_pool.display_name_no_leading_trailing_whitespace".into(),
            severity, entity_type: "MlagDomainPool".into(), entity_id: Some(id),
            message: format!(
                "MLAG domain pool display_name '{name}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `asn_pool.pool_code_no_leading_trailing_whitespace` — trim-equal.
async fn run_asn_pool_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, pool_code
           FROM net.asn_pool
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (pool_code <> btrim(pool_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "asn_pool.pool_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "AsnPool".into(), entity_id: Some(id),
            message: format!(
                "ASN pool pool_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `vlan_pool.pool_code_no_leading_trailing_whitespace` — trim-equal.
async fn run_vlan_pool_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, pool_code
           FROM net.vlan_pool
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (pool_code <> btrim(pool_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "vlan_pool.pool_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "VlanPool".into(), entity_id: Some(id),
            message: format!(
                "VLAN pool pool_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `ip_pool.pool_code_no_leading_trailing_whitespace` — trim-equal.
async fn run_ip_pool_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, pool_code
           FROM net.ip_pool
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (pool_code <> btrim(pool_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "ip_pool.pool_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "IpPool".into(), entity_id: Some(id),
            message: format!(
                "IP pool pool_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `device_role.role_code_no_leading_trailing_whitespace` — trim-equal.
async fn run_device_role_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, role_code
           FROM net.device_role
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (role_code <> btrim(role_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "device_role.role_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "DeviceRole".into(), entity_id: Some(id),
            message: format!(
                "Device role role_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `link_type.type_code_no_leading_trailing_whitespace` — trim-equal.
async fn run_link_type_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, type_code
           FROM net.link_type
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (type_code <> btrim(type_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "link_type.type_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "LinkType".into(), entity_id: Some(id),
            message: format!(
                "Link type type_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `server_profile.profile_code_no_leading_trailing_whitespace` — trim-equal.
async fn run_server_profile_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, profile_code
           FROM net.server_profile
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (profile_code <> btrim(profile_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "server_profile.profile_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "ServerProfile".into(), entity_id: Some(id),
            message: format!(
                "Server profile profile_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `asn_block.block_code_no_leading_trailing_whitespace` — trim-equal.
async fn run_asn_block_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, block_code
           FROM net.asn_block
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (block_code <> btrim(block_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "asn_block.block_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "AsnBlock".into(), entity_id: Some(id),
            message: format!(
                "ASN block block_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `vlan_block.block_code_no_leading_trailing_whitespace` — trim-equal.
async fn run_vlan_block_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, block_code
           FROM net.vlan_block
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (block_code <> btrim(block_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "vlan_block.block_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "VlanBlock".into(), entity_id: Some(id),
            message: format!(
                "VLAN block block_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `reservation_shelf.resource_key_no_leading_trailing_whitespace` — trim-equal.
async fn run_shelf_key_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, resource_key
           FROM net.reservation_shelf
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (resource_key <> btrim(resource_key))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, key) in rows {
        out.push(Violation {
            rule_code: "reservation_shelf.resource_key_no_leading_trailing_whitespace".into(),
            severity, entity_type: "ReservationShelf".into(), entity_id: Some(id),
            message: format!(
                "Shelf resource_key '{key}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `device_role.display_name_no_leading_trailing_whitespace` — trim-equal.
async fn run_device_role_name_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, display_name
           FROM net.device_role
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (display_name <> btrim(display_name))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name) in rows {
        out.push(Violation {
            rule_code: "device_role.display_name_no_leading_trailing_whitespace".into(),
            severity, entity_type: "DeviceRole".into(), entity_id: Some(id),
            message: format!(
                "Device role display_name '{name}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `link_type.display_name_no_leading_trailing_whitespace` — trim-equal.
async fn run_link_type_name_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, display_name
           FROM net.link_type
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (display_name <> btrim(display_name))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name) in rows {
        out.push(Violation {
            rule_code: "link_type.display_name_no_leading_trailing_whitespace".into(),
            severity, entity_type: "LinkType".into(), entity_id: Some(id),
            message: format!(
                "Link type display_name '{name}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `server_profile.display_name_no_leading_trailing_whitespace` — trim-equal.
async fn run_server_profile_name_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, display_name
           FROM net.server_profile
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (display_name <> btrim(display_name))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name) in rows {
        out.push(Violation {
            rule_code: "server_profile.display_name_no_leading_trailing_whitespace".into(),
            severity, entity_type: "ServerProfile".into(), entity_id: Some(id),
            message: format!(
                "Server profile display_name '{name}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `change_set.title_not_empty` — flag change-sets with empty /
/// whitespace-only titles.
async fn run_change_set_title_empty(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid,)> = sqlx::query_as(
        "SELECT id
           FROM net.change_set
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (title IS NULL OR btrim(title) = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id,) in rows {
        out.push(Violation {
            rule_code: "change_set.title_not_empty".into(),
            severity, entity_type: "ChangeSet".into(), entity_id: Some(id),
            message: "Change-set title is empty or whitespace-only — operators can't identify the set.".to_string(),
        });
    }
    Ok(())
}

/// `change_set.title_no_leading_trailing_whitespace` — trim-equal.
async fn run_change_set_title_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, title
           FROM net.change_set
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND title IS NOT NULL
            AND (title <> btrim(title))
            AND btrim(title) <> ''")
        .bind(org_id).fetch_all(pool).await?;
    for (id, title) in rows {
        out.push(Violation {
            rule_code: "change_set.title_no_leading_trailing_whitespace".into(),
            severity, entity_type: "ChangeSet".into(), entity_id: Some(id),
            message: format!(
                "Change-set title '{title}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `vlan_template.template_code_no_leading_trailing_whitespace` — trim-equal.
async fn run_vlan_template_code_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, template_code
           FROM net.vlan_template
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (template_code <> btrim(template_code))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "vlan_template.template_code_no_leading_trailing_whitespace".into(),
            severity, entity_type: "VlanTemplate".into(), entity_id: Some(id),
            message: format!(
                "VLAN template template_code '{code}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `region.display_name_no_leading_trailing_whitespace` — trim-equal.
async fn run_region_name_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, display_name
           FROM net.region
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (display_name <> btrim(display_name))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name) in rows {
        out.push(Violation {
            rule_code: "region.display_name_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Region".into(), entity_id: Some(id),
            message: format!(
                "Region display_name '{name}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `site.display_name_no_leading_trailing_whitespace` — trim-equal.
async fn run_site_name_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, display_name
           FROM net.site
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (display_name <> btrim(display_name))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name) in rows {
        out.push(Violation {
            rule_code: "site.display_name_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Site".into(), entity_id: Some(id),
            message: format!(
                "Site display_name '{name}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `building.display_name_no_leading_trailing_whitespace` — trim-equal.
async fn run_building_name_ws(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, display_name
           FROM net.building
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (display_name <> btrim(display_name))")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name) in rows {
        out.push(Violation {
            rule_code: "building.display_name_no_leading_trailing_whitespace".into(),
            severity, entity_type: "Building".into(), entity_id: Some(id),
            message: format!(
                "Building display_name '{name}' has leading/trailing whitespace — trim before saving."),
        });
    }
    Ok(())
}

/// `rack.uheight_positive` — flag rack rows with non-positive
/// u_height (can't place any device).
async fn run_rack_uheight_positive(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i32)> = sqlx::query_as(
        "SELECT id, rack_code, u_height
           FROM net.rack
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND u_height <= 0")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, u) in rows {
        out.push(Violation {
            rule_code: "rack.uheight_positive".into(),
            severity, entity_type: "Rack".into(), entity_id: Some(id),
            message: format!(
                "Rack '{code}' has u_height={u} — can't place any device."),
        });
    }
    Ok(())
}

/// `room.max_racks_positive_when_set` — NULL is fine; zero or
/// negative is nonsense.
async fn run_room_max_racks_positive(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i32)> = sqlx::query_as(
        "SELECT id, room_code, max_racks
           FROM net.room
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND max_racks IS NOT NULL
            AND max_racks <= 0")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, max) in rows {
        out.push(Violation {
            rule_code: "room.max_racks_positive_when_set".into(),
            severity, entity_type: "Room".into(), entity_id: Some(id),
            message: format!(
                "Room '{code}' has max_racks={max} — clear to NULL for uncapped or set a positive value."),
        });
    }
    Ok(())
}

/// `vlan_template.display_name_not_empty` — blank display_name
/// breaks the template picker at VLAN creation.
async fn run_vlan_template_display_name(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, template_code FROM net.vlan_template
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND btrim(display_name) = ''")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "vlan_template.display_name_not_empty".into(),
            severity, entity_type: "VlanTemplate".into(), entity_id: Some(id),
            message: format!("VLAN template '{code}' has an empty display_name."),
        });
    }
    Ok(())
}

/// `loopback.number_unique_per_device` — GROUP BY (device_id,
/// loopback_number) HAVING count>1 across active rows.
async fn run_loopback_number_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, i64, Vec<Uuid>)> = sqlx::query_as(
        "SELECT device_id, loopback_number, COUNT(*) AS n, array_agg(id) AS ids
           FROM net.loopback
          WHERE organization_id = $1
            AND deleted_at IS NULL
          GROUP BY device_id, loopback_number
         HAVING COUNT(*) > 1")
        .bind(org_id).fetch_all(pool).await?;
    for (device_id, num, n, ids) in rows {
        let primary = ids.first().copied();
        let id_list = ids.iter().map(|id| id.to_string())
            .collect::<Vec<_>>().join(", ");
        out.push(Violation {
            rule_code: "loopback.number_unique_per_device".into(),
            severity, entity_type: "Loopback".into(), entity_id: primary,
            message: format!(
                "{n} loopbacks on device {device_id} claim number {num}: {id_list}."),
        });
    }
    Ok(())
}

/// `vlan_template.code_unique_per_tenant` — GROUP BY template_code
/// HAVING count>1.
async fn run_vlan_template_code_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(String, i64, Vec<Uuid>)> = sqlx::query_as(
        "SELECT template_code, COUNT(*) AS n, array_agg(id) AS ids
           FROM net.vlan_template
          WHERE organization_id = $1
            AND deleted_at IS NULL
          GROUP BY template_code
         HAVING COUNT(*) > 1")
        .bind(org_id).fetch_all(pool).await?;
    for (code, n, ids) in rows {
        let primary = ids.first().copied();
        let id_list = ids.iter().map(|id| id.to_string())
            .collect::<Vec<_>>().join(", ");
        out.push(Violation {
            rule_code: "vlan_template.code_unique_per_tenant".into(),
            severity, entity_type: "VlanTemplate".into(), entity_id: primary,
            message: format!(
                "{n} VLAN templates share code '{code}': {id_list}."),
        });
    }
    Ok(())
}

/// `subnet.active_scope_entity_resolves` — parallel to
/// vlan.scope_entity_resolves but for net.subnet.
async fn run_subnet_scope_entity_resolves_active(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT sn.id, sn.subnet_code, sn.scope_level
           FROM net.subnet sn
          WHERE sn.organization_id = $1
            AND sn.deleted_at IS NULL
            AND sn.status = 'Active'::net.entity_status
            AND sn.scope_level != 'Free'
            AND (
                  sn.scope_entity_id IS NULL
               OR (sn.scope_level = 'Region'   AND NOT EXISTS
                     (SELECT 1 FROM net.region   x WHERE x.id = sn.scope_entity_id AND x.deleted_at IS NULL))
               OR (sn.scope_level = 'Site'     AND NOT EXISTS
                     (SELECT 1 FROM net.site     x WHERE x.id = sn.scope_entity_id AND x.deleted_at IS NULL))
               OR (sn.scope_level = 'Building' AND NOT EXISTS
                     (SELECT 1 FROM net.building x WHERE x.id = sn.scope_entity_id AND x.deleted_at IS NULL))
               OR (sn.scope_level = 'Floor'    AND NOT EXISTS
                     (SELECT 1 FROM net.floor    x WHERE x.id = sn.scope_entity_id AND x.deleted_at IS NULL))
               OR (sn.scope_level = 'Room'     AND NOT EXISTS
                     (SELECT 1 FROM net.room     x WHERE x.id = sn.scope_entity_id AND x.deleted_at IS NULL))
            )")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, scope_level) in rows {
        out.push(Violation {
            rule_code: "subnet.active_scope_entity_resolves".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Active subnet '{code}' with scope_level='{scope_level}' has no resolvable scope_entity_id."),
        });
    }
    Ok(())
}

/// `loopback.active_has_ip_address` — Active loopback needs an
/// ip_address_id; Planned / other statuses skipped.
async fn run_loopback_active_has_ip(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, Uuid, i32)> = sqlx::query_as(
        "SELECT id, device_id, loopback_number
           FROM net.loopback
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status = 'Active'::net.entity_status
            AND ip_address_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, device_id, num) in rows {
        out.push(Violation {
            rule_code: "loopback.active_has_ip_address".into(),
            severity, entity_type: "Loopback".into(), entity_id: Some(id),
            message: format!(
                "Active loopback {num} on device {device_id} has no ip_address_id — broken allocation."),
        });
    }
    Ok(())
}

/// `port.speed_mbps_positive_when_set` — NULL is fine (auto-neg);
/// only check populated rows for positive value.
async fn run_port_speed_positive(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i64)> = sqlx::query_as(
        "SELECT id, interface_name, speed_mbps
           FROM net.port
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND speed_mbps IS NOT NULL
            AND speed_mbps <= 0")
        .bind(org_id).fetch_all(pool).await?;
    for (id, name, speed) in rows {
        out.push(Violation {
            rule_code: "port.speed_mbps_positive_when_set".into(),
            severity, entity_type: "Port".into(), entity_id: Some(id),
            message: format!(
                "Port '{name}' has speed_mbps={speed} — clear to NULL for auto-negotiate or set a positive value."),
        });
    }
    Ok(())
}

/// `module.slot_unique_per_device` — GROUP BY (device_id, slot)
/// HAVING count>1. Slots are physical positions.
async fn run_module_slot_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i64, Vec<Uuid>)> = sqlx::query_as(
        "SELECT device_id, slot, COUNT(*) AS n, array_agg(id) AS ids
           FROM net.module
          WHERE organization_id = $1
            AND deleted_at IS NULL
          GROUP BY device_id, slot
         HAVING COUNT(*) > 1")
        .bind(org_id).fetch_all(pool).await?;
    for (device_id, slot, n, ids) in rows {
        let primary = ids.first().copied();
        let id_list = ids.iter().map(|id| id.to_string())
            .collect::<Vec<_>>().join(", ");
        out.push(Violation {
            rule_code: "module.slot_unique_per_device".into(),
            severity, entity_type: "Module".into(), entity_id: primary,
            message: format!(
                "{n} modules on device {device_id} claim slot '{slot}': {id_list}."),
        });
    }
    Ok(())
}

/// `mstp_priority_rule.has_steps` — NOT EXISTS subquery against
/// net.mstp_priority_rule_step.
async fn run_mstp_rule_has_steps(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT r.id, r.rule_code
           FROM net.mstp_priority_rule r
          WHERE r.organization_id = $1
            AND r.deleted_at IS NULL
            AND r.status = 'Active'::net.entity_status
            AND NOT EXISTS (
                SELECT 1 FROM net.mstp_priority_rule_step s
                 WHERE s.rule_id = r.id
                   AND s.deleted_at IS NULL
            )")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "mstp_priority_rule.has_steps".into(),
            severity, entity_type: "MstpPriorityRule".into(), entity_id: Some(id),
            message: format!(
                "MSTP rule '{code}' is Active but has no steps defined — config-gen emits nothing."),
        });
    }
    Ok(())
}

/// `reservation_shelf.resource_key_not_empty` — btrim guard.
async fn run_shelf_resource_key_not_empty(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, resource_type
           FROM net.reservation_shelf
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND btrim(resource_key) = ''")
        .bind(org_id).fetch_all(pool).await?;
    for (id, resource_type) in rows {
        out.push(Violation {
            rule_code: "reservation_shelf.resource_key_not_empty".into(),
            severity, entity_type: "ReservationShelf".into(), entity_id: Some(id),
            message: format!(
                "Shelf entry ({resource_type}) has an empty resource_key — recycler can't match it."),
        });
    }
    Ok(())
}

/// `building.display_name_not_empty` — parallel to region + site.
async fn run_building_display_name(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, building_code FROM net.building
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND btrim(display_name) = ''")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "building.display_name_not_empty".into(),
            severity, entity_type: "Building".into(), entity_id: Some(id),
            message: format!("Building '{code}' has an empty display_name."),
        });
    }
    Ok(())
}

/// `floor_profile.display_name_not_empty` — parallel to site_profile.
async fn run_floor_profile_display_name(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid,)> = sqlx::query_as(
        "SELECT id FROM net.floor_profile
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND btrim(display_name) = ''")
        .bind(org_id).fetch_all(pool).await?;
    for (id,) in rows {
        out.push(Violation {
            rule_code: "floor_profile.display_name_not_empty".into(),
            severity, entity_type: "FloorProfile".into(), entity_id: Some(id),
            message: format!("Floor profile {id} has an empty display_name."),
        });
    }
    Ok(())
}

/// `rack.rack_code_unique_per_room` — parallel to floor/room rules.
async fn run_rack_code_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i64, Vec<Uuid>)> = sqlx::query_as(
        "SELECT room_id, rack_code, COUNT(*) AS n, array_agg(id) AS ids
           FROM net.rack
          WHERE organization_id = $1
            AND deleted_at IS NULL
          GROUP BY room_id, rack_code
         HAVING COUNT(*) > 1")
        .bind(org_id).fetch_all(pool).await?;
    for (room_id, code, n, ids) in rows {
        let primary = ids.first().copied();
        let id_list = ids.iter().map(|id| id.to_string())
            .collect::<Vec<_>>().join(", ");
        out.push(Violation {
            rule_code: "rack.rack_code_unique_per_room".into(),
            severity, entity_type: "Rack".into(), entity_id: primary,
            message: format!(
                "{n} racks in room {room_id} share rack_code '{code}': {id_list}."),
        });
    }
    Ok(())
}

// ─── Tests ────────────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::HashSet;

    #[test]
    fn rule_codes_are_unique() {
        let mut seen = HashSet::new();
        for r in RULES {
            assert!(seen.insert(r.code), "duplicate rule code: {}", r.code);
        }
    }

    #[test]
    fn rule_codes_are_non_empty_and_lowercase_dotted() {
        for r in RULES {
            assert!(!r.code.is_empty(), "rule has empty code: {}", r.name);
            assert!(r.code.contains('.'), "rule code must be dotted.namespace form: {}", r.code);
            assert!(
                r.code.chars().all(|c|
                    c.is_ascii_lowercase() || c.is_ascii_digit() || c == '.' || c == '_'),
                "rule code must be lowercase letters, digits, dots, or underscores only: {}",
                r.code);
        }
    }

    #[test]
    fn rule_names_and_descriptions_are_non_empty() {
        for r in RULES {
            assert!(!r.name.is_empty(), "rule has empty name: {}", r.code);
            assert!(r.description.len() >= 20,
                "rule description is too short to be useful: {}", r.code);
        }
    }

    #[test]
    fn rule_categories_are_known() {
        for r in RULES {
            assert!(matches!(r.category, "Consistency" | "Integrity" | "Safety" | "Advisory"),
                "unknown category '{}' on rule {}", r.category, r.code);
        }
    }

    #[test]
    fn find_rule_returns_entry_for_every_catalog_code() {
        for r in RULES {
            assert!(find_rule(r.code).is_some(), "find_rule missed {}", r.code);
        }
        assert!(find_rule("does.not.exist").is_none());
    }

    #[test]
    fn severity_round_trips_every_variant() {
        for s in [Severity::Error, Severity::Warning, Severity::Info] {
            assert_eq!(Severity::from_db(s.as_str()).unwrap(), s);
        }
        assert!(Severity::from_db("Nuclear").is_err());
    }

    /// The dispatcher must have an arm for every catalog entry. Any miss
    /// is caught at test time rather than production by making an SQL call
    /// that we expect to resolve *locally* (unknown code) without touching
    /// the DB.
    #[test]
    fn dispatcher_has_arm_for_every_catalog_rule() {
        // Rebuild the known set from the real dispatcher so we don't
        // have to hand-maintain this list. Using a static string list
        // would just shift the "is it complete?" problem one level.
        let dispatcher_arms: &[&str] = &[
            "device.hostname_required",
            "server.hostname_required",
            "link.endpoint_count",
            "server.nic_count_matches_profile",
            "loopback.ipv4_slash_32",
            "vlan.unique_per_block",
            "hierarchy.building_requires_site",
            "hierarchy.site_requires_region",
            "asn_allocation.value_in_block_range",
            "vlan.value_in_block_range",
            "device.active_requires_building",
            "link.endpoints_distinct_devices",
            "ip_address.contained_in_subnet",
            "subnet.scope_entity_present_when_non_free",
            "device.unique_hostname",
            "server.unique_hostname",
            "ip_address.unique_per_subnet",
            "subnet.parent_same_pool",
            "subnet.parent_contains_child",
            "server_nic.index_unique_per_server",
            "server_nic.index_dense_sequence",
            "link.building_matches_endpoints",
            "loopback.ipv6_slash_128",
            "mlag_domain.unique_per_tenant",
            "asn_allocation.unique_per_tenant",
            "device.management_ip_for_active",
            "device.has_device_role",
            "link.vlan_in_tenant",
            "link.subnet_in_tenant",
            "hierarchy.floor_requires_building",
            "hierarchy.rack_requires_room",
            "server_nic.target_device_resolves",
            "server.loopback_in_loopback_subnet",
            "asn_block.range_not_empty",
            "vlan_block.range_not_empty",
            "vlan.vlan_id_valid_range",
            "mlag_domain.domain_id_valid_range",
            "subnet.matches_pool_family",
            "server_profile.nic_count_positive",
            "server_nic.mlag_side_valid",
            "device.unique_mac_address",
            "server.unique_mac_address",
            "device_role.naming_template_not_empty",
            "asn_allocation.allocated_to_set",
            "asn_pool.range_not_empty",
            "mlag_domain_pool.range_not_empty",
            "link_type.naming_template_not_empty",
            "server_profile.naming_template_not_empty",
            "link.endpoint_interface_unique_per_device",
            "dhcp_relay_target.unique_per_vlan_ip",
            "device_role.display_name_not_empty",
            "dhcp_relay_target.priority_non_negative",
            "vlan.display_name_not_empty",
            "subnet.display_name_not_empty",
            "link.endpoint_devices_resolve",
            "subnet.active_subnet_has_pool",
            "server.active_has_building",
            "subnet.within_parent_pool_cidr",
            "link.unique_link_code_active",
            "device_role.unique_role_code_per_tenant",
            "vlan.scope_entity_resolves",
            "dhcp_relay_target.vlan_active",
            "subnet.vlan_link_is_active",
            "ip_address.assigned_to_id_when_typed",
            "rack.position_positive",
            "building_profile.role_counts_non_empty",
            "rendered_config.chain_integrity",
            "scope_grant.no_duplicate_tuple",
            "change_set.applied_has_no_pending_items",
            "naming_template_override.scope_entity_resolves",
            "asn_allocation.unique_allocated_to",
            "port.interface_name_not_empty",
            "ip_address.reserved_type_is_marked_reserved",
            "subnet.network_is_network_address",
            "link_endpoint.port_resolves",
            "server_nic.target_port_resolves",
            "asn_allocation.block_resolves_active",
            "server_nic.target_device_matches_port_device",
            "port.breakout_parent_on_same_device",
            "port.aggregate_on_same_device",
            "aggregate_ethernet.name_unique_per_device",
            "change_set.submitted_has_items",
            "reservation_shelf.cooldown_respected",
            "vlan_block.range_within_pool",
            "floor.floor_code_unique_per_building",
            "room.room_code_unique_per_floor",
            "rack.rack_code_unique_per_room",
            "ip_address.vrrp_has_peer_on_other_device",
            "site_profile.display_name_not_empty",
            "floor_profile.display_name_not_empty",
            "region.display_name_not_empty",
            "site.display_name_not_empty",
            "building.display_name_not_empty",
            "module.slot_unique_per_device",
            "mstp_priority_rule.has_steps",
            "reservation_shelf.resource_key_not_empty",
            "mlag_domain.scope_entity_present_when_non_global",
            "change_set_item.entity_id_required_for_mutations",
            "port.speed_mbps_positive_when_set",
            "naming_template_override.template_not_empty",
            "ip_pool.network_is_network_address",
            "loopback.active_has_ip_address",
            "loopback.number_unique_per_device",
            "vlan_template.code_unique_per_tenant",
            "subnet.active_scope_entity_resolves",
            "rack.uheight_positive",
            "room.max_racks_positive_when_set",
            "vlan_template.display_name_not_empty",
            "ip_address.subnet_resolves_active",
            "link.active_has_endpoints",
            "floor.building_resolves_active",
            "room.floor_resolves_active",
            "rack.room_resolves_active",
            "port.device_resolves_active",
            "module.device_resolves_active",
            "loopback.device_resolves_active",
            "aggregate_ethernet.device_resolves_active",
            "server_nic.server_resolves_active",
            "link_endpoint.link_resolves_active",
            "device.role_resolves_active",
            "vlan.block_resolves_active",
            "subnet.pool_resolves_active",
            "mlag_domain.pool_resolves_active",
            "asn_block.pool_resolves_active",
            "vlan_block.pool_resolves_active",
            "vlan.template_resolves_active_when_set",
            "server_nic.vlan_resolves_active_when_set",
            "server_nic.subnet_resolves_active_when_set",
            "link_endpoint.vlan_resolves_active_when_set",
            "vlan_template.default_unique_per_tenant",
            "port.interface_name_starts_with_prefix",
            "device.hardware_model_set_when_active",
            "device.room_resolves_active_when_set",
            "device.rack_resolves_active_when_set",
            "subnet.parent_subnet_resolves_active_when_set",
            "device.building_resolves_active_when_set",
            "server.room_resolves_active_when_set",
            "server.rack_resolves_active_when_set",
            "link.link_type_resolves_active",
            "link.building_resolves_active_when_set",
            "server.management_ip_set_when_active",
            "port.module_resolves_active_when_set",
            "port.breakout_parent_resolves_active_when_set",
            "port.aggregate_ethernet_resolves_active_when_set",
            "link_endpoint.device_resolves_active_when_set",
            "link_endpoint.ip_address_resolves_active_when_set",
            "server_nic.ip_address_resolves_active_when_set",
            "device.asn_allocation_resolves_active_when_set",
            "server.asn_allocation_resolves_active_when_set",
            "server.server_profile_resolves_active_when_set",
            "server.loopback_ip_address_resolves_active_when_set",
            "link_endpoint.port_resolves_active_when_set",
            "server.building_resolves_active_when_set",
            "server_nic.target_port_resolves_active_when_set",
            "rack.uheight_within_reason",
            "device.firmware_version_set_when_active",
            "device.serial_number_unique_per_tenant_when_set",
            "server.serial_number_unique_per_tenant_when_set",
            "module.serial_number_unique_per_tenant_when_set",
            "device.last_ping_ok_when_active",
            "server.last_ping_ok_when_active",
            "link.description_set_when_active",
            "device.management_ip_unique_per_tenant_when_set",
            "port.admin_up_false_on_active_status",
            "server.management_ip_unique_per_tenant_when_set",
            "server_nic.admin_up_false_on_active_status",
            "port.speed_mbps_reasonable_when_set",
            "rack.max_devices_positive_when_set",
            "port.native_vlan_requires_access_or_trunk",
            "aggregate_ethernet.member_count_meets_min_links",
            "change_set_item.expected_version_set_for_update",
            "port.speed_mbps_set_when_active",
            "server_nic.nic_index_in_range",
            "device.hostname_no_leading_trailing_whitespace",
            "server.hostname_no_leading_trailing_whitespace",
            "port.description_set_when_active",
            "link.link_code_no_leading_trailing_whitespace",
            "ip_address.gateway_unique_per_subnet",
            "subnet.display_name_no_leading_trailing_whitespace",
            "vlan.display_name_no_leading_trailing_whitespace",
            "server.display_name_no_leading_trailing_whitespace",
            "link.display_name_no_leading_trailing_whitespace",
            "ip_pool.display_name_no_leading_trailing_whitespace",
            "port.breakout_parent_not_self_loop",
            "subnet.parent_subnet_not_self_loop",
            "device.rack_implies_room",
            "server.rack_implies_room",
            "mlag_domain.display_name_no_leading_trailing_whitespace",
            "rack.rack_code_no_leading_trailing_whitespace",
            "room.room_code_no_leading_trailing_whitespace",
            "floor.floor_code_no_leading_trailing_whitespace",
            "building.building_code_no_leading_trailing_whitespace",
            "site.site_code_no_leading_trailing_whitespace",
            "region.region_code_no_leading_trailing_whitespace",
            "device.device_code_no_leading_trailing_whitespace",
            "asn_pool.display_name_no_leading_trailing_whitespace",
            "vlan_pool.display_name_no_leading_trailing_whitespace",
            "mlag_domain_pool.display_name_no_leading_trailing_whitespace",
            "asn_pool.pool_code_no_leading_trailing_whitespace",
            "vlan_pool.pool_code_no_leading_trailing_whitespace",
            "ip_pool.pool_code_no_leading_trailing_whitespace",
            "device_role.role_code_no_leading_trailing_whitespace",
            "link_type.type_code_no_leading_trailing_whitespace",
            "server_profile.profile_code_no_leading_trailing_whitespace",
            "asn_block.block_code_no_leading_trailing_whitespace",
            "vlan_block.block_code_no_leading_trailing_whitespace",
            "reservation_shelf.resource_key_no_leading_trailing_whitespace",
            "device_role.display_name_no_leading_trailing_whitespace",
            "link_type.display_name_no_leading_trailing_whitespace",
            "server_profile.display_name_no_leading_trailing_whitespace",
            "change_set.title_not_empty",
            "change_set.title_no_leading_trailing_whitespace",
            "vlan_template.template_code_no_leading_trailing_whitespace",
            "region.display_name_no_leading_trailing_whitespace",
            "site.display_name_no_leading_trailing_whitespace",
            "building.display_name_no_leading_trailing_whitespace",
        ];
        // Every catalog entry must be in the dispatcher list.
        for r in RULES {
            assert!(dispatcher_arms.contains(&r.code),
                "rule {} is in RULES but has no dispatcher arm", r.code);
        }
        // No dispatcher arm should be orphaned (rule removed from catalog).
        for arm in dispatcher_arms {
            assert!(RULES.iter().any(|r| r.code == *arm),
                "dispatcher has arm for '{arm}' but no rule in catalog");
        }
    }
}
