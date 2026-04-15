#!/bin/bash
# Central Platform — Health Check
#
# Verifies every documented service URL is reachable.
# Exit code 0 = all healthy, non-zero = N services down.
#
# Usage:
#   ./scripts/check-services.sh           # Pretty output
#   ./scripts/check-services.sh --quiet   # Only show failures
#   ./scripts/check-services.sh --json    # JSON output for CI

set -uo pipefail

QUIET=false
JSON=false
for arg in "$@"; do
  case "$arg" in
    --quiet) QUIET=true ;;
    --json)  JSON=true ;;
  esac
done

# ── Service endpoints (URL, expected_http_code, name) ──
SERVICES=(
  # Local dev servers
  "http://localhost:4200|200|Angular Web (dev)"
  "http://localhost:7472|401|FastAPI Web (auth=basic, 401 expected)"

  # Gateway + APIs
  "http://192.168.56.203:8000/health|200|API Gateway"
  "http://192.168.56.200:5000/api/health|200|Central API"
  "http://192.168.56.10:30081/health|200|Auth Service"

  # Observability
  "http://192.168.56.10:30909/-/healthy|200|Prometheus"
  "http://192.168.56.210:3000/api/health|200|Grafana"
  "http://192.168.56.10:30686/|200|Jaeger UI"

  # Storage
  "http://192.168.56.10:30901/|200|MinIO Console"
  "http://192.168.56.10:30900/minio/health/live|200|MinIO S3 API"

  # Container registry
  "http://192.168.56.10:30500/v2/|200|Container Registry"
)

# ── TCP-only checks (no HTTP) ──
TCP_CHECKS=(
  "192.168.56.201|5432|PostgreSQL (write)"
  "192.168.56.202|5432|PostgreSQL (read)"
  "192.168.56.10|30432|PostgreSQL (NodePort)"
)

PASS=0
FAIL=0
RESULTS=""

check_http() {
  local url="$1"
  local expected="$2"
  local name="$3"
  local actual
  actual=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 "$url" 2>/dev/null || echo "000")
  if [ "$actual" = "$expected" ] || [ "$actual" = "200" ] || [ "$actual" = "302" ] || [ "$actual" = "301" ]; then
    PASS=$((PASS + 1))
    [ "$QUIET" = "false" ] && [ "$JSON" = "false" ] && printf "  \033[32m✓\033[0m %-40s %s\n" "$name" "$url"
    RESULTS+="{\"name\":\"$name\",\"url\":\"$url\",\"status\":\"ok\",\"http_code\":$actual},"
  else
    FAIL=$((FAIL + 1))
    [ "$JSON" = "false" ] && printf "  \033[31m✗\033[0m %-40s %s (HTTP %s)\n" "$name" "$url" "$actual"
    RESULTS+="{\"name\":\"$name\",\"url\":\"$url\",\"status\":\"fail\",\"http_code\":$actual},"
  fi
}

check_tcp() {
  local host="$1"
  local port="$2"
  local name="$3"
  if timeout 3 bash -c "echo > /dev/tcp/$host/$port" 2>/dev/null; then
    PASS=$((PASS + 1))
    [ "$QUIET" = "false" ] && [ "$JSON" = "false" ] && printf "  \033[32m✓\033[0m %-40s %s:%s\n" "$name" "$host" "$port"
    RESULTS+="{\"name\":\"$name\",\"host\":\"$host\",\"port\":$port,\"status\":\"ok\"},"
  else
    FAIL=$((FAIL + 1))
    [ "$JSON" = "false" ] && printf "  \033[31m✗\033[0m %-40s %s:%s\n" "$name" "$host" "$port"
    RESULTS+="{\"name\":\"$name\",\"host\":\"$host\",\"port\":$port,\"status\":\"fail\"},"
  fi
}

[ "$JSON" = "false" ] && echo "═══════════════════════════════════════════════════════════════"
[ "$JSON" = "false" ] && echo "  Central Platform — Health Check"
[ "$JSON" = "false" ] && echo "═══════════════════════════════════════════════════════════════"

[ "$JSON" = "false" ] && [ "$QUIET" = "false" ] && echo ""
[ "$JSON" = "false" ] && [ "$QUIET" = "false" ] && echo "HTTP/HTTPS endpoints:"
for entry in "${SERVICES[@]}"; do
  IFS='|' read -r url expected name <<< "$entry"
  check_http "$url" "$expected" "$name"
done

[ "$JSON" = "false" ] && [ "$QUIET" = "false" ] && echo ""
[ "$JSON" = "false" ] && [ "$QUIET" = "false" ] && echo "TCP endpoints:"
for entry in "${TCP_CHECKS[@]}"; do
  IFS='|' read -r host port name <<< "$entry"
  check_tcp "$host" "$port" "$name"
done

if [ "$JSON" = "true" ]; then
  RESULTS="${RESULTS%,}"
  echo "{\"pass\":$PASS,\"fail\":$FAIL,\"total\":$((PASS+FAIL)),\"services\":[$RESULTS]}"
else
  echo ""
  echo "═══════════════════════════════════════════════════════════════"
  if [ $FAIL -eq 0 ]; then
    printf "  \033[32m✓ All %d services healthy\033[0m\n" "$PASS"
  else
    printf "  \033[33m%d passing\033[0m, \033[31m%d failed\033[0m\n" "$PASS" "$FAIL"
  fi
  echo "═══════════════════════════════════════════════════════════════"
fi

exit $FAIL
