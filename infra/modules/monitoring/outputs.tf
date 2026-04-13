output "log_group_arns" {
  value = { for k, v in aws_cloudwatch_log_group.services : k => v.arn }
}

output "dashboard_arn" { value = aws_cloudwatch_dashboard.main.dashboard_arn }
