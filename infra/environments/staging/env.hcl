locals {
  environment         = "staging"
  aws_region          = "eu-west-2"
  vpc_cidr            = "10.1.0.0/16"
  az_count            = 2
  eks_cluster_version = "1.31"
  rds_instance_class  = "db.t3.large"
  redis_node_type     = "cache.t3.medium"
  redis_num_nodes     = 2

  # EKS node groups
  general_instance_types = ["t3.large"]
  general_desired        = 2
  general_min            = 2
  general_max            = 6
  spot_enabled           = true
}
