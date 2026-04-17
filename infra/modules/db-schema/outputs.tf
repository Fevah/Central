output "app_user" {
  description = "Application database role"
  value       = postgresql_role.app_user.name
}

output "readonly_user" {
  description = "Read-only database role (for replicas, reports)"
  value       = postgresql_role.readonly.name
}

output "backup_user" {
  description = "Backup role (replication-capable)"
  value       = postgresql_role.backup.name
}

output "central_platform_schema" {
  description = "Shared platform schema for cross-tenant data"
  value       = postgresql_schema.central_platform.name
}

output "installed_extensions" {
  description = "List of PostgreSQL extensions installed"
  value = [
    postgresql_extension.uuid_ossp.name,
    postgresql_extension.pgcrypto.name,
    postgresql_extension.pg_trgm.name,
    postgresql_extension.citext.name,
    postgresql_extension.btree_gin.name,
    postgresql_extension.pg_stat_statements.name,
  ]
}
