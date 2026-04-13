variable "project" {
  type = string
  default = "central"
}
variable "environment" {
  type = string
  default = "local"
}
variable "infra_dir" {
  description = "Absolute path to the infra/ directory in the repo"
  type        = string
}

# VM Provider
variable "vagrant_box" {
  type    = string
  default = "bento/ubuntu-24.04"
}

variable "vagrant_provider" {
  type    = string
  default = "vmware_desktop"
}

# Network
variable "network_prefix" {
  description = "First 3 octets of the VM network (e.g. 192.168.56)"
  type        = string
  default     = "192.168.56"
}

# Master node
variable "master_cpus" {
  type = number
  default = 2
}
variable "master_memory" {
  type = number
  default = 4096
}

# Worker nodes
variable "worker_count" {
  description = "Number of K8s worker nodes (4-6 recommended)"
  type        = number
  default     = 5
}

variable "worker_cpus" {
  type = number
  default = 2
}
variable "worker_memory" {
  type = number
  default = 4096
}

# Database-labeled workers (first N workers get extra resources)
variable "db_worker_count" {
  description = "How many workers are database-labeled (get more CPU/RAM)"
  type        = number
  default     = 1
}

variable "db_worker_cpus" {
  type = number
  default = 4
}
variable "db_worker_memory" {
  type = number
  default = 8192
}

# Kubeconfig output
variable "kubeconfig_path" {
  type    = string
  default = "~/.kube/central-local.conf"
}
