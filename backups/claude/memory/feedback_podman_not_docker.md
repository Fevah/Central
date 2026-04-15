---
name: Podman for builds, never Docker
description: Use Podman for container image builds. Never use Docker CLI or docker-compose. Services run in K8s, not Podman pods.
type: feedback
---

Use Podman for building container images. Never use Docker.

**Why:** User uses Podman exclusively. As of 2026-03-30, services run in K8s (not Podman pods). Podman is only for `podman build` + `podman push`.

**How to apply:** Container build commands use `podman build`. Image files named `Containerfile` not `Dockerfile`. Push to K8s registry: `podman push 192.168.56.10:30500/central/<image>:latest --tls-verify=false`. Never write docker-compose.yml.
