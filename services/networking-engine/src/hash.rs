//! Deterministic 63-bit hash of a UUID for `pg_advisory_xact_lock` keys.
//!
//! Must produce byte-for-byte identical output to the C# `AllocationService.StableHash(Guid)`
//! in `libs/persistence/Net/AllocationService.cs` so Rust callers and any leftover C# callers
//! serialise on the same lock during the Phase 6.5 cutover.
//!
//! Algorithm: FNV-1a 64-bit over the GUID's byte representation, then clear the sign bit
//! so the value fits Postgres's signed `bigint`.
//!
//! **Byte-order contract:** the C# side uses `Guid.ToByteArray()`, which returns the .NET
//! mixed-endian layout (Data1/Data2/Data3 little-endian, Data4 big-endian). The `uuid` crate's
//! `as_bytes()` returns RFC 4122 big-endian. To match C#, we reorder bytes 0-7 on the way in.

use uuid::Uuid;

const FNV_OFFSET: u64 = 14695981039346656037;
const FNV_PRIME: u64 = 1099511628211;

/// 63-bit stable hash of a UUID. Identical output to the C# `StableHash(Guid)` helper.
pub fn stable_hash(id: Uuid) -> i64 {
    let cs_bytes = to_dotnet_guid_bytes(id);
    let mut hash: u64 = FNV_OFFSET;
    for b in cs_bytes {
        hash ^= b as u64;
        hash = hash.wrapping_mul(FNV_PRIME);
    }
    (hash & 0x7FFF_FFFF_FFFF_FFFF) as i64
}

/// Reorder `uuid` crate bytes (RFC 4122 big-endian) into the .NET `Guid.ToByteArray()`
/// mixed-endian layout so FNV-1a consumes the same byte sequence on both sides.
fn to_dotnet_guid_bytes(id: Uuid) -> [u8; 16] {
    let b = id.as_bytes();
    [
        b[3], b[2], b[1], b[0],   // Data1 (u32) → little-endian
        b[5], b[4],               // Data2 (u16) → little-endian
        b[7], b[6],               // Data3 (u16) → little-endian
        b[8], b[9], b[10], b[11], // Data4 (8 bytes) → as-is
        b[12], b[13], b[14], b[15],
    ]
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn zero_guid_matches_csharp() {
        // Reference: C# `StableHash(Guid.Empty)` FNV-1a over 16 zero bytes.
        // 16 rounds of: hash = (hash ^ 0) * FNV_PRIME = hash * FNV_PRIME
        let mut expected: u64 = FNV_OFFSET;
        for _ in 0..16 {
            expected = expected.wrapping_mul(FNV_PRIME);
        }
        let expected = (expected & 0x7FFF_FFFF_FFFF_FFFF) as i64;
        assert_eq!(stable_hash(Uuid::nil()), expected);
    }

    #[test]
    fn different_guids_hash_differently() {
        let a = Uuid::parse_str("11111111-1111-1111-1111-111111111111").unwrap();
        let b = Uuid::parse_str("22222222-2222-2222-2222-222222222222").unwrap();
        assert_ne!(stable_hash(a), stable_hash(b));
    }

    #[test]
    fn hash_is_always_non_negative() {
        // 63-bit hash must fit in signed i64 without sign-bit confusion on the PG side.
        for _ in 0..1000 {
            assert!(stable_hash(Uuid::new_v4()) >= 0);
        }
    }

    #[test]
    fn hash_is_deterministic() {
        let id = Uuid::parse_str("deadbeef-1234-5678-9abc-def012345678").unwrap();
        assert_eq!(stable_hash(id), stable_hash(id));
    }

    #[test]
    fn dotnet_byte_order_is_mixed_endian() {
        // Guid 00112233-4455-6677-8899-aabbccddeeff under .NET ToByteArray() produces
        // [33 22 11 00 55 44 77 66 88 99 AA BB CC DD EE FF]. Verify the reordering.
        let id = Uuid::parse_str("00112233-4455-6677-8899-aabbccddeeff").unwrap();
        let bytes = to_dotnet_guid_bytes(id);
        assert_eq!(bytes, [
            0x33, 0x22, 0x11, 0x00,
            0x55, 0x44,
            0x77, 0x66,
            0x88, 0x99, 0xaa, 0xbb,
            0xcc, 0xdd, 0xee, 0xff,
        ]);
    }
}
