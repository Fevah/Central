# =============================================================================
# RDS Module — Aurora PostgreSQL (multi-database, RLS-enabled)
# =============================================================================

resource "aws_security_group" "rds" {
  name_prefix = "${var.project}-${var.environment}-rds-"
  vpc_id      = var.vpc_id

  ingress {
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = var.allowed_security_groups
    cidr_blocks     = var.allowed_cidrs
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-rds-sg"
  })

  lifecycle { create_before_destroy = true }
}

resource "aws_rds_cluster_parameter_group" "main" {
  name   = "${var.project}-${var.environment}-pg18"
  family = "aurora-postgresql16"

  parameter {
    name  = "shared_preload_libraries"
    value = "pg_stat_statements"
  }

  parameter {
    name  = "log_min_duration_statement"
    value = var.slow_query_ms
  }

  parameter {
    name  = "rds.force_ssl"
    value = var.force_ssl ? "1" : "0"
  }

  tags = var.common_tags
}

resource "aws_rds_cluster" "main" {
  cluster_identifier     = "${var.project}-${var.environment}"
  engine                 = "aurora-postgresql"
  engine_version         = var.engine_version
  master_username        = var.master_username
  master_password        = var.master_password
  db_subnet_group_name   = var.db_subnet_group_name
  vpc_security_group_ids = [aws_security_group.rds.id]
  database_name          = "central"

  db_cluster_parameter_group_name = aws_rds_cluster_parameter_group.main.name

  storage_encrypted = true
  kms_key_id        = var.kms_key_arn

  backup_retention_period = var.backup_retention_days
  preferred_backup_window = "03:00-04:00"

  deletion_protection = var.environment == "prod"
  skip_final_snapshot = var.environment != "prod"

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-aurora"
  })
}

resource "aws_rds_cluster_instance" "writer" {
  identifier         = "${var.project}-${var.environment}-writer"
  cluster_identifier = aws_rds_cluster.main.id
  instance_class     = var.instance_class
  engine             = aws_rds_cluster.main.engine
  engine_version     = aws_rds_cluster.main.engine_version

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-writer"
  })
}

resource "aws_rds_cluster_instance" "reader" {
  count              = var.reader_count
  identifier         = "${var.project}-${var.environment}-reader-${count.index}"
  cluster_identifier = aws_rds_cluster.main.id
  instance_class     = var.reader_instance_class != "" ? var.reader_instance_class : var.instance_class
  engine             = aws_rds_cluster.main.engine
  engine_version     = aws_rds_cluster.main.engine_version

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-reader-${count.index}"
  })
}
