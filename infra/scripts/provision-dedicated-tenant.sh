#!/usr/bin/env bash
# =============================================================================
# Manual provisioning of a dedicated database for an enterprise tenant.
# Mirrors the Rust tenant-provisioner service — use only for emergencies.
#
# Usage:
#   PG_ADMIN_URL="postgres://postgres:...@postgres:5432/postgres" \
#   PLATFORM_URL="postgres://central:...@postgres:5432/central" \
#   ./provision-dedicated-tenant.sh <tenant-slug>
# =============================================================================
set -euo pipefail

SLUG="${1:?Usage: $0 <tenant-slug>}"
SLUG_SAFE=$(echo "$SLUG" | sed 's/[^a-zA-Z0-9_]/_/g')
SLUG_DASH=$(echo "$SLUG" | sed 's/[^a-zA-Z0-9-]/-/g')
DB_NAME="central_${SLUG_SAFE}"
NS_NAME="central-${SLUG_DASH}"
SOURCE_SCHEMA="tenant_${SLUG_SAFE}"
DUMP_FILE="/tmp/tenant-${SLUG_SAFE}-$(date +%s).dump"

PG_ADMIN_URL="${PG_ADMIN_URL:?PG_ADMIN_URL required (superuser)}"
PLATFORM_URL="${PLATFORM_URL:?PLATFORM_URL required (central_platform access)}"

echo "==> Provisioning dedicated DB for tenant '$SLUG'"
echo "    Database:   $DB_NAME"
echo "    Namespace:  $NS_NAME"
echo "    Source:     $SOURCE_SCHEMA"

# Resolve tenant ID
TENANT_ID=$(psql "$PLATFORM_URL" -tAc "SELECT id FROM central_platform.tenants WHERE slug = '$SLUG'")
[ -n "$TENANT_ID" ] || { echo "ERROR: tenant '$SLUG' not found"; exit 1; }
echo "    TenantId:   $TENANT_ID"

# 1. Create new database
echo "==> Creating database $DB_NAME"
psql "$PG_ADMIN_URL" -c "CREATE DATABASE \"$DB_NAME\" OWNER central" || echo "  (already exists)"

# 2. Install extensions
echo "==> Installing extensions"
NEW_DB_URL=$(echo "$PG_ADMIN_URL" | sed "s|/[^/?]*\$|/$DB_NAME|")
for ext in uuid-ossp pgcrypto pg_trgm citext btree_gin; do
    psql "$NEW_DB_URL" -c "CREATE EXTENSION IF NOT EXISTS \"$ext\"" || true
done

# 3. pg_dump source schema
echo "==> Dumping source schema $SOURCE_SCHEMA → $DUMP_FILE"
pg_dump "$PG_ADMIN_URL" -n "$SOURCE_SCHEMA" -Fc -f "$DUMP_FILE"

# 4. Restore into new DB
echo "==> Restoring into $DB_NAME"
pg_restore -d "$NEW_DB_URL" --no-owner --no-privileges --if-exists --clean "$DUMP_FILE" || true

# 5. Apply migrations (completeness)
if [ -d /migrations ]; then
    echo "==> Applying migrations"
    for f in /migrations/*.sql; do
        psql "$NEW_DB_URL" -v ON_ERROR_STOP=1 -f "$f" >/dev/null || echo "  skipped: $(basename $f)"
    done
fi

# 6. Create K8s namespace from template
if command -v kubectl >/dev/null; then
    echo "==> Creating K8s namespace $NS_NAME"
    TENANT_TEMPLATE_DIR="$(dirname $0)/../k8s/tenant-template"
    PROVISIONED_AT=$(date -u +%Y-%m-%dT%H:%M:%SZ)
    find "$TENANT_TEMPLATE_DIR" -name '*.yaml' -exec cat {} \; \
        | sed "s/__TENANT_SLUG__/$SLUG_DASH/g; s/__TENANT_ID__/$TENANT_ID/g; s/__DATABASE_NAME__/$DB_NAME/g; s/__PROVISIONED_AT__/$PROVISIONED_AT/g" \
        | kubectl apply -f -
fi

# 7. Update tenant_connection_map
echo "==> Routing tenant '$SLUG' to dedicated database"
psql "$PLATFORM_URL" <<SQL
INSERT INTO central_platform.tenant_connection_map
    (tenant_id, sizing_model, database_name, schema_name, k8s_namespace)
VALUES ('$TENANT_ID', 'dedicated', '$DB_NAME', 'public', '$NS_NAME')
ON CONFLICT (tenant_id) DO UPDATE
SET sizing_model  = 'dedicated',
    database_name = '$DB_NAME',
    schema_name   = 'public',
    k8s_namespace = '$NS_NAME',
    updated_at    = NOW();

UPDATE central_platform.tenants
SET sizing_model = 'dedicated', provisioning_status = 'ready'
WHERE id = '$TENANT_ID';
SQL

echo "==> Provisioning complete for tenant '$SLUG'"
echo "    Verify:    psql '$NEW_DB_URL' -c '\\dt'"
echo "    K8s:       kubectl -n $NS_NAME get all"
