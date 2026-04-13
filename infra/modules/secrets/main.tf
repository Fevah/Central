# =============================================================================
# Secrets Manager Module — Database creds, JWT keys, encryption keys
# =============================================================================

resource "aws_secretsmanager_secret" "db_credentials" {
  name       = "${var.project}/${var.environment}/db-credentials"
  kms_key_id = var.kms_key_arn

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-db-credentials"
  })
}

resource "aws_secretsmanager_secret_version" "db_credentials" {
  secret_id = aws_secretsmanager_secret.db_credentials.id
  secret_string = jsonencode({
    username = var.db_username
    password = var.db_password
    host     = var.db_host
    port     = var.db_port
  })
}

resource "aws_secretsmanager_secret" "jwt_signing_key" {
  name       = "${var.project}/${var.environment}/jwt-signing-key"
  kms_key_id = var.kms_key_arn

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-jwt-key"
  })
}

resource "aws_secretsmanager_secret_version" "jwt_signing_key" {
  secret_id     = aws_secretsmanager_secret.jwt_signing_key.id
  secret_string = var.jwt_signing_key
}

resource "aws_secretsmanager_secret" "encryption_key" {
  name       = "${var.project}/${var.environment}/encryption-key"
  kms_key_id = var.kms_key_arn

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-encryption-key"
  })
}

resource "aws_secretsmanager_secret_version" "encryption_key" {
  secret_id     = aws_secretsmanager_secret.encryption_key.id
  secret_string = var.encryption_key
}

resource "aws_secretsmanager_secret" "redis_auth" {
  name       = "${var.project}/${var.environment}/redis-auth"
  kms_key_id = var.kms_key_arn

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-redis-auth"
  })
}

resource "aws_secretsmanager_secret_version" "redis_auth" {
  secret_id     = aws_secretsmanager_secret.redis_auth.id
  secret_string = var.redis_auth_token
}
