terraform {
  source = "${get_repo_root()}/infra/modules/rds"
}

include "root" {
  path = find_in_parent_folders("terragrunt.hcl")
}

locals {
  env = read_terragrunt_config(find_in_parent_folders("env.hcl"))
}

dependency "vpc" {
  config_path = "../vpc"
}

dependency "eks" {
  config_path = "../eks"
}

dependency "kms" {
  config_path = "../kms"
}

inputs = {
  vpc_id                  = dependency.vpc.outputs.vpc_id
  db_subnet_group_name    = dependency.vpc.outputs.db_subnet_group_name
  allowed_security_groups = [dependency.eks.outputs.cluster_security_group_id]
  kms_key_arn             = dependency.kms.outputs.rds_key_arn
  instance_class          = local.env.locals.rds_instance_class
  master_password         = get_env("TF_VAR_db_password", "change-me-in-ci")
}
