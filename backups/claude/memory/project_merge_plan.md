---
name: Central + Secure merge — Phases 1-4 + 7-10 complete
description: Merge plan nearly complete. 7/8 Rust services deployed in K8s. Admin-service pending container fix.
type: project
---

As of 2026-03-31, the Central + Secure merge is substantially complete.

**Why:** Unified enterprise platform — Central desktop engine + Secure Rust microservices + shared K8s infrastructure.

**Phase status:**
- [x] Phase 1 — Unified auth (auth-service JWT, MFA, RLS, tenant context in DbRepository)
- [x] Phase 2 — Rust API gateway (23 routes, JWT validation, health aggregation)
- [x] Phase 3 — Task-service (13 endpoints, cursor pagination, batch, search)
- [x] Phase 4 — Storage (CAS dedup, MinIO) + Sync (vector clocks, Merkle diff)
- [ ] Phase 5 — Flutter mobile (deferred — desktop first)
- [ ] Phase 6 — Angular web (deferred — desktop first)
- [x] Phase 7 — Audit-service deployed (M365/GDPR, mock mode)
- [x] Phase 8 — K8s + elastic scaling (7 nodes, HPA, MetalLB, Terraform IaC)
- [x] Phase 9 — IaC complete (11 TF modules, Terragrunt, Ansible, CI/CD)
- [x] Phase 10 — Admin-service (building — needs cmake+protobuf for kube crate)

**Services in K8s (namespace: central):**
- central-api (2 replicas), auth-service (2), gateway (2), task-service (2)
- storage-service (1), sync-service (1), audit-service (1)
- admin-service (pending container build fix)
- minio (1), postgres (1), redis (1)

**Gateway:** http://192.168.56.203:8000 — single entry point for all APIs
