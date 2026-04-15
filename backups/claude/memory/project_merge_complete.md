---
name: Merge plan complete
description: All 10 phases of Central + Secure merge delivered (2026-04-14). 7 Rust services, 3 client surfaces, 12 K8s manifests.
type: project
originSessionId: b843b558-8aff-4405-a09b-8537ec6eb980
---
Central + Secure merge — all 10 phases complete as of 2026-04-14.

**Why:** Platform convergence — unified enterprise system with multi-surface access.

**How to apply:** All merge deliverables are tracked in `docs/MERGE_PLAN.md`. When working on any service, check the source in `SecureAPP/services/`. Gateway routes all traffic — WPF desktop `api.url` defaults to port 8000.

Key locations:
- 7 Rust services: `SecureAPP/services/{auth,admin,gateway,task,storage,sync,audit}-service/`
- Flutter mobile: `SecureAPP/clients/mobile/` (4 screens, drift DB, sync, FCM)
- Angular web: `web-client/` (7 modules, DxTreeList/DxDataGrid, SSE)
- K8s manifests: `infra/k8s/base/` (12 files incl. monitoring, PDB, sealed-secrets)
- CI/CD: `.github/workflows/` (ci, nightly, pr-check, promote)
- Backup: `scripts/backup.sh` (7 daily / 4 weekly / 12 monthly retention)
- User migration: `scripts/migrate-users-to-auth-service.sh`
