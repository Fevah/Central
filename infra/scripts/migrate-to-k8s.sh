#!/usr/bin/env bash
# =============================================================================
# Migrate Central Database — Podman → K8s
# =============================================================================
# Exports the central DB from the old Podman pod and imports into the K8s
# PostgreSQL StatefulSet.
#
# Prerequisites:
#   - K8s cluster running (terragrunt apply in environments/local)
#   - PostgreSQL StatefulSet deployed (kubectl apply -k infra/k8s/base)
#   - Old Podman pod data still available (central-postgres container or backup)
#
# Usage:
#   ./infra/scripts/migrate-to-k8s.sh [backup-file]
#   ./infra/scripts/migrate-to-k8s.sh central_backup_20260330.dump
# =============================================================================

set -euo pipefail

NAMESPACE="central"
PG_POD="postgres-0"
PG_USER="central"
CENTRAL_DB="central"
AUTH_DB="secure_auth"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
KUBECONFIG="${KUBECONFIG:-$HOME/.kube/central-local.conf}"

log() { echo "[migrate] $*"; }

# --- Step 1: Get the backup ---

BACKUP_FILE="${1:-}"
if [ -z "$BACKUP_FILE" ]; then
    # Try to dump from running Podman pod
    if podman ps --filter name=central-postgres --format '{{.Names}}' 2>/dev/null | grep -q central-postgres; then
        log "Dumping from running Podman pod..."
        BACKUP_FILE="/tmp/central_migration.dump"
        podman exec central-postgres pg_dump -U "$PG_USER" -d "$CENTRAL_DB" --format=custom > "$BACKUP_FILE"
        log "Dump saved: $BACKUP_FILE ($(du -h "$BACKUP_FILE" | cut -f1))"
    else
        # Look for existing backup
        BACKUP_FILE=$(ls -t "$REPO_ROOT"/central_backup_*.dump 2>/dev/null | head -1)
        if [ -z "$BACKUP_FILE" ]; then
            echo "ERROR: No backup file specified and no Podman pod running."
            echo "Usage: $0 <backup-file.dump>"
            exit 1
        fi
        log "Using existing backup: $BACKUP_FILE"
    fi
fi

[ -f "$BACKUP_FILE" ] || { echo "ERROR: Backup file not found: $BACKUP_FILE"; exit 1; }

# --- Step 2: Wait for K8s PostgreSQL to be ready ---

log "Waiting for K8s PostgreSQL pod..."
kubectl --kubeconfig="$KUBECONFIG" -n "$NAMESPACE" wait \
    --for=condition=ready pod/"$PG_POD" --timeout=120s

# --- Step 3: Copy backup into the pod ---

log "Copying backup to K8s pod..."
kubectl --kubeconfig="$KUBECONFIG" -n "$NAMESPACE" cp "$BACKUP_FILE" "$PG_POD":/tmp/central.dump

# --- Step 4: Restore central database ---

log "Restoring central database..."
kubectl --kubeconfig="$KUBECONFIG" -n "$NAMESPACE" exec "$PG_POD" -- \
    pg_restore -U "$PG_USER" -d "$CENTRAL_DB" --clean --if-exists --no-owner /tmp/central.dump 2>&1 || true

# --- Step 5: Create secure_auth database if not exists ---

log "Ensuring secure_auth database exists..."
kubectl --kubeconfig="$KUBECONFIG" -n "$NAMESPACE" exec "$PG_POD" -- \
    psql -U "$PG_USER" -d postgres -c \
    "SELECT 'CREATE DATABASE secure_auth OWNER central' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'secure_auth')\gexec" 2>/dev/null || true

# --- Step 6: Apply auth migrations ---

if [ -d "$REPO_ROOT/../Secure/SecureAPP/migrations/auth-service" ]; then
    log "Applying auth-service migrations..."
    for migration in "$REPO_ROOT/../Secure/SecureAPP/migrations/auth-service"/V*.sql; do
        [ -f "$migration" ] || continue
        log "  $(basename "$migration")"
        kubectl --kubeconfig="$KUBECONFIG" -n "$NAMESPACE" exec -i "$PG_POD" -- \
            psql -U "$PG_USER" -d "$AUTH_DB" < "$migration" 2>/dev/null || true
    done
fi

# --- Step 7: Apply auth seed ---

if [ -f "$REPO_ROOT/db/seed_auth.sql" ]; then
    log "Applying auth seed data..."
    kubectl --kubeconfig="$KUBECONFIG" -n "$NAMESPACE" exec -i "$PG_POD" -- \
        psql -U "$PG_USER" -d "$AUTH_DB" < "$REPO_ROOT/db/seed_auth.sql" 2>/dev/null || true
fi

# --- Step 8: Verify ---

log "Verifying migration..."
CENTRAL_TABLES=$(kubectl --kubeconfig="$KUBECONFIG" -n "$NAMESPACE" exec "$PG_POD" -- \
    psql -U "$PG_USER" -d "$CENTRAL_DB" -tAc "SELECT count(*) FROM pg_tables WHERE schemaname='public'")
AUTH_TABLES=$(kubectl --kubeconfig="$KUBECONFIG" -n "$NAMESPACE" exec "$PG_POD" -- \
    psql -U "$PG_USER" -d "$AUTH_DB" -tAc "SELECT count(*) FROM pg_tables WHERE schemaname='public'")
RLS_COUNT=$(kubectl --kubeconfig="$KUBECONFIG" -n "$NAMESPACE" exec "$PG_POD" -- \
    psql -U "$PG_USER" -d "$CENTRAL_DB" -tAc "SELECT count(*) FROM pg_policies WHERE schemaname='public'")

log ""
log "================================================================"
log "  Migration complete!"
log "  Central DB:  ${CENTRAL_TABLES} tables, ${RLS_COUNT} RLS policies"
log "  Auth DB:     ${AUTH_TABLES} tables"
log ""
log "  K8s access:  kubectl -n central exec -it postgres-0 -- psql -U central -d central"
log "  External:    psql -h <metallb-ip> -U central -d central"
log "================================================================"

# Cleanup
kubectl --kubeconfig="$KUBECONFIG" -n "$NAMESPACE" exec "$PG_POD" -- rm -f /tmp/central.dump
