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
REDIS_CONTAINER="central-redis"
AUTH_CONTAINER="central-auth-service"
VOLUME_NAME="central-pgdata"
PG_IMAGE="docker.io/library/postgres:18-alpine"
REDIS_IMAGE="docker.io/library/redis:7-alpine"
PG_PORT=5432
PG_DB="central"
PG_USER="central"
PG_PASS="central"
AUTH_DB="secure_auth"
AUTH_PORT=8081
SCHEMA_FILE="$(dirname "$0")/../db/schema.sql"
AUTH_MIGRATIONS_DIR="${SECURE_APP_DIR:-/c/Development/Secure/SecureAPP}/migrations/auth-service"
AUTH_SEED_FILE="$(dirname "$0")/../db/seed_auth.sql"

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
# Auth Service Database
# ---------------------------------------------------------------------------

setup_auth_db() {
    # Create secure_auth database if it doesn't exist
    if ! podman exec "$CONTAINER_NAME" \
        psql -U "$PG_USER" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='${AUTH_DB}'" | grep -q 1; then
        log "Creating auth database: ${AUTH_DB}"
        podman exec "$CONTAINER_NAME" \
            psql -U "$PG_USER" -d postgres -c "CREATE DATABASE ${AUTH_DB} OWNER ${PG_USER};"
    else
        log "Auth database '${AUTH_DB}' already exists."
    fi

    # Apply auth-service migrations (V001-V017)
    if [ -d "$AUTH_MIGRATIONS_DIR" ]; then
        log "Applying auth-service migrations..."
        for migration in "$AUTH_MIGRATIONS_DIR"/V*.sql; do
            [ -f "$migration" ] || continue
            log "  Auth migration: $(basename "$migration")"
            podman exec -i "$CONTAINER_NAME" \
                psql -U "$PG_USER" -d "$AUTH_DB" < "$migration" 2>/dev/null || true
        done
    else
        log "WARNING: Auth migrations not found at ${AUTH_MIGRATIONS_DIR}"
        log "  Set SECURE_APP_DIR env var to point to your SecureAPP checkout."
    fi

    # Apply Central auth seed (default tenant + admin user)
    if [ -f "$AUTH_SEED_FILE" ]; then
        log "Applying auth seed data..."
        podman exec -i "$CONTAINER_NAME" \
            psql -U "$PG_USER" -d "$AUTH_DB" < "$AUTH_SEED_FILE" 2>/dev/null || true
    fi
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

    # Create pod (shared network namespace — all containers share localhost)
    if podman pod inspect "$POD_NAME" &>/dev/null; then
        log "Pod '$POD_NAME' already exists — starting..."
        podman pod start "$POD_NAME"
    else
        podman pod create \
            --name "$POD_NAME" \
            --publish "${PG_PORT}:5432" \
            --publish "6379:6379" \
            --publish "5000:5000" \
            --publish "${AUTH_PORT}:8081"
        log "Created pod: $POD_NAME"

        # --- PostgreSQL ---
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

        # --- Redis (session store for auth-service) ---
        podman run -d \
            --pod "$POD_NAME" \
            --name "$REDIS_CONTAINER" \
            --restart=always \
            "$REDIS_IMAGE" \
            redis-server --maxmemory 256mb --maxmemory-policy allkeys-lru --save "" --appendonly no
        log "Started container: $REDIS_CONTAINER"

        # --- Auth Service (Rust) — only if image exists ---
        if podman image exists localhost/auth-service:latest 2>/dev/null; then
            podman run -d \
                --pod "$POD_NAME" \
                --name "$AUTH_CONTAINER" \
                --restart=always \
                --env DATABASE_HOST=localhost \
                --env DATABASE_PORT=5432 \
                --env DATABASE_NAME="$AUTH_DB" \
                --env DATABASE_USER="$PG_USER" \
                --env DATABASE_PASSWORD="$PG_PASS" \
                --env REDIS_URL="redis://localhost:6379" \
                --env AUTH__JWT__SIGNING_KEY="Central-Auth-Shared-JWT-Key-Override-This-In-Production-32bytes!" \
                --env AUTH__JWT__ISSUER="central-auth" \
                --env AUTH__JWT__AUDIENCE="central" \
                --env AUTH__ENCRYPTION__KEY="Q2VudHJhbC1BdXRoLUVuY3J5cHRpb24tS2V5LTMyYnl0ZXMh" \
                --env AUTH__SERVER__PORT=8081 \
                --env AUTH__APP_NAME="Central" \
                --env RUST_LOG="auth_service=info,tower_http=info" \
                localhost/auth-service:latest
            log "Started container: $AUTH_CONTAINER"
        else
            log "NOTE: auth-service image not found. Run './infra/setup.sh build-auth' first to enable."
        fi
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

    # --- Auth Service Database (secure_auth) ---
    setup_auth_db

    log ""
    log "================================================================"
    log "  PostgreSQL is running:"
    log "    Host:     localhost:${PG_PORT}"
    log "    Central:  ${PG_DB}"
    log "    Auth:     ${AUTH_DB}"
    log "    User:     ${PG_USER}"
    log "    Password: ${PG_PASS}"
    log ""
    log "  Central DSN: postgresql://${PG_USER}:${PG_PASS}@localhost:${PG_PORT}/${PG_DB}"
    log "  Auth DSN:    postgresql://${PG_USER}:${PG_PASS}@localhost:${PG_PORT}/${AUTH_DB}"
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

cmd_build_auth() {
    require_podman
    local secure_dir="${SECURE_APP_DIR:-/c/Development/Secure/SecureAPP}"
    if [ ! -f "$secure_dir/services/auth-service/Containerfile" ]; then
        echo "ERROR: auth-service Containerfile not found at $secure_dir"
        echo "Set SECURE_APP_DIR to your SecureAPP checkout."
        exit 1
    fi
    log "Building auth-service container image..."
    podman build -f "$secure_dir/services/auth-service/Containerfile" -t auth-service "$secure_dir"
    log "Auth service image built: auth-service:latest"
}

cmd_api_logs() {
    require_podman
    podman logs -f central-api 2>/dev/null || log "API container not running"
}

cmd_auth_logs() {
    require_podman
    podman logs -f "$AUTH_CONTAINER" 2>/dev/null || log "Auth service container not running"
}

cmd_auth_psql() {
    require_podman
    podman exec -it "$CONTAINER_NAME" psql -U "$PG_USER" -d "$AUTH_DB"
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
    echo -n "Central API:  "; curl -sf http://localhost:5000/health 2>/dev/null && echo "OK" || echo "not running"
    echo -n "Auth Service: "; curl -sf http://localhost:${AUTH_PORT}/health 2>/dev/null && echo "OK" || echo "not running"
    echo ""
    echo "=== Databases ==="
    podman exec "$CONTAINER_NAME" psql -U "$PG_USER" -d postgres -tAc "SELECT datname FROM pg_database WHERE datname IN ('central','secure_auth') ORDER BY datname;" 2>/dev/null || echo "Cannot query"
}

# ---------------------------------------------------------------------------
# K8s Commands (local cluster via Terraform/Vagrant)
# ---------------------------------------------------------------------------

K8S_KUBECONFIG="${KUBECONFIG:-$HOME/.kube/central-local.conf}"
K8S_NAMESPACE="central"
INFRA_DIR="$(dirname "$0")"

cmd_k8s_up() {
    log "Provisioning local K8s cluster..."
    cd "$INFRA_DIR/environments/local"
    if command -v terragrunt &>/dev/null; then
        terragrunt apply -auto-approve
    else
        log "Terragrunt not installed. Using Vagrant directly..."
        cd "$INFRA_DIR/vagrant"
        vagrant up
    fi
    log "Cluster is up. Run '$0 k8s-deploy' to deploy services."
}

cmd_k8s_deploy() {
    log "Deploying Central platform to K8s..."
    kubectl --kubeconfig="$K8S_KUBECONFIG" apply -k "$INFRA_DIR/k8s/base"
    log "Waiting for PostgreSQL..."
    kubectl --kubeconfig="$K8S_KUBECONFIG" -n "$K8S_NAMESPACE" wait \
        --for=condition=ready pod/postgres-0 --timeout=180s
    log "Applying Central DB migrations..."
    for migration in "$INFRA_DIR/../db/migrations"/*.sql; do
        [ -f "$migration" ] || continue
        kubectl --kubeconfig="$K8S_KUBECONFIG" -n "$K8S_NAMESPACE" exec -i postgres-0 -- \
            psql -U "$PG_USER" -d "$PG_DB" < "$migration" 2>/dev/null || true
    done
    setup_auth_db_k8s
    log "All services deployed. Run '$0 k8s-status' to check."
}

setup_auth_db_k8s() {
    local k="kubectl --kubeconfig=$K8S_KUBECONFIG -n $K8S_NAMESPACE"
    # Create secure_auth DB
    $k exec postgres-0 -- psql -U "$PG_USER" -d postgres -c \
        "SELECT 'CREATE DATABASE secure_auth OWNER central' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'secure_auth')\gexec" 2>/dev/null || true
    # Apply auth migrations
    if [ -d "$AUTH_MIGRATIONS_DIR" ]; then
        log "Applying auth-service migrations to K8s..."
        for migration in "$AUTH_MIGRATIONS_DIR"/V*.sql; do
            [ -f "$migration" ] || continue
            $k exec -i postgres-0 -- psql -U "$PG_USER" -d "$AUTH_DB" < "$migration" 2>/dev/null || true
        done
    fi
    # Apply seed
    if [ -f "$AUTH_SEED_FILE" ]; then
        $k exec -i postgres-0 -- psql -U "$PG_USER" -d "$AUTH_DB" < "$AUTH_SEED_FILE" 2>/dev/null || true
    fi
}

cmd_k8s_status() {
    echo "=== Nodes ==="
    kubectl --kubeconfig="$K8S_KUBECONFIG" get nodes -o wide 2>/dev/null || echo "Cluster not reachable"
    echo ""
    echo "=== Pods (central namespace) ==="
    kubectl --kubeconfig="$K8S_KUBECONFIG" -n "$K8S_NAMESPACE" get pods -o wide 2>/dev/null || echo "Namespace not found"
    echo ""
    echo "=== Services ==="
    kubectl --kubeconfig="$K8S_KUBECONFIG" -n "$K8S_NAMESPACE" get svc 2>/dev/null || echo "No services"
    echo ""
    echo "=== HPA ==="
    kubectl --kubeconfig="$K8S_KUBECONFIG" -n "$K8S_NAMESPACE" get hpa 2>/dev/null || echo "No HPA"
}

cmd_k8s_psql() {
    kubectl --kubeconfig="$K8S_KUBECONFIG" -n "$K8S_NAMESPACE" exec -it postgres-0 -- psql -U "$PG_USER" -d "${2:-$PG_DB}"
}

cmd_k8s_logs() {
    local svc="${2:-central-api}"
    kubectl --kubeconfig="$K8S_KUBECONFIG" -n "$K8S_NAMESPACE" logs -f "deployment/$svc" --all-containers 2>/dev/null || \
        kubectl --kubeconfig="$K8S_KUBECONFIG" -n "$K8S_NAMESPACE" logs -f "$svc" 2>/dev/null || \
        log "Cannot find $svc"
}

cmd_k8s_migrate() {
    log "Migrating data from Podman to K8s..."
    bash "$INFRA_DIR/scripts/migrate-to-k8s.sh" "${2:-}"
}

cmd_k8s_down() {
    log "Tearing down K8s cluster..."
    cd "$INFRA_DIR/environments/local"
    if command -v terragrunt &>/dev/null; then
        terragrunt destroy -auto-approve
    else
        cd "$INFRA_DIR/vagrant"
        vagrant destroy -f
    fi
}

cmd_k8s_push() {
    local image="${2:?Usage: $0 k8s-push <image-name>}"
    local master_ip
    master_ip=$(grep 'k8s-master' "$INFRA_DIR/ansible/inventory/hosts.yml" 2>/dev/null | grep ansible_host | awk '{print $2}' || echo "192.168.56.10")
    local registry="${master_ip}:30500"
    log "Tagging and pushing $image to $registry..."
    podman tag "$image:latest" "$registry/central/$image:latest"
    podman push "$registry/central/$image:latest" --tls-verify=false
    log "Pushed: $registry/central/$image:latest"
}

# ---------------------------------------------------------------------------
# Dispatch
# ---------------------------------------------------------------------------

ACTION="${1:-setup}"
case "$ACTION" in
    # --- Podman (legacy local dev) ---
    setup)      cmd_setup      ;;
    start)      cmd_start      ;;
    stop)       cmd_stop       ;;
    restart)    cmd_restart    ;;
    destroy)    cmd_destroy    ;;
    logs)       cmd_logs       ;;
    psql)       cmd_psql       ;;
    status)     cmd_status     ;;
    build-api)  cmd_build_api  ;;
    build-auth) cmd_build_auth ;;
    api-logs)   cmd_api_logs   ;;
    auth-logs)  cmd_auth_logs  ;;
    auth-psql)  cmd_auth_psql  ;;
    # --- K8s (production-like local cluster) ---
    k8s-up)      cmd_k8s_up      ;;
    k8s-deploy)  cmd_k8s_deploy  ;;
    k8s-status)  cmd_k8s_status  ;;
    k8s-psql)    cmd_k8s_psql "$@" ;;
    k8s-logs)    cmd_k8s_logs "$@" ;;
    k8s-migrate) cmd_k8s_migrate "$@" ;;
    k8s-push)    cmd_k8s_push "$@" ;;
    k8s-down)    cmd_k8s_down    ;;
    *)
        echo "Unknown action: $ACTION"
        echo ""
        echo "Podman (local dev):"
        echo "  $0 {setup|start|stop|restart|destroy|logs|psql|status}"
        echo "  $0 {build-api|build-auth|api-logs|auth-logs|auth-psql}"
        echo ""
        echo "K8s (production-like cluster):"
        echo "  $0 k8s-up          Create VMs + bootstrap K8s cluster"
        echo "  $0 k8s-deploy      Deploy all services to K8s"
        echo "  $0 k8s-status      Show nodes, pods, services, HPA"
        echo "  $0 k8s-psql [db]   psql into K8s PostgreSQL"
        echo "  $0 k8s-logs [svc]  Tail logs for a service"
        echo "  $0 k8s-migrate     Move data from Podman to K8s"
        echo "  $0 k8s-push <img>  Push image to local K8s registry"
        echo "  $0 k8s-down        Destroy K8s cluster VMs"
        exit 1
        ;;
esac
