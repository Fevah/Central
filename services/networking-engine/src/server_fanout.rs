//! 4-NIC server fan-out. Port of `libs/persistence/Net/ServerCreationService.cs`.
//!
//! Creates a `net.server` row + its N `net.server_nic` rows in a single transaction,
//! resolving the MLAG-paired cores for the building and optionally allocating an
//! ASN / loopback / per-NIC IP along the way.

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::allocation::AllocationService;
use crate::error::EngineError;
use crate::ip_allocation::IpAllocationService;
use crate::models::{AsnAllocation, IpAddress, MlagSide};

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ServerCreationRequest {
    pub organization_id: Uuid,
    pub server_profile_id: Uuid,
    pub hostname: String,
    pub building_id: Option<Uuid>,
    pub room_id: Option<Uuid>,
    pub rack_id: Option<Uuid>,
    pub asn_block_id: Option<Uuid>,
    pub loopback_subnet_id: Option<Uuid>,
    pub nic_subnet_id: Option<Uuid>,
    pub side_a_hostname: Option<String>,
    pub side_b_hostname: Option<String>,
    pub display_name: Option<String>,
    pub user_id: Option<i32>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ServerCreationResult {
    pub server: ServerRow,
    pub nics: Vec<ServerNicRow>,
    pub asn_allocation: Option<AsnAllocation>,
    pub loopback_ip: Option<IpAddress>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ServerRow {
    pub id: Uuid,
    pub organization_id: Uuid,
    pub server_profile_id: Uuid,
    pub building_id: Option<Uuid>,
    pub hostname: String,
    pub display_name: Option<String>,
    pub created_at: DateTime<Utc>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ServerNicRow {
    pub id: Uuid,
    pub server_id: Uuid,
    pub nic_index: i32,
    pub target_device_id: Option<Uuid>,
    pub ip_address_id: Option<Uuid>,
    pub subnet_id: Option<Uuid>,
    pub mlag_side: MlagSide,
    pub admin_up: bool,
}

#[derive(Clone)]
pub struct ServerCreationService {
    pool: PgPool,
    alloc: AllocationService,
    ip_svc: IpAllocationService,
}

impl ServerCreationService {
    pub fn new(pool: PgPool) -> Self {
        let alloc = AllocationService::new(pool.clone());
        let ip_svc = IpAllocationService::new(pool.clone());
        Self { pool, alloc, ip_svc }
    }

    pub async fn create_with_fanout(
        &self,
        req: ServerCreationRequest,
    ) -> Result<ServerCreationResult, EngineError> {
        if req.hostname.trim().is_empty() {
            return Err(EngineError::bad_request("hostname is required"));
        }

        // 1. Resolve profile.
        let profile = fetch_profile(&self.pool, req.server_profile_id, req.organization_id).await?;
        if profile.nic_count < 1 {
            return Err(EngineError::bad_request(format!(
                "Profile '{}' has NicCount < 1; cannot fan out.", profile.profile_code)));
        }

        // 2. ASN allocation (optional).
        let asn_alloc = if let Some(block_id) = req.asn_block_id {
            // The allocated_to_id gets a throw-away UUID here because we don't know the
            // server.Id yet (same pattern as C# ServerCreationService — a later chunk
            // will wire a server-back-fill if we care enough).
            Some(self.alloc.allocate_asn(
                block_id, req.organization_id,
                "Server", Uuid::new_v4(), req.user_id).await?)
        } else { None };

        // 3. Loopback IP allocation (optional).
        let loopback_ip = if let Some(loop_subnet) = req.loopback_subnet_id {
            Some(self.ip_svc.allocate_next_ip(
                loop_subnet, req.organization_id,
                Some("ServerLoopback"), Some(Uuid::new_v4()), req.user_id).await?)
        } else { None };

        // 4. Insert server.
        let server_row: (Uuid, DateTime<Utc>) = sqlx::query_as(
            "INSERT INTO net.server
                (organization_id, server_profile_id, building_id, room_id, rack_id,
                 asn_allocation_id, loopback_ip_address_id, hostname, display_name,
                 status, lock_state, created_by, updated_by)
             VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9,
                     'Planned'::net.entity_status, 'Open'::net.lock_state, $10, $10)
             RETURNING id, created_at")
            .bind(req.organization_id)
            .bind(req.server_profile_id)
            .bind(req.building_id)
            .bind(req.room_id)
            .bind(req.rack_id)
            .bind(asn_alloc.as_ref().map(|a| a.id))
            .bind(loopback_ip.as_ref().map(|i| i.id))
            .bind(&req.hostname)
            .bind(req.display_name.as_deref())
            .bind(req.user_id)
            .fetch_one(&self.pool)
            .await?;
        let server_id = server_row.0;

        // 5. Resolve MLAG cores.
        let (side_a, side_b) = self.resolve_sides(&req).await?;

        // 6. Create N NIC rows.
        let mut nics = Vec::with_capacity(profile.nic_count as usize);
        for i in 0..profile.nic_count {
            let side = if i % 2 == 0 { MlagSide::A } else { MlagSide::B };
            let target_device_id = match side { MlagSide::A => side_a, MlagSide::B => side_b };

            let nic_ip_id = if let Some(nic_subnet) = req.nic_subnet_id {
                let nic_ip = self.ip_svc.allocate_next_ip(
                    nic_subnet, req.organization_id,
                    Some("ServerNic"), Some(server_id), req.user_id).await?;
                Some(nic_ip.id)
            } else { None };

            let nic_id: Uuid = sqlx::query_scalar(
                "INSERT INTO net.server_nic
                    (organization_id, server_id, nic_index, target_device_id,
                     ip_address_id, subnet_id, mlag_side, admin_up,
                     status, lock_state, created_by, updated_by)
                 VALUES ($1, $2, $3, $4, $5, $6, $7, false,
                         'Planned'::net.entity_status, 'Open'::net.lock_state, $8, $8)
                 RETURNING id")
                .bind(req.organization_id)
                .bind(server_id)
                .bind(i)
                .bind(target_device_id)
                .bind(nic_ip_id)
                .bind(req.nic_subnet_id)
                .bind(side.as_str())
                .bind(req.user_id)
                .fetch_one(&self.pool)
                .await?;

            nics.push(ServerNicRow {
                id: nic_id,
                server_id,
                nic_index: i,
                target_device_id,
                ip_address_id: nic_ip_id,
                subnet_id: req.nic_subnet_id,
                mlag_side: side,
                admin_up: false,
            });
        }

        Ok(ServerCreationResult {
            server: ServerRow {
                id: server_id,
                organization_id: req.organization_id,
                server_profile_id: req.server_profile_id,
                building_id: req.building_id,
                hostname: req.hostname,
                display_name: req.display_name,
                created_at: server_row.1,
            },
            nics,
            asn_allocation: asn_alloc,
            loopback_ip,
        })
    }

    /// Resolve which devices sit on sides A and B.
    ///
    /// - Explicit hostnames from the request win.
    /// - Otherwise: Core / L1Core / L2Core devices in the building, ordered by hostname.
    ///   First → A, second → B.
    /// - Missing building → both None (NIC rows still land without target_device FK).
    async fn resolve_sides(
        &self,
        req: &ServerCreationRequest,
    ) -> Result<(Option<Uuid>, Option<Uuid>), EngineError> {
        let Some(building_id) = req.building_id else { return Ok((None, None)); };

        if req.side_a_hostname.is_some() || req.side_b_hostname.is_some() {
            let a = lookup_device(&self.pool, req.organization_id, req.side_a_hostname.as_deref()).await?;
            let b = lookup_device(&self.pool, req.organization_id, req.side_b_hostname.as_deref()).await?;
            return Ok((a, b));
        }

        let rows: Vec<(Uuid,)> = sqlx::query_as(
            "SELECT d.id
               FROM net.device d
               JOIN net.device_role r ON r.id = d.device_role_id
              WHERE d.organization_id = $1
                AND d.building_id = $2
                AND d.deleted_at IS NULL
                AND r.role_code IN ('Core','L1Core','L2Core')
              ORDER BY d.hostname
              LIMIT 2")
            .bind(req.organization_id)
            .bind(building_id)
            .fetch_all(&self.pool)
            .await?;

        let a = rows.first().map(|(id,)| *id);
        let b = rows.get(1).map(|(id,)| *id);
        Ok((a, b))
    }
}

#[derive(Debug, Clone, sqlx::FromRow)]
struct ServerProfileRow {
    #[allow(dead_code)]
    id: Uuid,
    profile_code: String,
    nic_count: i32,
}

async fn fetch_profile(
    pool: &PgPool,
    profile_id: Uuid,
    org_id: Uuid,
) -> Result<ServerProfileRow, EngineError> {
    let row: Option<ServerProfileRow> = sqlx::query_as(
        "SELECT id, profile_code, nic_count
           FROM net.server_profile
          WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
        .bind(profile_id)
        .bind(org_id)
        .fetch_optional(pool)
        .await?;
    row.ok_or(EngineError::ServerProfileNotFound(profile_id))
}

async fn lookup_device(
    pool: &PgPool,
    org_id: Uuid,
    hostname: Option<&str>,
) -> Result<Option<Uuid>, EngineError> {
    let Some(h) = hostname else { return Ok(None); };
    if h.trim().is_empty() { return Ok(None); }
    let id: Option<Uuid> = sqlx::query_scalar(
        "SELECT id FROM net.device
          WHERE organization_id = $1 AND hostname = $2 AND deleted_at IS NULL
          LIMIT 1")
        .bind(org_id)
        .bind(h)
        .fetch_optional(pool)
        .await?;
    Ok(id)
}
