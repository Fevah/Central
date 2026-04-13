variable "project" {
  type = string
}
variable "environment" {
  type = string
}
variable "common_tags" {
  type = map(string)
  default = {}
}
variable "vpc_id" {
  type = string
}
variable "db_subnet_group_name" {
  type = string
}
variable "allowed_security_groups" {
  type = list(string)
  default = []
}
variable "allowed_cidrs" {
  type = list(string)
  default = []
}
variable "kms_key_arn" {
  type = string
  default = ""
}

variable "engine_version" {
  type = string
  default = "16.4"
}
variable "instance_class" {
  type = string
  default = "db.t3.medium"
}
variable "reader_instance_class" {
  type = string
  default = ""
}
variable "reader_count" {
  type = number
  default = 0
}
variable "master_username" {
  type = string
  default = "central"
  sensitive = true
}
variable "master_password" {
  type = string
  sensitive = true
}
variable "backup_retention_days" {
  type = number
  default = 7
}
variable "slow_query_ms" {
  type = string
  default = "1000"
}
variable "force_ssl" {
  type = bool
  default = true
}

variable "databases" {
  description = "Additional databases to create (central is created by default)"
  type        = list(string)
  default     = ["secure_auth", "secure_audit", "secure_sync", "secure_storage"]
}
