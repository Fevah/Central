terraform {
  source = "${get_repo_root()}/infra/modules/elasticache"
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

inputs = {
  vpc_id                  = dependency.vpc.outputs.vpc_id
  subnet_group_name       = dependency.vpc.outputs.cache_subnet_group_name
  allowed_security_groups = [dependency.eks.outputs.cluster_security_group_id]
  node_type               = local.env.locals.redis_node_type
  num_nodes               = local.env.locals.redis_num_nodes
}
