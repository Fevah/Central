variable "project" {
  type = string
}
variable "environment" {
  type = string
}
variable "aws_region" {
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
  type    = list(string)
  default = ["central-api", "auth-service", "gateway-service", "task-service", "audit-service"]
}

variable "alarm_services" {
  type    = list(string)
  default = ["central-api", "auth-service", "gateway-service"]
}

variable "log_retention_days" {
  type = number
  default = 30
}
variable "cpu_alarm_threshold" {
  type = number
  default = 80
}
variable "rds_connection_threshold" {
  type = number
  default = 150
}
variable "rds_cluster_id" {
  type = string
  default = ""
}
variable "alarm_sns_topic_arn" {
  type = string
  default = ""
}
