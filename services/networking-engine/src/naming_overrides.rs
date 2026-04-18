//! CRUD for `net.naming_template_override`. Phase 7a.
//!
//! Admin-facing — the WPF module's "Naming overrides" panel reads/writes
//! through these endpoints. Optimistic concurrency on UPDATE via the
//! `version` column so two admins can't silently overwrite each other.

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::error::EngineError;

#[derive(Debug, Clone, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
pub struct NamingOverride {
    pub id: Uuid,
    pub organization_id: Uuid,
    pub entity_type: String,
    pub subtype_code: Option<String>,
    pub scope_level: String,
    pub scope_entity_id: Option<Uuid>,
    pub naming_template: String,
    pub status: String,
    pub version: i32,
    pub created_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
    pub notes: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateOverrideBody {
    pub organization_id: Uuid,
    pub entity_type: String,
    pub subtype_code: Option<String>,
    pub scope_level: String,
    pub scope_entity_id: Option<Uuid>,
    pub naming_template: String,
    pub notes: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateOverrideBody {
    pub naming_template: String,
    pub notes: Option<String>,
    pub version: i32,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListOverridesQuery {
    pub organization_id: Uuid,
    pub entity_type: Option<String>,
    pub scope_level: Option<String>,
}

#[derive(Clone)]
pub struct NamingOverrideRepo {
    pool: PgPool,
}

impl NamingOverrideRepo {
    pub fn new(pool: PgPool) -> Self { Self { pool } }

    pub async fn create(
        &self,
        body: &CreateOverrideBody,
        user_id: Option<i32>,
    ) -> Result<NamingOverride, EngineError> {
        validate_scope(&body.scope_level, body.scope_entity_id)?;

        let row: NamingOverride = sqlx::query_as(
            "INSERT INTO net.naming_template_override
                (organization_id, entity_type, subtype_code, scope_level,
                 scope_entity_id, naming_template, notes,
                 status, lock_state, created_by, updated_by)
             VALUES ($1, $2, $3, $4, $5, $6, $7,
                     'Active'::net.entity_status, 'Open'::net.lock_state, $8, $8)
             RETURNING id, organization_id, entity_type, subtype_code,
                       scope_level, scope_entity_id, naming_template,
                       status::text AS status, version, created_at,
                       updated_at, notes")
            .bind(body.organization_id)
            .bind(&body.entity_type)
            .bind(body.subtype_code.as_deref())
            .bind(&body.scope_level)
            .bind(body.scope_entity_id)
            .bind(&body.naming_template)
            .bind(body.notes.as_deref())
            .bind(user_id)
            .fetch_one(&self.pool)
            .await?;
        Ok(row)
    }

    pub async fn list(&self, q: &ListOverridesQuery) -> Result<Vec<NamingOverride>, EngineError> {
        let rows: Vec<NamingOverride> = sqlx::query_as(
            "SELECT id, organization_id, entity_type, subtype_code,
                    scope_level, scope_entity_id, naming_template,
                    status::text AS status, version, created_at,
                    updated_at, notes
               FROM net.naming_template_override
              WHERE organization_id = $1
                AND deleted_at IS NULL
                AND ($2::text IS NULL OR entity_type = $2)
                AND ($3::text IS NULL OR scope_level = $3)
              ORDER BY scope_level, entity_type, subtype_code NULLS LAST")
            .bind(q.organization_id)
            .bind(q.entity_type.as_deref())
            .bind(q.scope_level.as_deref())
            .fetch_all(&self.pool)
            .await?;
        Ok(rows)
    }

    pub async fn get(&self, id: Uuid, org_id: Uuid) -> Result<NamingOverride, EngineError> {
        let row: Option<NamingOverride> = sqlx::query_as(
            "SELECT id, organization_id, entity_type, subtype_code,
                    scope_level, scope_entity_id, naming_template,
                    status::text AS status, version, created_at,
                    updated_at, notes
               FROM net.naming_template_override
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
            .bind(id)
            .bind(org_id)
            .fetch_optional(&self.pool)
            .await?;
        row.ok_or_else(|| EngineError::container_not_found("naming_template_override", id))
    }

    pub async fn update(
        &self,
        id: Uuid,
        org_id: Uuid,
        body: &UpdateOverrideBody,
        user_id: Option<i32>,
    ) -> Result<NamingOverride, EngineError> {
        // Optimistic concurrency: WHERE version = ? AND bump version.
        let row: Option<NamingOverride> = sqlx::query_as(
            "UPDATE net.naming_template_override
                SET naming_template = $3,
                    notes = $4,
                    updated_at = now(),
                    updated_by = $5,
                    version = version + 1
              WHERE id = $1
                AND organization_id = $2
                AND version = $6
                AND deleted_at IS NULL
              RETURNING id, organization_id, entity_type, subtype_code,
                        scope_level, scope_entity_id, naming_template,
                        status::text AS status, version, created_at,
                        updated_at, notes")
            .bind(id)
            .bind(org_id)
            .bind(&body.naming_template)
            .bind(body.notes.as_deref())
            .bind(user_id)
            .bind(body.version)
            .fetch_optional(&self.pool)
            .await?;
        row.ok_or_else(|| EngineError::bad_request(format!(
            "Override {id} not found, already updated by another caller, or wrong tenant.")))
    }

    pub async fn delete(
        &self,
        id: Uuid,
        org_id: Uuid,
        user_id: Option<i32>,
    ) -> Result<(), EngineError> {
        let rows = sqlx::query(
            "UPDATE net.naming_template_override
                SET deleted_at = now(), deleted_by = $3
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
            .bind(id)
            .bind(org_id)
            .bind(user_id)
            .execute(&self.pool)
            .await?;
        if rows.rows_affected() == 0 {
            return Err(EngineError::container_not_found("naming_template_override", id));
        }
        Ok(())
    }
}

fn validate_scope(scope_level: &str, scope_entity_id: Option<Uuid>) -> Result<(), EngineError> {
    match scope_level {
        "Global" if scope_entity_id.is_some() =>
            Err(EngineError::bad_request("Global scope must have scope_entity_id = null")),
        "Global" => Ok(()),
        "Region" | "Site" | "Building" if scope_entity_id.is_none() =>
            Err(EngineError::bad_request(format!(
                "{scope_level} scope requires scope_entity_id"))),
        "Region" | "Site" | "Building" => Ok(()),
        other =>
            Err(EngineError::bad_request(format!(
                "Invalid scope_level '{other}' — must be Global / Region / Site / Building"))),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn global_scope_rejects_entity_id() {
        assert!(validate_scope("Global", Some(Uuid::new_v4())).is_err());
        assert!(validate_scope("Global", None).is_ok());
    }

    #[test]
    fn non_global_scope_requires_entity_id() {
        assert!(validate_scope("Region", None).is_err());
        assert!(validate_scope("Site", None).is_err());
        assert!(validate_scope("Building", None).is_err());
        assert!(validate_scope("Region", Some(Uuid::new_v4())).is_ok());
    }

    #[test]
    fn invalid_scope_rejected() {
        assert!(validate_scope("Floor", Some(Uuid::new_v4())).is_err());
        assert!(validate_scope("", None).is_err());
    }
}
