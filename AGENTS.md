# AGENTS.md — AI Agent Instructions

> This file is read by Copilot cloud agents, Claude, Gemini, and other AI coding agents.
> It provides complete context about this repository so agents can make high-quality contributions immediately.

---

## Project Overview

**my-agent** is an AI-powered software engineering agent built in **.NET 8 (C#)** that automates the daily developer workflow. Given a set of Azure DevOps work items, the agent:

1. Queries work items in a "Ready" state from Azure DevOps (`https://dev.azure.com/NAF-Tech/`, project `NAF Marketing`).
2. Lets the human (or autonomously) pick a task.
3. Creates a feature branch in Azure DevOps Git: `feature/ae/{work_item_id}-{slugified-title}` from `develop`.
4. Implements the required changes using available tools (filesystem, terminal, etc.).
5. Raises a pull request to Azure DevOps targeting `develop` with the title `[AB#{id}] {title}`.
6. Transitions the work item state (Ready → Active → In Progress → Done).

The agent uses **Anthropic Claude Opus 4.6** via `Anthropic.SDK` and communicates with all capabilities via the **Model Context Protocol (MCP)**.

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│           src/MyAgent.Orchestrator/Program.cs           │
│              (.NET 8 Orchestrator / LLM Loop)           │
│                                                         │
│  1. Load config from appsettings.json + .env            │
│  2. Call Claude Opus 4.6 via Anthropic.SDK              │
│  3. Execute tool calls returned by LLM                  │
│  4. Feed results back → repeat until task complete      │
└───────────────────┬─────────────────────────────────────┘
                    │ MCP protocol (stdio JSON-RPC 2.0)
        ┌───────────┼──────────────┬────────────────┐
        ▼           ▼              ▼                ▼
  ┌──────────┐ ┌─────────┐  ┌──────────┐  ┌─────────────┐
  │  Azure   │ │  File   │  │ Terminal │  │  (optional) │
  │  DevOps  │ │ System  │  │   MCP    │  │  Community  │
  │   MCP    │ │   MCP   │  │ (shell)  │  │   Servers   │
  └──────────┘ └─────────┘  └──────────┘  └─────────────┘
  C# (custom)   community     community
```

### Components

| Component | Location | Language | Purpose |
|-----------|----------|----------|---------|
| Orchestrator | `src/MyAgent.Orchestrator/` | C# | Agentic loop, LLM calls, tool dispatch |
| MCP Client Manager | `Mcp/McpClientManager.cs` | C# | Connects to and manages all MCP servers |
| Human Loop | `Agent/HumanLoop.cs` | C# | `ask_human`: colored terminal I/O |
| System Prompt | `Agent/SystemPrompts.cs` | C# | LLM system prompt constants |
| Azure DevOps MCP | `src/MyAgent.McpServer.AzureDevOps/` | C# | Work items, Git branches, PRs via ADO API |
| Config | `Configuration/appsettings.json` | JSON | All non-secret configuration |

---

## Key Workflow (Step by Step)

1. **Startup**: `dotnet run --project src/MyAgent.Orchestrator` launches the orchestrator and connects to all MCP servers defined in `appsettings.json`.
2. **Fetch Work Items**: Agent calls `list-ready-work-items` MCP tool with WIQL to get items in "Ready" state.
3. **Pick Task**: Agent presents the list to the human via `ask_human`, or auto-selects based on priority.
4. **Create Branch**: Agent calls `create-branch` MCP tool → creates `feature/ae/{id}-{slug}` from `develop` in Azure DevOps Git.
5. **Implement**: Agent uses filesystem and terminal MCP tools to make code changes.
6. **Commit & Push**: Agent uses terminal MCP to `git add`, `git commit`, and `git push`.
7. **Raise PR**: Agent calls `create-pull-request` MCP tool → PR raised to Azure DevOps targeting `develop`, title `[AB#{id}] {title}`, work item linked.
8. **Update Work Item**: Agent calls `update-work-item-state` to transition state to "In Progress" or "Done".
9. **Human Review**: Agent calls `ask_human` to notify the human the PR is ready.

---

## How to Add a New MCP Server

### Option A: Community / Existing MCP Server

1. Add an entry to `src/MyAgent.Orchestrator/Configuration/appsettings.json` under `McpServers`:
   ```json
   "my-new-server": {
     "Command": "npx",
     "Args": ["-y", "@some-org/mcp-server-name"],
     "Enabled": true,
     "Env": {}
   }
   ```
2. Restart the agent — tools from the new server are automatically discovered.

### Option B: Custom .NET MCP Server

1. Create a new project: `src/MyAgent.McpServer.MyService/`
2. Use `src/MyAgent.McpServer.AzureDevOps/` as a template.
3. Implement a stdio JSON-RPC loop in `Program.cs` handling `initialize`, `tools/list`, `tools/call`.
4. Add tool handlers in `Tools/`.
5. Reference `MyAgent.Common` for shared models.
6. Add to `appsettings.json` with `"Command": "dotnet", "Args": ["run", "--project", "src/MyAgent.McpServer.MyService"]`.

---

## Important Conventions

### Branch Naming
```
feature/ae/{work_item_id}-{slugified-description}
```
- Example: `feature/ae/1234-add-user-authentication`
- Always branch from `develop`.
- Use `BranchUtils.MakeBranchName(id, title)` to generate the name.

### PR Titles
```
[AB#{work_item_id}] {PR title}
```
- Example: `[AB#1234] Add user authentication`
- PRs target the `develop` branch in Azure DevOps.
- Always link the work item via `WorkItemRefs`.

### `ask_human` Tool
- **Always use `ask_human`** when you are unsure about scope, requirements, or approach.
- Use it to present choices to the human.
- Use it to confirm before destructive actions.

---

## File-by-File Guide

### `src/MyAgent.Orchestrator/Program.cs`
Entry point. Loads `.env`, configures DI with `AgentConfig`, `McpClientManager`, `EngineerAgent`, then calls `agent.RunAsync()`.

### `src/MyAgent.Orchestrator/Agent/EngineerAgent.cs`
The main agentic loop. Calls Anthropic Claude via `Anthropic.SDK`, handles `tool_use` stop reason by dispatching to MCP or `HumanLoop.Ask()`, feeds results back, loops until `end_turn`.

### `src/MyAgent.Orchestrator/Mcp/McpClientManager.cs`
Manages stdio JSON-RPC 2.0 connections to all configured MCP servers. Uses `SemaphoreSlim` for serialized request/response. Discovers tools, routes calls.

### `src/MyAgent.McpServer.AzureDevOps/Program.cs`
MCP server entry point. Reads JSON-RPC from stdin, dispatches to tool handlers, writes responses to stdout. All logging goes to stderr.

### `src/MyAgent.McpServer.AzureDevOps/AzureDevOpsClient.cs`
Creates a `VssConnection` with PAT authentication. Provides `GetWorkItemClient()` and `GetGitClient()`.

### `src/MyAgent.Common/`
Shared C# record models: `WorkItem`, `Repository`, `PullRequest`. Shared constants.

### `src/MyAgent.Orchestrator/Configuration/appsettings.json`
Single source of truth for all non-secret configuration. Defines LLM model, ADO org URL, branching config, and MCP server definitions.

---

## Build & Run

```bash
# Build
dotnet build MyAgent.slnx

# Run tests
dotnet test MyAgent.slnx

# Start agent
dotnet run --project src/MyAgent.Orchestrator
```

## Environment Setup

```bash
cp .env.example .env
# Fill in: ANTHROPIC_API_KEY, AZURE_DEVOPS_PAT
./scripts/setup.sh
./scripts/start.sh
```
