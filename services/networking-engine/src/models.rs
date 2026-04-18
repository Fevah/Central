//! DTO + enum types returned by the engine's REST endpoints.
//!
//! Kept thin — we don't model the full universal-base column set here because the engine
//! only needs to return the fields an allocation result actually carries. Full
//! CRUD reads still live on the desktop-side through its repositories (transitional).

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

#[derive(Debug, Copy, Clone, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub enum PoolScopeLevel {
    Free,
    Region,
    Site,
    Building,
    Floor,
    Room,
    Device,
}

impl PoolScopeLevel {
    pub fn as_str(&self) -> &'static str {
        match self {
            Self::Free => "Free",
            Self::Region => "Region",
            Self::Site => "Site",
            Self::Building => "Building",
            Self::Floor => "Floor",
            Self::Room => "Room",
            Self::Device => "Device",
        }
    }
}

#[derive(Debug, Copy, Clone, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub enum ShelfResourceType {
    Asn,
    Ip,
    Subnet,
    Vlan,
    Mlag,
    Mstp,
}

impl ShelfResourceType {
    pub fn as_db_str(&self) -> &'static str {
        // Lowercase for the DB column (`net.reservation_shelf.resource_type`),
        // matches the C# `ToString().ToLowerInvariant()` convention.
        match self {
            Self::Asn => "asn",
            Self::Ip => "ip",
            Self::Subnet => "subnet",
            Self::Vlan => "vlan",
            Self::Mlag => "mlag",
            Self::Mstp => "mstp",
        }
    }
}

#[derive(Debug, Copy, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub enum MlagSide { A, B }

impl MlagSide {
    pub fn as_str(&self) -> &'static str {
        match self { Self::A => "A", Self::B => "B" }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AsnAllocation {
    pub id: Uuid,
    pub organization_id: Uuid,
    pub block_id: Uuid,
    pub asn: i64,
    pub allocated_to_type: String,
    pub allocated_to_id: Uuid,
    pub allocated_at: DateTime<Utc>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Vlan {
    pub id: Uuid,
    pub organization_id: Uuid,
    pub block_id: Uuid,
    pub template_id: Option<Uuid>,
    pub vlan_id: i32,
    pub display_name: String,
    pub description: Option<String>,
    pub scope_level: PoolScopeLevel,
    pub scope_entity_id: Option<Uuid>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MlagDomain {
    pub id: Uuid,
    pub organization_id: Uuid,
    pub pool_id: Uuid,
    pub domain_id: i32,
    pub display_name: String,
    pub scope_level: PoolScopeLevel,
    pub scope_entity_id: Option<Uuid>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IpAddress {
    pub id: Uuid,
    pub organization_id: Uuid,
    pub subnet_id: Uuid,
    pub address: String,
    pub assigned_to_type: Option<String>,
    pub assigned_to_id: Option<Uuid>,
    pub is_reserved: bool,
    pub assigned_at: DateTime<Utc>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Subnet {
    pub id: Uuid,
    pub organization_id: Uuid,
    pub pool_id: Uuid,
    pub parent_subnet_id: Option<Uuid>,
    pub subnet_code: String,
    pub display_name: String,
    pub network: String,
    pub scope_level: PoolScopeLevel,
    pub scope_entity_id: Option<Uuid>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ReservationShelfEntry {
    pub id: Uuid,
    pub organization_id: Uuid,
    pub resource_type: ShelfResourceType,
    pub resource_key: String,
    pub pool_id: Option<Uuid>,
    pub block_id: Option<Uuid>,
    pub retired_at: DateTime<Utc>,
    pub available_after: DateTime<Utc>,
    pub retired_reason: Option<String>,
}
