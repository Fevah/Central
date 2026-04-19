//! Bulk import — CSV upload → validate → (optionally) apply pipeline.
//!
//! The counterpart to `bulk_export`. First entity supported is
//! `net.device`; sibling entities follow once the pattern stabilises.
//! Each import request flows through three stages:
//!
//!   1. **Parse** — split the uploaded CSV into rows per RFC 4180.
//!      Malformed CSV short-circuits with a top-level error; partial
//!      parses aren't accepted because row N+1 can't be trusted when
//!      row N's quoting confused the tokenizer.
//!
//!   2. **Validate** — per-row checks (required fields non-empty,
//!      foreign-key references resolve, formats match, status enum
//!      valid). Collects every row's outcome into a structured result
//!      so operators see all problems in one pass rather than fixing-
//!      and-retrying.
//!
//!   3. **Apply** — write the successful rows to the DB inside one
//!      transaction; rollback on any per-row failure so imports are
//!      atomic. Apply is gated on `dry_run = false`; dry-run returns
//!      the validation result without writing anything, matching the
//!      pattern the Immunocore XLSX wizard uses today.
//!
//! This module ships the parse + validate halves for devices plus the
//! response types. The apply half lands in a follow-on slice once the
//! validation shape is proven against real legacy data.
//!
//! ## RFC 4180 parser
//!
//! Hand-rolled rather than pulling in the `csv` crate — we already
//! hand-rolled the escape side in `bulk_export`, and the round-trip
//! invariant wants both sides to live in one module family. The
//! parser handles:
//!   - Commas separating fields within a row
//!   - Double-quoted fields containing embedded commas
//!   - Doubled quotes within a quoted field (`""` → `"`)
//!   - CRLF + LF + CR line terminators (readers-be-liberal)
//!   - Trailing newlines on the last line (optional)

use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::audit::{self, AuditEvent};
use crate::error::EngineError;
use crate::scope_grants;

// ─── Parser ──────────────────────────────────────────────────────────────

/// Parse an RFC 4180 CSV body into `Vec<Vec<String>>` — one inner
/// vector per row. Does not treat the first row as a header; callers
/// that need header handling check `rows[0]` themselves.
///
/// Rejects the input on:
///   - Unterminated quoted field at EOF
///   - Content following a closing quote that isn't a comma or EOL
///
/// Empty input yields an empty vector (not one empty row). Trailing
/// blank lines are skipped so a file ending in `\r\n\r\n` doesn't
/// materialise a phantom empty row.
pub fn parse_csv(body: &str) -> Result<Vec<Vec<String>>, EngineError> {
    let mut rows: Vec<Vec<String>> = Vec::new();
    let mut current_row: Vec<String> = Vec::new();
    let mut current_field = String::new();
    let mut in_quotes = false;
    let mut prev_was_quote_close = false;
    let mut chars = body.chars().peekable();

    while let Some(ch) = chars.next() {
        if in_quotes {
            if ch == '"' {
                if chars.peek() == Some(&'"') {
                    // Doubled quote → literal "
                    chars.next();
                    current_field.push('"');
                } else {
                    // Closing quote.
                    in_quotes = false;
                    prev_was_quote_close = true;
                }
            } else {
                current_field.push(ch);
            }
            continue;
        }

        // Not in quotes.
        match ch {
            '"' => {
                if !current_field.is_empty() || prev_was_quote_close {
                    return Err(EngineError::bad_request(
                        "quote appears mid-field outside a quoted value"));
                }
                in_quotes = true;
                prev_was_quote_close = false;
            }
            ',' => {
                current_row.push(std::mem::take(&mut current_field));
                prev_was_quote_close = false;
            }
            '\r' => {
                // Swallow an immediately-following \n so CRLF ends one row.
                if chars.peek() == Some(&'\n') { chars.next(); }
                current_row.push(std::mem::take(&mut current_field));
                if !is_blank_row(&current_row) { rows.push(std::mem::take(&mut current_row)); }
                else { current_row.clear(); }
                prev_was_quote_close = false;
            }
            '\n' => {
                current_row.push(std::mem::take(&mut current_field));
                if !is_blank_row(&current_row) { rows.push(std::mem::take(&mut current_row)); }
                else { current_row.clear(); }
                prev_was_quote_close = false;
            }
            _ => {
                if prev_was_quote_close {
                    // A closing quote may only be followed by a
                    // delimiter (comma / CRLF / LF / EOF). Anything
                    // else is a user-typo pattern like `"foo"bar`
                    // which would smuggle garbage into the next
                    // field — reject rather than silently accept.
                    return Err(EngineError::bad_request(
                        "content after closing quote must be a delimiter (comma or line break)"));
                }
                current_field.push(ch);
            }
        }
    }

    if in_quotes {
        return Err(EngineError::bad_request(
            "unterminated quoted field at end of input"));
    }
    // Flush the last partial row (no trailing newline).
    if !current_field.is_empty() || !current_row.is_empty() {
        current_row.push(current_field);
        if !is_blank_row(&current_row) { rows.push(current_row); }
    }
    Ok(rows)
}

fn is_blank_row(row: &[String]) -> bool {
    row.iter().all(String::is_empty) || row.is_empty()
}

// ─── Import result types ─────────────────────────────────────────────────

