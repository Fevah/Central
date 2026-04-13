# =============================================================================
# VPC Module — Network foundation
# =============================================================================

data "aws_availability_zones" "available" {
  state = "available"
}

locals {
  azs = slice(data.aws_availability_zones.available.names, 0, var.az_count)
}

resource "aws_vpc" "main" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-vpc"
  })
}

# --- Public Subnets (NAT, ALB, bastion) ---

resource "aws_subnet" "public" {
  count                   = var.az_count
  vpc_id                  = aws_vpc.main.id
  cidr_block              = cidrsubnet(var.vpc_cidr, 8, count.index)
  availability_zone       = local.azs[count.index]
  map_public_ip_on_launch = true

  tags = merge(var.common_tags, {
    Name                     = "${var.project}-${var.environment}-public-${local.azs[count.index]}"
    "kubernetes.io/role/elb" = "1"
  })
}

# --- Private Subnets (EKS nodes, services) ---

resource "aws_subnet" "private" {
  count             = var.az_count
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(var.vpc_cidr, 8, count.index + 10)
  availability_zone = local.azs[count.index]

  tags = merge(var.common_tags, {
    Name                              = "${var.project}-${var.environment}-private-${local.azs[count.index]}"
    "kubernetes.io/role/internal-elb" = "1"
  })
}

# --- Database Subnets (RDS, ElastiCache) ---

resource "aws_subnet" "database" {
  count             = var.az_count
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(var.vpc_cidr, 8, count.index + 20)
  availability_zone = local.azs[count.index]

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-database-${local.azs[count.index]}"
  })
}

# --- Internet Gateway ---

resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-igw"
  })
}

# --- NAT Gateway (one per AZ for HA in prod, single for dev) ---

resource "aws_eip" "nat" {
  count  = var.single_nat ? 1 : var.az_count
  domain = "vpc"

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-nat-eip-${count.index}"
  })
}

resource "aws_nat_gateway" "main" {
  count         = var.single_nat ? 1 : var.az_count
  allocation_id = aws_eip.nat[count.index].id
  subnet_id     = aws_subnet.public[count.index].id

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-nat-${count.index}"
  })

  depends_on = [aws_internet_gateway.main]
}

# --- Route Tables ---

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.main.id
  }

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-public-rt"
  })
}

resource "aws_route_table" "private" {
  count  = var.az_count
  vpc_id = aws_vpc.main.id

  route {
    cidr_block     = "0.0.0.0/0"
    nat_gateway_id = aws_nat_gateway.main[var.single_nat ? 0 : count.index].id
  }

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-private-rt-${count.index}"
  })
}

resource "aws_route_table_association" "public" {
  count          = var.az_count
  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

resource "aws_route_table_association" "private" {
  count          = var.az_count
  subnet_id      = aws_subnet.private[count.index].id
  route_table_id = aws_route_table.private[count.index].id
}

resource "aws_route_table_association" "database" {
  count          = var.az_count
  subnet_id      = aws_subnet.database[count.index].id
  route_table_id = aws_route_table.private[count.index].id
}

# --- DB Subnet Group ---

resource "aws_db_subnet_group" "main" {
  name       = "${var.project}-${var.environment}-db"
  subnet_ids = aws_subnet.database[*].id

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-db-subnet-group"
  })
}

resource "aws_elasticache_subnet_group" "main" {
  name       = "${var.project}-${var.environment}-cache"
  subnet_ids = aws_subnet.database[*].id

  tags = merge(var.common_tags, {
    Name = "${var.project}-${var.environment}-cache-subnet-group"
  })
}
