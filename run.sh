#!/usr/bin/env bash
# Start SwitchBuilder web app
# Usage: ./run.sh [port]

PORT="${1:-7472}"
export SWITCHBUILDER_DSN="postgresql://switchbuilder:switchbuilder@localhost:5432/switchbuilder"

# Ensure the Podman pod is running
if ! podman exec switchbuilder-postgres pg_isready -U switchbuilder -q 2>/dev/null; then
    echo "Starting Podman pod..."
    podman pod start switchbuilder 2>/dev/null || true
    sleep 2
fi

cd "$(dirname "$0")"
echo "SwitchBuilder → http://127.0.0.1:${PORT}"
python -m uvicorn web.app:app \
    --host 127.0.0.1 \
    --port "$PORT" \
    --reload
