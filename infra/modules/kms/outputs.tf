output "key_arn" { value = aws_kms_key.main.arn }
output "key_id" { value = aws_kms_key.main.key_id }
output "alias_arn" { value = aws_kms_alias.main.arn }
output "rds_key_arn" { value = var.separate_rds_key ? aws_kms_key.rds[0].arn : aws_kms_key.main.arn }
