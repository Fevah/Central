# =============================================================================
# KMS Module — Customer-managed encryption keys
# =============================================================================

resource "aws_kms_key" "main" {
  description             = "Central ${var.environment} master encryption key"
  deletion_window_in_days = var.environment == "prod" ? 30 : 7
  enable_key_rotation     = true
  multi_region            = false

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-master"
  })
}

resource "aws_kms_alias" "main" {
  name          = "alias/${var.project}-${var.environment}"
  target_key_id = aws_kms_key.main.key_id
}

# Separate key for RDS if needed
resource "aws_kms_key" "rds" {
  count                   = var.separate_rds_key ? 1 : 0
  description             = "Central ${var.environment} RDS encryption key"
  deletion_window_in_days = var.environment == "prod" ? 30 : 7
  enable_key_rotation     = true

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-rds"
  })
}

resource "aws_kms_alias" "rds" {
  count         = var.separate_rds_key ? 1 : 0
  name          = "alias/${var.project}-${var.environment}-rds"
  target_key_id = aws_kms_key.rds[0].key_id
}
