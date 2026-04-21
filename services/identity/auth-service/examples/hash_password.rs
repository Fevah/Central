//! Dev tool — compute an Argon2id hash for a password passed as argv[1].
//!
//! Use from repo root:
//!
//!     cargo run -p auth-service --example hash_password -- 'your-password'
//!
//! Prints the hash string suitable for dropping into a SQL migration's
//! `password_hash` column. Output is deterministic *format* (same
//! parameters + algorithm) but the salt is random per run, so two
//! invocations on the same password produce different valid hashes.

use argon2::{password_hash::{PasswordHasher, SaltString, rand_core::OsRng}, Argon2};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let pwd = std::env::args().nth(1)
        .ok_or("usage: hash_password <password>")?;
    let salt = SaltString::generate(&mut OsRng);
    let hash = Argon2::default().hash_password(pwd.as_bytes(), &salt)?;
    println!("{}", hash);
    Ok(())
}
