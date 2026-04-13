terraform {
  source = "${get_repo_root()}/infra/modules/kms"
}

include "root" {
  path = find_in_parent_folders("terragrunt.hcl")
}

inputs = {
  separate_rds_key = true
}
