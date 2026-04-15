#!/bin/bash
# migrate-users-to-auth-service.sh
#
# Exports users from Central's app_users table and imports them into
# auth-service via its admin API. Passwords stay as SHA256 (hex:salt format)
# — auth-service will re-hash to Argon2id on each user's first login.
#
# Prerequisites:
#   - auth-service running and reachable
#   - Admin JWT token for auth-service
#   - psql available
#
# Usage:
#   export AUTH_SERVICE_URL=http://192.168.56.10:30081
#   export AUTH_ADMIN_TOKEN=<jwt>
#   ./scripts/migrate-users-to-auth-service.sh

set -euo pipefail

CENTRAL_DSN="${CENTRAL_DSN:-postgresql://central:central@192.168.56.201:5432/central}"
AUTH_URL="${AUTH_SERVICE_URL:-http://192.168.56.10:30081}"
TENANT_ID="00000000-0000-0000-0000-000000000001"

echo "=== Central → Auth-Service User Migration ==="
echo "Central DB: $CENTRAL_DSN"
echo "Auth-Service: $AUTH_URL"
echo ""

# Apply migration columns
echo "Step 1: Adding migration tracking columns..."
psql "$CENTRAL_DSN" -c "
  ALTER TABLE app_users ADD COLUMN IF NOT EXISTS auth_migrated boolean DEFAULT false;
  ALTER TABLE app_users ADD COLUMN IF NOT EXISTS auth_service_id uuid;
  COMMENT ON COLUMN app_users.password_hash IS 'DEPRECATED — auth handled by auth-service';
  COMMENT ON COLUMN app_users.salt IS 'DEPRECATED — auth handled by auth-service';
"

# Count users to migrate
TOTAL=$(psql "$CENTRAL_DSN" -t -A -c "SELECT count(*) FROM app_users WHERE auth_migrated = false")
echo "Step 2: Found $TOTAL users to migrate"

if [ "$TOTAL" -eq 0 ]; then
  echo "All users already migrated. Done."
  exit 0
fi

# Export users as JSON
echo "Step 3: Exporting users..."
USERS_JSON=$(psql "$CENTRAL_DSN" -t -A -c "
  SELECT json_agg(json_build_object(
    'username', username,
    'email', COALESCE(email, username || '@central.local'),
    'display_name', COALESCE(display_name, username),
    'role', role_name,
    'password_hash', COALESCE(password_hash, '') || ':' || COALESCE(salt, ''),
    'is_active', is_active,
    'department', COALESCE(department, ''),
    'title', COALESCE(title, '')
  )) FROM app_users WHERE auth_migrated = false;
")

echo "Step 4: Importing into auth-service..."
RESPONSE=$(curl -s -w "\n%{http_code}" \
  -X POST "$AUTH_URL/api/v1/admin/import-users" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-ID: $TENANT_ID" \
  -H "Authorization: Bearer ${AUTH_ADMIN_TOKEN:-}" \
  -d "{\"tenant_id\": \"$TENANT_ID\", \"users\": $USERS_JSON}")

HTTP_CODE=$(echo "$RESPONSE" | tail -1)
BODY=$(echo "$RESPONSE" | head -n -1)

if [ "$HTTP_CODE" = "200" ] || [ "$HTTP_CODE" = "201" ]; then
  echo "Import successful: $BODY"

  # Mark all as migrated
  echo "Step 5: Marking users as migrated..."
  psql "$CENTRAL_DSN" -c "UPDATE app_users SET auth_migrated = true WHERE auth_migrated = false"

  REMAINING=$(psql "$CENTRAL_DSN" -t -A -c "SELECT count(*) FROM app_users WHERE auth_migrated = false")
  echo ""
  echo "=== Migration Complete ==="
  echo "Migrated: $TOTAL users"
  echo "Remaining: $REMAINING"

  if [ "$REMAINING" -eq 0 ]; then
    echo ""
    echo "All users migrated! Local auth can now be disabled."
    echo "The auth-service will re-hash SHA256→Argon2id on each user's next login."
  fi
else
  echo "ERROR: Import failed (HTTP $HTTP_CODE)"
  echo "$BODY"
  echo ""
  echo "If auth-service doesn't have /api/v1/admin/import-users yet,"
  echo "you can manually insert users into secure_auth.users table."
  exit 1
fi
