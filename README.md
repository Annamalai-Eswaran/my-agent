# My Agent — AI Engineering Agent (.NET 8)

An AI-powered software engineering agent that automates the daily developer workflow using Azure DevOps and the Model Context Protocol (MCP), built with .NET 8 C#.

## Architecture

```
MyAgent.sln
├── src/
│   ├── MyAgent.Common/              # Shared models and constants
│   ├── MyAgent.Orchestrator/        # Main agent loop (LLM + MCP client)
│   └── MyAgent.McpServer.AzureDevOps/  # Azure DevOps MCP server (stdio JSON-RPC)
└── tests/
    ├── MyAgent.Orchestrator.Tests/
    └── MyAgent.McpServer.Tests/
```

## What it does

1. Queries Azure DevOps for work items in "Ready" state
2. Lets the human pick a task via `ask_human`
3. Creates a feature branch: `feature/ae/{id}-{slug}` from `develop`
4. Implements code changes using filesystem + terminal MCP tools
5. Raises a PR to Azure DevOps targeting `develop`
6. Updates the work item state

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- An `ANTHROPIC_API_KEY` environment variable (or GitHub Copilot token)
- An Azure DevOps PAT with work item + Git read/write permissions

## Setup

```bash
# Copy and fill in credentials
cp .env.example .env

# Restore and build
./scripts/setup.sh
```

## Run

```bash
./scripts/start.sh
# or directly:
dotnet run --project src/MyAgent.Orchestrator
```

## Configuration

All non-secret configuration is in `src/MyAgent.Orchestrator/Configuration/appsettings.json`.

Secrets go in `.env` (never committed):

```env
ANTHROPIC_API_KEY=your_key_here
AZURE_DEVOPS_PAT=your_pat_here
```

## Tests

```bash
dotnet test MyAgent.slnx
```

## Branching Convention

- Branch format: `feature/ae/{work_item_id}-{slugified-description}`
- PR title format: `[AB#{work_item_id}] {title}`
- Target branch: `develop`

## MCP Servers

| Server | Transport | Purpose |
|--------|-----------|---------|
| `MyAgent.McpServer.AzureDevOps` | stdio | Work items, branches, PRs |
| `@modelcontextprotocol/server-filesystem` | stdio (npx) | File read/write |
| `@simonwilson/mcp-server-commands` | stdio (npx) | Shell commands |
