#!/bin/bash
# Central Platform — Automated Backup with Retention
#
# Backs up: PostgreSQL (all databases), Redis snapshot, K8s manifests
# Retention: 7 daily, 4 weekly, 12 monthly
#
# Usage:
#   ./scripts/backup.sh                # Run backup now
#   ./scripts/backup.sh --prune        # Run backup + prune old backups
#
# Cron (daily at 02:00):
#   0 2 * * * /path/to/Central/scripts/backup.sh --prune >> /var/log/central-backup.log 2>&1
#
# Environment:
#   BACKUP_DIR       — backup root (default: /backups/central)
#   KUBECONFIG       — K8s config file
#   PG_HOST          — PostgreSQL host (default: 192.168.56.201)
#   PG_USER          — PostgreSQL user (default: central)

set -euo pipefail

BACKUP_DIR="${BACKUP_DIR:-/backups/central}"
PG_HOST="${PG_HOST:-192.168.56.201}"
PG_USER="${PG_USER:-central}"
PG_PORT="${PG_PORT:-5432}"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
DAY_OF_WEEK=$(date +%u)  # 1=Monday, 7=Sunday
DAY_OF_MONTH=$(date +%d)

DAILY_DIR="$BACKUP_DIR/daily"
WEEKLY_DIR="$BACKUP_DIR/weekly"
MONTHLY_DIR="$BACKUP_DIR/monthly"

mkdir -p "$DAILY_DIR" "$WEEKLY_DIR" "$MONTHLY_DIR"

echo "=== Central Backup — $TIMESTAMP ==="

# ── PostgreSQL dumps ──
echo "Backing up PostgreSQL databases..."
for DB in central secure_auth; do
  DUMP_FILE="$DAILY_DIR/${DB}_${TIMESTAMP}.sql.gz"
  PGPASSWORD=central pg_dump -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" "$DB" | gzip > "$DUMP_FILE"
  SIZE=$(du -h "$DUMP_FILE" | cut -f1)
  echo "  $DB → $DUMP_FILE ($SIZE)"
done

# ── Redis snapshot ──
echo "Backing up Redis..."
REDIS_FILE="$DAILY_DIR/redis_${TIMESTAMP}.rdb"
if command -v kubectl &>/dev/null; then
  kubectl exec -n central redis-0 -- redis-cli BGSAVE 2>/dev/null || true
  sleep 2
  kubectl cp central/redis-0:/data/dump.rdb "$REDIS_FILE" 2>/dev/null || echo "  Redis backup skipped (not reachable)"
else
  echo "  Redis backup skipped (no kubectl)"
fi

# ── K8s manifest export ──
echo "Exporting K8s manifests..."
K8S_FILE="$DAILY_DIR/k8s_manifests_${TIMESTAMP}.yaml"
if command -v kubectl &>/dev/null; then
  kubectl get all,configmaps,secrets,pdb,hpa -n central -o yaml > "$K8S_FILE" 2>/dev/null || true
  gzip "$K8S_FILE" 2>/dev/null || true
  echo "  K8s → ${K8S_FILE}.gz"
else
  echo "  K8s export skipped (no kubectl)"
fi

# ── Weekly copy (every Sunday) ──
if [ "$DAY_OF_WEEK" = "7" ]; then
  echo "Creating weekly backup..."
  for f in "$DAILY_DIR"/*_${TIMESTAMP}*; do
    cp "$f" "$WEEKLY_DIR/" 2>/dev/null || true
  done
fi

# ── Monthly copy (1st of month) ──
if [ "$DAY_OF_MONTH" = "01" ]; then
  echo "Creating monthly backup..."
  for f in "$DAILY_DIR"/*_${TIMESTAMP}*; do
    cp "$f" "$MONTHLY_DIR/" 2>/dev/null || true
  done
fi

# ── Prune old backups ──
if [ "${1:-}" = "--prune" ]; then
  echo "Pruning old backups..."
  # Keep 7 daily
  find "$DAILY_DIR" -type f -mtime +7 -delete 2>/dev/null
  DAILY_COUNT=$(find "$DAILY_DIR" -type f | wc -l)
  echo "  Daily: $DAILY_COUNT files (keeping 7 days)"

  # Keep 4 weekly
  find "$WEEKLY_DIR" -type f -mtime +28 -delete 2>/dev/null
  WEEKLY_COUNT=$(find "$WEEKLY_DIR" -type f | wc -l)
  echo "  Weekly: $WEEKLY_COUNT files (keeping 4 weeks)"

  # Keep 12 monthly
  find "$MONTHLY_DIR" -type f -mtime +365 -delete 2>/dev/null
  MONTHLY_COUNT=$(find "$MONTHLY_DIR" -type f | wc -l)
  echo "  Monthly: $MONTHLY_COUNT files (keeping 12 months)"
fi

# ── Summary ──
TOTAL_SIZE=$(du -sh "$BACKUP_DIR" 2>/dev/null | cut -f1)
echo ""
echo "=== Backup complete — $TOTAL_SIZE total ==="
