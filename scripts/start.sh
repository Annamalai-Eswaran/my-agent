#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Load .env if present
if [ -f "$ROOT_DIR/.env" ]; then
  set -o allexport
  # shellcheck source=/dev/null
  source "$ROOT_DIR/.env"
  set +o allexport
fi

cd "$ROOT_DIR"
python3 agent/main.py
