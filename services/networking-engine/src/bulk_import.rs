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
}

fn default_dry_run() -> bool { true }

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
    user_id: Option<i32>,
) -> Result<ImportValidationResult, EngineError> {
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

    // Existing hostname set — used to surface "already exists" as a
    // per-row error rather than letting the INSERT blow up later.
    let existing_rows: Vec<(String,)> = sqlx::query_as(
        "SELECT hostname FROM net.device
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let existing_hostnames: std::collections::HashSet<String> =
        existing_rows.into_iter().map(|(h,)| h).collect();

    let mut outcomes: Vec<ImportRowOutcome> = Vec::with_capacity(rows.len().saturating_sub(1));
    for (i, row) in rows.iter().enumerate().skip(1) {
        let row_number = i + 1;   // 1-based, header is row 1
        let mut outcome = validate_device_row(row, row_number, &role_codes, &building_codes);
        // Create-only semantics: a row is only valid for apply if
        // its hostname is new. Flag this here (not in
        // validate_device_row) so dry-run still shows the same
        // per-field validation — but we don't want apply to INSERT
        // an existing hostname.
        if outcome.ok && existing_hostnames.contains(&outcome.identifier) {
            outcome.ok = false;
            outcome.errors.push(
                "device with this hostname already exists — update mode not yet supported (delete + re-import if intended)"
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

    // Apply path — every row passed validation, so INSERT them all
    // inside one transaction. If ANY INSERT fails the whole tx
    // rolls back and we surface the failing row via its outcome.
    let mut tx = pool.begin().await?;
    for (outcome_idx, row) in rows.iter().enumerate().skip(1).map(|(_, r)| r).enumerate() {
        let hostname      = row[0].trim();
        let role_code     = row[1].trim();
        let building_code = row[2].trim();
        let management_ip = row[4].trim();
        let status        = row[6].trim();

        let role_id     = if role_code.is_empty()     { None } else { role_code_to_id.get(role_code).copied() };
        let building_id = if building_code.is_empty() { None } else { building_code_to_id.get(building_code).copied() };
        let status_val  = if status.is_empty() { "Planned" } else { status };
        // inet accepts either bare host or host/prefix; bind the
        // string and let Postgres parse, so "10.11.152.2" and
        // "10.11.152.2/24" both land correctly.
        let mgmt_ip_opt: Option<&str> = if management_ip.is_empty() { None } else { Some(management_ip) };

        // Insert. If the DB rejects (constraint we didn't pre-check
        // for, enum coercion, etc.) propagate as a typed row-level
        // failure + rollback the whole tx.
        let insert_result: Result<(Uuid,), sqlx::Error> = sqlx::query_as(
            "INSERT INTO net.device
                (organization_id, device_role_id, building_id,
                 hostname, management_ip, status, created_by, updated_by)
             VALUES
                ($1, $2, $3, $4, $5::inet, $6::net.entity_status, $7, $7)
             RETURNING id")
            .bind(org_id)
            .bind(role_id)
            .bind(building_id)
            .bind(hostname)
            .bind(mgmt_ip_opt)
            .bind(status_val)
            .bind(user_id)
            .fetch_one(&mut *tx).await;

        let device_id = match insert_result {
            Ok((id,)) => id,
            Err(e) => {
                // Rollback tx (by dropping it implicitly when we
                // return — sqlx rolls back on Drop) and mark this
                // row failed in the outcomes.
                let o = &mut outcomes[outcome_idx];
                o.ok = false;
                o.errors.push(format!("database INSERT failed: {e}"));
                return Ok(ImportValidationResult {
                    total_rows: outcomes.len(),
                    valid: outcomes.iter().filter(|o| o.ok).count(),
                    invalid: outcomes.iter().filter(|o| !o.ok).count(),
                    dry_run: false, applied: false,
                    outcomes,
                });
            }
        };

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "Device",
            entity_id: Some(device_id),
            action: "Created",
            actor_user_id: user_id,
            actor_display: None,
            client_ip: None,
            correlation_id: None,
            details: serde_json::json!({
                "source": "bulk_import",
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
    user_id: Option<i32>,
) -> Result<ImportValidationResult, EngineError> {
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

    // Existing (block_id, vlan_id) pairs — drives the dup check.
    // Keyed by the string "block_code|vlan_id" for easy lookup.
    let existing_rows: Vec<(String, i32)> = sqlx::query_as(
        "SELECT b.block_code, v.vlan_id
           FROM net.vlan v
           JOIN net.vlan_block b ON b.id = v.block_id AND b.deleted_at IS NULL
          WHERE v.organization_id = $1 AND v.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let existing_pairs: std::collections::HashSet<String> =
        existing_rows.into_iter().map(|(c, id)| format!("{c}|{id}")).collect();

    let mut outcomes: Vec<ImportRowOutcome> = Vec::with_capacity(rows.len().saturating_sub(1));
    for (i, row) in rows.iter().enumerate().skip(1) {
        let row_number = i + 1;
        let outcome = validate_vlan_row(row, row_number, &block_codes, &template_codes, &existing_pairs);
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
        let vlan_id: i32  = row[0].trim().parse().unwrap_or(0);  // validator checked
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

        let insert_result: Result<(Uuid,), sqlx::Error> = sqlx::query_as(
            "INSERT INTO net.vlan
                (organization_id, block_id, template_id, vlan_id,
                 display_name, description, scope_level, status,
                 created_by, updated_by)
             VALUES ($1, $2, $3, $4, $5, $6, $7, $8::net.entity_status, $9, $9)
             RETURNING id")
            .bind(org_id)
            .bind(block_id)
            .bind(template_id)
            .bind(vlan_id)
            .bind(display_name)
            .bind(desc_opt)
            .bind(scope_val)
            .bind(status_val)
            .bind(user_id)
            .fetch_one(&mut *tx).await;

        let vlan_uuid = match insert_result {
            Ok((id,)) => id,
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
        };

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "Vlan",
            entity_id: Some(vlan_uuid),
            action: "Created",
            actor_user_id: user_id,
            actor_display: None, client_ip: None, correlation_id: None,
            details: serde_json::json!({
                "source": "bulk_import",
                "vlan_id": vlan_id,
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
const SUBNET_COLUMNS: &[&str] = &[
    "subnet_code", "display_name", "network", "vlan_id",
    "pool_code", "scope_level", "status",
];

/// Validate + optionally apply a CSV bulk import of subnets. Same
/// semantics as the device + VLAN importers.
///
/// `pool_code` is required (subnets can't exist without a parent
/// pool); `network` is required + must be a valid CIDR; `subnet_code`
/// is required and must be unique per-tenant.
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
    user_id: Option<i32>,
) -> Result<ImportValidationResult, EngineError> {
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

    let existing_codes: Vec<(String,)> = sqlx::query_as(
        "SELECT subnet_code FROM net.subnet
          WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    let existing_codes: std::collections::HashSet<String> =
        existing_codes.into_iter().map(|(c,)| c).collect();

    let mut outcomes: Vec<ImportRowOutcome> = Vec::with_capacity(rows.len().saturating_sub(1));
    for (i, row) in rows.iter().enumerate().skip(1) {
        let row_number = i + 1;
        outcomes.push(validate_subnet_row(row, row_number, &pool_codes, &existing_codes));
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
        let subnet_code = row[0].trim();
        let display_name= row[1].trim();
        let network     = row[2].trim();
        // row[3] is vlan_id — carried by the CSV for reference,
        // ignored on apply (see module docs).
        let pool_code   = row[4].trim();
        let scope_level = row[5].trim();
        let status      = row[6].trim();

        let pool_id_val = pool_code_to_id.get(pool_code).copied()
            .expect("validator enforced pool_code exists");
        let scope_val   = if scope_level.is_empty() { "Free" }   else { scope_level };
        let status_val  = if status.is_empty()      { "Active" } else { status };

        let insert_result: Result<(Uuid,), sqlx::Error> = sqlx::query_as(
            "INSERT INTO net.subnet
                (organization_id, pool_id, subnet_code, display_name,
                 network, scope_level, status, created_by, updated_by)
             VALUES ($1, $2, $3, $4, $5::cidr, $6, $7::net.entity_status, $8, $8)
             RETURNING id")
            .bind(org_id)
            .bind(pool_id_val)
            .bind(subnet_code)
            .bind(display_name)
            .bind(network)
            .bind(scope_val)
            .bind(status_val)
            .bind(user_id)
            .fetch_one(&mut *tx).await;

        let subnet_id = match insert_result {
            Ok((id,)) => id,
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
        };

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "Subnet",
            entity_id: Some(subnet_id),
            action: "Created",
            actor_user_id: user_id,
            actor_display: None, client_ip: None, correlation_id: None,
            details: serde_json::json!({
                "source": "bulk_import",
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
) -> ImportRowOutcome {
    let mut errors = Vec::new();

    if row.len() != SUBNET_COLUMNS.len() {
        errors.push(format!("expected {} columns, got {}", SUBNET_COLUMNS.len(), row.len()));
        return ImportRowOutcome {
            row_number, ok: false, errors,
            identifier: row.first().cloned().unwrap_or_default(),
        };
    }

    let subnet_code = row[0].trim();
    let display_name= row[1].trim();
    let network     = row[2].trim();
    let pool_code   = row[4].trim();
    let scope_level = row[5].trim();
    let status      = row[6].trim();

    if subnet_code.is_empty() {
        errors.push("subnet_code is required".into());
    } else if existing_codes.contains(subnet_code) {
        errors.push(format!(
            "subnet_code '{subnet_code}' already exists — update mode not yet supported"));
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
