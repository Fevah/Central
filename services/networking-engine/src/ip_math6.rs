//! IPv6 CIDR arithmetic in `u128` space. Port of `libs/persistence/Net/IpMath6.cs`.
//!
//! IPv6 drops the broadcast reservation and the `/31` corner case (RFC 4291 — every
//! address is usable), so the shape matches v4 with fewer special cases.

use std::net::Ipv6Addr;
use std::str::FromStr;

use crate::error::EngineError;

pub fn parse_v6(cidr: &str) -> Result<(u128, u128, u32), EngineError> {
    let slash = cidr.find('/')
        .ok_or_else(|| EngineError::bad_cidr(cidr))?;
    if slash == 0 || slash == cidr.len() - 1 {
        return Err(EngineError::bad_cidr(cidr));
    }
    let addr_part = cidr[..slash].trim();
    let prefix_part = cidr[slash + 1..].trim();

    let addr = Ipv6Addr::from_str(addr_part)
        .map_err(|_| EngineError::bad_cidr(cidr))?;
    let prefix: u32 = prefix_part.parse()
        .map_err(|_| EngineError::bad_cidr(cidr))?;
    if prefix > 128 {
        return Err(EngineError::bad_cidr(cidr));
    }

    let value = u128::from(addr);
    let mask: u128 = if prefix == 0 {
        0
    } else {
        u128::MAX << (128 - prefix)
    };
    let network = value & mask;
    let last = network | !mask;
    Ok((network, last, prefix))
}

pub fn to_ip(value: u128) -> String {
    Ipv6Addr::from(value).to_string()
}

pub fn to_cidr(network: u128, prefix: u32) -> String {
    format!("{}/{}", to_ip(network), prefix)
}

/// Host-usable range for an IPv6 subnet — every address is usable (RFC 4291).
pub fn host_range(network: u128, last: u128) -> (u128, u128) {
    (network, last)
}

/// Block size as `u128`: `2 ^ (128 - prefix)`. A `/64` is `2^64`.
pub fn block_size(prefix: u32) -> u128 {
    if prefix == 0 { 0 } else { 1u128 << (128 - prefix) }
}

pub fn align_up(address: u128, stride: u128) -> u128 {
    if stride == 0 { return address; }
    let rem = address % stride;
    if rem == 0 { address } else { address + (stride - rem) }
}

#[allow(dead_code)]
pub fn overlaps(a1: u128, a2: u128, b1: u128, b2: u128) -> bool {
    a1 <= b2 && b1 <= a2
}

pub fn ip_to_u128(ip: &str) -> Result<u128, EngineError> {
    let addr = Ipv6Addr::from_str(ip).map_err(|_| EngineError::bad_cidr(ip))?;
    Ok(u128::from(addr))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_v6_slash_32() {
        let (net, last, prefix) = parse_v6("2001:db8::/32").unwrap();
        assert_eq!(to_ip(net), "2001:db8::");
        // /32 leaves 96 host bits; last = network + 2^96 - 1.
        assert_eq!(prefix, 32);
        assert!(last > net);
    }

    #[test]
    fn parse_v6_slash_128_single_host() {
        let (net, last, prefix) = parse_v6("2001:db8::1/128").unwrap();
        assert_eq!(net, last);
        assert_eq!(prefix, 128);
    }

    #[test]
    fn parse_v6_slash_64() {
        let (net, last, _) = parse_v6("fe80::/64").unwrap();
        assert_eq!(to_ip(net), "fe80::");
        assert_eq!(last - net, (1u128 << 64) - 1);
    }

    #[test]
    fn parse_v6_host_bits_set_are_masked() {
        let (net, _, _) = parse_v6("2001:db8::5/32").unwrap();
        assert_eq!(to_ip(net), "2001:db8::");
    }

    #[test]
    fn parse_v6_rejects_malformed() {
        assert!(parse_v6("not-v6").is_err());
        assert!(parse_v6("2001:db8::").is_err());
        assert!(parse_v6("2001:db8::/129").is_err());
    }

    #[test]
    fn host_range_has_no_reservations() {
        let (net, last, _) = parse_v6("2001:db8::/120").unwrap();
        let (first_usable, last_usable) = host_range(net, last);
        assert_eq!(first_usable, net);
        assert_eq!(last_usable, last);
    }

    #[test]
    fn block_size_slash_64() {
        assert_eq!(block_size(64), 1u128 << 64);
        assert_eq!(block_size(128), 1);
    }

    #[test]
    fn align_up_noop_when_aligned() {
        let stride = 1u128 << 16;
        assert_eq!(align_up(0, stride), 0);
        assert_eq!(align_up(stride, stride), stride);
    }

    #[test]
    fn align_up_rounds_up() {
        let stride = 16;
        assert_eq!(align_up(1, stride), 16);
        assert_eq!(align_up(17, stride), 32);
    }

    #[test]
    fn overlaps_basic() {
        assert!(overlaps(10, 20, 15, 25));
        assert!(!overlaps(10, 20, 21, 30));
    }

    #[test]
    fn ip_to_u128_round_trips() {
        let v = ip_to_u128("2001:db8::1").unwrap();
        assert_eq!(to_ip(v), "2001:db8::1");
    }
}
