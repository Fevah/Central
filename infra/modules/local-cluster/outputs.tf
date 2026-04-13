output "master_ip" {
  value = local.master_nodes["k8s-master"].ip
}

output "worker_ips" {
  value = { for k, v in local.worker_nodes : k => v.ip }
}

output "metallb_range" {
  value = "${local.metallb_range_start}-${local.metallb_range_end}"
}

output "kubeconfig_path" {
  value = var.kubeconfig_path
}

output "cluster_info" {
  value = {
    master     = "1 node"
    workers    = "${var.worker_count} nodes"
    total_cpus = var.master_cpus + (var.db_worker_count * var.db_worker_cpus) + ((var.worker_count - var.db_worker_count) * var.worker_cpus)
    total_ram  = "${var.master_memory + (var.db_worker_count * var.db_worker_memory) + ((var.worker_count - var.db_worker_count) * var.worker_memory)}MB"
  }
}
