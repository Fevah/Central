//! XLSX round-trip — thin adapter over the existing CSV pipeline.
//! The bulk export + import modules produce + parse RFC 4180 CSV;
//! this module wraps that CSV body as an XLSX workbook (for download)
//! or unwraps an uploaded XLSX into CSV (for import).
//!
//! ## Why adapt rather than re-implement
//!
//! Operators working in Excel want .xlsx. The CSV path already
//! encodes every escape invariant + column order that makes the
//! exports round-trippable; re-implementing per-entity XLSX
//! emitters would duplicate every one of those invariants and let
//! CSV and XLSX drift. Keeping XLSX a pure transport adapter means
//! one source of truth per entity.
//!
//! ## Read side
//!
//! `calamine` opens the workbook, reads the first sheet, and
//! produces a cell grid. We serialise that grid back to RFC 4180
//! CSV (via `bulk_export::csv_escape` + `csv_row`) so the existing
//! `bulk_import::parse_csv` + per-entity validators don't change.
//!
//! ## Write side
//!
//! `rust_xlsxwriter` builds the workbook in memory, writes one row
//! per CSV row. The first row (header) is bold.

use calamine::{Data, Reader, Xlsx};
use rust_xlsxwriter::{Format, Workbook};
use std::io::Cursor;

use crate::bulk_export::{csv_escape, csv_row};
use crate::error::EngineError;

/// Convert an RFC 4180 CSV body into an in-memory XLSX workbook.
/// Sheet is named by `sheet_name` (truncated to 31 chars — Excel's
/// hard limit on sheet names). First row styled bold as a header.
pub fn csv_body_to_xlsx(csv: &str, sheet_name: &str) -> Result<Vec<u8>, EngineError> {
    let rows = crate::bulk_import::parse_csv(csv)?;

    let mut wb = Workbook::new();
    // Sheet names: max 31 chars, no / \ * [ ] : ? — keep it safe
    // by truncating + substituting any banned chars.
    let safe_name: String = sheet_name.chars()
        .take(31)
        .map(|c| if matches!(c, '/'|'\\'|'*'|'['|']'|':'|'?') { '_' } else { c })
        .collect();
    let ws = wb.add_worksheet();
    ws.set_name(&safe_name)
        .map_err(|e| EngineError::bad_request(format!("xlsx sheet name error: {e}")))?;

    let header_fmt = Format::new().set_bold();

    for (row_idx, row) in rows.iter().enumerate() {
        for (col_idx, cell) in row.iter().enumerate() {
            let r = row_idx as u32;
            let c = col_idx as u16;
            if row_idx == 0 {
                ws.write_string_with_format(r, c, cell, &header_fmt)
            } else {
                ws.write_string(r, c, cell)
            }
            .map_err(|e| EngineError::bad_request(format!("xlsx write error at ({r},{c}): {e}")))?;
        }
    }

    wb.save_to_buffer()
        .map_err(|e| EngineError::bad_request(format!("xlsx save error: {e}")))
}

/// Convert an uploaded XLSX body into RFC 4180 CSV that
/// `bulk_import` can parse. Reads the FIRST sheet only (operators
/// importing a multi-sheet workbook should split it upstream — we
/// don't want to guess which sheet they meant).
///
/// Cell values are serialised as their display form: strings as-is,
/// numbers without scientific notation, dates as ISO 8601. Empty
/// cells become empty CSV fields.
pub fn xlsx_bytes_to_csv(bytes: &[u8]) -> Result<String, EngineError> {
    let cursor = Cursor::new(bytes.to_vec());
    let mut workbook: Xlsx<_> = calamine::open_workbook_from_rs(cursor)
        .map_err(|e| EngineError::bad_request(format!("xlsx parse error: {e}")))?;

    let sheet_names = workbook.sheet_names();
    let first_sheet = sheet_names.first()
        .ok_or_else(|| EngineError::bad_request("xlsx has no sheets"))?
        .clone();
    let range = workbook.worksheet_range(&first_sheet)
        .map_err(|e| EngineError::bad_request(format!("xlsx sheet read error: {e}")))?;

    let mut out = String::with_capacity(range.width() * range.height() * 16);
    for row in range.rows() {
        let fields: Vec<String> = row.iter().map(cell_to_string).map(|s| csv_escape(&s)).collect();
        out.push_str(&csv_row(&fields));
    }
    Ok(out)
}

