#!/bin/bash
# Stop MinIO server
#
# Usage: ./dev/minio-stop.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MINIO_DIR="$SCRIPT_DIR/minio"
MINIO_PID_FILE="$MINIO_DIR/minio.pid"

echo "========================================="
echo "Stopping MinIO Server"
echo "========================================="

if [ ! -f "$MINIO_PID_FILE" ]; then
    echo "MinIO is not running (no PID file found)"
    exit 0
fi

PID=$(cat "$MINIO_PID_FILE")

if ps -p "$PID" > /dev/null 2>&1; then
    echo "Stopping MinIO (PID: $PID)..."
    kill "$PID"

    # Wait for process to stop
    for i in {1..10}; do
        if ! ps -p "$PID" > /dev/null 2>&1; then
            echo "✓ MinIO stopped successfully"
            rm "$MINIO_PID_FILE"
            exit 0
        fi
        sleep 1
    done

    # Force kill if still running
    echo "Force stopping MinIO..."
    kill -9 "$PID" 2>/dev/null || true
    rm "$MINIO_PID_FILE"
    echo "✓ MinIO stopped (force)"
else
    echo "MinIO process not found (stale PID file)"
    rm "$MINIO_PID_FILE"
fi

echo "========================================="
echo "MinIO stopped"
echo "========================================="
