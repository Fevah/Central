# Backups

This folder contains point-in-time snapshots of state needed to fully
restore the Central platform. Committed to git so it's mirrored to the
remote (Fevah/Central on GitHub) — that's our offsite copy.

## Contents

```
backups/
├── db/                                     # PostgreSQL dumps (gzipped pg_dump)
│   ├── central-YYYYMMDD-HHMMSS.sql.gz      # Application DB (devices, switches, tasks, …)
│   └── secure_auth-YYYYMMDD-HHMMSS.sql.gz  # Auth-service DB (users, sessions, MFA)
└── claude/
    ├── memory/                             # Auto-memory facts (preferences, feedback, project notes)
    │   ├── MEMORY.md                       # Index — list of all memory files
    │   └── *.md                            # One file per memory entry
    └── sessions/
        └── session-<id>-YYYYMMDD-HHMMSS.jsonl  # Full Claude conversation transcript
```

## Backup cadence

These are taken on demand (after big migrations, before risky changes,
end of each significant work session). Not scheduled — the daily backup
job in K8s writes its own dumps to MinIO via the `db_backup` schedule.

To take a fresh snapshot, run from the repo root:

```bash
DATE=$(date +%Y%m%d-%H%M%S)
export KUBECONFIG=~/.kube/central-local.conf

# DB
kubectl -n central exec postgres-0 -- env PGPASSWORD=central \
    pg_dump -U central -d central --no-owner --no-acl \
    | gzip > backups/db/central-${DATE}.sql.gz

kubectl -n central exec postgres-0 -- env PGPASSWORD=central \
    pg_dump -U central -d secure_auth --no-owner --no-acl \
    | gzip > backups/db/secure_auth-${DATE}.sql.gz

# Claude state
cp ~/.claude/projects/c--Development-Central/*.jsonl \
   backups/claude/sessions/

cp -r ~/.claude/projects/c--Development-Central/memory/. \
      backups/claude/memory/
```

## Restore procedure

### Restore a database

```bash
# Drop + recreate (DESTRUCTIVE — only do this if you mean it)
kubectl -n central exec postgres-0 -- env PGPASSWORD=central \
    psql -U central -d postgres -c 'DROP DATABASE IF EXISTS central;'
kubectl -n central exec postgres-0 -- env PGPASSWORD=central \
    psql -U central -d postgres -c 'CREATE DATABASE central OWNER central;'

# Restore from snapshot (pick the file you want)
gunzip -c backups/db/central-20260415-115452.sql.gz | \
    kubectl -n central exec -i postgres-0 -- env PGPASSWORD=central \
    psql -U central -d central
```

### Restore Claude memory + chat history

```bash
# Memory — copy back into the live memory dir
cp -r backups/claude/memory/. \
      ~/.claude/projects/c--Development-Central/memory/

# Session transcript — Claude reads these on startup; drop the .jsonl
# back into ~/.claude/projects/<project>/ to make it visible again
cp backups/claude/sessions/session-*.jsonl \
   ~/.claude/projects/c--Development-Central/
```

## Retention

Everything in this folder is committed to git, so it's permanent in the
remote history. If file count or size grows past comfortable, prune older
snapshots with `git rm` — the old versions remain in git history but
don't bloat the working tree.

## Why git, not S3 / MinIO?

The K8s `db_backup` job already streams full dumps to MinIO with retention.
This folder is the **belt-and-braces** copy:

- Mirrors to GitHub on every push → off-host, off-cluster
- Lives in the same repo as the schema migrations that produced them →
  any restore is reproducible against the matching code revision
- Doesn't depend on K8s being healthy to recover (which is the very
  scenario where you'd reach for a backup)
