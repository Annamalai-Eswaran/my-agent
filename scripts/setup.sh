#!/usr/bin/env bash
set -euo pipefail

BOLD="\033[1m"
GREEN="\033[92m"
RED="\033[91m"
YELLOW="\033[93m"
RESET="\033[0m"

info()    { echo -e "${GREEN}✔  $*${RESET}"; }
warn()    { echo -e "${YELLOW}⚠  $*${RESET}"; }
error()   { echo -e "${RED}✘  $*${RESET}" >&2; exit 1; }
header()  { echo -e "\n${BOLD}$*${RESET}"; }

# ── Prerequisites ────────────────────────────────────────────────────────────

header "Checking prerequisites…"

command -v node  >/dev/null 2>&1 || error "Node.js is not installed. Please install Node.js 18+."
command -v npm   >/dev/null 2>&1 || error "npm is not installed."
command -v python3 >/dev/null 2>&1 || error "Python 3 is not installed. Please install Python 3.11+."
command -v pip   >/dev/null 2>&1 || command -v pip3 >/dev/null 2>&1 || error "pip is not installed."

NODE_VERSION=$(node --version | sed 's/v//')
NODE_MAJOR=$(echo "$NODE_VERSION" | cut -d. -f1)
if [ "$NODE_MAJOR" -lt 18 ]; then
  error "Node.js 18+ is required (found $NODE_VERSION)."
fi

info "Node.js $(node --version)"
info "npm $(npm --version)"
info "Python $(python3 --version)"

# ── Environment file ─────────────────────────────────────────────────────────

header "Setting up environment…"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

if [ ! -f "$ROOT_DIR/.env" ]; then
  cp "$ROOT_DIR/.env.example" "$ROOT_DIR/.env"
  warn ".env created from .env.example — please fill in your credentials before running the agent."
else
  info ".env already exists — skipping copy."
fi

# ── Azure DevOps MCP server ───────────────────────────────────────────────────

header "Building Azure DevOps MCP server…"

cd "$ROOT_DIR/mcp-servers/azure-devops"
npm install
npm run build
info "Azure DevOps MCP server built successfully."

# ── Python agent dependencies ─────────────────────────────────────────────────

header "Installing Python dependencies…"

cd "$ROOT_DIR/agent"
pip install -r requirements.txt
info "Python dependencies installed."

# ── Done ──────────────────────────────────────────────────────────────────────

echo ""
echo -e "${BOLD}${GREEN}Setup complete! 🎉${RESET}"
echo ""
echo -e "  1. Edit ${BOLD}.env${RESET} with your Azure DevOps PAT and other credentials."
echo -e "  2. Run ${BOLD}./scripts/start.sh${RESET} to launch the agent."
echo ""
