#!/usr/bin/env bash
set -e

echo "=== Setting up AI Engineering Agent (.NET) ==="

if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK not found. Install from https://dotnet.microsoft.com/download"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "Found .NET SDK: $DOTNET_VERSION"

if [ ! -f ".env" ]; then
    echo "Creating .env from .env.example..."
    cp .env.example .env
    echo "Please fill in your credentials in .env"
fi

echo "Restoring dependencies..."
dotnet restore MyAgent.slnx

echo "Building solution..."
dotnet build MyAgent.slnx --configuration Release

echo ""
echo "=== Setup complete! ==="
echo "Run: ./scripts/start.sh"
