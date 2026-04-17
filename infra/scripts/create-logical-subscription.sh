#!/usr/bin/env bash
# =============================================================================
# Create a logical replication subscription on a secondary region
# =============================================================================
# Usage:
#   PRIMARY_URL="postgres://replicator:pw@primary.eu-west-1/central" \
#   SUBSCRIBER_URL="postgres://central:pw@localhost:5432/central" \
#   ./create-logical-subscription.sh eu_west_1_subscriber
# =============================================================================
set -euo pipefail

SUB_NAME="${1:?Usage: $0 <subscription-name>}"
PRIMARY_URL="${PRIMARY_URL:?PRIMARY_URL required}"
SUBSCRIBER_URL="${SUBSCRIBER_URL:?SUBSCRIBER_URL required}"

echo "==> Creating logical subscription '$SUB_NAME'"
psql "$SUBSCRIBER_URL" <<SQL
CREATE SUBSCRIPTION $SUB_NAME
    CONNECTION '$PRIMARY_URL'
    PUBLICATION central_replication_data
    WITH (copy_data = true, enabled = true, create_slot = true);
SQL

echo "==> Subscription '$SUB_NAME' created"
echo "==> Monitor progress: SELECT * FROM pg_stat_subscription_stats;"
