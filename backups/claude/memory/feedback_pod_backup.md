---
name: Always backup DB before pod recreate
description: CRITICAL — never recreate podman pod without taking a pg_dump backup first. Data was lost when pod was recreated.
type: feedback
---

Always take a `pg_dump` backup BEFORE any destructive pod operation (stop+rm, destroy, recreate).

**Why:** On 2026-03-26, the pod was recreated to apply new resource limits. PG reinitialised the data directory on the existing volume, overwriting all data. SD requests (20K+ synced from ManageEngine), devices, switches, users, roles, credentials — all lost. Had to re-sync and re-seed.

**How to apply:** Before ANY `podman pod rm`, `podman pod stop` + recreate, or `setup.sh` after a `pod rm`:

```bash
# ALWAYS run this first
podman exec central-postgres pg_dump -U central -d central -F c -f /tmp/backup.dump
podman cp central-postgres:/tmp/backup.dump ./central-backup-$(date +%Y%m%d).dump
```

To restore after recreate:
```bash
podman cp ./central-backup-*.dump central-postgres:/tmp/backup.dump
podman exec central-postgres pg_restore -U central -d central --clean --if-exists /tmp/backup.dump
```

Never assume the volume will survive a pod recreate intact — PG may reinitialise.
