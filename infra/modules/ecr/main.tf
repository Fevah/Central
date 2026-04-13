# =============================================================================
# ECR Module — Container registries for all services
# =============================================================================

resource "aws_ecr_repository" "services" {
  for_each = toset(var.service_names)

  name                 = "${var.project}/${each.value}"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  encryption_configuration {
    encryption_type = var.kms_key_arn != "" ? "KMS" : "AES256"
    kms_key         = var.kms_key_arn != "" ? var.kms_key_arn : null
  }

  tags = merge(var.common_tags, {
    Service = each.value
  })
}

resource "aws_ecr_lifecycle_policy" "cleanup" {
  for_each   = aws_ecr_repository.services
  repository = each.value.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Keep last ${var.keep_tagged} tagged images"
        selection = {
          tagStatus   = "tagged"
          tagPrefixList = ["v", "release"]
          countType   = "imageCountMoreThan"
          countNumber = var.keep_tagged
        }
        action = { type = "expire" }
      },
      {
        rulePriority = 2
        description  = "Expire untagged after ${var.untagged_expiry_days} days"
        selection = {
          tagStatus   = "untagged"
          countType   = "sinceImagePushed"
          countUnit   = "days"
          countNumber = var.untagged_expiry_days
        }
        action = { type = "expire" }
      }
    ]
  })
}
