//! Token generation + hashing.
//!
//! Refresh tokens, password-reset tokens, and SSO state nonces all
//! share the same shape: 32 bytes of crypto-random entropy,
//! base64url-encoded, with a short type prefix for log grep-ability.
//! The server stores SHA-256 of the raw value so lookup is O(1) + a
//! DB leak doesn't hand the attacker live tokens.
//!
//! Why SHA-256 here when passwords use Argon2: these tokens carry
//! 256 bits of entropy (a cryptographic random choice over 2^256), so
//! rainbow-table / brute-force threats that make Argon2's slowness
//! worthwhile for 12-char human passwords don't apply.

use base64::{engine::general_purpose::URL_SAFE_NO_PAD, Engine};
use sha2::{Digest, Sha256};

/// Generate a fresh refresh token — 32 crypto-random bytes, URL-safe
/// base64, with an `rt_` prefix so it's greppable in logs. The `rt_`
/// marker is cosmetic — the raw random bytes are what provides
/// security; stripping the prefix doesn't change anything meaningful
/// about the token's strength.
pub fn generate_refresh_token() -> String {
    generate_prefixed("rt_")
}

/// Generate a password-reset token. Same shape as a refresh token with
/// a different prefix so `pwrst_` vs `rt_` is distinguishable in
/// structured logs + support workflows.
pub fn generate_password_reset_token() -> String {
    generate_prefixed("pwrst_")
}

/// Generate an SSO state nonce — used by `/sso/:provider/start` to
/// round-trip through an external IDP. Prefix `sso_` so it's obvious
/// in the audit trail.
pub fn generate_sso_state_nonce() -> String {
    generate_prefixed("sso_")
}

/// Inner helper — generates 32 crypto-random bytes + prepends the
/// given prefix. Exposed as `pub(crate)` rather than `pub` because
/// callers should use one of the typed wrappers above; that way a
/// log entry showing `rt_...` vs `sso_...` tells you which code path
/// minted it.
pub(crate) fn generate_prefixed(prefix: &str) -> String {
    use argon2::password_hash::rand_core::{OsRng, RngCore};
    let mut bytes = [0u8; 32];
    OsRng.fill_bytes(&mut bytes);
    format!("{}{}", prefix, URL_SAFE_NO_PAD.encode(bytes))
}

/// SHA-256 hex digest of the input, lowercase 64 chars. Used to store
/// a lookup key for refresh / password-reset / state-nonce tokens
/// server-side without holding the raw value.
pub fn sha256_hex(input: &str) -> String {
    let mut h = Sha256::new();
    h.update(input.as_bytes());
    hex::encode(h.finalize())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn refresh_tokens_have_expected_shape() {
        let t = generate_refresh_token();
        assert!(t.starts_with("rt_"), "prefix: {t}");
        // 32 bytes base64url-no-pad = ceil(32 * 8 / 6) = 43 chars
        assert_eq!(t.len(), "rt_".len() + 43, "len: {t}");
    }

    #[test]
    fn prefixes_distinguish_token_kinds() {
        assert!(generate_refresh_token().starts_with("rt_"));
        assert!(generate_password_reset_token().starts_with("pwrst_"));
        assert!(generate_sso_state_nonce().starts_with("sso_"));
    }

    #[test]
    fn sha256_hex_is_stable_and_64_chars() {
        let a = sha256_hex("hello");
        let b = sha256_hex("hello");
        assert_eq!(a, b, "deterministic");
        assert_eq!(a.len(), 64, "hex SHA-256 is 64 chars");
        assert!(a.chars().all(|c| c.is_ascii_hexdigit() && !c.is_ascii_uppercase()),
                "lowercase hex: {a}");
    }

    #[test]
    fn two_fresh_tokens_never_collide() {
        // Not a proof — just a sanity check that OsRng is doing its job.
        let a = generate_refresh_token();
        let b = generate_refresh_token();
        assert_ne!(a, b);
    }
}
