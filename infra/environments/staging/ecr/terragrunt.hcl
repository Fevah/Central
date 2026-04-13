include "envcommon" {
  path   = "${dirname(find_in_parent_folders("terragrunt.hcl"))}//environments/_envcommon/ecr.hcl"
  expose = true
}
