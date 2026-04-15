---
name: K8s for services, Podman only for container builds
description: All services run in K8s cluster — Podman is only used for building container images, never for running services
type: feedback
---

All services run in the K8s cluster, not standalone Podman pods. Podman is only for building container images (`podman build`).

**Why:** User consolidated everything under K8s (2026-03-30). Old Podman pods and Secure VMs were removed. Single environment that mirrors production.

**How to apply:**
- Never create Podman pods for running services (no `podman run`, no `podman pod create`)
- Use `podman build` to create images, then push to K8s registry (192.168.56.10:30500)
- Deploy via `kubectl apply` or `./infra/setup.sh k8s-deploy`
- Database is inside K8s as a StatefulSet, not a standalone container
