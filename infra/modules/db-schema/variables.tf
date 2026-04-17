variable "database_name" {
  description = "Target database name (e.g., central)"
  type        = string
  default     = "central"
}

variable "app_db_user" {
  description = "Application database role (least-privilege)"
  type        = string
  default     = "central"
}

variable "app_db_password" {
  description = "Password for the application role"
  type        = string
  sensitive   = true
}

variable "readonly_password" {
  description = "Password for the readonly role"
  type        = string
  sensitive   = true
}

variable "backup_password" {
  description = "Password for the backup role (replication-capable)"
  type        = string
  sensitive   = true
}
