# =============================================================================
# K8s Service Module — Reusable deployment for any microservice
# =============================================================================
# Usage: one instance per service (central-api, auth-service, task-service, etc.)
# Creates: Deployment + Service + HPA + PDB
# =============================================================================

resource "kubernetes_deployment" "service" {
  metadata {
    name      = var.service_name
    namespace = var.namespace
    labels    = local.labels
  }

  spec {
    replicas = var.replicas

    selector {
      match_labels = local.selector_labels
    }

    strategy {
      type = "RollingUpdate"
      rolling_update {
        max_surge       = "25%"
        max_unavailable = 0
      }
    }

    template {
      metadata {
        labels = local.labels
        annotations = {
          "prometheus.io/scrape" = "true"
          "prometheus.io/port"   = tostring(var.container_port)
          "prometheus.io/path"   = "/metrics"
        }
      }

      spec {
        service_account_name = var.service_account

        dynamic "init_container" {
          for_each = var.wait_for_postgres ? [1] : []
          content {
            name    = "wait-postgres"
            image   = "docker.io/library/postgres:18-alpine"
            command = ["sh", "-c", "until pg_isready -h $DATABASE_HOST -p 5432; do sleep 2; done"]
            env_from {
              config_map_ref { name = var.config_map_name }
            }
          }
        }

        container {
          name              = var.service_name
          image             = "${var.image_repository}:${var.image_tag}"
          image_pull_policy = var.image_pull_policy

          port {
            container_port = var.container_port
            name           = "http"
          }

          env_from {
            config_map_ref { name = var.config_map_name }
          }

          dynamic "env_from" {
            for_each = var.secret_name != "" ? [1] : []
            content {
              secret_ref { name = var.secret_name }
            }
          }

          dynamic "env" {
            for_each = var.extra_env
            content {
              name  = env.key
              value = env.value
            }
          }

          resources {
            requests = {
              cpu    = var.cpu_request
              memory = var.memory_request
            }
            limits = {
              cpu    = var.cpu_limit
              memory = var.memory_limit
            }
          }

          liveness_probe {
            http_get {
              path = var.health_path
              port = var.container_port
            }
            initial_delay_seconds = var.liveness_initial_delay
            period_seconds        = 10
            failure_threshold     = 3
          }

          readiness_probe {
            http_get {
              path = var.health_path
              port = var.container_port
            }
            initial_delay_seconds = 5
            period_seconds        = 5
            failure_threshold     = 3
          }
        }
      }
    }
  }

  lifecycle {
    ignore_changes = [spec[0].replicas] # HPA manages replicas
  }
}

resource "kubernetes_service" "service" {
  metadata {
    name      = var.service_name
    namespace = var.namespace
    labels    = local.labels
  }

  spec {
    selector = local.selector_labels
    type     = "ClusterIP"

    port {
      port        = var.service_port
      target_port = var.container_port
      protocol    = "TCP"
      name        = "http"
    }
  }
}

resource "kubernetes_horizontal_pod_autoscaler_v2" "service" {
  count = var.hpa_enabled ? 1 : 0

  metadata {
    name      = var.service_name
    namespace = var.namespace
  }

  spec {
    scale_target_ref {
      api_version = "apps/v1"
      kind        = "Deployment"
      name        = kubernetes_deployment.service.metadata[0].name
    }

    min_replicas = var.hpa_min
    max_replicas = var.hpa_max

    metric {
      type = "Resource"
      resource {
        name = "cpu"
        target {
          type                = "Utilization"
          average_utilization = var.hpa_cpu_target
        }
      }
    }

    metric {
      type = "Resource"
      resource {
        name = "memory"
        target {
          type                = "Utilization"
          average_utilization = var.hpa_memory_target
        }
      }
    }
  }
}

resource "kubernetes_pod_disruption_budget_v1" "service" {
  count = var.pdb_enabled ? 1 : 0

  metadata {
    name      = var.service_name
    namespace = var.namespace
  }

  spec {
    min_available = var.pdb_min_available

    selector {
      match_labels = local.selector_labels
    }
  }
}

locals {
  selector_labels = {
    "app.kubernetes.io/name" = var.service_name
  }
  labels = merge(local.selector_labels, {
    "app.kubernetes.io/part-of"    = "central-platform"
    "app.kubernetes.io/managed-by" = "terragrunt"
    "app.kubernetes.io/version"    = var.image_tag
  })
}
