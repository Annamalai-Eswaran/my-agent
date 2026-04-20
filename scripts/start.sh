#!/usr/bin/env bash
set -e

if [ -f ".env" ]; then
    set -a
    source .env
    set +a
fi

echo "Starting AI Engineering Agent..."
dotnet run --project src/MyAgent.Orchestrator --configuration Release
