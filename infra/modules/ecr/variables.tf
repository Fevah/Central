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
variable "kms_key_arn" {
  type = string
  default = ""
}

variable "service_names" {
  type = list(string)
  default = [
    "central-api",
    "auth-service",
    "admin-service",
    "audit-service",
    "gateway-service",
    "sync-service",
    "storage-service",
    "task-service",
  ]
}

variable "keep_tagged" {
  type = number
  default = 10
}
variable "untagged_expiry_days" {
  type = number
  default = 7
}
