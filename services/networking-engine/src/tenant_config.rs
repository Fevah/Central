//! Per-tenant naming configuration. Phase 7b.
//!
//! `net.tenant_naming_config` is a single row per tenant. The only tunable
//! today is `default_separator` — the character seeded into newly-created
//! templates. Existing templates are NOT rewritten when the flag changes;
//! admins rename through overrides if they want a retroactive change.
//!
//! The `apply_default_separator` helper is a pure function so a caller
//! (typically a migration or import job that's creating templates in bulk)
//! can transform a "raw" template into one that uses the tenant's chosen
//! separator without a round-trip through the DB.

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::error::EngineError;

#[derive(Debug, Clone, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
pub struct TenantNamingConfig {
    pub organization_id: Uuid,
    pub default_separator: String,
    pub applied_to_new: bool,
    pub version: i32,
    pub updated_at: DateTime<Utc>,
    pub notes: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpsertTenantConfigBody {
    pub default_separator: String,
    pub applied_to_new: Option<bool>,
    pub notes: Option<String>,
    /// Current version, or 0 for "row doesn't exist yet — create it". The
    /// update path enforces optimistic concurrency; insert races are caught
    /// by the PK and surface as `bad_request`.
    pub version: i32,
}

#[derive(Clone)]
pub struct TenantConfigRepo {
    pool: PgPool,
}

impl TenantConfigRepo {
    pub fn new(pool: PgPool) -> Self { Self { pool } }

