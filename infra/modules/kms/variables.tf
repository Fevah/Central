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
variable "separate_rds_key" {
  type = bool
  default = false
}