/// Render one calamine cell as its plain string form. Matches how
/// Excel copy-paste produces values: numbers flat (no `1E+05`),
/// bools as `true`/`false`, dates as ISO, empty as `""`.
fn cell_to_string(cell: &Data) -> String {
    match cell {
        Data::Empty            => String::new(),
        Data::String(s)        => s.clone(),
        Data::Float(f)         => {
            // Avoid scientific notation; trim trailing .0 so "42.0"
            // imports as "42" and downstream i32::parse works.
            if f.fract() == 0.0 && f.is_finite() && f.abs() < 1e15 {
                format!("{}", *f as i64)
            } else {
                format!("{}", f)
            }
        }
        Data::Int(i)           => i.to_string(),
        Data::Bool(b)          => b.to_string(),
        Data::DateTime(dt)     => dt.to_string(),
        Data::DateTimeIso(s)   => s.clone(),
        Data::DurationIso(s)   => s.clone(),
        Data::Error(e)         => format!("#{:?}", e),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn csv_to_xlsx_produces_non_empty_bytes_for_a_simple_header_row() {
        let csv = "hostname,role_code\r\nSRV-A,Core\r\nSRV-B,Core\r\n";
        let xlsx = csv_body_to_xlsx(csv, "test").expect("xlsx");
        // Actual byte shape is rust_xlsxwriter's business; we just
        // need to confirm it produced a real workbook (non-empty +
        // starts with the PK zip magic because .xlsx is a zip).
        assert!(!xlsx.is_empty(), "xlsx bytes should be non-empty");
        assert_eq!(&xlsx[..2], b"PK",
            "xlsx file signature must be PK (zip archive): {:?}",
            &xlsx[..4.min(xlsx.len())]);
    }

    #[test]
    fn csv_to_xlsx_back_to_csv_round_trips_cell_contents() {
        let original = "hostname,description\r\nSRV-A,plain\r\nSRV-B,has a comma,inside\r\n";
        // Note: the "has a comma,inside" cell is currently going in
        // as ONE CSV field via the parser understanding quotes.
        // Using csv_escape input — operator submits properly-quoted CSV.
        let input = "hostname,description\r\nSRV-A,plain\r\nSRV-B,\"has a comma, inside\"\r\n";
        let xlsx = csv_body_to_xlsx(input, "data").expect("xlsx");
        let back = xlsx_bytes_to_csv(&xlsx).expect("parse back");

        // Parse both via bulk_import::parse_csv and compare row-by-row.
        let original_rows = crate::bulk_import::parse_csv(input).expect("input rows");
        let round_trip_rows = crate::bulk_import::parse_csv(&back).expect("round-trip rows");

        assert_eq!(original_rows, round_trip_rows,
            "cell contents must round-trip through CSV→XLSX→CSV unchanged");
        // Silence the unused-var warning without suppressing the
        // signal this assertion gives about the original shape.
        let _ = original;
    }

    #[test]
    fn csv_to_xlsx_handles_empty_body() {
        // Empty CSV should produce a minimal valid workbook with an
        // empty sheet — not an error.
        let xlsx = csv_body_to_xlsx("", "empty").expect("xlsx");
        assert_eq!(&xlsx[..2], b"PK");
    }

    #[test]
    fn csv_to_xlsx_truncates_long_sheet_names_to_excel_31_char_limit() {
        let csv = "a,b\r\n1,2\r\n";
        let long = "this-sheet-name-is-definitely-more-than-thirty-one-chars-long";
        // Should not error — the helper truncates to 31 chars.
        let xlsx = csv_body_to_xlsx(csv, long).expect("xlsx");
        assert!(!xlsx.is_empty());
    }

    #[test]
    fn csv_to_xlsx_substitutes_banned_sheet_name_chars() {
        // Excel rejects /\*[]:? in sheet names. The helper replaces
        // each with `_` rather than rejecting the request.
        let csv = "a\r\n1\r\n";
        let xlsx = csv_body_to_xlsx(csv, "foo/bar:baz").expect("xlsx");
        assert!(!xlsx.is_empty(),
            "helper must substitute banned chars not reject the write");
    }

    #[test]
    fn xlsx_bytes_to_csv_rejects_non_xlsx_input() {
        let err = xlsx_bytes_to_csv(b"not an xlsx file").unwrap_err().to_string();
        assert!(err.contains("xlsx parse error"), "err: {err}");
    }

    #[test]
    fn cell_to_string_formats_floats_without_trailing_zero() {
        // VLAN id arriving from Excel as the float 120.0 must land
        // in CSV as "120" so the downstream i32 parse works.
        assert_eq!(cell_to_string(&Data::Float(120.0)), "120");
        assert_eq!(cell_to_string(&Data::Float(65112.0)), "65112");
    }

    #[test]
    fn cell_to_string_keeps_real_decimals() {
        assert_eq!(cell_to_string(&Data::Float(3.14)), "3.14");
    }

    #[test]
    fn cell_to_string_empty_cell_becomes_empty_string() {
        assert_eq!(cell_to_string(&Data::Empty), "");
    }

    #[test]
    fn cell_to_string_bool_roundtrips_as_true_false() {
        assert_eq!(cell_to_string(&Data::Bool(true)),  "true");
        assert_eq!(cell_to_string(&Data::Bool(false)), "false");
    }
}
