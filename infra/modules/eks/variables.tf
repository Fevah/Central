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
variable "vpc_cidr" {
  type = string
}
variable "private_subnet_ids" {
  type = list(string)
}

variable "cluster_version" {
  type    = string
  default = "1.31"
}

# General node group
variable "general_instance_types" {
  type    = list(string)
  default = ["t3.medium"]
}
variable "general_desired" {
  type = number
  default = 2
}
variable "general_min" {
  type = number
  default = 1
}
variable "general_max" {
  type = number
  default = 6
}

# Spot node group
variable "spot_enabled" {
  type = bool
  default = false
}
variable "spot_instance_types" {
  type    = list(string)
  default = ["m6i.large", "m6a.large", "m5.large"]
}
variable "spot_desired" {
  type = number
  default = 2
}
variable "spot_min" {
  type = number
  default = 0
}
variable "spot_max" {
  type = number
  default = 10
}
