locals {
  environment         = "prod"
  aws_region          = "eu-west-2"
  vpc_cidr            = "10.2.0.0/16"
  az_count            = 3
  eks_cluster_version = "1.31"
  rds_instance_class  = "db.r6g.xlarge"
  redis_node_type     = "cache.r6g.large"
  redis_num_nodes     = 3

  # EKS node groups
  general_instance_types = ["m6i.large", "m6a.large"]
  general_desired        = 3
  general_min            = 3
  general_max            = 12
  spot_enabled           = true
  spot_instance_types    = ["m6i.large", "m6a.large", "m5.large"]
  spot_desired           = 4
  spot_min               = 2
  spot_max               = 20
}
