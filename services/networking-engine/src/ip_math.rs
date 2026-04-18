//! IPv4 CIDR arithmetic in `i64` space. Port of `libs/persistence/Net/IpMath.cs`.
//!
//! Using `i64` (not `u32`) sidesteps sign-bit gymnastics when computing `last - first + 1`
//! on supernets near the top of the space. The v6 sibling [`crate::ip_math6`] uses `u128`.

use std::net::Ipv4Addr;
use std::str::FromStr;

use crate::error::EngineError;

/// CIDR parse result: `(network, broadcast, prefix)`.
pub fn parse_v4(cidr: &str) -> Result<(i64, i64, u32), EngineError> {
    let slash = cidr.find('/')
        .ok_or_else(|| EngineError::bad_cidr(cidr))?;
    if slash == 0 || slash == cidr.len() - 1 {
        return Err(EngineError::bad_cidr(cidr));
    }
    let addr_part = cidr[..slash].trim();
    let prefix_part = cidr[slash + 1..].trim();

    let addr = Ipv4Addr::from_str(addr_part)
        .map_err(|_| EngineError::bad_cidr(cidr))?;
    let prefix: u32 = prefix_part.parse()
        .map_err(|_| EngineError::bad_cidr(cidr))?;
    if prefix > 32 {
        return Err(EngineError::bad_cidr(cidr));
    }

    let addr_long = u32::from(addr) as i64;
    let mask: i64 = if prefix == 0 {
        0
    } else {
        ((0xFFFF_FFFFu32 << (32 - prefix)) as i64) & 0xFFFF_FFFF
    };
    let network = addr_long & mask;
    let broadcast = network | ((!mask) & 0xFFFF_FFFF);
    Ok((network, broadcast, prefix))
}

/// Convert a numeric IPv4 value back to dotted-quad notation.
pub fn to_ip(value: i64) -> String {
    let v = (value & 0xFFFF_FFFF) as u32;
    Ipv4Addr::from(v).to_string()
}

/// Format a CIDR string from a network address and prefix.
pub fn to_cidr(network: i64, prefix: u32) -> String {
    format!("{}/{}", to_ip(network), prefix)
}

/// Host-usable range inside a CIDR.
///
/// * `/30` and larger → reserve network + broadcast (RFC 1812).
/// * `/31` → both addresses usable (RFC 3021 point-to-point).
/// * `/32` → single host.
pub fn host_range(network: i64, broadcast: i64, prefix: u32) -> (i64, i64) {
    if prefix >= 31 {
        (network, broadcast)
    } else {
        (network + 1, broadcast - 1)
    }
}

/// Block size for a prefix length. `/24` → 256, `/30` → 4, `/32` → 1.
pub fn block_size(prefix: u32) -> i64 {
    1_i64 << (32 - prefix)
}

/// Round `address` up to the next `stride`-aligned value.
pub fn align_up(address: i64, stride: i64) -> i64 {
    if stride <= 0 { return address; }
    let rem = address % stride;
    if rem == 0 { address } else { address + (stride - rem) }
}

/// Inclusive interval overlap check.
#[allow(dead_code)]
pub fn overlaps(a1: i64, a2: i64, b1: i64, b2: i64) -> bool {
    a1 <= b2 && b1 <= a2
}