    pub async fn get(&self, org_id: Uuid) -> Result<TenantNamingConfig, EngineError> {
        let row: Option<TenantNamingConfig> = sqlx::query_as(
            "SELECT organization_id, default_separator, applied_to_new,
                    version, updated_at, notes
               FROM net.tenant_naming_config
              WHERE organization_id = $1 AND deleted_at IS NULL")
            .bind(org_id)
            .fetch_optional(&self.pool)
            .await?;

        // Defaults make the row effectively present for read purposes — an
        // admin querying before they've ever set a preference sees the
        // same shape as after. Write path still needs to INSERT on first
        // save because there's no row yet to UPDATE.
        Ok(row.unwrap_or(TenantNamingConfig {
            organization_id: org_id,
            default_separator: "-".into(),
            applied_to_new: true,
            version: 0,
            updated_at: Utc::now(),
            notes: None,
        }))
    }

    pub async fn upsert(
        &self,
        org_id: Uuid,
        body: &UpsertTenantConfigBody,
        user_id: Option<i32>,
    ) -> Result<TenantNamingConfig, EngineError> {
        validate_separator(&body.default_separator)?;
        let applied = body.applied_to_new.unwrap_or(true);

        if body.version == 0 {
            // Insert path. If the row already exists (race), surface as bad_request.
            let row: Option<TenantNamingConfig> = sqlx::query_as(
                "INSERT INTO net.tenant_naming_config
                    (organization_id, default_separator, applied_to_new, notes,
                     status, lock_state, created_by, updated_by)
                 VALUES ($1, $2, $3, $4,
                         'Active'::net.entity_status, 'Open'::net.lock_state, $5, $5)
                 ON CONFLICT (organization_id) DO NOTHING
                 RETURNING organization_id, default_separator, applied_to_new,
                           version, updated_at, notes")
                .bind(org_id)
                .bind(&body.default_separator)
                .bind(applied)
                .bind(body.notes.as_deref())
                .bind(user_id)
                .fetch_optional(&self.pool)
                .await?;
            return row.ok_or_else(|| EngineError::bad_request(
                "Tenant naming config already exists — pass the current version to update."));
        }

        let row: Option<TenantNamingConfig> = sqlx::query_as(
            "UPDATE net.tenant_naming_config
                SET default_separator = $2,
                    applied_to_new   = $3,
                    notes            = $4,
                    updated_at       = now(),
                    updated_by       = $5,
                    version          = version + 1
              WHERE organization_id = $1 AND version = $6 AND deleted_at IS NULL
              RETURNING organization_id, default_separator, applied_to_new,
                        version, updated_at, notes")
            .bind(org_id)
            .bind(&body.default_separator)
            .bind(applied)
            .bind(body.notes.as_deref())
            .bind(user_id)
            .bind(body.version)
            .fetch_optional(&self.pool)
            .await?;

        row.ok_or_else(|| EngineError::bad_request(
            "Tenant naming config version mismatch or missing — refresh and retry."))
    }
}

fn validate_separator(s: &str) -> Result<(), EngineError> {
    match s {
        "-" | "_" | "." => Ok(()),
        _ => Err(EngineError::bad_request(
            "default_separator must be one of '-', '_', '.'")),
    }
}

/// Replace every separator character already present in `template` with the
/// tenant's configured `separator`. Idempotent — running twice with the same
/// separator yields the same output. Works on token-boundary separators
/// (between `}` and `{`, or between two literal chars) without touching
/// separators *inside* token names.
///
/// Rule set:
/// - The characters `- _ .` are considered template-level separators.
/// - Sequences of those between text/tokens collapse to a single separator.
/// - Anything inside `{...}` is left alone.
///
/// This is the function the Rust side calls when seeding new templates at
/// tenant-creation time, or when a migration wants to auto-apply a
/// separator change. Existing stored templates stay untouched unless an
/// admin explicitly regenerates.
#[allow(dead_code)]
pub fn apply_default_separator(template: &str, separator: char) -> String {
    if template.is_empty() { return String::new(); }
    let mut out = String::with_capacity(template.len());
    let mut in_token = false;
    let mut last_was_sep = false;

    for ch in template.chars() {
        if in_token {
            out.push(ch);
            if ch == '}' { in_token = false; }
            last_was_sep = false;
            continue;
        }
        if ch == '{' {
            in_token = true;
            out.push(ch);
            last_was_sep = false;
            continue;
        }
        let is_sep = ch == '-' || ch == '_' || ch == '.';
        if is_sep {
            if !last_was_sep {
                out.push(separator);
                last_was_sep = true;
            }
            // Else: collapse consecutive separators.
        } else {
            out.push(ch);
            last_was_sep = false;
        }
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn validate_separator_accepts_three_chars() {
        assert!(validate_separator("-").is_ok());
        assert!(validate_separator("_").is_ok());
        assert!(validate_separator(".").is_ok());
        assert!(validate_separator("|").is_err());
        assert!(validate_separator("").is_err());
        assert!(validate_separator("--").is_err());
    }

    #[test]
    fn apply_sep_noop_when_empty() {
        assert_eq!(apply_default_separator("", '-'), "");
    }

    #[test]
    fn apply_sep_no_tokens_rewrites_all_separators() {
        assert_eq!(apply_default_separator("a-b_c.d", '_'), "a_b_c_d");
    }

    #[test]
    fn apply_sep_collapses_runs() {
        assert_eq!(apply_default_separator("a--b__c", '-'), "a-b-c");
        assert_eq!(apply_default_separator("a-_-b", '.'), "a.b");
    }

    #[test]
    fn apply_sep_idempotent() {
        let out1 = apply_default_separator("{a}-{b}_{c}", '-');
        let out2 = apply_default_separator(&out1, '-');
        assert_eq!(out1, out2);
    }

    #[test]
    fn apply_sep_preserves_tokens() {
        // Token names might contain underscores ({role_code}, {site_code}). The rewrite
        // must not touch anything between `{` and `}`.
        assert_eq!(apply_default_separator("{role_code}-{building_code}", '_'),
                   "{role_code}_{building_code}");
        assert_eq!(apply_default_separator("{role_code}-{instance}", '.'),
                   "{role_code}.{instance}");
    }

    #[test]
    fn apply_sep_handles_mixed_literal_and_tokens() {
        assert_eq!(
            apply_default_separator("x-{a}_b.{c}-y", '_'),
            "x_{a}_b_{c}_y");
    }

    #[test]
    fn apply_sep_leaves_non_separator_chars_alone() {
        assert_eq!(apply_default_separator("AbC123{tok}ZzZ", '-'), "AbC123{tok}ZzZ");
    }
}
