# =============================================================================
# ElastiCache Module — Redis for sessions, rate limiting, pub/sub
# =============================================================================

resource "aws_security_group" "redis" {
  name_prefix = "${var.project}-${var.environment}-redis-"
  vpc_id      = var.vpc_id

  ingress {
    from_port       = 6379
    to_port         = 6379
    protocol        = "tcp"
    security_groups = var.allowed_security_groups
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-redis-sg"
  })

  lifecycle { create_before_destroy = true }
}

resource "aws_elasticache_replication_group" "main" {
  replication_group_id = "${var.project}-${var.environment}"
  description          = "Central ${var.environment} Redis cluster"
  node_type            = var.node_type
  num_cache_clusters   = var.num_nodes
  engine_version       = var.engine_version
  port                 = 6379
  subnet_group_name    = var.subnet_group_name
  security_group_ids   = [aws_security_group.redis.id]

  at_rest_encryption_enabled = true
  transit_encryption_enabled = var.transit_encryption
  auth_token                 = var.transit_encryption ? var.auth_token : null

  automatic_failover_enabled = var.num_nodes > 1
  multi_az_enabled           = var.num_nodes > 1

  snapshot_retention_limit = var.snapshot_retention_days
  snapshot_window          = "04:00-05:00"
  maintenance_window       = "sun:05:00-sun:06:00"

  parameter_group_name = aws_elasticache_parameter_group.main.name

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-redis"
  })
}

resource "aws_elasticache_parameter_group" "main" {
  name   = "${var.project}-${var.environment}-redis7"
  family = "redis7"

  parameter {
    name  = "maxmemory-policy"
    value = "allkeys-lru"
  }

  tags = var.common_tags
}