/// Convert a dotted-quad string to an `i64` numeric value.
pub fn ip_to_long(ip: &str) -> Result<i64, EngineError> {
    let addr = Ipv4Addr::from_str(ip).map_err(|_| EngineError::bad_cidr(ip))?;
    Ok(u32::from(addr) as i64)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_v4_basic() {
        let (net, bcast, prefix) = parse_v4("10.0.0.0/8").unwrap();
        assert_eq!(net, 0x0A00_0000);
        assert_eq!(bcast, 0x0AFF_FFFF);
        assert_eq!(prefix, 8);
    }

    #[test]
    fn parse_v4_slash_24() {
        let (net, bcast, prefix) = parse_v4("192.168.1.0/24").unwrap();
        assert_eq!(net, 0xC0A8_0100);
        assert_eq!(bcast, 0xC0A8_01FF);
        assert_eq!(prefix, 24);
    }

    #[test]
    fn parse_v4_host_bits_set_are_masked() {
        // 10.1.2.3/24 → network 10.1.2.0
        let (net, _, _) = parse_v4("10.1.2.3/24").unwrap();
        assert_eq!(to_ip(net), "10.1.2.0");
    }

    #[test]
    fn parse_v4_slash_0_is_whole_space() {
        let (net, bcast, prefix) = parse_v4("0.0.0.0/0").unwrap();
        assert_eq!(net, 0);
        assert_eq!(bcast, 0xFFFF_FFFF);
        assert_eq!(prefix, 0);
    }

    #[test]
    fn parse_v4_slash_32_is_single_host() {
        let (net, bcast, prefix) = parse_v4("1.2.3.4/32").unwrap();
        assert_eq!(net, bcast);
        assert_eq!(prefix, 32);
    }

    #[test]
    fn parse_v4_rejects_malformed() {
        assert!(parse_v4("not-a-cidr").is_err());
        assert!(parse_v4("10.0.0.0").is_err());
        assert!(parse_v4("10.0.0.0/33").is_err());
        assert!(parse_v4("/24").is_err());
        assert!(parse_v4("10.0.0.0/").is_err());
    }

    #[test]
    fn to_ip_round_trip() {
        assert_eq!(to_ip(0x0A00_0001), "10.0.0.1");
        assert_eq!(to_ip(0xC0A8_0101), "192.168.1.1");
        assert_eq!(to_ip(0), "0.0.0.0");
        assert_eq!(to_ip(0xFFFF_FFFF), "255.255.255.255");
    }

    #[test]
    fn host_range_slash_24_excludes_network_and_broadcast() {
        let (first, last) = host_range(0x0A00_0000, 0x0A00_00FF, 24);
        assert_eq!(to_ip(first), "10.0.0.1");
        assert_eq!(to_ip(last), "10.0.0.254");
    }

    #[test]
    fn host_range_slash_31_uses_both_addresses() {
        let (first, last) = host_range(0x0A00_0000, 0x0A00_0001, 31);
        assert_eq!(first, 0x0A00_0000);
        assert_eq!(last, 0x0A00_0001);
    }

    #[test]
    fn host_range_slash_32_is_single_host() {
        let (first, last) = host_range(0x0A00_0005, 0x0A00_0005, 32);
        assert_eq!(first, last);
    }

    #[test]
    fn block_size_for_common_prefixes() {
        assert_eq!(block_size(32), 1);
        assert_eq!(block_size(30), 4);
        assert_eq!(block_size(24), 256);
        assert_eq!(block_size(16), 65536);
    }

    #[test]
    fn align_up_noop_when_already_aligned() {
        assert_eq!(align_up(0x0A00_0000, 256), 0x0A00_0000);
        assert_eq!(align_up(0x0A00_0100, 256), 0x0A00_0100);
    }

    #[test]
    fn align_up_rounds_to_next_stride() {
        assert_eq!(align_up(0x0A00_0001, 256), 0x0A00_0100);
        assert_eq!(align_up(0x0A00_0003, 4), 0x0A00_0004);
    }

    #[test]
    fn overlaps_detects_basic_overlap() {
        assert!(overlaps(10, 20, 15, 25));
        assert!(overlaps(10, 20, 5, 15));
        assert!(overlaps(10, 20, 10, 20));
        assert!(!overlaps(10, 20, 21, 30));
        assert!(!overlaps(10, 20, 0, 9));
    }

    #[test]
    fn overlaps_treats_touching_endpoints_as_overlap() {
        // Inclusive convention: a1 == b2 counts as overlap.
        assert!(overlaps(10, 20, 20, 30));
        assert!(overlaps(20, 30, 10, 20));
    }

    #[test]
    fn ip_to_long_round_trips() {
        let n = ip_to_long("10.0.0.1").unwrap();
        assert_eq!(to_ip(n), "10.0.0.1");
    }
}
