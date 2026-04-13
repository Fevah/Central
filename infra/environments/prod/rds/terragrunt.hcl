include "envcommon" {
  path   = "${dirname(find_in_parent_folders("terragrunt.hcl"))}//environments/_envcommon/rds.hcl"
  expose = true
}

inputs = {
  reader_count         = 1
  backup_retention_days = 30
}
