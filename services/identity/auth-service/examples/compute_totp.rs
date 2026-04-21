//! Dev tool — compute the current TOTP code for a base32 secret.
//! Used by the Phase C end-to-end test; authenticator apps use the
//! same algorithm so the output is what the user would see on their
//! phone.
//!
//! Usage:
//!     cargo run -p auth-service --example compute_totp -- <BASE32_SECRET>

use totp_rs::{Algorithm, Secret, TOTP};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let secret_b32 = std::env::args().nth(1)
        .ok_or("usage: compute_totp <BASE32_SECRET>")?;
    let bytes = Secret::Encoded(secret_b32).to_bytes()?;
    let totp = TOTP::new(Algorithm::SHA1, 6, 1, 30, bytes,
                         None, String::new())?;
    println!("{}", totp.generate_current()?);
    Ok(())
}
