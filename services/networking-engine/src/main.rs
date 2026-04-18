//! Central Networking Engine — Rust port of the services previously living in
//! `libs/persistence/Net/*.cs` and the naming services from `libs/engine/Net/`.
//!
//! Owns allocation (ASN / VLAN / MLAG / IP / subnet), reservation-shelf cool-down,
//! 4-NIC server fan-out, and naming-template expansion. Phase 6.5 of the
//! networking buildout (see `docs/NETWORKING_BUILDOUT_PLAN.md`).
//!
//! Designed so every .NET caller today can be swapped to hit this service over
//! HTTP with matching semantics — including `pg_advisory_xact_lock` keys
//! (shared FNV-1a StableHash) so the two codepaths serialise together during
//! the cutover window.

mod allocation;
mod api;
mod audit;
mod change_sets;
mod cli_flavor;
mod config_gen;
mod error;
mod hash;
mod ip_allocation;
mod ip_math;
mod ip_math6;
mod locks;
mod models;
mod naming;
mod naming_overrides;
mod naming_resolver;
mod regenerate;
mod server_fanout;
mod tenant_config;
mod validation;

use anyhow::Result;
use std::env;
use tracing_subscriber::{EnvFilter, fmt};

#[tokio::main]
async fn main() -> Result<()> {
    fmt()
        .with_env_filter(EnvFilter::try_from_default_env()
            .unwrap_or_else(|_| EnvFilter::new("info")))
        .json()
        .init();

    let dsn = env::var("DATABASE_URL")
        .expect("DATABASE_URL required (central tenant DB connection)");
    let bind = env::var("BIND_ADDR").unwrap_or_else(|_| "0.0.0.0:8091".into());

    tracing::info!("networking-engine starting on {bind}");

    let pool = sqlx::postgres::PgPoolOptions::new()
        .max_connections(20)
        .connect(&dsn)
        .await?;

    let state = api::AppState { pool };
    let app = api::build_router(state);

    let listener = tokio::net::TcpListener::bind(&bind).await?;
    axum::serve(listener, app).await?;
    Ok(())
}
