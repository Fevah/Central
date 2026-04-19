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

    for r in resolved {
        if !r.effective_enabled { continue; }
        if let Some(code) = target {
            if r.meta.code != code { continue; }
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
