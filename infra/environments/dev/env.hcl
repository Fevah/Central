locals {
  environment         = "dev"
  aws_region          = "eu-west-2"
  vpc_cidr            = "10.0.0.0/16"
  az_count            = 2
  eks_cluster_version = "1.31"
  rds_instance_class  = "db.t3.medium"
  redis_node_type     = "cache.t3.small"
  redis_num_nodes     = 1

  # EKS node groups
  general_instance_types = ["t3.medium"]
  general_desired        = 2
  general_min            = 1
  general_max            = 4
  spot_enabled           = false
}
