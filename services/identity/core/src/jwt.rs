//! Access-token JWT shape + sign/decode helpers.
//!
//! Claims shape matches what auth-service has been issuing since
//! Phase A (sub / exp / iat / iss / aud / email) with `sid` added
//! in Phase B (session id for logout-by-session). Every identity
//! binary in the workspace signs/verifies through the same struct
//! so an admin-service endpoint that expects a global_admin claim
//! sees exactly what auth-service puts in.

use serde::{Deserialize, Serialize};

/// Claim set every identity JWT carries. All fields except `sid`
/// predate Phase B; `sid` is Phase B+ and is required for `/logout`
/// to revoke a specific session. `#[serde(default)]` on `sid`
/// preserves back-compat with any Phase-A tokens still in flight at
/// migration time.
#[derive(Debug, Serialize, Deserialize)]
pub struct Claims {
    /// User id (UUID as string).
    pub sub:   String,

    /// Expiry (epoch seconds).
    pub exp:   i64,

    /// Issued-at (epoch seconds).
    pub iat:   i64,

    /// Issuer — e.g. `central-auth`. Configurable per deployment.
    pub iss:   String,

    /// Audience — e.g. `central-platform`. Central.Api + auth-service
    /// agree on this string in [config/auth-service.toml].
    pub aud:   String,

    /// Primary email for the subject. Replicated in the access-token
    /// so Central.Api doesn't need to look up the user on every call.
    pub email: String,

    /// Session id (matches `secure_auth.sessions.id`). Phase B+.
    #[serde(default)]
    pub sid: String,
}

/// Sign a claim set with the given HMAC-SHA256 secret. Thin wrapper
/// around [`jsonwebtoken::encode`] so callers don't have to reach
/// into the crate directly; keeps the header defaulted to HS256.
pub fn sign(claims: &Claims, secret: &[u8])
    -> jsonwebtoken::errors::Result<String>
{
    use jsonwebtoken::{encode, EncodingKey, Header};
    encode(&Header::default(), claims, &EncodingKey::from_secret(secret))
}

/// Decode + verify a JWT against the given secret. 30-second clock
/// leeway is applied (matches auth-service's Phase B behaviour for
/// refresh + logout).
pub fn decode_with_leeway(token: &str, secret: &[u8], validate_exp: bool)
    -> jsonwebtoken::errors::Result<Claims>
{
    use jsonwebtoken::{decode, DecodingKey, Validation};
    let mut v = Validation::default();
    v.validate_exp = validate_exp;
    v.validate_aud = false;
    v.leeway       = 30;
    let data = decode::<Claims>(token, &DecodingKey::from_secret(secret), &v)?;
    Ok(data.claims)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn claims(now: i64, ttl: i64) -> Claims {
        Claims {
            sub:   "00000000-0000-0000-0000-000000000001".into(),
            exp:   now + ttl,
            iat:   now,
            iss:   "central-auth".into(),
            aud:   "central-platform".into(),
            email: "test@central.local".into(),
            sid:   "11111111-1111-1111-1111-111111111111".into(),
        }
    }

    #[test]
    fn sign_then_decode_matches_input() {
        let secret = b"test-secret-32-chars-minimum-please";
        let now = 1_700_000_000;
        let original = claims(now, 900);
        let token    = sign(&original, secret).unwrap();
        let decoded  = decode_with_leeway(&token, secret, false).unwrap();

        assert_eq!(decoded.sub,   original.sub);
        assert_eq!(decoded.email, original.email);
        assert_eq!(decoded.sid,   original.sid);
    }

    #[test]
    fn wrong_secret_fails_verification() {
        let secret = b"test-secret-32-chars-minimum-please";
        let token  = sign(&claims(1_700_000_000, 900), secret).unwrap();
        assert!(decode_with_leeway(&token, b"wrong-secret", false).is_err());
    }

    #[test]
    fn expired_token_still_decodes_when_exp_check_disabled() {
        // Logout path decodes with validate_exp=false to allow
        // revoking an already-expired session.
        let secret = b"test-secret-32-chars-minimum-please";
        let token  = sign(&claims(1_000, -1), secret).unwrap();
        assert!(decode_with_leeway(&token, secret, false).is_ok());
        assert!(decode_with_leeway(&token, secret, true).is_err());
    }
}
