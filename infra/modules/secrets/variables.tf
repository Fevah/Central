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
}

variable "db_username" {
  type = string
  sensitive = true
}
variable "db_password" {
  type = string
  sensitive = true
}
variable "db_host" {
  type = string
  default = ""
}
variable "db_port" {
  type = number
  default = 5432
}
variable "jwt_signing_key" {
  type = string
  sensitive = true
}
variable "encryption_key" {
  type = string
  sensitive = true
}
variable "redis_auth_token" {
  type = string
  default = ""
  sensitive = true
}
