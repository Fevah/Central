---
name: Start all services on reboot
description: When user says "rebooted" or mentions starting up, start ALL services — K8s VMs, Angular dev server, API server, not just the K8s check
type: feedback
originSessionId: b843b558-8aff-4405-a09b-8537ec6eb980
---
When the user says "rebooted" or asks to "start" the platform, start **all services**, not just verify K8s is up.

**Why:** The user relies on me to bring the full stack online. Missing any service means they hit the issue later ("http://localhost:4200 not loading"). Starting only some leaves the platform half-up.

**How to apply:** On "rebooted" / "start services" / "bring up the platform":

1. **K8s VMs** — `cd infra/vagrant && vagrant status` (start if needed: `vagrant up`)
2. **K8s cluster** — `kubectl get nodes` (refresh kubeconfig from master if cert errors: `vagrant ssh k8s-master -c "sudo cat /etc/kubernetes/admin.conf" > ~/.kube/central-local.conf`)
3. **K8s services** — `kubectl -n central get pods` (check all services running)
4. **Angular dev server** — `cd web-client && npx ng serve --port 4200 --host 0.0.0.0` in background (for http://localhost:4200)
5. **FastAPI web app** — `./run.sh` in background (for http://localhost:8080)
6. **WPF desktop** — only if user explicitly asks (it's a GUI app they launch manually)
7. **Final confirm** — report the status of each service with its URL

Always run long-running servers in background (`run_in_background: true`) so they don't block, and confirm each one is reachable before saying "ready".

Default URLs to confirm:
- Angular: http://localhost:4200
- FastAPI: http://localhost:8080
- Gateway: http://192.168.56.203:8000/health
- Grafana: http://192.168.56.210:3000
- Jaeger: http://192.168.56.10:30686
- MinIO Console: http://192.168.56.10:30901
