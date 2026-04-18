//! Library target for the networking-engine. The actual service
//! lives in `main.rs`; this file exists so integration tests under
//! `tests/` can import the crate's modules via
//! `use networking_engine::config_gen;` etc.
//!
//! All module declarations are duplicated from `main.rs` as
//! `pub mod` — `cargo` builds both targets from the same `src/`
//! tree so there's no code duplication, just declaration.

pub mod allocation;
pub mod api;
pub mod audit;
pub mod change_sets;
pub mod cli_flavor;
pub mod config_gen;
pub mod dhcp_relay;
pub mod error;
pub mod hash;
pub mod ip_allocation;
pub mod ip_math;
pub mod ip_math6;
pub mod locks;
pub mod models;
pub mod naming;
pub mod naming_overrides;
pub mod naming_resolver;
pub mod regenerate;
pub mod server_fanout;
pub mod tenant_config;
pub mod validation;
