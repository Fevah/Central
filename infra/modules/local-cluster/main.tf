# =============================================================================
# Local Cluster Module — VMware Workstation K8s via Vagrant + Ansible
# =============================================================================
# Creates a production-like K8s cluster on local VMware Workstation VMs.
# Uses Vagrant for VM lifecycle and Ansible for K8s bootstrap.
#
# Architecture:
#   1 master + N workers, MetalLB for LoadBalancer services,
#   PostgreSQL + Redis run as K8s StatefulSets (no separate DB VMs).
# =============================================================================

terraform {
  required_providers {
    local = {
      source  = "hashicorp/local"
      version = "~> 2.4"
    }
    null = {
      source  = "hashicorp/null"
      version = "~> 3.2"
    }
  }
}

locals {
  infra_dir      = var.infra_dir
  vagrant_dir    = "${local.infra_dir}/vagrant"
  ansible_dir    = "${local.infra_dir}/ansible"
  k8s_dir        = "${local.infra_dir}/k8s"
  network_prefix = var.network_prefix

  master_nodes = {
    "k8s-master" = {
      ip     = "${local.network_prefix}.10"
      cpus   = var.master_cpus
      memory = var.master_memory
    }
  }

  worker_nodes = { for i in range(var.worker_count) :
    "k8s-worker-${format("%02d", i + 1)}" => {
      ip     = "${local.network_prefix}.${21 + i}"
      cpus   = i < var.db_worker_count ? var.db_worker_cpus : var.worker_cpus
      memory = i < var.db_worker_count ? var.db_worker_memory : var.worker_memory
      labels = i < var.db_worker_count ? "role=database" : "role=general"
    }
  }

  all_nodes = merge(local.master_nodes, local.worker_nodes)

  metallb_range_start = "${local.network_prefix}.200"
  metallb_range_end   = "${local.network_prefix}.220"
}

# --- Generate Vagrantfile ---

resource "local_file" "vagrantfile" {
  filename = "${local.vagrant_dir}/Vagrantfile"
  content  = templatefile("${path.module}/templates/Vagrantfile.tftpl", {
    box            = var.vagrant_box
    provider       = var.vagrant_provider
    master_nodes   = local.master_nodes
    worker_nodes   = local.worker_nodes
    ansible_dir    = local.ansible_dir
    metallb_start  = local.metallb_range_start
    metallb_end    = local.metallb_range_end
  })
}

# --- Generate Ansible inventory ---

resource "local_file" "ansible_inventory" {
  filename = "${local.ansible_dir}/inventory/hosts.yml"
  content  = templatefile("${path.module}/templates/hosts.yml.tftpl", {
    master_nodes   = local.master_nodes
    worker_nodes   = local.worker_nodes
    network_prefix = local.network_prefix
    metallb_start  = local.metallb_range_start
    metallb_end    = local.metallb_range_end
  })
}

# --- Generate MetalLB config ---

resource "local_file" "metallb_config" {
  filename = "${local.k8s_dir}/overlays/local/metallb-config.yaml"
  content  = templatefile("${path.module}/templates/metallb-config.yaml.tftpl", {
    metallb_start = local.metallb_range_start
    metallb_end   = local.metallb_range_end
  })
}

# --- Vagrant up (creates all VMs) ---

resource "null_resource" "vagrant_up" {
  depends_on = [local_file.vagrantfile]

  triggers = {
    vagrantfile_hash = local_file.vagrantfile.content_md5
    worker_count     = var.worker_count
    vagrant_dir      = local.vagrant_dir
  }

  provisioner "local-exec" {
    command     = "vagrant up --provider=${var.vagrant_provider}"
    working_dir = local.vagrant_dir
  }

  provisioner "local-exec" {
    when        = destroy
    command     = "vagrant destroy -f"
    working_dir = self.triggers.vagrant_dir
    on_failure  = continue
  }
}
