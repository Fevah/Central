# =============================================================================
# Local K8s Cluster — VMware Workstation VMs
# =============================================================================
# Creates 1 master + 5 workers via Vagrant, bootstraps K8s via Ansible,
# installs MetalLB for LoadBalancer services.
#
# Usage:
#   cd infra/environments/local
#   terragrunt apply       # creates VMs + bootstraps K8s
#   terragrunt destroy     # tears down all VMs
# =============================================================================

terraform {
  source = "${get_repo_root()}/infra/modules/local-cluster"
}

# Local environment doesn't use S3 backend — local state file
generate "backend" {
  path      = "backend.tf"
  if_exists = "overwrite_terragrunt"
  contents  = <<-EOF
    terraform {
      backend "local" {
        path = "terraform.tfstate"
      }
    }
  EOF
}

generate "provider" {
  path      = "provider.tf"
  if_exists = "overwrite_terragrunt"
  contents  = <<-EOF
    terraform {
      required_version = ">= 1.6.0"
    }
  EOF
}

locals {
  env = read_terragrunt_config("env.hcl")
}

inputs = {
  project          = "central"
  environment      = "local"
  infra_dir        = "${get_repo_root()}/infra"
  worker_count     = local.env.locals.worker_count
  master_cpus      = local.env.locals.master_cpus
  master_memory    = local.env.locals.master_memory
  worker_cpus      = local.env.locals.worker_cpus
  worker_memory    = local.env.locals.worker_memory
  db_worker_count  = local.env.locals.db_worker_count
  db_worker_cpus   = local.env.locals.db_worker_cpus
  db_worker_memory = local.env.locals.db_worker_memory
  network_prefix   = local.env.locals.network_prefix
  kubeconfig_path  = pathexpand("~/.kube/central-local.conf")
}
