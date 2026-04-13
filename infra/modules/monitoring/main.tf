# =============================================================================
# Monitoring Module — CloudWatch logs, metrics, alarms
# =============================================================================

# --- Log Groups (one per service) ---

resource "aws_cloudwatch_log_group" "services" {
  for_each          = toset(var.service_names)
  name              = "/central/${var.environment}/${each.value}"
  retention_in_days = var.log_retention_days
  kms_key_id        = var.kms_key_arn

  tags = merge(var.common_tags, {
    Service = each.value
  })
}

# --- Alarms ---

resource "aws_cloudwatch_metric_alarm" "cpu_high" {
  for_each            = toset(var.alarm_services)
  alarm_name          = "${var.project}-${var.environment}-${each.value}-cpu-high"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 3
  metric_name         = "CPUUtilization"
  namespace           = "AWS/ECS"
  period              = 300
  statistic           = "Average"
  threshold           = var.cpu_alarm_threshold
  alarm_description   = "CPU utilization > ${var.cpu_alarm_threshold}% for ${each.value}"
  alarm_actions       = var.alarm_sns_topic_arn != "" ? [var.alarm_sns_topic_arn] : []

  dimensions = {
    ServiceName = each.value
  }

  tags = var.common_tags
}

resource "aws_cloudwatch_metric_alarm" "rds_connections" {
  count               = var.rds_cluster_id != "" ? 1 : 0
  alarm_name          = "${var.project}-${var.environment}-rds-connections-high"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 2
  metric_name         = "DatabaseConnections"
  namespace           = "AWS/RDS"
  period              = 300
  statistic           = "Average"
  threshold           = var.rds_connection_threshold
  alarm_description   = "RDS connections > ${var.rds_connection_threshold}"
  alarm_actions       = var.alarm_sns_topic_arn != "" ? [var.alarm_sns_topic_arn] : []

  dimensions = {
    DBClusterIdentifier = var.rds_cluster_id
  }

  tags = var.common_tags
}

# --- Dashboard ---

resource "aws_cloudwatch_dashboard" "main" {
  dashboard_name = "${var.project}-${var.environment}"
  dashboard_body = jsonencode({
    widgets = [
      {
        type   = "metric"
        x      = 0
        y      = 0
        width  = 12
        height = 6
        properties = {
          title   = "Service CPU"
          metrics = [for s in var.alarm_services : ["AWS/ECS", "CPUUtilization", "ServiceName", s]]
          period  = 300
          region  = var.aws_region
        }
      },
      {
        type   = "metric"
        x      = 12
        y      = 0
        width  = 12
        height = 6
        properties = {
          title   = "Service Memory"
          metrics = [for s in var.alarm_services : ["AWS/ECS", "MemoryUtilization", "ServiceName", s]]
          period  = 300
          region  = var.aws_region
        }
      },
      {
        type   = "log"
        x      = 0
        y      = 6
        width  = 24
        height = 6
        properties = {
          title  = "Error Logs"
          query  = "fields @timestamp, @message | filter @message like /ERROR/ | sort @timestamp desc | limit 50"
          region = var.aws_region
          stacked = false
          view   = "table"
        }
      }
    ]
  })
}
