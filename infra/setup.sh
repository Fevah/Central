#!/usr/bin/env bash
# =============================================================================
# Central Podman Setup Script
# =============================================================================
# Creates a local Podman pod running PostgreSQL for Central.
#
# Usage:
#   ./infra/setup.sh            # first-time setup + start
#   ./infra/setup.sh start      # start existing pod
#   ./infra/setup.sh stop       # stop pod
#   ./infra/setup.sh restart    # restart pod
#   ./infra/setup.sh destroy    # remove pod and data volume
#   ./infra/setup.sh logs       # tail postgres logs
#   ./infra/setup.sh psql       # open psql shell
#   ./infra/setup.sh status     # show pod and container status
# =============================================================================

set -euo pipefail

POD_NAME="central"
CONTAINER_NAME="central-postgres"
VOLUME_NAME="central-pgdata"
PG_IMAGE="docker.io/library/postgres:18-alpine"
PG_PORT=5432
PG_DB="central"
PG_USER="central"
PG_PASS="central"
SCHEMA_FILE="$(dirname "$0")/../db/schema.sql"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

log() { echo "[central] $*"; }

require_podman() {
    if ! command -v podman &>/dev/null; then
        echo "ERROR: podman is not installed or not in PATH"
        exit 1
    fi
}

wait_for_postgres() {
    log "Waiting for PostgreSQL to be ready..."
    local max=30 i=0
    until podman exec "$CONTAINER_NAME" pg_isready -U "$PG_USER" -d "$PG_DB" &>/dev/null; do
        i=$((i+1))
        if [ $i -ge $max ]; then
            echo "ERROR: PostgreSQL did not become ready after ${max}s"
            exit 1
        fi
        sleep 1
    done
    log "PostgreSQL is ready."
}

# ---------------------------------------------------------------------------
# Commands
# ---------------------------------------------------------------------------

cmd_setup() {
    require_podman
    log "Setting up Central pod..."

    # Create named volume for persistent data
    if ! podman volume inspect "$VOLUME_NAME" &>/dev/null; then
        podman volume create "$VOLUME_NAME"
        log "Created volume: $VOLUME_NAME"
    fi

    # Create pod (shared network namespace)
    if podman pod inspect "$POD_NAME" &>/dev/null; then
        log "Pod '$POD_NAME' already exists — starting..."
        podman pod start "$POD_NAME"
    else
        podman pod create \
            --name "$POD_NAME" \
            --publish "${PG_PORT}:5432"
        log "Created pod: $POD_NAME"

        podman run -d \
            --pod "$POD_NAME" \
            --name "$CONTAINER_NAME" \
            --restart=always \
            --env POSTGRES_DB="$PG_DB" \
            --env POSTGRES_USER="$PG_USER" \
            --env POSTGRES_PASSWORD="$PG_PASS" \
            --env PGDATA=/var/lib/postgresql/data/pgdata \
            --volume "${VOLUME_NAME}:/var/lib/postgresql/data:Z" \
            "$PG_IMAGE"
        log "Started container: $CONTAINER_NAME"
    fi

    wait_for_postgres

    # Apply schema if not already applied
    if ! podman exec "$CONTAINER_NAME" \
        psql -U "$PG_USER" -d "$PG_DB" -c "\dt switches" 2>/dev/null | grep -q "switches"; then
        log "Applying database schema..."
        podman exec -i "$CONTAINER_NAME" \
            psql -U "$PG_USER" -d "$PG_DB" < "$SCHEMA_FILE"
        log "Schema applied."
    else
        log "Schema already exists — checking migrations..."
    fi
    # Always apply migrations (idempotent)
    MIGRATIONS_DIR="$(dirname "$0")/../db/migrations"
    for migration in "$MIGRATIONS_DIR"/*.sql; do
        [ -f "$migration" ] || continue
        log "  Applying migration: $(basename "$migration")"
        podman exec -i "$CONTAINER_NAME" \
            psql -U "$PG_USER" -d "$PG_DB" < "$migration" 2>/dev/null || true
    done

    log ""
    log "================================================================"
    log "  PostgreSQL is running:"
    log "    Host:     localhost:${PG_PORT}"
    log "    Database: ${PG_DB}"
    log "    User:     ${PG_USER}"
    log "    Password: ${PG_PASS}"
    log ""
    log "  DSN: postgresql://${PG_USER}:${PG_PASS}@localhost:${PG_PORT}/${PG_DB}"
    log "================================================================"
}

cmd_start() {
    require_podman
    podman pod start "$POD_NAME"
    log "Pod '$POD_NAME' started."
}

cmd_stop() {
    require_podman
    podman pod stop "$POD_NAME"
    log "Pod '$POD_NAME' stopped."
}

cmd_restart() {
    cmd_stop
    cmd_start
}

cmd_destroy() {
    require_podman
    log "WARNING: This will delete all data in the '$VOLUME_NAME' volume!"
    read -rp "Are you sure? [y/N]: " confirm
    if [[ "$confirm" =~ ^[Yy]$ ]]; then
        podman pod stop "$POD_NAME" 2>/dev/null || true
        podman pod rm "$POD_NAME" 2>/dev/null || true
        podman volume rm "$VOLUME_NAME" 2>/dev/null || true
        log "Pod and volume removed."
    else
        log "Cancelled."
    fi
}

cmd_logs() {
    require_podman
    podman logs -f "$CONTAINER_NAME"
}

cmd_psql() {
    require_podman
    podman exec -it "$CONTAINER_NAME" psql -U "$PG_USER" -d "$PG_DB"
}

cmd_build_api() {
    require_podman
    log "Building API container image..."
    local desktop_dir
    desktop_dir="$(dirname "$0")/../desktop"
    podman build -f "$desktop_dir/Central.Api/Dockerfile" -t central-api "$desktop_dir"
    log "API image built: central-api:latest"
}

cmd_api_logs() {
    require_podman
    podman logs -f central-api 2>/dev/null || log "API container not running"
}

cmd_status() {
    require_podman
    echo "=== Pod ==="
    podman pod ps --filter name="$POD_NAME" 2>/dev/null || echo "Pod not found"
    echo ""
    echo "=== Containers ==="
    podman ps -a --pod --filter pod="$POD_NAME" 2>/dev/null || echo "No containers"
    echo ""
    echo "=== Volume ==="
    podman volume inspect "$VOLUME_NAME" --format '{{.Name}}: {{.Mountpoint}}' 2>/dev/null || echo "Volume not found"
    echo ""
    echo "=== Health ==="
    curl -s http://localhost:5000/health 2>/dev/null && echo "" || echo "API: not running"
}

# ---------------------------------------------------------------------------
# Dispatch
# ---------------------------------------------------------------------------

ACTION="${1:-setup}"
case "$ACTION" in
    setup)     cmd_setup     ;;
    start)     cmd_start     ;;
    stop)      cmd_stop      ;;
    restart)   cmd_restart   ;;
    destroy)   cmd_destroy   ;;
    logs)      cmd_logs      ;;
    psql)      cmd_psql      ;;
    status)    cmd_status    ;;
    build-api) cmd_build_api ;;
    api-logs)  cmd_api_logs  ;;
    *)
        echo "Unknown action: $ACTION"
        echo "Usage: $0 {setup|start|stop|restart|destroy|logs|psql|status|build-api|api-logs}"
        exit 1
        ;;
esac
