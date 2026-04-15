---
name: Server infrastructure — K8s cluster with HA PostgreSQL
description: All services in 7-node K8s cluster. API+Auth+PG+Redis. MetalLB LoadBalancer. Terraform+Terragrunt IaC.
type: project
---

Server infrastructure runs in a local K8s cluster as of 2026-03-30.

**Stack:**
- .NET 10, Rust (Axum 0.7), PostgreSQL 18, Redis 7
- K8s 1.31.14 on VMware Workstation (7 nodes)
- Terraform 1.14.8 + Terragrunt 0.99.5
- MetalLB for LoadBalancer services
- Calico CNI for pod networking

**Running services (namespace: central):**
- central-api (2 replicas, HPA 2-8) — .NET API, http://192.168.56.200:5000
- auth-service (2 replicas, HPA 2-6) — Rust auth, v0.2.0
- postgres-0 (StatefulSet, HA) — 192.168.56.201:5432 write, .202 read
- redis-0 (StatefulSet) — session store

**Databases:**
- central: 48 tables, 48 RLS policies, 68 migrations
- secure_auth: 26 tables, V001-V017 migrations, admin@central.local seeded

**How to apply:**
- K8s: `export KUBECONFIG=~/.kube/central-local.conf`
- Deploy: `./infra/setup.sh k8s-deploy`
- Build+push: `podman build ... && podman push 192.168.56.10:30500/central/<img>:latest --tls-verify=false`
- Desktop DSN: `Host=192.168.56.201;Port=5432;Database=central;Username=central;Password=central`
