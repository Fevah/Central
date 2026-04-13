output "db_credentials_arn" { value = aws_secretsmanager_secret.db_credentials.arn }
output "jwt_signing_key_arn" { value = aws_secretsmanager_secret.jwt_signing_key.arn }
output "encryption_key_arn" { value = aws_secretsmanager_secret.encryption_key.arn }
output "redis_auth_arn" { value = aws_secretsmanager_secret.redis_auth.arn }
