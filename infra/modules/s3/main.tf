# =============================================================================
# S3 Module — Object storage (backups, media, CAS)
# =============================================================================

resource "aws_s3_bucket" "main" {
  for_each = var.buckets
  bucket   = "${var.project}-${var.environment}-${each.key}"

  tags = merge(var.common_tags, {
    Name    = "${var.project}-${var.environment}-${each.key}"
    Purpose = each.value.purpose
  })
}

resource "aws_s3_bucket_versioning" "main" {
  for_each = { for k, v in var.buckets : k => v if v.versioning }
  bucket   = aws_s3_bucket.main[each.key].id

  versioning_configuration { status = "Enabled" }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "main" {
  for_each = aws_s3_bucket.main
  bucket   = each.value.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm     = var.kms_key_arn != "" ? "aws:kms" : "AES256"
      kms_master_key_id = var.kms_key_arn != "" ? var.kms_key_arn : null
    }
    bucket_key_enabled = var.kms_key_arn != ""
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "main" {
  for_each = { for k, v in var.buckets : k => v if v.lifecycle_days > 0 }
  bucket   = aws_s3_bucket.main[each.key].id

  rule {
    id     = "cleanup"
    status = "Enabled"

    transition {
      days          = each.value.lifecycle_days
      storage_class = "GLACIER"
    }

    expiration {
      days = each.value.lifecycle_days * 3
    }
  }
}

resource "aws_s3_bucket_public_access_block" "main" {
  for_each                = aws_s3_bucket.main
  bucket                  = each.value.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}
