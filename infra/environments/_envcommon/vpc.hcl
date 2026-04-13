terraform {
  source = "${get_repo_root()}/infra/modules/vpc"
}

include "root" {
  path = find_in_parent_folders("terragrunt.hcl")
}

locals {
  env = read_terragrunt_config(find_in_parent_folders("env.hcl"))
}

inputs = {
  vpc_cidr   = local.env.locals.vpc_cidr
  az_count   = local.env.locals.az_count
  single_nat = local.env.locals.environment != "prod"
}
