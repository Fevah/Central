//! Live-DB integration tests for `saved_views::SavedViewRepo`.

use networking_engine::saved_views::{
    CreateSavedViewBody, ListSavedViewsQuery, SavedViewRepo, UpdateSavedViewBody,
};
use sqlx::{PgPool, postgres::PgPoolOptions};
use std::env;
use uuid::Uuid;

async fn pool_or_skip(test_name: &str) -> Option<PgPool> {
    let Ok(dsn) = env::var("TEST_DATABASE_URL") else {
        eprintln!("[{test_name}] skipped: TEST_DATABASE_URL not set");
        return None;
    };
    PgPoolOptions::new().max_connections(4).connect(&dsn).await
        .map_err(|e| eprintln!("[{test_name}] skipped: {e}")).ok()
}

struct Fixture { pool: PgPool, org_id: Uuid }

impl Fixture {
    async fn new(pool: PgPool) -> sqlx::Result<Self> {
        let org_id = Uuid::new_v4();
        sqlx::query(
            "INSERT INTO central_platform.tenants (id, slug, display_name, status)
             VALUES ($1, $2, $2, 'Active') ON CONFLICT (id) DO NOTHING")
            .bind(org_id).bind(format!("sv-itest-{org_id}"))
            .execute(&pool).await?;
        Ok(Self { pool, org_id })
    }
}

impl Drop for Fixture {
    fn drop(&mut self) {
        let pool = self.pool.clone();
        let org_id = self.org_id;
        tokio::spawn(async move {
            let _ = sqlx::query("DELETE FROM central_platform.tenants WHERE id = $1")
                .bind(org_id).execute(&pool).await;
        });
    }
}

// ─── Tests ───────────────────────────────────────────────────────────────

#[tokio::test]
#[ignore]
async fn create_list_round_trip() {
    let Some(pool) = pool_or_skip("create_list_round_trip").await else { return; };
    let fx = Fixture::new(pool).await.expect("fixture");
    let repo = SavedViewRepo::new(fx.pool.clone());

    let v = repo.create(&CreateSavedViewBody {
        organization_id: fx.org_id,
        name: "Critical".into(),
        q: "retired core".into(),
        entity_types: Some("Device".into()),
        filters: serde_json::json!({"status": "Retired"}),
        notes: None,
    }, Some(42)).await.expect("create");
    assert_eq!(v.user_id, 42);
    assert_eq!(v.name, "Critical");

    let list = repo.list(&ListSavedViewsQuery { organization_id: fx.org_id }, Some(42))
        .await.expect("list");
    assert_eq!(list.len(), 1);
    assert_eq!(list[0].id, v.id);
}

#[tokio::test]
#[ignore]
async fn list_returns_only_caller_user_views_not_other_users() {
    let Some(pool) = pool_or_skip("list_returns_only_caller_user_views_not_other_users").await else { return; };
    let fx = Fixture::new(pool).await.expect("fixture");
    let repo = SavedViewRepo::new(fx.pool.clone());

    // User 1 creates a view
    repo.create(&CreateSavedViewBody {
        organization_id: fx.org_id, name: "U1-private".into(),
        q: "".into(), entity_types: None, filters: serde_json::json!({}), notes: None,
    }, Some(1)).await.expect("u1");
    // User 2 creates their own
    repo.create(&CreateSavedViewBody {
        organization_id: fx.org_id, name: "U2-private".into(),
        q: "".into(), entity_types: None, filters: serde_json::json!({}), notes: None,
    }, Some(2)).await.expect("u2");

    // User 2 lists — should NOT see user 1's view.
    let rows = repo.list(&ListSavedViewsQuery { organization_id: fx.org_id }, Some(2))
        .await.expect("u2 list");
    assert_eq!(rows.len(), 1);
    assert_eq!(rows[0].user_id, 2);
    assert_eq!(rows[0].name, "U2-private");
}

#[tokio::test]
#[ignore]
async fn list_without_user_id_returns_empty_not_everyone() {
    let Some(pool) = pool_or_skip("list_without_user_id_returns_empty_not_everyone").await else { return; };
    let fx = Fixture::new(pool).await.expect("fixture");
    let repo = SavedViewRepo::new(fx.pool.clone());

    repo.create(&CreateSavedViewBody {
        organization_id: fx.org_id, name: "someone's view".into(),
        q: "".into(), entity_types: None, filters: serde_json::json!({}), notes: None,
    }, Some(1)).await.expect("create");

    // Service bypass — not an admin backdoor for reading all users'
    // personal views.
    let rows = repo.list(&ListSavedViewsQuery { organization_id: fx.org_id }, None)
        .await.expect("list");
    assert!(rows.is_empty(),
        "service call must return EMPTY list (not everyone's views) — saved views are personal");
}

#[tokio::test]
#[ignore]
async fn get_on_other_users_view_returns_not_found_not_forbidden() {
    // 404 rather than 403 — leaking "this id belongs to someone else"
    // is a privacy hole.
    let Some(pool) = pool_or_skip("get_on_other_users_view_returns_not_found_not_forbidden").await else { return; };
    let fx = Fixture::new(pool).await.expect("fixture");
    let repo = SavedViewRepo::new(fx.pool.clone());

    let v = repo.create(&CreateSavedViewBody {
        organization_id: fx.org_id, name: "private".into(),
        q: "".into(), entity_types: None, filters: serde_json::json!({}), notes: None,
    }, Some(1)).await.expect("u1");

    let err = repo.get(v.id, fx.org_id, Some(2)).await.unwrap_err().to_string();
    assert!(err.contains("saved_view"),
        "error should reference the resource name but not leak ownership: {err}");
}

#[tokio::test]
#[ignore]
async fn update_by_non_owner_rejected_without_mutation() {
    let Some(pool) = pool_or_skip("update_by_non_owner_rejected_without_mutation").await else { return; };
    let fx = Fixture::new(pool).await.expect("fixture");
    let repo = SavedViewRepo::new(fx.pool.clone());

    let v = repo.create(&CreateSavedViewBody {
        organization_id: fx.org_id, name: "original".into(),
        q: "orig".into(), entity_types: None, filters: serde_json::json!({}), notes: None,
    }, Some(1)).await.expect("u1 create");

    let err = repo.update(v.id, fx.org_id, &UpdateSavedViewBody {
        name: "HIJACKED".into(), q: "h".into(), entity_types: None,
        filters: serde_json::json!({}), notes: None, version: v.version,
    }, Some(2)).await.unwrap_err().to_string();
    assert!(err.contains("not owned by caller") || err.contains("version mismatch"),
        "err: {err}");

    // Owner's view unchanged.
    let still = repo.get(v.id, fx.org_id, Some(1)).await.expect("owner read");
    assert_eq!(still.name, "original");
    assert_eq!(still.version, 1);
}

#[tokio::test]
#[ignore]
async fn update_bumps_version_on_optimistic_concurrency_pass() {
    let Some(pool) = pool_or_skip("update_bumps_version_on_optimistic_concurrency_pass").await else { return; };
    let fx = Fixture::new(pool).await.expect("fixture");
    let repo = SavedViewRepo::new(fx.pool.clone());

    let v = repo.create(&CreateSavedViewBody {
        organization_id: fx.org_id, name: "original".into(),
        q: "orig".into(), entity_types: None, filters: serde_json::json!({}), notes: None,
    }, Some(42)).await.expect("create");
    assert_eq!(v.version, 1);

    let updated = repo.update(v.id, fx.org_id, &UpdateSavedViewBody {
        name: "renamed".into(), q: "new q".into(), entity_types: None,
        filters: serde_json::json!({}), notes: None, version: 1,
    }, Some(42)).await.expect("update");
    assert_eq!(updated.version, 2);
    assert_eq!(updated.name, "renamed");

    // Stale version → fails.
    let err = repo.update(v.id, fx.org_id, &UpdateSavedViewBody {
        name: "again".into(), q: "".into(), entity_types: None,
        filters: serde_json::json!({}), notes: None, version: 1,   // stale
    }, Some(42)).await.unwrap_err().to_string();
    assert!(err.contains("version mismatch"), "err: {err}");
}

#[tokio::test]
#[ignore]
async fn create_rejected_without_user_id() {
    let Some(pool) = pool_or_skip("create_rejected_without_user_id").await else { return; };
    let fx = Fixture::new(pool).await.expect("fixture");
    let repo = SavedViewRepo::new(fx.pool.clone());

    let err = repo.create(&CreateSavedViewBody {
        organization_id: fx.org_id, name: "orphan".into(),
        q: "".into(), entity_types: None, filters: serde_json::json!({}), notes: None,
    }, None).await.unwrap_err().to_string();
    assert!(err.contains("require an X-User-Id"),
        "creates without a user should be rejected with a clear message: {err}");
}

#[tokio::test]
#[ignore]
async fn soft_delete_filters_out_of_list() {
    let Some(pool) = pool_or_skip("soft_delete_filters_out_of_list").await else { return; };
    let fx = Fixture::new(pool).await.expect("fixture");
    let repo = SavedViewRepo::new(fx.pool.clone());

    let v = repo.create(&CreateSavedViewBody {
        organization_id: fx.org_id, name: "going-away".into(),
        q: "".into(), entity_types: None, filters: serde_json::json!({}), notes: None,
    }, Some(42)).await.expect("create");

    repo.soft_delete(v.id, fx.org_id, Some(42)).await.expect("delete");

    let rows = repo.list(&ListSavedViewsQuery { organization_id: fx.org_id }, Some(42))
        .await.expect("list after delete");
    assert!(rows.is_empty());
}
