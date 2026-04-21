//! # identity-core
//!
//! Shared identity primitives for the `services/identity/` workspace.
//!
//! Phase 3 of [docs/IDP_BUILDOUT.md]. Lifted from `auth-service` so that
//! future binaries in the workspace (`admin-service`, `federation` in
//! phases 4 + 7) don't duplicate the same JWT / password / token logic.
//!
//! What lives here:
//!
//! * [`tokens`] — crypto-random refresh tokens (URL-safe base64) +
//!   SHA-256 hashing for deterministic lookup. Shared across refresh,
//!   password-reset, SSO state nonces.
//! * [`passwords`] — Argon2id hash + verify wrappers.
//! * [`jwt`] — [`Claims`] struct + sign / decode helpers. Shape matches
//!   the tokens auth-service has been issuing since Phase A so existing
//!   clients don't need to change.
//!
//! What deliberately stays in `auth-service` (for now):
//!
//! * Config file parsing + DB connection setup. Will move here once
//!   `admin-service` + `federation` need it in phases 4 + 7.
//! * HTTP handlers. Those are service-specific; `core` is pure logic.

pub mod tokens;
pub mod passwords;
pub mod jwt;
