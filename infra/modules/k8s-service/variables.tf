variable "service_name" {
  type = string
}
variable "namespace" {
  type = string
  default = "central"
}
variable "service_account" {
  type = string
  default = "central-service"
}
variable "config_map_name" {
  type = string
  default = "central-config"
}
variable "secret_name" {
  type = string
  default = ""
}

# Container
variable "image_repository" {
  type = string
}
variable "image_tag" {
  type = string
  default = "latest"
}
variable "image_pull_policy" {
  type = string
  default = "IfNotPresent"
}
variable "container_port" {
  type = number
}
variable "service_port" {
  type = number
  default = 0
}  # defaults to container_port
variable "health_path" {
  type = string
  default = "/health"
}
variable "wait_for_postgres" {
  type = bool
  default = true
}
variable "extra_env" {
  type = map(string)
  default = {}
}

# Resources
variable "replicas" {
  type = number
  default = 1
}
variable "cpu_request" {
  type = string
  default = "100m"
}
variable "cpu_limit" {
  type = string
  default = "500m"
}
variable "memory_request" {
  type = string
  default = "128Mi"
}
variable "memory_limit" {
  type = string
  default = "512Mi"
}
variable "liveness_initial_delay" {
  type = number
  default = 15
}

# HPA
variable "hpa_enabled" {
  type = bool
  default = true
}
variable "hpa_min" {
  type = number
  default = 1
}
variable "hpa_max" {
  type = number
  default = 5
}
variable "hpa_cpu_target" {
  type = number
  default = 70
}
variable "hpa_memory_target" {
  type = number
  default = 80
}

# PDB
variable "pdb_enabled" {
  type = bool
  default = true
}
variable "pdb_min_available" {
  type = string
  default = "1"
}