/// Per-row outcome. `ok = true` means the row passed validation and
/// would apply cleanly; `ok = false` means an `errors` list describes
/// what's wrong.
#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ImportRowOutcome {
    /// 1-based row index matching what a spreadsheet displays (header
    /// row is row 1, first data row is row 2). Easier to correlate
    /// than 0-based indices when operators are fixing a file.
    pub row_number: usize,
    pub ok: bool,
    pub errors: Vec<String>,
    /// Identifier column echoed back so operators see which record
    /// failed without counting rows (hostname for devices, vlan_id
    /// for VLANs, etc.).
    pub identifier: String,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ImportValidationResult {
    pub total_rows: usize,
    pub valid: usize,
    pub invalid: usize,
    pub dry_run: bool,
    pub applied: bool,
    pub outcomes: Vec<ImportRowOutcome>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ImportQuery {
    pub organization_id: Uuid,
    #[serde(default = "default_dry_run")]
    pub dry_run: bool,
    /// Import mode. `create` (default) — every row must be new;
    /// existing hostnames error out per-row ("already exists").
    /// `upsert` — existing rows UPDATE with version-check; missing
    /// rows INSERT. The CSV's `version` column is the snapshot
    /// the update runs against, so stale versions surface as a
    /// concurrent-write row error.
    ///
    /// Not an enum in the DTO because the raw query string doesn't
    /// serialise ergonomically to a Rust enum via serde — keeping
    /// it as a String lets the validator produce a helpful error
    /// on typos ("`mode=upser` is not a valid mode").
    #[serde(default)]
    pub mode: Option<String>,
}

fn default_dry_run() -> bool { true }

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ImportMode {
    /// Every row must be new. Duplicate rows surface as a per-row
    /// "already exists — update mode not yet supported" error.
    /// Default — matches the original bulk-import contract.
    Create,
    /// Existing rows UPDATE with version-check; missing rows
    /// INSERT. Operators use this for "export → edit in Excel →
    /// re-import" round-trips.
    Upsert,
}

impl ImportMode {
    pub fn parse(raw: Option<&str>) -> Result<Self, EngineError> {
        match raw.map(str::trim).filter(|s| !s.is_empty()) {
            None | Some("create") => Ok(Self::Create),
            Some("upsert")        => Ok(Self::Upsert),
            Some(other)           => Err(EngineError::bad_request(format!(
                "mode '{other}' must be one of: create, upsert"))),
        }
    }
}

// ─── Device import — validate-only (apply lands in a follow-on slice) ────

/// Expected column order for devices. Mirrors the shape the device
/// export produces so round-trip is a no-op: export → save as xlsx →
/// edit a cell → import → same rows come back.
const DEVICE_COLUMNS: &[&str] = &[
    "hostname", "role_code", "building_code", "site_code",
    "management_ip", "asn", "status", "version",
];

/// Parse + per-row validate (and, when `dry_run = false`, apply) a
/// CSV import payload of devices. Same return shape for both modes so
/// UI callers get a consistent response to drive the summary banner.
///
/// ## Apply semantics
///
/// **Create-only.** Rows whose hostname already exists in the
/// tenant render as failed outcomes ("device exists — update mode
/// not yet supported"). Update-on-upsert lands in a follow-on slice
/// once we've decided how to surface the optimistic-concurrency
/// `version` column from the CSV (today it's informational only —
/// the export emits it but apply ignores it).
///
/// **Transactional.** If ANY row fails the INSERT (e.g. a DB
/// constraint bites despite validation), the whole transaction
/// rolls back and `applied = false` comes back. That matches the
/// spreadsheet-import mental model: one bad row fails the whole
/// file; operators fix the file and retry.
///
/// **ASN ignored on apply.** The CSV carries an `asn` column for
/// human reference, but resolving it to an `asn_allocation_id` FK
/// requires choosing a block + calling the allocation service —
/// which the bulk-import flow doesn't do. Operators use the
/// allocation CRUD panel for ASN assignments; the imported device
/// starts with `asn_allocation_id = NULL`.
pub async fn import_devices(
    pool: &PgPool,
    org_id: Uuid,
    body: &str,
    dry_run: bool,
    mode: ImportMode,
    user_id: Option<i32>,
) -> Result<ImportValidationResult, EngineError> {
    // RBAC — both create and upsert are `write:Device`. For creates
    // the entity doesn't exist yet so hierarchy-scoped grants can't
    // resolve; Global is the only thing that matches. For upsert
    // the "update existing" branch COULD check per-row hierarchy,
    // but that'd split into two auth checks per row (Global
    // upfront vs per-row scope later). Easier rule: bulk import
    // always requires Global write, same for both modes. Operators
    // with finer-grained access use the single-row CRUD paths.
    scope_grants::require_permission(
        pool, org_id, user_id, "write", "Device", None,
    ).await?;

    let rows = parse_csv(body)?;
    if rows.is_empty() {
        return Ok(ImportValidationResult {
            total_rows: 0, valid: 0, invalid: 0,
            dry_run, applied: false, outcomes: vec![],
        });
    }

    // Enforce the header shape. Making the parser tolerant of column
    // order drift (looking up by name rather than position) is a
    // follow-on; for now the import must match what the export emits.
    let header = &rows[0];
    if header.len() != DEVICE_COLUMNS.len()
        || !header.iter().zip(DEVICE_COLUMNS).all(|(a,b)| a.eq_ignore_ascii_case(b))
    {
        return Err(EngineError::bad_request(format!(
            "device import header must be: {}", DEVICE_COLUMNS.join(","))));
    }

    // Pre-fetch the tenant's role codes + building codes so per-row
    // foreign-key validation stays one query-per-import rather than
    // one query-per-row. For apply mode we also need the code→uuid
    // mapping to actually INSERT; build both shapes from the same
    // query result.
    let role_rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, role_code FROM net.device_role
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let role_codes: std::collections::HashSet<String> =
        role_rows.iter().map(|(_, c)| c.clone()).collect();
    let role_code_to_id: std::collections::HashMap<String, Uuid> =
        role_rows.into_iter().map(|(id, c)| (c, id)).collect();

    let building_rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, building_code FROM net.building
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let building_codes: std::collections::HashSet<String> =
        building_rows.iter().map(|(_, c)| c.clone()).collect();
    let building_code_to_id: std::collections::HashMap<String, Uuid> =
        building_rows.into_iter().map(|(id, c)| (c, id)).collect();

    // Existing devices keyed by hostname so the apply loop can
    // distinguish "new row → INSERT" from "existing row → UPDATE"
    // without another query per row. We also grab the id + version
    // so the UPDATE's version-check has the snapshot the row was
    // based on.
    let existing_rows: Vec<(String, Uuid, i32)> = sqlx::query_as(
        "SELECT hostname, id, version FROM net.device
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let existing_by_hostname: std::collections::HashMap<String, (Uuid, i32)> =
        existing_rows.into_iter().map(|(h, id, v)| (h, (id, v))).collect();

    let mut outcomes: Vec<ImportRowOutcome> = Vec::with_capacity(rows.len().saturating_sub(1));
    for (i, row) in rows.iter().enumerate().skip(1) {
        let row_number = i + 1;
        let mut outcome = validate_device_row(row, row_number, &role_codes, &building_codes);
        // Create mode: existing hostname is a per-row error.
        // Upsert mode: existing hostname is expected — it's an
        // update target, no error.
        if outcome.ok && existing_by_hostname.contains_key(&outcome.identifier)
            && mode == ImportMode::Create
        {
            outcome.ok = false;
            outcome.errors.push(
                "device with this hostname already exists — use mode=upsert to update existing rows"
                .to_string());
        }
        outcomes.push(outcome);
    }

    let valid   = outcomes.iter().filter(|o| o.ok).count();
    let invalid = outcomes.len() - valid;

    // Dry-run or any invalid row → return without touching the DB.
    if dry_run || invalid > 0 {
        return Ok(ImportValidationResult {
            total_rows: outcomes.len(),
            valid, invalid,
            dry_run, applied: false,
            outcomes,
        });
    }

    // Apply path — every row passed validation. For each row:
    //   - new hostname (both create + upsert modes) → INSERT
    //   - existing hostname (upsert mode only; create mode already
    //     flagged the row as a validation error so we won't reach
    //     here) → UPDATE with version-check; version column on the
    //     row is the snapshot the update runs against
    // Any DB failure rolls back the whole batch.
    let mut tx = pool.begin().await?;
    for (outcome_idx, row) in rows.iter().enumerate().skip(1).map(|(_, r)| r).enumerate() {
        let hostname      = row[0].trim();
        let role_code     = row[1].trim();
        let building_code = row[2].trim();
        let management_ip = row[4].trim();
        let status        = row[6].trim();
        let version_str   = row[7].trim();

        let role_id     = if role_code.is_empty()     { None } else { role_code_to_id.get(role_code).copied() };
        let building_id = if building_code.is_empty() { None } else { building_code_to_id.get(building_code).copied() };
        let status_val  = if status.is_empty() { "Planned" } else { status };
        let mgmt_ip_opt: Option<&str> = if management_ip.is_empty() { None } else { Some(management_ip) };

        let existing = existing_by_hostname.get(hostname).copied();
        let (device_id, action) = match (existing, mode) {
            // UPSERT path — existing row gets UPDATE with version check.
            (Some((id, db_version)), ImportMode::Upsert) => {
                // Operator's CSV version (what they edited against).
                // Empty version cell → fall back to the DB's current
                // version, treating "I don't know" as "don't bother
                // version-checking me" — the common case for CSVs
                // hand-written rather than exported from the system.
                let expected_version: i32 = version_str.parse().unwrap_or(db_version);

                let update_result: Result<Option<(Uuid,)>, sqlx::Error> = sqlx::query_as(
                    "UPDATE net.device
                        SET device_role_id = COALESCE($3, device_role_id),
                            building_id    = COALESCE($4, building_id),
                            management_ip  = CASE WHEN $5::text IS NULL
                                                  THEN management_ip
                                                  ELSE $5::inet END,
                            status         = $6::net.entity_status,
                            updated_at     = now(),
                            updated_by     = $7,
                            version        = version + 1
                      WHERE id = $1 AND organization_id = $2
                        AND version = $8 AND deleted_at IS NULL
                      RETURNING id")
                    .bind(id)
                    .bind(org_id)
                    .bind(role_id)
                    .bind(building_id)
                    .bind(mgmt_ip_opt)
                    .bind(status_val)
                    .bind(user_id)
                    .bind(expected_version)
                    .fetch_optional(&mut *tx).await;

                match update_result {
                    Ok(Some((updated_id,))) => (updated_id, "Updated"),
                    Ok(None) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!(
                            "version mismatch — CSV had version {expected_version} but DB is at {db_version} (someone else edited this device)"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                    Err(e) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!("database UPDATE failed: {e}"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                }
            }
            // INSERT path — create mode (always) or upsert mode
            // for a new hostname (existing is None).
            _ => {
                let insert_result: Result<(Uuid,), sqlx::Error> = sqlx::query_as(
                    "INSERT INTO net.device
                        (organization_id, device_role_id, building_id,
                         hostname, management_ip, status, created_by, updated_by)
                     VALUES ($1, $2, $3, $4, $5::inet, $6::net.entity_status, $7, $7)
                     RETURNING id")
                    .bind(org_id)
                    .bind(role_id)
                    .bind(building_id)
                    .bind(hostname)
                    .bind(mgmt_ip_opt)
                    .bind(status_val)
                    .bind(user_id)
                    .fetch_one(&mut *tx).await;

                match insert_result {
                    Ok((id,)) => (id, "Created"),
                    Err(e) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!("database INSERT failed: {e}"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                }
            }
        };

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "Device",
            entity_id: Some(device_id),
            // Created / Updated — audit consumers can distinguish
            // "new via bulk_import" from "modified via bulk_import"
            // by the action column + the source=bulk_import tag.
            action,
            actor_user_id: user_id,
            actor_display: None,
            client_ip: None,
            correlation_id: None,
            details: serde_json::json!({
                "source": "bulk_import",
                "mode": match mode {
                    ImportMode::Create => "create",
                    ImportMode::Upsert => "upsert",
                },
                "hostname": hostname,
                "role_code": role_code,
                "building_code": building_code,
            }),
        }).await?;
    }
    tx.commit().await?;

    Ok(ImportValidationResult {
        total_rows: outcomes.len(),
        valid, invalid: 0,
        dry_run: false, applied: true,
        outcomes,
    })
}

/// Pure per-row validator — kept out of `import_devices` so tests can
/// exercise it without a DB. The FK existence sets come in as
/// pre-computed HashSets rather than live queries.
fn validate_device_row(
    row: &[String],
    row_number: usize,
    role_codes: &std::collections::HashSet<String>,
    building_codes: &std::collections::HashSet<String>,
) -> ImportRowOutcome {
    let mut errors = Vec::new();

    if row.len() != DEVICE_COLUMNS.len() {
        errors.push(format!("expected {} columns, got {}", DEVICE_COLUMNS.len(), row.len()));
        return ImportRowOutcome {
            row_number, ok: false, errors,
            identifier: row.first().cloned().unwrap_or_default(),
        };
    }

    let hostname      = row[0].trim();
    let role_code     = row[1].trim();
    let building_code = row[2].trim();
    // site_code is informational only (derivable from building); we
    // don't cross-check to avoid rejecting rows where the operator
    // pasted in a recently-renamed site code that's still valid upstream.
    let management_ip = row[4].trim();
    let asn           = row[5].trim();
    let status        = row[6].trim();
    let version       = row[7].trim();

    if hostname.is_empty() {
        errors.push("hostname is required".into());
    }
    if !role_code.is_empty() && !role_codes.contains(role_code) {
        errors.push(format!("role_code '{role_code}' not found in this tenant's device_role catalog"));
    }
    if !building_code.is_empty() && !building_codes.contains(building_code) {
        errors.push(format!("building_code '{building_code}' not found in this tenant's building catalog"));
    }
    if !management_ip.is_empty() {
        // Accept bare host or host/prefix — matches what the export
        // emits and what PicOS + most ops tools actually use.
        let ip_part = management_ip.split('/').next().unwrap_or(management_ip);
        if ip_part.parse::<std::net::IpAddr>().is_err() {
            errors.push(format!("management_ip '{management_ip}' is not a valid IP address"));
        }
    }
    if !asn.is_empty() && asn.parse::<i64>().is_err() {
        errors.push(format!("asn '{asn}' is not a valid integer"));
    }
    if !status.is_empty() &&
        !matches!(status, "Planned"|"Reserved"|"Active"|"Deprecated"|"Retired")
    {
        errors.push(format!("status '{status}' must be one of Planned/Reserved/Active/Deprecated/Retired"));
    }
    if !version.is_empty() && version.parse::<i32>().is_err() {
        errors.push(format!("version '{version}' is not a valid integer"));
    }

    ImportRowOutcome {
        row_number,
        ok: errors.is_empty(),
        errors,
        identifier: hostname.to_string(),
    }
}

// ─── VLAN import ─────────────────────────────────────────────────────────

/// Column order mirrors `bulk_export::export_vlans_csv` so exports
/// round-trip cleanly through the import.
const VLAN_COLUMNS: &[&str] = &[
    "vlan_id", "display_name", "description", "scope_level",
    "template_code", "block_code", "status",
];

/// Validate + optionally apply a CSV bulk import of VLANs. Same
/// shape as `import_devices`: dry-run is the default, apply is
/// create-only + transactional.
///
/// `block_code` is **required** (VLANs can't exist without a parent
/// block); `template_code` is optional; `description` / `scope_level`
/// / `status` have sensible defaults when empty.
pub async fn import_vlans(
    pool: &PgPool,
    org_id: Uuid,
    body: &str,
    dry_run: bool,
    mode: ImportMode,
    user_id: Option<i32>,
) -> Result<ImportValidationResult, EngineError> {
    scope_grants::require_permission(
        pool, org_id, user_id, "write", "Vlan", None,
    ).await?;
    let rows = parse_csv(body)?;
    if rows.is_empty() {
        return Ok(ImportValidationResult {
            total_rows: 0, valid: 0, invalid: 0,
            dry_run, applied: false, outcomes: vec![],
        });
    }

    let header = &rows[0];
    if header.len() != VLAN_COLUMNS.len()
        || !header.iter().zip(VLAN_COLUMNS).all(|(a,b)| a.eq_ignore_ascii_case(b))
    {
        return Err(EngineError::bad_request(format!(
            "vlan import header must be: {}", VLAN_COLUMNS.join(","))));
    }

    // Pre-fetch FK dimensions — block + template codes.
    let block_rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, block_code FROM net.vlan_block
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let block_codes: std::collections::HashSet<String> =
        block_rows.iter().map(|(_, c)| c.clone()).collect();
    let block_code_to_id: std::collections::HashMap<String, Uuid> =
        block_rows.into_iter().map(|(id, c)| (c, id)).collect();

    let template_rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, template_code FROM net.vlan_template
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let template_codes: std::collections::HashSet<String> =
        template_rows.iter().map(|(_, c)| c.clone()).collect();
    let template_code_to_id: std::collections::HashMap<String, Uuid> =
        template_rows.into_iter().map(|(id, c)| (c, id)).collect();

    // Existing (block_code, vlan_id) → (id, version) so the apply
    // loop can branch INSERT vs version-checked UPDATE without
    // another query per row.
    let existing_rows: Vec<(String, i32, Uuid, i32)> = sqlx::query_as(
        "SELECT b.block_code, v.vlan_id, v.id, v.version
           FROM net.vlan v
           JOIN net.vlan_block b ON b.id = v.block_id AND b.deleted_at IS NULL
          WHERE v.organization_id = $1 AND v.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let existing_by_pair: std::collections::HashMap<String, (Uuid, i32)> =
        existing_rows.into_iter()
            .map(|(c, vid, id, ver)| (format!("{c}|{vid}"), (id, ver)))
            .collect();
    let existing_pairs_set: std::collections::HashSet<String> =
        existing_by_pair.keys().cloned().collect();

    // Suppress the "already exists" validator error when we're
    // upserting — existing rows are valid update targets in that
    // mode. In create mode the populated set drives the per-row
    // error.
    let empty_set = std::collections::HashSet::new();
    let dup_check_set = match mode {
        ImportMode::Create => &existing_pairs_set,
        ImportMode::Upsert => &empty_set,
    };

    let mut outcomes: Vec<ImportRowOutcome> = Vec::with_capacity(rows.len().saturating_sub(1));
    for (i, row) in rows.iter().enumerate().skip(1) {
        let row_number = i + 1;
        let outcome = validate_vlan_row(
            row, row_number, &block_codes, &template_codes, dup_check_set,
        );
        outcomes.push(outcome);
    }

    let valid   = outcomes.iter().filter(|o| o.ok).count();
    let invalid = outcomes.len() - valid;

    if dry_run || invalid > 0 {
        return Ok(ImportValidationResult {
            total_rows: outcomes.len(),
            valid, invalid,
            dry_run, applied: false, outcomes,
        });
    }

    let mut tx = pool.begin().await?;
    for (outcome_idx, row) in rows.iter().enumerate().skip(1).map(|(_, r)| r).enumerate() {
        let vlan_id_num: i32 = row[0].trim().parse().unwrap_or(0);  // validator checked
        let display_name  = row[1].trim();
        let description   = row[2].trim();
        let scope_level   = row[3].trim();
        let template_code = row[4].trim();
        let block_code    = row[5].trim();
        let status        = row[6].trim();

        let block_id    = block_code_to_id.get(block_code).copied()
            .expect("validator enforced block_code exists");
        let template_id = if template_code.is_empty() { None }
                          else { template_code_to_id.get(template_code).copied() };
        let desc_opt: Option<&str>  = if description.is_empty() { None } else { Some(description) };
        let scope_val   = if scope_level.is_empty() { "Free" }     else { scope_level };
        let status_val  = if status.is_empty()      { "Active" }   else { status };

        let pair_key = format!("{block_code}|{vlan_id_num}");
        let existing = existing_by_pair.get(&pair_key).copied();
        let (vlan_uuid, action) = match (existing, mode) {
            // UPSERT path — version-checked UPDATE for the existing
            // (block, vlan_id) pair. CSV's version column is the
            // operator's snapshot.
            (Some((id, db_version)), ImportMode::Upsert) => {
                // Note: vlan import header doesn't currently include
                // a version column (only 7 cols vs devices' 8). Fall
                // back to current DB version — same "I don't know,
                // just apply" semantic devices use for blank versions.
                let expected_version = db_version;

                let update_result: Result<Option<(Uuid,)>, sqlx::Error> = sqlx::query_as(
                    "UPDATE net.vlan
                        SET template_id  = $3,
                            display_name = $4,
                            description  = $5,
                            scope_level  = $6,
                            status       = $7::net.entity_status,
                            updated_at   = now(),
                            updated_by   = $8,
                            version      = version + 1
                      WHERE id = $1 AND organization_id = $2
                        AND version = $9 AND deleted_at IS NULL
                      RETURNING id")
                    .bind(id).bind(org_id)
                    .bind(template_id)
                    .bind(display_name)
                    .bind(desc_opt)
                    .bind(scope_val)
                    .bind(status_val)
                    .bind(user_id)
                    .bind(expected_version)
                    .fetch_optional(&mut *tx).await;

                match update_result {
                    Ok(Some((updated_id,))) => (updated_id, "Updated"),
                    Ok(None) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!(
                            "version mismatch — DB advanced past version {expected_version} between read and write"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                    Err(e) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!("database UPDATE failed: {e}"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                }
            }
            // INSERT path — Create (always) or Upsert with no
            // existing match.
            _ => {
                let insert_result: Result<(Uuid,), sqlx::Error> = sqlx::query_as(
                    "INSERT INTO net.vlan
                        (organization_id, block_id, template_id, vlan_id,
                         display_name, description, scope_level, status,
                         created_by, updated_by)
                     VALUES ($1, $2, $3, $4, $5, $6, $7, $8::net.entity_status, $9, $9)
                     RETURNING id")
                    .bind(org_id).bind(block_id).bind(template_id).bind(vlan_id_num)
                    .bind(display_name).bind(desc_opt).bind(scope_val).bind(status_val)
                    .bind(user_id)
                    .fetch_one(&mut *tx).await;

                match insert_result {
                    Ok((id,)) => (id, "Created"),
                    Err(e) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!("database INSERT failed: {e}"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                }
            }
        };

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "Vlan",
            entity_id: Some(vlan_uuid),
            action,
            actor_user_id: user_id,
            actor_display: None, client_ip: None, correlation_id: None,
            details: serde_json::json!({
                "source": "bulk_import",
                "mode": match mode {
                    ImportMode::Create => "create",
                    ImportMode::Upsert => "upsert",
                },
                "vlan_id": vlan_id_num,
                "block_code": block_code,
            }),
        }).await?;
    }
    tx.commit().await?;

    Ok(ImportValidationResult {
        total_rows: outcomes.len(),
        valid, invalid: 0,
        dry_run: false, applied: true, outcomes,
    })
}

fn validate_vlan_row(
    row: &[String],
    row_number: usize,
    block_codes: &std::collections::HashSet<String>,
    template_codes: &std::collections::HashSet<String>,
    existing_pairs: &std::collections::HashSet<String>,
) -> ImportRowOutcome {
    let mut errors = Vec::new();

    if row.len() != VLAN_COLUMNS.len() {
        errors.push(format!("expected {} columns, got {}", VLAN_COLUMNS.len(), row.len()));
        return ImportRowOutcome {
            row_number, ok: false, errors,
            identifier: row.first().cloned().unwrap_or_default(),
        };
    }

    let vlan_id_str   = row[0].trim();
    let display_name  = row[1].trim();
    let scope_level   = row[3].trim();
    let template_code = row[4].trim();
    let block_code    = row[5].trim();
    let status        = row[6].trim();

    // vlan_id: parse + range.
    let vlan_id_ok = vlan_id_str.parse::<i32>().ok();
    match vlan_id_ok {
        None    => errors.push(format!("vlan_id '{vlan_id_str}' is not a valid integer")),
        Some(n) if !(1..=4094).contains(&n) =>
            errors.push(format!("vlan_id {n} is outside the 1-4094 range")),
        Some(_) => {}
    }
    if display_name.is_empty() {
        errors.push("display_name is required".into());
    }
    if block_code.is_empty() {
        errors.push("block_code is required".into());
    } else if !block_codes.contains(block_code) {
        errors.push(format!("block_code '{block_code}' not found in this tenant's vlan_block catalog"));
    }
    if !template_code.is_empty() && !template_codes.contains(template_code) {
        errors.push(format!("template_code '{template_code}' not found in this tenant's vlan_template catalog"));
    }
    if !scope_level.is_empty() &&
        !matches!(scope_level, "Free"|"Region"|"Site"|"Building"|"Device")
    {
        errors.push(format!("scope_level '{scope_level}' must be Free/Region/Site/Building/Device"));
    }
    if !status.is_empty() &&
        !matches!(status, "Planned"|"Reserved"|"Active"|"Deprecated"|"Retired")
    {
        errors.push(format!("status '{status}' must be Planned/Reserved/Active/Deprecated/Retired"));
    }
    // Dup check — only if vlan_id parsed AND block_code resolved.
    if let (Some(n), true) = (vlan_id_ok, !block_code.is_empty() && block_codes.contains(block_code)) {
        let key = format!("{block_code}|{n}");
        if existing_pairs.contains(&key) {
            errors.push(format!(
                "vlan {n} already exists in block '{block_code}' — update mode not yet supported"));
        }
    }

    ImportRowOutcome {
        row_number,
        ok: errors.is_empty(),
        errors,
        identifier: vlan_id_str.to_string(),
    }
}

// ─── Subnet import ───────────────────────────────────────────────────────

/// Column order mirrors `bulk_export::export_subnets_csv`.
///
/// `scope_entity_code` is the human-readable path to the row's
/// scope entity. Shape depends on `scope_level`:
///
///   Free      → empty
///   Building  → `BUILDING_CODE` (unique per tenant)
///   Floor     → `BUILDING_CODE/FLOOR_CODE` (compound — floor_code
///               isn't unique across buildings)
///   Room      → `BUILDING_CODE/FLOOR_CODE/ROOM_CODE`
///
/// Region and Site scopes are permitted by the schema CHECK but
/// not resolvable from the CSV in this slice — operators needing
/// region/site-scoped subnets can still create them via the CRUD
/// panel, which writes scope_entity_id directly.
const SUBNET_COLUMNS: &[&str] = &[
    "subnet_code", "display_name", "network", "vlan_id",
    "pool_code", "scope_level", "scope_entity_code", "status",
];

/// Validate + optionally apply a CSV bulk import of subnets. Same
/// semantics as the device + VLAN importers.
///
/// `pool_code` is required (subnets can't exist without a parent
/// pool); `network` is required + must be a valid CIDR; `subnet_code`
/// is required and must be unique per-tenant.
///
/// `scope_entity_code` is optional when `scope_level` is Free;
/// required and resolved against the tenant's building / floor /
/// room catalog for those scopes.
///
/// **vlan_id is ignored on apply.** Multi-block tenants can have the
/// same numeric vlan_id in multiple blocks, making the resolution
/// ambiguous from a numeric tag alone. Same "ignored-on-apply"
/// semantic as ASN on the device importer — operators carry the
/// column for human reference + wire it up via the CRUD panel.
pub async fn import_subnets(
    pool: &PgPool,
    org_id: Uuid,
    body: &str,
    dry_run: bool,
    mode: ImportMode,
    user_id: Option<i32>,
) -> Result<ImportValidationResult, EngineError> {
    scope_grants::require_permission(
        pool, org_id, user_id, "write", "Subnet", None,
    ).await?;
    let rows = parse_csv(body)?;
    if rows.is_empty() {
        return Ok(ImportValidationResult {
            total_rows: 0, valid: 0, invalid: 0,
            dry_run, applied: false, outcomes: vec![],
        });
    }

    let header = &rows[0];
    if header.len() != SUBNET_COLUMNS.len()
        || !header.iter().zip(SUBNET_COLUMNS).all(|(a,b)| a.eq_ignore_ascii_case(b))
    {
        return Err(EngineError::bad_request(format!(
            "subnet import header must be: {}", SUBNET_COLUMNS.join(","))));
    }

    let pool_rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, pool_code FROM net.ip_pool
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let pool_codes: std::collections::HashSet<String> =
        pool_rows.iter().map(|(_, c)| c.clone()).collect();
    let pool_code_to_id: std::collections::HashMap<String, Uuid> =
        pool_rows.into_iter().map(|(id, c)| (c, id)).collect();

    // Hierarchy catalogs for scope_entity_code resolution.
    //
    // building_code is globally unique per tenant (UNIQUE (org,
    // building_code)), so that's the map key. Floor + Room compound
    // keys join up through the parent hierarchy so we can resolve
    // "BUILDING_CODE/FLOOR_CODE" / "…/ROOM_CODE" without an extra
    // per-row lookup.
    let building_rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, building_code FROM net.building
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let building_code_to_id: std::collections::HashMap<String, Uuid> =
        building_rows.into_iter().map(|(id, c)| (c, id)).collect();

    let floor_rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT f.id, b.building_code, f.floor_code
           FROM net.floor f
           JOIN net.building b ON b.id = f.building_id AND b.deleted_at IS NULL
          WHERE f.organization_id = $1 AND f.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let floor_code_to_id: std::collections::HashMap<String, Uuid> =
        floor_rows.into_iter()
            .map(|(id, bldg, fl)| (format!("{bldg}/{fl}"), id))
            .collect();

    let room_rows: Vec<(Uuid, String, String, String)> = sqlx::query_as(
        "SELECT r.id, b.building_code, f.floor_code, r.room_code
           FROM net.room r
           JOIN net.floor    f ON f.id = r.floor_id      AND f.deleted_at IS NULL
           JOIN net.building b ON b.id = f.building_id   AND b.deleted_at IS NULL
          WHERE r.organization_id = $1 AND r.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let room_code_to_id: std::collections::HashMap<String, Uuid> =
        room_rows.into_iter()
            .map(|(id, bldg, fl, rm)| (format!("{bldg}/{fl}/{rm}"), id))
            .collect();

    // existing subnet_code → (id, version) so the apply loop can
    // version-check on upsert.
    let existing_rows: Vec<(String, Uuid, i32)> = sqlx::query_as(
        "SELECT subnet_code, id, version FROM net.subnet
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let existing_by_code: std::collections::HashMap<String, (Uuid, i32)> =
        existing_rows.into_iter().map(|(c, id, v)| (c, (id, v))).collect();
    let existing_codes_set: std::collections::HashSet<String> =
        existing_by_code.keys().cloned().collect();

    // Same suppression trick as VLAN — the validator's "already
    // exists" error is only useful in create mode. Upsert treats
    // existing rows as legitimate update targets.
    let empty_set = std::collections::HashSet::new();
    let dup_check_set = match mode {
        ImportMode::Create => &existing_codes_set,
        ImportMode::Upsert => &empty_set,
    };

    let mut outcomes: Vec<ImportRowOutcome> = Vec::with_capacity(rows.len().saturating_sub(1));
    for (i, row) in rows.iter().enumerate().skip(1) {
        let row_number = i + 1;
        outcomes.push(validate_subnet_row(
            row, row_number, &pool_codes, dup_check_set,
            &building_code_to_id, &floor_code_to_id, &room_code_to_id,
        ));
    }

    let valid   = outcomes.iter().filter(|o| o.ok).count();
    let invalid = outcomes.len() - valid;

    if dry_run || invalid > 0 {
        return Ok(ImportValidationResult {
            total_rows: outcomes.len(),
            valid, invalid,
            dry_run, applied: false, outcomes,
        });
    }

    let mut tx = pool.begin().await?;
    for (outcome_idx, row) in rows.iter().enumerate().skip(1).map(|(_, r)| r).enumerate() {
        let subnet_code       = row[0].trim();
        let display_name      = row[1].trim();
        let network           = row[2].trim();
        // row[3] is vlan_id — carried by the CSV for reference,
        // ignored on apply (see module docs).
        let pool_code         = row[4].trim();
        let scope_level       = row[5].trim();
        let scope_entity_code = row[6].trim();
        let status            = row[7].trim();

        let pool_id_val = pool_code_to_id.get(pool_code).copied()
            .expect("validator enforced pool_code exists");
        let scope_val   = if scope_level.is_empty() { "Free" }   else { scope_level };
        let status_val  = if status.is_empty()      { "Active" } else { status };

        // Validator has already checked that scope_entity_code matches
        // scope_level + resolves against the tenant's catalog. Rehit
        // the maps here to get the uuid.
        let scope_entity_id: Option<Uuid> = match scope_val {
            "Free"                          => None,
            "Building" if !scope_entity_code.is_empty() =>
                building_code_to_id.get(scope_entity_code).copied(),
            "Floor"    if !scope_entity_code.is_empty() =>
                floor_code_to_id.get(scope_entity_code).copied(),
            "Room"     if !scope_entity_code.is_empty() =>
                room_code_to_id.get(scope_entity_code).copied(),
            _ => None,
        };

        let existing = existing_by_code.get(subnet_code).copied();
        let (subnet_id, action) = match (existing, mode) {
            (Some((id, db_version)), ImportMode::Upsert) => {
                // Subnet upsert: update display_name, network,
                // scope_level, scope_entity_id, status. subnet_code
                // stays as the identifier; pool_id stays put (re-
                // parenting a subnet between pools is a CRUD-only
                // operation).
                let update_result: Result<Option<(Uuid,)>, sqlx::Error> = sqlx::query_as(
                    "UPDATE net.subnet
                        SET display_name     = $3,
                            network          = $4::cidr,
                            scope_level      = $5,
                            scope_entity_id  = $6,
                            status           = $7::net.entity_status,
                            updated_at       = now(),
                            updated_by       = $8,
                            version          = version + 1
                      WHERE id = $1 AND organization_id = $2
                        AND version = $9 AND deleted_at IS NULL
                      RETURNING id")
                    .bind(id).bind(org_id)
                    .bind(display_name).bind(network)
                    .bind(scope_val).bind(scope_entity_id)
                    .bind(status_val)
                    .bind(user_id).bind(db_version)
                    .fetch_optional(&mut *tx).await;

                match update_result {
                    Ok(Some((updated_id,))) => (updated_id, "Updated"),
                    Ok(None) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!(
                            "version mismatch — DB advanced past version {db_version} between read and write"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                    Err(e) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!("database UPDATE failed: {e}"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                }
            }
            _ => {
                let insert_result: Result<(Uuid,), sqlx::Error> = sqlx::query_as(
                    "INSERT INTO net.subnet
                        (organization_id, pool_id, subnet_code, display_name,
                         network, scope_level, scope_entity_id, status,
                         created_by, updated_by)
                     VALUES ($1, $2, $3, $4, $5::cidr, $6, $7,
                             $8::net.entity_status, $9, $9)
                     RETURNING id")
                    .bind(org_id).bind(pool_id_val).bind(subnet_code).bind(display_name)
                    .bind(network).bind(scope_val).bind(scope_entity_id)
                    .bind(status_val).bind(user_id)
                    .fetch_one(&mut *tx).await;

                match insert_result {
                    Ok((id,)) => (id, "Created"),
                    Err(e) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!("database INSERT failed: {e}"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                }
            }
        };

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "Subnet",
            entity_id: Some(subnet_id),
            action,
            actor_user_id: user_id,
            actor_display: None, client_ip: None, correlation_id: None,
            details: serde_json::json!({
                "source": "bulk_import",
                "mode": match mode {
                    ImportMode::Create => "create",
                    ImportMode::Upsert => "upsert",
                },
                "subnet_code": subnet_code,
                "network": network,
                "pool_code": pool_code,
            }),
        }).await?;
    }
    tx.commit().await?;

    Ok(ImportValidationResult {
        total_rows: outcomes.len(),
        valid, invalid: 0,
        dry_run: false, applied: true, outcomes,
    })
}

fn validate_subnet_row(
    row: &[String],
    row_number: usize,
    pool_codes: &std::collections::HashSet<String>,
    existing_codes: &std::collections::HashSet<String>,
    building_code_to_id: &std::collections::HashMap<String, Uuid>,
    floor_code_to_id: &std::collections::HashMap<String, Uuid>,
    room_code_to_id: &std::collections::HashMap<String, Uuid>,
) -> ImportRowOutcome {
    let mut errors = Vec::new();

    if row.len() != SUBNET_COLUMNS.len() {
        errors.push(format!("expected {} columns, got {}", SUBNET_COLUMNS.len(), row.len()));
        return ImportRowOutcome {
            row_number, ok: false, errors,
            identifier: row.first().cloned().unwrap_or_default(),
        };
    }

    let subnet_code       = row[0].trim();
    let display_name      = row[1].trim();
    let network           = row[2].trim();
    let pool_code         = row[4].trim();
    let scope_level       = row[5].trim();
    let scope_entity_code = row[6].trim();
    let status            = row[7].trim();

    if subnet_code.is_empty() {
        errors.push("subnet_code is required".into());
    } else if existing_codes.contains(subnet_code) {
        errors.push(format!(
            "subnet_code '{subnet_code}' already exists — pass mode=upsert to update existing rows"));
    }
    if display_name.is_empty() {
        errors.push("display_name is required".into());
    }
    if network.is_empty() {
        errors.push("network is required".into());
    } else if network.parse::<ipnetwork::IpNetwork>().is_err() {
        errors.push(format!("network '{network}' is not a valid CIDR"));
    }
    if pool_code.is_empty() {
        errors.push("pool_code is required".into());
    } else if !pool_codes.contains(pool_code) {
        errors.push(format!("pool_code '{pool_code}' not found in this tenant's ip_pool catalog"));
    }
    if !scope_level.is_empty() &&
        !matches!(scope_level, "Free"|"Region"|"Site"|"Building"|"Floor"|"Room")
    {
        errors.push(format!("scope_level '{scope_level}' must be Free/Region/Site/Building/Floor/Room"));
    }

    // scope_entity_code ↔ scope_level consistency. Empty scope_level
    // defaults to Free, which can't carry a scope_entity_code.
    let effective_scope = if scope_level.is_empty() { "Free" } else { scope_level };
    match effective_scope {
        "Free" => {
            if !scope_entity_code.is_empty() {
                errors.push(format!(
                    "scope_entity_code '{scope_entity_code}' set but scope_level is Free — clear one or the other"));
            }
        }
        "Region" | "Site" => {
            // Region + Site aren't yet resolvable from the CSV. The
            // schema accepts them but building this slice only
            // handles Building / Floor / Room — an operator on
            // Region/Site must drop into the CRUD panel.
            if !scope_entity_code.is_empty() {
                errors.push(format!(
                    "scope_level '{effective_scope}' isn't resolvable from CSV yet — use the CRUD panel, or pick Building/Floor/Room"));
            }
        }
        "Building" => {
            if scope_entity_code.is_empty() {
                errors.push("scope_level 'Building' requires scope_entity_code (building_code)".into());
            } else if !building_code_to_id.contains_key(scope_entity_code) {
                errors.push(format!(
                    "scope_entity_code '{scope_entity_code}' not found in this tenant's building catalog"));
            }
        }
        "Floor" => {
            if scope_entity_code.is_empty() {
                errors.push("scope_level 'Floor' requires scope_entity_code (BUILDING_CODE/FLOOR_CODE)".into());
            } else if !scope_entity_code.contains('/') {
                errors.push(format!(
                    "scope_entity_code '{scope_entity_code}' must be BUILDING_CODE/FLOOR_CODE for Floor scope"));
            } else if !floor_code_to_id.contains_key(scope_entity_code) {
                errors.push(format!(
                    "scope_entity_code '{scope_entity_code}' not found in this tenant's floor catalog"));
            }
        }
        "Room" => {
            if scope_entity_code.is_empty() {
                errors.push("scope_level 'Room' requires scope_entity_code (BUILDING_CODE/FLOOR_CODE/ROOM_CODE)".into());
            } else if scope_entity_code.matches('/').count() != 2 {
                errors.push(format!(
                    "scope_entity_code '{scope_entity_code}' must be BUILDING_CODE/FLOOR_CODE/ROOM_CODE for Room scope"));
            } else if !room_code_to_id.contains_key(scope_entity_code) {
                errors.push(format!(
                    "scope_entity_code '{scope_entity_code}' not found in this tenant's room catalog"));
            }
        }
        _ => { /* scope_level already flagged above */ }
    }

    if !status.is_empty() &&
        !matches!(status, "Planned"|"Reserved"|"Active"|"Deprecated"|"Retired")
    {
        errors.push(format!("status '{status}' must be Planned/Reserved/Active/Deprecated/Retired"));
    }

    ImportRowOutcome {
        row_number,
        ok: errors.is_empty(),
        errors,
        identifier: subnet_code.to_string(),
    }
}

// ─── Server import ───────────────────────────────────────────────────────

/// Column order mirrors `bulk_export::export_servers_csv`.
///
/// nic_count is reference-only (driven by the server_profile; the
/// import ignores what the CSV says). asn is reference-only too —
/// same "ignored on apply" semantic as devices (allocating ASN
/// needs the allocation service + a block choice, which the bulk-
/// import flow doesn't do).
const SERVER_COLUMNS: &[&str] = &[
    "hostname", "profile_code", "building_code", "asn",
    "loopback_ip", "management_ip", "nic_count", "status",
];

pub async fn import_servers(
    pool: &PgPool,
    org_id: Uuid,
    body: &str,
    dry_run: bool,
    mode: ImportMode,
    user_id: Option<i32>,
) -> Result<ImportValidationResult, EngineError> {
    scope_grants::require_permission(
        pool, org_id, user_id, "write", "Server", None,
    ).await?;

    let rows = parse_csv(body)?;
    if rows.is_empty() {
        return Ok(ImportValidationResult {
            total_rows: 0, valid: 0, invalid: 0,
            dry_run, applied: false, outcomes: vec![],
        });
    }

    let header = &rows[0];
    if header.len() != SERVER_COLUMNS.len()
        || !header.iter().zip(SERVER_COLUMNS).all(|(a,b)| a.eq_ignore_ascii_case(b))
    {
        return Err(EngineError::bad_request(format!(
            "server import header must be: {}", SERVER_COLUMNS.join(","))));
    }

    let profile_rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, profile_code FROM net.server_profile
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let profile_codes: std::collections::HashSet<String> =
        profile_rows.iter().map(|(_, c)| c.clone()).collect();
    let profile_code_to_id: std::collections::HashMap<String, Uuid> =
        profile_rows.into_iter().map(|(id, c)| (c, id)).collect();

    let building_rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, building_code FROM net.building
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let building_codes: std::collections::HashSet<String> =
        building_rows.iter().map(|(_, c)| c.clone()).collect();
    let building_code_to_id: std::collections::HashMap<String, Uuid> =
        building_rows.into_iter().map(|(id, c)| (c, id)).collect();

    // Existing hostname → (id, version) so apply can branch INSERT vs
    // version-checked UPDATE. Server CSV has no version column (8 cols
    // — same shape as before upsert) so update applies against current
    // DB version, mirroring the VLAN/subnet semantic.
    let existing_rows: Vec<(String, Uuid, i32)> = sqlx::query_as(
        "SELECT hostname, id, version FROM net.server
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let existing_by_hostname: std::collections::HashMap<String, (Uuid, i32)> =
        existing_rows.into_iter().map(|(h, id, v)| (h, (id, v))).collect();
    let existing_hostnames: std::collections::HashSet<String> =
        existing_by_hostname.keys().cloned().collect();
    let empty_set = std::collections::HashSet::new();
    let dup_check_set = match mode {
        ImportMode::Create => &existing_hostnames,
        ImportMode::Upsert => &empty_set,
    };

    let mut outcomes: Vec<ImportRowOutcome> = Vec::with_capacity(rows.len().saturating_sub(1));
    for (i, row) in rows.iter().enumerate().skip(1) {
        let row_number = i + 1;
        let mut outcome = validate_server_row(row, row_number, &profile_codes, &building_codes);
        if outcome.ok && dup_check_set.contains(&outcome.identifier) {
            outcome.ok = false;
            outcome.errors.push(
                "server with this hostname already exists — pass mode=upsert to update existing rows"
                .to_string());
        }
        outcomes.push(outcome);
    }

    let valid   = outcomes.iter().filter(|o| o.ok).count();
    let invalid = outcomes.len() - valid;
    if dry_run || invalid > 0 {
        return Ok(ImportValidationResult {
            total_rows: outcomes.len(),
            valid, invalid,
            dry_run, applied: false, outcomes,
        });
    }

    let mut tx = pool.begin().await?;
    for (outcome_idx, row) in rows.iter().enumerate().skip(1).map(|(_, r)| r).enumerate() {
        let hostname      = row[0].trim();
        let profile_code  = row[1].trim();
        let building_code = row[2].trim();
        let management_ip = row[5].trim();   // row[3]=asn, row[4]=loopback (ignored)
        let status        = row[7].trim();

        let profile_id  = if profile_code.is_empty()  { None } else { profile_code_to_id.get(profile_code).copied() };
        let building_id = if building_code.is_empty() { None } else { building_code_to_id.get(building_code).copied() };
        let status_val  = if status.is_empty() { "Planned" } else { status };
        let mgmt_ip_opt: Option<&str> = if management_ip.is_empty() { None } else { Some(management_ip) };

        let existing = existing_by_hostname.get(hostname).copied();
        let (server_id, action) = match (existing, mode) {
            (Some((id, db_version)), ImportMode::Upsert) => {
                let update_result: Result<Option<(Uuid,)>, sqlx::Error> = sqlx::query_as(
                    "UPDATE net.server
                        SET server_profile_id = $3,
                            building_id       = $4,
                            management_ip     = $5::inet,
                            status            = $6::net.entity_status,
                            updated_at        = now(),
                            updated_by        = $7,
                            version           = version + 1
                      WHERE id = $1 AND organization_id = $2
                        AND version = $8 AND deleted_at IS NULL
                      RETURNING id")
                    .bind(id).bind(org_id)
                    .bind(profile_id).bind(building_id)
                    .bind(mgmt_ip_opt).bind(status_val)
                    .bind(user_id).bind(db_version)
                    .fetch_optional(&mut *tx).await;

                match update_result {
                    Ok(Some((updated_id,))) => (updated_id, "Updated"),
                    Ok(None) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!(
                            "version mismatch — DB advanced past version {db_version} between read and write"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                    Err(e) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!("database UPDATE failed: {e}"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                }
            }
            _ => {
                let insert_result: Result<(Uuid,), sqlx::Error> = sqlx::query_as(
                    "INSERT INTO net.server
                        (organization_id, server_profile_id, building_id,
                         hostname, management_ip, status, created_by, updated_by)
                     VALUES ($1, $2, $3, $4, $5::inet, $6::net.entity_status, $7, $7)
                     RETURNING id")
                    .bind(org_id)
                    .bind(profile_id)
                    .bind(building_id)
                    .bind(hostname)
                    .bind(mgmt_ip_opt)
                    .bind(status_val)
                    .bind(user_id)
                    .fetch_one(&mut *tx).await;

                match insert_result {
                    Ok((id,)) => (id, "Created"),
                    Err(e) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!("database INSERT failed: {e}"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                }
            }
        };

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "Server",
            entity_id: Some(server_id),
            action,
            actor_user_id: user_id,
            actor_display: None, client_ip: None, correlation_id: None,
            details: serde_json::json!({
                "source": "bulk_import",
                "mode": match mode {
                    ImportMode::Create => "create",
                    ImportMode::Upsert => "upsert",
                },
                "hostname": hostname,
                "profile_code": profile_code,
                "building_code": building_code,
            }),
        }).await?;
    }
    tx.commit().await?;

    Ok(ImportValidationResult {
        total_rows: outcomes.len(),
        valid, invalid: 0,
        dry_run: false, applied: true, outcomes,
    })
}

fn validate_server_row(
    row: &[String],
    row_number: usize,
    profile_codes: &std::collections::HashSet<String>,
    building_codes: &std::collections::HashSet<String>,
) -> ImportRowOutcome {
    let mut errors = Vec::new();
    if row.len() != SERVER_COLUMNS.len() {
        errors.push(format!("expected {} columns, got {}", SERVER_COLUMNS.len(), row.len()));
        return ImportRowOutcome {
            row_number, ok: false, errors,
            identifier: row.first().cloned().unwrap_or_default(),
        };
    }
    let hostname      = row[0].trim();
    let profile_code  = row[1].trim();
    let building_code = row[2].trim();
    let management_ip = row[5].trim();
    let status        = row[7].trim();

    if hostname.is_empty() {
        errors.push("hostname is required".into());
    }
    if !profile_code.is_empty() && !profile_codes.contains(profile_code) {
        errors.push(format!(
            "profile_code '{profile_code}' not found in this tenant's server_profile catalog"));
    }
    if !building_code.is_empty() && !building_codes.contains(building_code) {
        errors.push(format!(
            "building_code '{building_code}' not found in this tenant's building catalog"));
    }
    if !management_ip.is_empty() {
        let ip_part = management_ip.split('/').next().unwrap_or(management_ip);
        if ip_part.parse::<std::net::IpAddr>().is_err() {
            errors.push(format!("management_ip '{management_ip}' is not a valid IP address"));
        }
    }
    if !status.is_empty() &&
        !matches!(status, "Planned"|"Reserved"|"Active"|"Deprecated"|"Retired")
    {
        errors.push(format!("status '{status}' must be Planned/Reserved/Active/Deprecated/Retired"));
    }

    ImportRowOutcome {
        row_number,
        ok: errors.is_empty(),
        errors,
        identifier: hostname.to_string(),
    }
}

// ─── DHCP relay target import ────────────────────────────────────────────

/// Column order mirrors `bulk_export::export_dhcp_relay_targets_csv`.
///
/// `linked_ip_address_id` is accepted but ignored on apply — resolving
/// it would require a separate lookup on net.ip_address that the
/// import flow doesn't need today (the CSV is the source of truth
/// for the server_ip, and operators wire linkage via the CRUD panel
/// when they care about it).
const DHCP_RELAY_COLUMNS: &[&str] = &[
    "vlan_id", "server_ip", "priority", "linked_ip_address_id", "notes", "status",
];

pub async fn import_dhcp_relay_targets(
    pool: &PgPool,
    org_id: Uuid,
    body: &str,
    dry_run: bool,
    mode: ImportMode,
    user_id: Option<i32>,
) -> Result<ImportValidationResult, EngineError> {
    scope_grants::require_permission(
        pool, org_id, user_id, "write", "DhcpRelayTarget", None,
    ).await?;

    let rows = parse_csv(body)?;
    if rows.is_empty() {
        return Ok(ImportValidationResult {
            total_rows: 0, valid: 0, invalid: 0,
            dry_run, applied: false, outcomes: vec![],
        });
    }

    let header = &rows[0];
    if header.len() != DHCP_RELAY_COLUMNS.len()
        || !header.iter().zip(DHCP_RELAY_COLUMNS).all(|(a,b)| a.eq_ignore_ascii_case(b))
    {
        return Err(EngineError::bad_request(format!(
            "dhcp relay target import header must be: {}", DHCP_RELAY_COLUMNS.join(","))));
    }

    // Pre-fetch VLANs (numeric vlan_id → uuid) + existing targets
    // for dup detection. VLANs may have the same numeric id across
    // blocks — we key on (org, vlan_id) so multi-block tenants
    // that happen to reuse a number land on the first match; that
    // limitation is the same one the subnet importer has and lands
    // in a follow-on slice.
    let vlan_rows: Vec<(Uuid, i32)> = sqlx::query_as(
        "SELECT id, vlan_id FROM net.vlan
          WHERE organization_id = $1 AND deleted_at IS NULL
          ORDER BY vlan_id")
        .bind(org_id).fetch_all(pool).await?;
    let mut vlan_numeric_to_id: std::collections::HashMap<i32, Uuid> = std::collections::HashMap::new();
    for (id, n) in vlan_rows {
        // First-wins on duplicate numeric vlan_ids.
        vlan_numeric_to_id.entry(n).or_insert(id);
    }

    // Existing (vlan_uuid, server_ip) → (id, version). Key the map
    // by "vlan_uuid|host_ip" so the apply loop can branch INSERT vs
    // version-checked UPDATE.
    let existing_target_rows: Vec<(Uuid, String, Uuid, i32)> = sqlx::query_as(
        "SELECT vlan_id, host(server_ip), id, version FROM net.dhcp_relay_target
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let existing_by_pair: std::collections::HashMap<String, (Uuid, i32)> =
        existing_target_rows.into_iter()
            .map(|(vid, ip, id, v)| (format!("{vid}|{ip}"), (id, v)))
            .collect();
    let existing_pairs_set: std::collections::HashSet<String> =
        existing_by_pair.keys().cloned().collect();
    let empty_set = std::collections::HashSet::new();
    let dup_check_set = match mode {
        ImportMode::Create => &existing_pairs_set,
        ImportMode::Upsert => &empty_set,
    };

    let mut outcomes: Vec<ImportRowOutcome> = Vec::with_capacity(rows.len().saturating_sub(1));
    for (i, row) in rows.iter().enumerate().skip(1) {
        let row_number = i + 1;
        outcomes.push(validate_dhcp_relay_row(row, row_number, &vlan_numeric_to_id, dup_check_set));
    }

    let valid   = outcomes.iter().filter(|o| o.ok).count();
    let invalid = outcomes.len() - valid;
    if dry_run || invalid > 0 {
        return Ok(ImportValidationResult {
            total_rows: outcomes.len(),
            valid, invalid,
            dry_run, applied: false, outcomes,
        });
    }

    let mut tx = pool.begin().await?;
    for (outcome_idx, row) in rows.iter().enumerate().skip(1).map(|(_, r)| r).enumerate() {
        let vlan_numeric: i32 = row[0].trim().parse().unwrap_or(0);
        let server_ip  = row[1].trim();
        let priority: i32 = row[2].trim().parse().unwrap_or(10);
        // row[3]=linked_ip_address_id ignored on apply
        let notes      = row[4].trim();
        // row[5]=status not bound — INSERT defaults to Active

        let vlan_uuid = vlan_numeric_to_id.get(&vlan_numeric).copied()
            .expect("validator enforced VLAN exists");
        let notes_opt: Option<&str> = if notes.is_empty() { None } else { Some(notes) };

        let pair_key = format!("{vlan_uuid}|{server_ip}");
        let existing = existing_by_pair.get(&pair_key).copied();
        let (target_id, action) = match (existing, mode) {
            (Some((id, db_version)), ImportMode::Upsert) => {
                let update_result: Result<Option<(Uuid,)>, sqlx::Error> = sqlx::query_as(
                    "UPDATE net.dhcp_relay_target
                        SET priority   = $3,
                            notes      = $4,
                            updated_at = now(),
                            updated_by = $5,
                            version    = version + 1
                      WHERE id = $1 AND organization_id = $2
                        AND version = $6 AND deleted_at IS NULL
                      RETURNING id")
                    .bind(id).bind(org_id)
                    .bind(priority).bind(notes_opt)
                    .bind(user_id).bind(db_version)
                    .fetch_optional(&mut *tx).await;

                match update_result {
                    Ok(Some((updated_id,))) => (updated_id, "Updated"),
                    Ok(None) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!(
                            "version mismatch — DB advanced past version {db_version} between read and write"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                    Err(e) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!("database UPDATE failed: {e}"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                }
            }
            _ => {
                let insert_result: Result<(Uuid,), sqlx::Error> = sqlx::query_as(
                    "INSERT INTO net.dhcp_relay_target
                        (organization_id, vlan_id, server_ip, priority, notes,
                         status, lock_state, created_by, updated_by)
                     VALUES ($1, $2, $3::inet, $4, $5,
                             'Active'::net.entity_status, 'Open'::net.lock_state, $6, $6)
                     RETURNING id")
                    .bind(org_id)
                    .bind(vlan_uuid)
                    .bind(server_ip)
                    .bind(priority)
                    .bind(notes_opt)
                    .bind(user_id)
                    .fetch_one(&mut *tx).await;

                match insert_result {
                    Ok((id,)) => (id, "Created"),
                    Err(e) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!("database INSERT failed: {e}"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                }
            }
        };

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "DhcpRelayTarget",
            entity_id: Some(target_id),
            action,
            actor_user_id: user_id,
            actor_display: None, client_ip: None, correlation_id: None,
            details: serde_json::json!({
                "source": "bulk_import",
                "mode": match mode {
                    ImportMode::Create => "create",
                    ImportMode::Upsert => "upsert",
                },
                "vlan_id": vlan_numeric,
                "server_ip": server_ip,
            }),
        }).await?;
    }
    tx.commit().await?;

    Ok(ImportValidationResult {
        total_rows: outcomes.len(),
        valid, invalid: 0,
        dry_run: false, applied: true, outcomes,
    })
}

fn validate_dhcp_relay_row(
    row: &[String],
    row_number: usize,
    vlan_numeric_to_id: &std::collections::HashMap<i32, Uuid>,
    existing_pairs: &std::collections::HashSet<String>,
) -> ImportRowOutcome {
    let mut errors = Vec::new();
    if row.len() != DHCP_RELAY_COLUMNS.len() {
        errors.push(format!("expected {} columns, got {}", DHCP_RELAY_COLUMNS.len(), row.len()));
        return ImportRowOutcome {
            row_number, ok: false, errors,
            identifier: row.first().cloned().unwrap_or_default(),
        };
    }
    let vlan_str   = row[0].trim();
    let server_ip  = row[1].trim();
    let priority   = row[2].trim();

    let vlan_parse = vlan_str.parse::<i32>().ok();
    match vlan_parse {
        None => errors.push(format!("vlan_id '{vlan_str}' is not a valid integer")),
        Some(n) if !(1..=4094).contains(&n) =>
            errors.push(format!("vlan_id {n} is outside the 1-4094 range")),
        Some(n) if !vlan_numeric_to_id.contains_key(&n) =>
            errors.push(format!(
                "vlan_id {n} not found in this tenant's vlan catalog — create the VLAN first")),
        _ => {}
    }
    if server_ip.is_empty() {
        errors.push("server_ip is required".into());
    } else {
        let ip_part = server_ip.split('/').next().unwrap_or(server_ip);
        if ip_part.parse::<std::net::IpAddr>().is_err() {
            errors.push(format!("server_ip '{server_ip}' is not a valid IP address"));
        }
    }
    if !priority.is_empty() {
        match priority.parse::<i32>() {
            Ok(p) if p < 0 => errors.push(format!("priority '{p}' must be non-negative")),
            Ok(_) => {}
            Err(_) => errors.push(format!("priority '{priority}' is not a valid integer")),
        }
    }
    if let (Some(n), true) = (vlan_parse, !server_ip.is_empty()) {
        let key = format!("{}|{}", vlan_numeric_to_id.get(&n)
            .copied().unwrap_or_else(Uuid::nil), server_ip);
        if existing_pairs.contains(&key) {
            errors.push(format!(
                "dhcp relay target for vlan {n} + server_ip {server_ip} already exists"));
        }
    }

    ImportRowOutcome {
        row_number,
        ok: errors.is_empty(),
        errors,
        identifier: format!("{vlan_str}|{server_ip}"),
    }
}

// ─── Link import ─────────────────────────────────────────────────────────

/// Column order mirrors `bulk_export::export_links_csv`. One CSV row
/// decomposes on apply into 1 `net.link` row + 2 `net.link_endpoint`
/// rows (A side = order 0, B side = order 1). The whole decomposition
/// lands in one transaction — either all three rows materialise or
/// none do.
///
/// `ip_a` / `ip_b` are accepted-for-reference-but-ignored-on-apply.
/// Resolving them to `net.ip_address` rows needs subnet context the
/// import flow doesn't carry (the subnet_code column alone isn't
/// enough — a pair of endpoints on a P2P link typically sits in a
/// /30 that's carved separately). Operators create the IP rows via
/// the IP-allocation CRUD and link them to endpoints afterwards; a
/// future "link import with auto-allocate" slice can remove that
/// step once the semantics are pinned down.
const LINK_COLUMNS: &[&str] = &[
    "link_code", "link_type", "vlan_id", "subnet_code",
    "device_a", "port_a", "ip_a",
    "device_b", "port_b", "ip_b",
    "status",
];

pub async fn import_links(
    pool: &PgPool,
    org_id: Uuid,
    body: &str,
    dry_run: bool,
    mode: ImportMode,
    user_id: Option<i32>,
) -> Result<ImportValidationResult, EngineError> {
    scope_grants::require_permission(
        pool, org_id, user_id, "write", "Link", None,
    ).await?;

    let rows = parse_csv(body)?;
    if rows.is_empty() {
        return Ok(ImportValidationResult {
            total_rows: 0, valid: 0, invalid: 0,
            dry_run, applied: false, outcomes: vec![],
        });
    }

    let header = &rows[0];
    if header.len() != LINK_COLUMNS.len()
        || !header.iter().zip(LINK_COLUMNS).all(|(a,b)| a.eq_ignore_ascii_case(b))
    {
        return Err(EngineError::bad_request(format!(
            "link import header must be: {}", LINK_COLUMNS.join(","))));
    }

    // FK maps — link_type, vlan, subnet, device.
    let type_rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, type_code FROM net.link_type
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let link_type_codes: std::collections::HashSet<String> =
        type_rows.iter().map(|(_, c)| c.clone()).collect();
    let link_type_to_id: std::collections::HashMap<String, Uuid> =
        type_rows.into_iter().map(|(id, c)| (c, id)).collect();

    let vlan_rows: Vec<(Uuid, i32)> = sqlx::query_as(
        "SELECT id, vlan_id FROM net.vlan
          WHERE organization_id = $1 AND deleted_at IS NULL
          ORDER BY vlan_id")
        .bind(org_id).fetch_all(pool).await?;
    let mut vlan_numeric_to_id: std::collections::HashMap<i32, Uuid> = std::collections::HashMap::new();
    for (id, n) in vlan_rows {
        vlan_numeric_to_id.entry(n).or_insert(id);
    }

    let subnet_rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, subnet_code FROM net.subnet
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let subnet_codes: std::collections::HashSet<String> =
        subnet_rows.iter().map(|(_, c)| c.clone()).collect();
    let subnet_code_to_id: std::collections::HashMap<String, Uuid> =
        subnet_rows.into_iter().map(|(id, c)| (c, id)).collect();

    let device_rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname FROM net.device
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let device_hostnames: std::collections::HashSet<String> =
        device_rows.iter().map(|(_, h)| h.clone()).collect();
    let device_hostname_to_id: std::collections::HashMap<String, Uuid> =
        device_rows.into_iter().map(|(id, h)| (h, id)).collect();

    // Existing link_code → (id, version). dup_check_set is suppressed
    // in upsert mode (existing rows are valid update targets) so the
    // validator doesn't fire "already exists" on rows we want to UPDATE.
    let existing_link_rows: Vec<(String, Uuid, i32)> = sqlx::query_as(
        "SELECT link_code, id, version FROM net.link
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let existing_by_code: std::collections::HashMap<String, (Uuid, i32)> =
        existing_link_rows.into_iter().map(|(c, id, v)| (c, (id, v))).collect();
    let existing_codes_set: std::collections::HashSet<String> =
        existing_by_code.keys().cloned().collect();
    let empty_set = std::collections::HashSet::new();
    let dup_check_set = match mode {
        ImportMode::Create => &existing_codes_set,
        ImportMode::Upsert => &empty_set,
    };

    let mut outcomes: Vec<ImportRowOutcome> = Vec::with_capacity(rows.len().saturating_sub(1));
    for (i, row) in rows.iter().enumerate().skip(1) {
        let row_number = i + 1;
        outcomes.push(validate_link_row(
            row, row_number,
            &link_type_codes, &vlan_numeric_to_id, &subnet_codes,
            &device_hostnames, dup_check_set,
        ));
    }

    let valid   = outcomes.iter().filter(|o| o.ok).count();
    let invalid = outcomes.len() - valid;
    if dry_run || invalid > 0 {
        return Ok(ImportValidationResult {
            total_rows: outcomes.len(),
            valid, invalid,
            dry_run, applied: false, outcomes,
        });
    }

    let mut tx = pool.begin().await?;
    for (outcome_idx, row) in rows.iter().enumerate().skip(1).map(|(_, r)| r).enumerate() {
        let link_code   = row[0].trim();
        let link_type   = row[1].trim();
        let vlan_str    = row[2].trim();
        let subnet_code = row[3].trim();
        let device_a    = row[4].trim();
        let port_a      = row[5].trim();
        // row[6] = ip_a — ignored on apply (see module doc)
        let device_b    = row[7].trim();
        let port_b      = row[8].trim();
        // row[9] = ip_b — ignored
        let status      = row[10].trim();

        let type_id = link_type_to_id.get(link_type).copied()
            .expect("validator enforced link_type exists");
        let vlan_uuid = if vlan_str.is_empty() { None } else {
            vlan_str.parse::<i32>().ok().and_then(|n| vlan_numeric_to_id.get(&n).copied())
        };
        let subnet_id_val = if subnet_code.is_empty() { None }
                            else { subnet_code_to_id.get(subnet_code).copied() };
        let device_a_id = device_hostname_to_id.get(device_a).copied()
            .expect("validator enforced device_a exists");
        let device_b_id = device_hostname_to_id.get(device_b).copied()
            .expect("validator enforced device_b exists");
        let status_val  = if status.is_empty() { "Planned" } else { status };
        let port_a_opt: Option<&str> = if port_a.is_empty() { None } else { Some(port_a) };
        let port_b_opt: Option<&str> = if port_b.is_empty() { None } else { Some(port_b) };

        // For upsert: UPDATE the link row's mutable fields + DELETE
        // the existing endpoints + INSERT both sides fresh. Doing it
        // as delete+insert (rather than UPDATE-the-endpoints) means
        // we don't have to track endpoint identity stability across
        // imports — the CSV is the source of truth for the A/B pair
        // and a single transaction guarantees atomicity.
        let existing = existing_by_code.get(link_code).copied();
        let (link_id, action) = match (existing, mode) {
            (Some((id, db_version)), ImportMode::Upsert) => {
                let update_result: Result<Option<(Uuid,)>, sqlx::Error> = sqlx::query_as(
                    "UPDATE net.link
                        SET link_type_id = $3,
                            vlan_id      = $4,
                            subnet_id    = $5,
                            status       = $6::net.entity_status,
                            updated_at   = now(),
                            updated_by   = $7,
                            version      = version + 1
                      WHERE id = $1 AND organization_id = $2
                        AND version = $8 AND deleted_at IS NULL
                      RETURNING id")
                    .bind(id).bind(org_id)
                    .bind(type_id).bind(vlan_uuid).bind(subnet_id_val)
                    .bind(status_val)
                    .bind(user_id).bind(db_version)
                    .fetch_optional(&mut *tx).await;

                let updated_id = match update_result {
                    Ok(Some((uid,))) => uid,
                    Ok(None) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!(
                            "version mismatch — DB advanced past version {db_version} between read and write"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                    Err(e) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!("database UPDATE (link) failed: {e}"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                };

                // Wipe the existing endpoint pair so we can re-insert
                // from the CSV row. Hard delete (not soft) — endpoints
                // are owned by the link and have no independent
                // identity to preserve.
                let del_result = sqlx::query(
                    "DELETE FROM net.link_endpoint
                      WHERE organization_id = $1 AND link_id = $2")
                    .bind(org_id).bind(updated_id)
                    .execute(&mut *tx).await;
                if let Err(e) = del_result {
                    let o = &mut outcomes[outcome_idx];
                    o.ok = false;
                    o.errors.push(format!("database DELETE (endpoints) failed: {e}"));
                    return Ok(ImportValidationResult {
                        total_rows: outcomes.len(),
                        valid: outcomes.iter().filter(|o| o.ok).count(),
                        invalid: outcomes.iter().filter(|o| !o.ok).count(),
                        dry_run: false, applied: false, outcomes,
                    });
                }
                (updated_id, "Updated")
            }
            _ => {
                let link_insert: Result<(Uuid,), sqlx::Error> = sqlx::query_as(
                    "INSERT INTO net.link
                        (organization_id, link_type_id, link_code,
                         vlan_id, subnet_id, status, created_by, updated_by)
                     VALUES ($1, $2, $3, $4, $5,
                             $6::net.entity_status, $7, $7)
                     RETURNING id")
                    .bind(org_id)
                    .bind(type_id)
                    .bind(link_code)
                    .bind(vlan_uuid)
                    .bind(subnet_id_val)
                    .bind(status_val)
                    .bind(user_id)
                    .fetch_one(&mut *tx).await;

                match link_insert {
                    Ok((id,)) => (id, "Created"),
                    Err(e) => {
                        let o = &mut outcomes[outcome_idx];
                        o.ok = false;
                        o.errors.push(format!("database INSERT (link) failed: {e}"));
                        return Ok(ImportValidationResult {
                            total_rows: outcomes.len(),
                            valid: outcomes.iter().filter(|o| o.ok).count(),
                            invalid: outcomes.iter().filter(|o| !o.ok).count(),
                            dry_run: false, applied: false, outcomes,
                        });
                    }
                }
            }
        };

        // A endpoint — same INSERT regardless of upsert/create
        // (delete-then-insert handles the upsert side).
        let ep_a_insert: Result<(), sqlx::Error> = sqlx::query(
            "INSERT INTO net.link_endpoint
                (organization_id, link_id, endpoint_order, device_id,
                 interface_name, status, created_by, updated_by)
             VALUES ($1, $2, 0, $3, $4, 'Active'::net.entity_status, $5, $5)")
            .bind(org_id).bind(link_id).bind(device_a_id).bind(port_a_opt).bind(user_id)
            .execute(&mut *tx).await.map(|_| ());
        if let Err(e) = ep_a_insert {
            let o = &mut outcomes[outcome_idx];
            o.ok = false;
            o.errors.push(format!("database INSERT (endpoint A) failed: {e}"));
            return Ok(ImportValidationResult {
                total_rows: outcomes.len(),
                valid: outcomes.iter().filter(|o| o.ok).count(),
                invalid: outcomes.iter().filter(|o| !o.ok).count(),
                dry_run: false, applied: false, outcomes,
            });
        }

        // B endpoint
        let ep_b_insert: Result<(), sqlx::Error> = sqlx::query(
            "INSERT INTO net.link_endpoint
                (organization_id, link_id, endpoint_order, device_id,
                 interface_name, status, created_by, updated_by)
             VALUES ($1, $2, 1, $3, $4, 'Active'::net.entity_status, $5, $5)")
            .bind(org_id).bind(link_id).bind(device_b_id).bind(port_b_opt).bind(user_id)
            .execute(&mut *tx).await.map(|_| ());
        if let Err(e) = ep_b_insert {
            let o = &mut outcomes[outcome_idx];
            o.ok = false;
            o.errors.push(format!("database INSERT (endpoint B) failed: {e}"));
            return Ok(ImportValidationResult {
                total_rows: outcomes.len(),
                valid: outcomes.iter().filter(|o| o.ok).count(),
                invalid: outcomes.iter().filter(|o| !o.ok).count(),
                dry_run: false, applied: false, outcomes,
            });
        }

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "Link",
            entity_id: Some(link_id),
            action,
            actor_user_id: user_id,
            actor_display: None, client_ip: None, correlation_id: None,
            details: serde_json::json!({
                "source": "bulk_import",
                "mode": match mode {
                    ImportMode::Create => "create",
                    ImportMode::Upsert => "upsert",
                },
                "link_code": link_code,
                "link_type": link_type,
                "device_a": device_a,
                "device_b": device_b,
            }),
        }).await?;
    }
    tx.commit().await?;

    Ok(ImportValidationResult {
        total_rows: outcomes.len(),
        valid, invalid: 0,
        dry_run: false, applied: true, outcomes,
    })
}

#[allow(clippy::too_many_arguments)]
fn validate_link_row(
    row: &[String],
    row_number: usize,
    link_type_codes: &std::collections::HashSet<String>,
    vlan_numeric_to_id: &std::collections::HashMap<i32, Uuid>,
    subnet_codes: &std::collections::HashSet<String>,
    device_hostnames: &std::collections::HashSet<String>,
    existing_codes: &std::collections::HashSet<String>,
) -> ImportRowOutcome {
    let mut errors = Vec::new();
    if row.len() != LINK_COLUMNS.len() {
        errors.push(format!("expected {} columns, got {}", LINK_COLUMNS.len(), row.len()));
        return ImportRowOutcome {
            row_number, ok: false, errors,
            identifier: row.first().cloned().unwrap_or_default(),
        };
    }

    let link_code   = row[0].trim();
    let link_type   = row[1].trim();
    let vlan_str    = row[2].trim();
    let subnet_code = row[3].trim();
    let device_a    = row[4].trim();
    let device_b    = row[7].trim();
    let status      = row[10].trim();

    if link_code.is_empty() {
        errors.push("link_code is required".into());
    } else if existing_codes.contains(link_code) {
        errors.push(format!(
            "link_code '{link_code}' already exists — update mode not yet supported"));
    }
    if link_type.is_empty() {
        errors.push("link_type is required".into());
    } else if !link_type_codes.contains(link_type) {
        errors.push(format!(
            "link_type '{link_type}' not found in this tenant's link_type catalog"));
    }
    if !vlan_str.is_empty() {
        match vlan_str.parse::<i32>() {
            Ok(n) if !(1..=4094).contains(&n) =>
                errors.push(format!("vlan_id {n} is outside the 1-4094 range")),
            Ok(n) if !vlan_numeric_to_id.contains_key(&n) =>
                errors.push(format!(
                    "vlan_id {n} not found in this tenant's vlan catalog")),
            Ok(_) => {},
            Err(_) => errors.push(format!("vlan_id '{vlan_str}' is not a valid integer")),
        }
    }
    if !subnet_code.is_empty() && !subnet_codes.contains(subnet_code) {
        errors.push(format!(
            "subnet_code '{subnet_code}' not found in this tenant's subnet catalog"));
    }
    if device_a.is_empty() {
        errors.push("device_a is required".into());
    } else if !device_hostnames.contains(device_a) {
        errors.push(format!(
            "device_a '{device_a}' not found in this tenant's device catalog"));
    }
    if device_b.is_empty() {
        errors.push("device_b is required".into());
    } else if !device_hostnames.contains(device_b) {
        errors.push(format!(
            "device_b '{device_b}' not found in this tenant's device catalog"));
    }
    if !status.is_empty() &&
        !matches!(status, "Planned"|"Reserved"|"Active"|"Deprecated"|"Retired")
    {
        errors.push(format!("status '{status}' must be Planned/Reserved/Active/Deprecated/Retired"));
    }

    ImportRowOutcome {
        row_number,
        ok: errors.is_empty(),
        errors,
        identifier: link_code.to_string(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::HashSet;

    #[test]
    fn parse_csv_handles_plain_rows() {
        let body = "a,b,c\r\n1,2,3\r\n4,5,6\r\n";
        let rows = parse_csv(body).unwrap();
        assert_eq!(rows, vec![
            vec!["a","b","c"], vec!["1","2","3"], vec!["4","5","6"],
        ]);
    }

    #[test]
    fn parse_csv_handles_quoted_fields_with_commas() {
        let body = r#"a,b,c
"one, two",plain,"three, four"
"#;
        let rows = parse_csv(body).unwrap();
        assert_eq!(rows[1], vec!["one, two", "plain", "three, four"]);
    }

    #[test]
    fn parse_csv_unescapes_doubled_quotes() {
        let body = r#"a,b
"she said ""hi""",plain
"#;
        let rows = parse_csv(body).unwrap();
        assert_eq!(rows[1][0], r#"she said "hi""#);
    }

    #[test]
    fn parse_csv_tolerates_lf_crlf_and_cr_line_endings() {
        // Mixed LF + CRLF; parser should produce identical rows.
        let body_lf   = "a,b\n1,2\n3,4\n";
        let body_crlf = "a,b\r\n1,2\r\n3,4\r\n";
        assert_eq!(parse_csv(body_lf).unwrap(), parse_csv(body_crlf).unwrap());
    }

    #[test]
    fn parse_csv_skips_blank_trailing_rows() {
        // A file ending with \r\n\r\n shouldn't give a phantom row.
        let body = "a,b\r\n1,2\r\n\r\n";
        let rows = parse_csv(body).unwrap();
        assert_eq!(rows.len(), 2, "header + one data row, no phantom: {rows:?}");
    }

    #[test]
    fn parse_csv_rejects_unterminated_quoted_field() {
        let body = "a,b\n\"unterminated,2\n";
        let err = parse_csv(body).unwrap_err().to_string();
        assert!(err.contains("unterminated"), "err: {err}");
    }

    #[test]
    fn parse_csv_rejects_content_after_closing_quote() {
        // This trips operators more often than they'd like: a cell
        // typed as "foo"bar instead of "foo" and bar.
        let body = "a,b\n\"foo\"bar,2\n";
        let err = parse_csv(body);
        assert!(err.is_err(), "mid-field quote should be rejected");
    }

    #[test]
    fn parse_csv_handles_empty_input() {
        assert_eq!(parse_csv("").unwrap(), Vec::<Vec<String>>::new());
    }

    #[test]
    fn parse_csv_handles_file_without_trailing_newline() {
        // Last row must still emit even if no \n at the end.
        let rows = parse_csv("a,b\n1,2").unwrap();
        assert_eq!(rows, vec![vec!["a","b"], vec!["1","2"]]);
    }

    fn sample_fk_sets() -> (HashSet<String>, HashSet<String>) {
        let mut roles = HashSet::new();
        roles.insert("Core".to_string());
        roles.insert("L1SW".to_string());
        let mut buildings = HashSet::new();
        buildings.insert("MEP-91".to_string());
        (roles, buildings)
    }

    #[test]
    fn validate_device_row_accepts_fully_valid_row() {
        let (roles, buildings) = sample_fk_sets();
        let row: Vec<String> = vec![
            "MEP-91-CORE02", "Core", "MEP-91", "MP",
            "10.11.152.2/24", "65112", "Active", "1",
        ].into_iter().map(String::from).collect();
        let outcome = validate_device_row(&row, 2, &roles, &buildings);
        assert!(outcome.ok, "all-valid row should pass: {:?}", outcome.errors);
    }

    #[test]
    fn validate_device_row_reports_every_error_not_just_first() {
        // Operators fixing an import want to see ALL problems in one
        // pass, not fix-retry-fix-retry.
        let (roles, buildings) = sample_fk_sets();
        let row: Vec<String> = vec![
            "",                   // missing hostname
            "UnknownRole",        // bad role
            "UnknownBuilding",    // bad building
            "MP",
            "not-an-ip",          // bad IP
            "abc",                // bad ASN
            "ActiveX",            // bad status
            "v1",                 // bad version
        ].into_iter().map(String::from).collect();
        let outcome = validate_device_row(&row, 2, &roles, &buildings);
        assert!(!outcome.ok);
        assert_eq!(outcome.errors.len(), 7,
            "should surface all 7 problems at once, got: {:?}", outcome.errors);
    }

    #[test]
    fn validate_device_row_accepts_empty_optional_fields() {
        // Only hostname is strictly required — role / building / IP /
        // ASN / status / version all tolerate empty (the apply path
        // will then insert NULLs).
        let (roles, buildings) = sample_fk_sets();
        let row: Vec<String> = vec![
            "MEP-91-CORE02", "", "", "", "", "", "", "",
        ].into_iter().map(String::from).collect();
        let outcome = validate_device_row(&row, 2, &roles, &buildings);
        assert!(outcome.ok, "empty optionals should pass: {:?}", outcome.errors);
    }

    #[test]
    fn validate_device_row_flags_column_count_drift() {
        // Wrong column count is a single top-level error rather than
        // a cascade of per-column failures.
        let (roles, buildings) = sample_fk_sets();
        let row: Vec<String> = vec!["hostname-only"].into_iter().map(String::from).collect();
        let outcome = validate_device_row(&row, 2, &roles, &buildings);
        assert!(!outcome.ok);
        assert_eq!(outcome.errors.len(), 1);
        assert!(outcome.errors[0].contains("expected"));
    }
}
