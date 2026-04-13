variable "project" {
  type = string
}
variable "environment" {
  type = string
}
variable "common_tags" {
  type = map(string)
  default = {}
}
variable "kms_key_arn" {
  type = string
  default = ""
}

variable "buckets" {
  type = map(object({
    purpose        = string
    versioning     = bool
    lifecycle_days = number
  }))
  default = {
    media = {
      purpose        = "CAS object storage (task attachments, documents)"
      versioning     = true
      lifecycle_days = 0
    }
    backups = {
      purpose        = "Database backups and config snapshots"
      versioning     = true
      lifecycle_days = 90
    }
    configs = {
      purpose        = "Switch configs and generated templates"
      versioning     = true
      lifecycle_days = 365
    }
  }
}
