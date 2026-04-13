locals {
  environment = "local"
  aws_region  = "local"  # Not used for local, but required by root terragrunt.hcl

  # Cluster sizing — 1 master + 6 workers = 7 VMs
  # Total: 18 CPU, 36GB RAM (fits in 97GB host)
  worker_count     = 6
  master_cpus      = 2
  master_memory    = 4096
  worker_cpus      = 2
  worker_memory    = 4096
  db_worker_count  = 2   # First 2 workers get extra resources for PG HA (primary + replica)
  db_worker_cpus   = 4
  db_worker_memory = 8192

  # Network
  network_prefix = "192.168.56"
}
