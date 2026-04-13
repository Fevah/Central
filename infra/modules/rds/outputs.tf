output "cluster_endpoint" {
  value     = aws_rds_cluster.main.endpoint
  sensitive = true
}

output "reader_endpoint" {
  value     = aws_rds_cluster.main.reader_endpoint
  sensitive = true
}

output "cluster_id" { value = aws_rds_cluster.main.id }
output "port" { value = aws_rds_cluster.main.port }
output "security_group_id" { value = aws_security_group.rds.id }

output "connection_strings" {
  value = {
    central       = "postgresql://${var.master_username}@${aws_rds_cluster.main.endpoint}:5432/central"
    secure_auth   = "postgresql://${var.master_username}@${aws_rds_cluster.main.endpoint}:5432/secure_auth"
    secure_audit  = "postgresql://${var.master_username}@${aws_rds_cluster.main.endpoint}:5432/secure_audit"
    secure_sync   = "postgresql://${var.master_username}@${aws_rds_cluster.main.endpoint}:5432/secure_sync"
  }
  sensitive = true
}
