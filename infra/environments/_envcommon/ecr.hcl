terraform {
  source = "${get_repo_root()}/infra/modules/ecr"
}

include "root" {
  path = find_in_parent_folders("terragrunt.hcl")
}

dependency "kms" {
  config_path = "../kms"
}

inputs = {
  kms_key_arn = dependency.kms.outputs.key_arn
}
