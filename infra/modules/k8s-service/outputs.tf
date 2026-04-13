output "deployment_name" { value = kubernetes_deployment.service.metadata[0].name }
output "service_name" { value = kubernetes_service.service.metadata[0].name }
output "service_cluster_ip" { value = kubernetes_service.service.spec[0].cluster_ip }
