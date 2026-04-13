# =============================================================================
# Root Terragrunt Configuration
# =============================================================================
# All environments inherit from this file.
# Backend: S3 + DynamoDB for state locking.
# Provider: AWS with region from environment.
# =============================================================================

locals {
  # Parse the environment from the directory path
  # e.g. environments/dev/vpc -> environment = "dev"
  env_vars    = read_terragrunt_config(find_in_parent_folders("env.hcl"))
  environment = local.env_vars.locals.environment
  aws_region  = local.env_vars.locals.aws_region
  project     = "central"
}

# Remote state — S3 + DynamoDB
remote_state {
  backend = "s3"
  generate = {
    path      = "backend.tf"
    if_exists = "overwrite_terragrunt"
  }
  config = {
    bucket         = "${local.project}-terraform-state-${get_aws_account_id()}"
    key            = "${local.environment}/${path_relative_to_include()}/terraform.tfstate"
    region         = local.aws_region
    encrypt        = true
    dynamodb_table = "${local.project}-terraform-locks"

    s3_bucket_tags = {
      Project     = local.project
      Environment = local.environment
      ManagedBy   = "Terragrunt"
    }
  }
}

# Generate provider block
generate "provider" {
  path      = "provider.tf"
  if_exists = "overwrite_terragrunt"
  contents  = <<-EOF
    terraform {
      required_version = ">= 1.6.0"
      required_providers {
        aws = {
          source  = "hashicorp/aws"
          version = "~> 5.0"
        }
        kubernetes = {
          source  = "hashicorp/kubernetes"
          version = "~> 2.24"
        }
        helm = {
          source  = "hashicorp/helm"
          version = "~> 2.12"
        }
      }
    }

    provider "aws" {
      region = "${local.aws_region}"

      default_tags {
        tags = {
          Project     = "${local.project}"
          Environment = "${local.environment}"
          ManagedBy   = "Terragrunt"
        }
      }
    }
  EOF
}

# Common inputs passed to all modules
inputs = {
  project     = local.project
  environment = local.environment
  aws_region  = local.aws_region

  common_tags = {
    Project     = local.project
    Environment = local.environment
    ManagedBy   = "Terragrunt"
  }
}
