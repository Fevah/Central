//! Argon2id password hashing + verification.
//!
//! All password work in `services/identity/` flows through these two
//! functions — auth-service login, /change-password, /password-reset/
//! confirm, MFA recovery-code verify. Centralising here means a
//! future CredentialEncryptor wrap (Phase G) touches one module
//! instead of three.
//!
//! Default parameters are `argon2` crate defaults: Argon2id, m=19MiB,
//! t=2, p=1. Suitable for interactive logins on commodity hardware —
//! ~200ms per hash on a 2022 laptop. Policy tuning happens in the
//! config layer (Phase D+); callers that want custom cost can build
//! their own `Argon2` instance and skip these helpers.

use argon2::{
    password_hash::{PasswordHash, PasswordHasher, PasswordVerifier, SaltString, Error},
    Argon2,
};

/// Argon2id-hash a password with a fresh random salt. Returns the
/// full phc-string (`$argon2id$v=19$m=...$<salt>$<hash>`) suitable
/// for drop-in storage in a `text` / `varchar` column.
pub fn hash_password(plain: &str) -> Result<String, Error> {
    use argon2::password_hash::rand_core::OsRng;
    let salt = SaltString::generate(&mut OsRng);
    Argon2::default()
        .hash_password(plain.as_bytes(), &salt)
        .map(|h| h.to_string())
}

/// Verify a candidate password against a stored Argon2 phc-string.
/// `false` on bad hash format, wrong password, or any verify error
/// — callers get a single yes/no signal so they can't accidentally
/// leak "bad hash" vs "wrong password" to the client.
pub fn verify_password(plain: &str, stored_hash: &str) -> bool {
    let parsed = match PasswordHash::new(stored_hash) {
        Ok(p)  => p,
        Err(_) => return false,
    };
    Argon2::default().verify_password(plain.as_bytes(), &parsed).is_ok()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn round_trip_correct_password() {
        let hash = hash_password("correct horse battery staple").unwrap();
        assert!(verify_password("correct horse battery staple", &hash));
    }

    #[test]
    fn rejects_wrong_password() {
        let hash = hash_password("first").unwrap();
        assert!(!verify_password("second", &hash));
    }

    #[test]
    fn rejects_garbled_hash_without_panicking() {
        assert!(!verify_password("anything", "not-a-real-argon2-hash"));
        assert!(!verify_password("anything", ""));
        assert!(!verify_password("anything", "$argon2id$bogus"));
    }

    #[test]
    fn two_hashes_of_same_password_differ_by_salt() {
        let a = hash_password("same").unwrap();
        let b = hash_password("same").unwrap();
        assert_ne!(a, b, "fresh salt per hash");
        assert!(verify_password("same", &a));
        assert!(verify_password("same", &b));
    }
}
