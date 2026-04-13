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
variable "subnet_group_name" {
  type = string
}
variable "allowed_security_groups" {
  type = list(string)
  default = []
}

variable "node_type" {
  type = string
  default = "cache.t3.small"
}
variable "num_nodes" {
  type = number
  default = 1
}
variable "engine_version" {
  type = string
  default = "7.1"
}
variable "transit_encryption" {
  type = bool
  default = false
}
variable "auth_token" {
  type = string
  default = ""
  sensitive = true
}
variable "snapshot_retention_days" {
  type = number
  default = 1
}
