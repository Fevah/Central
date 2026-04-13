locals {
  environment = "local"
  aws_region  = "local"  # Not used for local, but required by root terragrunt.hcl

  # Host: 1 socket, 24 cores, 97GB RAM
  # Cluster sizing — 1 master + 4 workers = 5 VMs
  # Total: 16 CPU, 28GB RAM — leaves 8 cores + 69GB for Windows host + desktop app
  worker_count     = 4
  master_cpus      = 2
  master_memory    = 4096
  worker_cpus      = 3          # 3 cores each for general workers (was 2)
  worker_memory    = 4096
  db_worker_count  = 1          # Worker-01 gets extra for PG
  db_worker_cpus   = 4
  db_worker_memory = 8192

  # Network
  network_prefix = "192.168.56"
}
