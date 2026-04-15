---
name: Local K8s cluster — 7 nodes, 14 pods, all services
description: Full platform running in K8s. 7 VMs on VMware, Terraform IaC, MetalLB, 7 Rust + 1 .NET services.
type: project
---

As of 2026-03-31, the entire Central + Secure platform runs in a single K8s cluster.

**Cluster:** 1 master + 6 workers on VMware Workstation (1 socket, 24 cores, 97GB host)
- k8s-master (192.168.56.10) — 2 CPU / 4GB
- k8s-worker-01 (.21) — 4 CPU / 8GB — role=database (PG primary)
- k8s-worker-02 (.22) — 4 CPU / 8GB — role=database
- k8s-worker-03-06 (.23-.26) — 2-3 CPU / 4GB — role=general

**Soft lockup fix applied:** `kernel.watchdog_thresh=30`, `softlockup_panic=0` on all VMs.
**PG replica disabled:** StatefulSet scaled to 1 (dev doesn't need HA).

**External access (MetalLB):**
- Gateway: http://192.168.56.203:8000 (single entry point)
- Central API: http://192.168.56.200:5000
- PostgreSQL: 192.168.56.10:30432
- Auth Service: 192.168.56.10:30081
- Container Registry: 192.168.56.10:30500

**Desktop DSN:** `Host=192.168.56.10;Port=30432;Database=central;Username=central;Password=central`

**Key commands:**
- `export KUBECONFIG=~/.kube/central-local.conf`
- `kubectl -n central get pods`
- `cd infra/vagrant && vagrant status`
- `./infra/setup.sh k8s-status`
