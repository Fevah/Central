include "envcommon" {
  path   = "${dirname(find_in_parent_folders("terragrunt.hcl"))}//environments/_envcommon/eks.hcl"
  expose = true
}

locals {
  env = read_terragrunt_config(find_in_parent_folders("env.hcl"))
}

inputs = {
  general_instance_types = local.env.locals.general_instance_types
  general_desired        = local.env.locals.general_desired
  general_min            = local.env.locals.general_min
  general_max            = local.env.locals.general_max
}
