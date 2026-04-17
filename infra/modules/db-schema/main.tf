# =============================================================================
# Database Schema Module — applies all SQL migrations + seed data
# =============================================================================
# Uses postgresql provider to apply migrations declaratively.
# For production, prefer the K8s Job approach (infra/k8s/base/db-migrations.yaml)
# and use this module only for initial extension/role creation.
# =============================================================================

terraform {
  required_providers {
    postgresql = {
      source  = "cyrilgdn/postgresql"
      version = "~> 1.23"
    }
  }
}

# ── Extensions ──────────────────────────────────────────────────────────────

resource "postgresql_extension" "uuid_ossp" {
  name     = "uuid-ossp"
  database = var.database_name
}

resource "postgresql_extension" "pgcrypto" {
  name     = "pgcrypto"
  database = var.database_name
}

resource "postgresql_extension" "pg_trgm" {
  name     = "pg_trgm"
  database = var.database_name
}

resource "postgresql_extension" "citext" {
  name     = "citext"
  database = var.database_name
}

resource "postgresql_extension" "btree_gin" {
  name     = "btree_gin"
  database = var.database_name
}

resource "postgresql_extension" "pg_stat_statements" {
  name     = "pg_stat_statements"
  database = var.database_name
}

# ── Roles ───────────────────────────────────────────────────────────────────

resource "postgresql_role" "app_user" {
  name     = var.app_db_user
  login    = true
  password = var.app_db_password

  # Least-privilege: no superuser, no create DB
  superuser = false
  create_database = false
  create_role = false
}

resource "postgresql_role" "readonly" {
  name    = "${var.app_db_user}_readonly"
  login   = true
  password = var.readonly_password
}

resource "postgresql_role" "backup" {
  name            = "${var.app_db_user}_backup"
  login           = true
  password        = var.backup_password
  replication     = true
}

# ── Schema Grants ───────────────────────────────────────────────────────────

resource "postgresql_grant" "app_user_schema" {
  database    = var.database_name
  role        = postgresql_role.app_user.name
  schema      = "public"
  object_type = "schema"
  privileges  = ["USAGE", "CREATE"]
}

resource "postgresql_grant" "app_user_tables" {
  database    = var.database_name
  role        = postgresql_role.app_user.name
  schema      = "public"
  object_type = "table"
  privileges  = ["SELECT", "INSERT", "UPDATE", "DELETE", "TRUNCATE", "REFERENCES"]
}

resource "postgresql_grant" "app_user_sequences" {
  database    = var.database_name
  role        = postgresql_role.app_user.name
  schema      = "public"
  object_type = "sequence"
  privileges  = ["USAGE", "SELECT", "UPDATE"]
}

resource "postgresql_grant" "readonly_schema" {
  database    = var.database_name
  role        = postgresql_role.readonly.name
  schema      = "public"
  object_type = "schema"
  privileges  = ["USAGE"]
}

resource "postgresql_grant" "readonly_tables" {
  database    = var.database_name
  role        = postgresql_role.readonly.name
  schema      = "public"
  object_type = "table"
  privileges  = ["SELECT"]
}

# ── Default privileges for future objects ───────────────────────────────────

resource "postgresql_default_privileges" "app_user_future_tables" {
  role        = postgresql_role.app_user.name
  database    = var.database_name
  schema      = "public"
  owner       = postgresql_role.app_user.name
  object_type = "table"
  privileges  = ["SELECT", "INSERT", "UPDATE", "DELETE", "TRUNCATE", "REFERENCES"]
}

resource "postgresql_default_privileges" "readonly_future_tables" {
  role        = postgresql_role.readonly.name
  database    = var.database_name
  schema      = "public"
  owner       = postgresql_role.app_user.name
  object_type = "table"
  privileges  = ["SELECT"]
}

# ── Platform schema (shared across tenants) ─────────────────────────────────

resource "postgresql_schema" "central_platform" {
  name     = "central_platform"
  database = var.database_name
  owner    = postgresql_role.app_user.name
}
