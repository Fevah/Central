//! CRUD for `net.dhcp_relay_target` — the M:N table that maps client
//! VLANs to the DHCP server IPs the switch should forward to.
//! Consumed at render time by `config_gen::fetch_context`; this
//! module is the write path (admin-facing).
//!
//! Shape mirrors `naming_overrides` so the two CRUD surfaces look
//! the same from a client's perspective — optimistic concurrency
//! via `version`, soft-delete on DELETE, audit-log entries in the
//! same transaction as the DB mutation so neither can land without
//! the other.

use chrono::{DateTime, Utc};
use ipnetwork::IpNetwork;
use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::audit::{self, AuditEvent};
use crate::error::EngineError;

#[derive(Debug, Clone, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
pub struct DhcpRelayTarget {
    pub id: Uuid,
    pub organization_id: Uuid,
    pub vlan_id: Uuid,
    #[serde(serialize_with = "serialize_inet")]
    pub server_ip: IpNetwork,
    pub ip_address_id: Option<Uuid>,
    pub priority: i32,
    pub status: String,
    pub version: i32,
    pub created_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
    pub notes: Option<String>,
}

/// Serialize `inet` as the bare host string (`"10.11.120.10"`) rather
/// than the default CIDR form (`"10.11.120.10/32"`). Matches what
/// the renderer emits and how every client picks the field up.
fn serialize_inet<S: serde::Serializer>(ip: &IpNetwork, s: S) -> Result<S::Ok, S::Error> {
    s.serialize_str(&ip.ip().to_string())
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateDhcpRelayBody {
    pub organization_id: Uuid,
    pub vlan_id: Uuid,
    pub server_ip: IpNetwork,
    pub ip_address_id: Option<Uuid>,
    #[serde(default = "default_priority")]
    pub priority: i32,
    pub notes: Option<String>,
}

fn default_priority() -> i32 { 10 }

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateDhcpRelayBody {
    pub priority: i32,
    pub ip_address_id: Option<Uuid>,
    pub notes: Option<String>,
    pub version: i32,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListDhcpRelayQuery {
    pub organization_id: Uuid,
    pub vlan_id: Option<Uuid>,
}

#[derive(Clone)]
pub struct DhcpRelayRepo {
    pool: PgPool,
}

impl DhcpRelayRepo {
    pub fn new(pool: PgPool) -> Self { Self { pool } }

    pub async fn list(&self, q: &ListDhcpRelayQuery) -> Result<Vec<DhcpRelayTarget>, EngineError> {
        let rows: Vec<DhcpRelayTarget> = sqlx::query_as(
            "SELECT id, organization_id, vlan_id, server_ip, ip_address_id,
                    priority, status::text AS status, version,
                    created_at, updated_at, notes
               FROM net.dhcp_relay_target
              WHERE organization_id = $1
                AND deleted_at IS NULL
                AND ($2::uuid IS NULL OR vlan_id = $2)
              ORDER BY vlan_id, priority ASC, server_ip")
            .bind(q.organization_id)
            .bind(q.vlan_id)
            .fetch_all(&self.pool)
            .await?;
        Ok(rows)
    }

    pub async fn get(&self, id: Uuid, org_id: Uuid) -> Result<DhcpRelayTarget, EngineError> {
        let row: Option<DhcpRelayTarget> = sqlx::query_as(
            "SELECT id, organization_id, vlan_id, server_ip, ip_address_id,
                    priority, status::text AS status, version,
                    created_at, updated_at, notes
               FROM net.dhcp_relay_target
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
            .bind(id)
            .bind(org_id)
            .fetch_optional(&self.pool)
            .await?;
        row.ok_or_else(|| EngineError::container_not_found("dhcp_relay_target", id))
    }

    pub async fn create(
        &self,
        body: &CreateDhcpRelayBody,
        user_id: Option<i32>,
    ) -> Result<DhcpRelayTarget, EngineError> {
        validate_priority(body.priority)?;

        let mut tx = self.pool.begin().await?;
        let row: DhcpRelayTarget = sqlx::query_as(
            "INSERT INTO net.dhcp_relay_target
                (organization_id, vlan_id, server_ip, ip_address_id,
                 priority, notes, status, lock_state, created_by, updated_by)
             VALUES ($1, $2, $3, $4, $5, $6,
                     'Active'::net.entity_status, 'Open'::net.lock_state, $7, $7)
             RETURNING id, organization_id, vlan_id, server_ip, ip_address_id,
                       priority, status::text AS status, version,
                       created_at, updated_at, notes")
            .bind(body.organization_id)
            .bind(body.vlan_id)
            .bind(body.server_ip)
            .bind(body.ip_address_id)
            .bind(body.priority)
            .bind(body.notes.as_deref())
            .bind(user_id)
            .fetch_one(&mut *tx)
            .await?;

        let details = serde_json::json!({
            "vlan_id": row.vlan_id,
            "server_ip": row.server_ip.ip().to_string(),
            "priority": row.priority,
        });
        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: row.organization_id,
            source_service: "networking-engine",
            entity_type: "DhcpRelayTarget",
            entity_id: Some(row.id),
            action: "Created",
            actor_user_id: user_id,
            actor_display: None,
            client_ip: None,
            correlation_id: None,
            details,
        }).await?;

        tx.commit().await?;
        Ok(row)
    }

    pub async fn update(
        &self,
        id: Uuid,
        org_id: Uuid,
        body: &UpdateDhcpRelayBody,
        user_id: Option<i32>,
    ) -> Result<DhcpRelayTarget, EngineError> {
        validate_priority(body.priority)?;

        let mut tx = self.pool.begin().await?;
        let before: Option<DhcpRelayTarget> = sqlx::query_as(
            "SELECT id, organization_id, vlan_id, server_ip, ip_address_id,
                    priority, status::text AS status, version,
                    created_at, updated_at, notes
               FROM net.dhcp_relay_target
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
            .bind(id)
            .bind(org_id)
            .fetch_optional(&mut *tx)
            .await?;

        let row: Option<DhcpRelayTarget> = sqlx::query_as(
            "UPDATE net.dhcp_relay_target
                SET priority      = $3,
                    ip_address_id = $4,
                    notes         = $5,
                    updated_at    = now(),
                    updated_by    = $6,
                    version       = version + 1
              WHERE id = $1
                AND organization_id = $2
                AND version = $7
                AND deleted_at IS NULL
              RETURNING id, organization_id, vlan_id, server_ip, ip_address_id,
                        priority, status::text AS status, version,
                        created_at, updated_at, notes")
            .bind(id)
            .bind(org_id)
            .bind(body.priority)
            .bind(body.ip_address_id)
            .bind(body.notes.as_deref())
            .bind(user_id)
            .bind(body.version)
            .fetch_optional(&mut *tx)
            .await?;
        let row = row.ok_or_else(|| EngineError::bad_request(format!(
            "DHCP relay target {id} not found, already updated by another caller, or wrong tenant.")))?;

        let details = serde_json::json!({
            "before": before.as_ref().map(|b| serde_json::json!({
                "priority":      b.priority,
                "ip_address_id": b.ip_address_id,
                "notes":         b.notes,
            })),
            "after": {
                "priority":      row.priority,
                "ip_address_id": row.ip_address_id,
                "notes":         row.notes,
            },
        });
        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "DhcpRelayTarget",
            entity_id: Some(row.id),
            action: "Updated",
            actor_user_id: user_id,
            actor_display: None,
            client_ip: None,
            correlation_id: None,
            details,
        }).await?;

        tx.commit().await?;
        Ok(row)
    }

    /// Soft-delete. Matches every other net.* table — `deleted_at`
    /// stamp + audit entry; row stays in place so historical renders
    /// referencing it still make sense.
    pub async fn soft_delete(
        &self,
        id: Uuid,
        org_id: Uuid,
        user_id: Option<i32>,
    ) -> Result<(), EngineError> {
        let mut tx = self.pool.begin().await?;
        let affected = sqlx::query(
            "UPDATE net.dhcp_relay_target
                SET deleted_at = now(),
                    deleted_by = $3
              WHERE id = $1
                AND organization_id = $2
                AND deleted_at IS NULL")
            .bind(id)
            .bind(org_id)
            .bind(user_id)
            .execute(&mut *tx)
            .await?
            .rows_affected();
        if affected == 0 {
            return Err(EngineError::container_not_found("dhcp_relay_target", id));
        }
        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "DhcpRelayTarget",
            entity_id: Some(id),
            action: "Deleted",
            actor_user_id: user_id,
            actor_display: None,
            client_ip: None,
            correlation_id: None,
            details: serde_json::json!({}),
        }).await?;
        tx.commit().await?;
        Ok(())
    }
}

/// Priority is stored as `int` with no DB-level constraint, but a
/// negative priority breaks the "primary first, backup second"
/// ordering contract the renderer depends on. Reject at the API so
/// operators don't ship a silently broken config.
fn validate_priority(p: i32) -> Result<(), EngineError> {
    if p < 0 {
        return Err(EngineError::bad_request(format!(
            "priority must be non-negative (got {p})")));
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn validate_priority_accepts_zero_and_positive() {
        assert!(validate_priority(0).is_ok());
        assert!(validate_priority(10).is_ok());
        assert!(validate_priority(i32::MAX).is_ok());
    }

    #[test]
    fn validate_priority_rejects_negative() {
        let err = validate_priority(-1).unwrap_err();
        // The error message shape is what clients will see in the
        // RFC 7807 problem+json body.
        assert!(err.to_string().contains("priority must be non-negative"),
            "error message should be actionable: {err}");
    }

    #[test]
    fn create_body_defaults_priority_to_10() {
        // Clients that don't pass a priority get the legacy Immunocore
        // default — pinned here so a future refactor that changes the
        // default breaks the test rather than silently shifting ops.
        let json = r#"{
            "organizationId": "00000000-0000-0000-0000-000000000001",
            "vlanId":         "00000000-0000-0000-0000-000000000002",
            "serverIp":       "10.11.120.10"
        }"#;
        let body: CreateDhcpRelayBody = serde_json::from_str(json).expect("parses");
        assert_eq!(body.priority, 10);
    }

    #[test]
    fn dhcp_relay_target_serialises_inet_as_bare_host() {
        // Clients expect "10.11.120.10" not "10.11.120.10/32" — matches
        // what the renderer emits. Locks the serializer helper.
        let t = DhcpRelayTarget {
            id:                Uuid::nil(),
            organization_id:   Uuid::nil(),
            vlan_id:           Uuid::nil(),
            server_ip:         "10.11.120.10/32".parse().unwrap(),
            ip_address_id:     None,
            priority:          10,
            status:            "Active".into(),
            version:           1,
            created_at:        Utc::now(),
            updated_at:        Utc::now(),
            notes:             None,
        };
        let json = serde_json::to_string(&t).expect("serialises");
        assert!(json.contains("\"serverIp\":\"10.11.120.10\""),
            "serverIp should be bare host, not CIDR: {json}");
    }
}
