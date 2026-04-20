# AGENTS.md — AI Agent Instructions

> This file is read by Copilot cloud agents, Claude, Gemini, and other AI coding agents.
> It provides complete context about this repository so agents can make high-quality contributions immediately.

---

## Project Overview

**my-agent** is an AI-powered software engineering agent that automates the daily developer workflow. Given a set of Azure DevOps work items, the agent:

1. Queries work items in a "Ready" state from Azure DevOps (`https://dev.azure.com/NAF-Tech/`, project `NAF Marketing`).
2. Lets the human (or autonomously) pick a task.
3. Creates a feature branch in Azure DevOps Git: `feature/ae/{work_item_id}-{slugified-title}` from `develop`.
4. Implements the required changes using available tools (filesystem, terminal, etc.).
5. Raises a pull request to Azure DevOps targeting `develop` with the title `[AB#{id}] {title}`.
6. Transitions the work item state (Ready → Active → In Progress → Done).

The agent uses **GitHub Copilot with Claude Opus 4.6** as the LLM backbone and communicates with all capabilities via the **Model Context Protocol (MCP)**.

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    agent/main.py                        │
│              (Python Orchestrator / LLM Loop)           │
│                                                         │
│  1. Load system prompt + tools list                     │
│  2. Call Claude Opus 4.6 via GitHub Copilot API         │
│  3. Execute tool calls returned by LLM                  │
│  4. Feed results back → repeat until task complete      │
└───────────────────┬─────────────────────────────────────┘
                    │ MCP protocol (stdio / SSE)
        ┌───────────┼──────────────┬────────────────┐
        ▼           ▼              ▼                ▼
  ┌──────────┐ ┌─────────┐  ┌──────────┐  ┌─────────────┐
  │  Azure   │ │  File   │  │ Terminal │  │    Figma    │
  │  DevOps  │ │ System  │  │   MCP    │  │    MCP      │
  │   MCP    │ │   MCP   │  │ (shell)  │  │ (optional)  │
  └──────────┘ └─────────┘  └──────────┘  └─────────────┘
  TypeScript    community     community      community
  (custom)
```

### Components

| Component | Location | Language | Purpose |
|-----------|----------|----------|---------|
| Orchestrator | `agent/main.py` | Python | Agentic loop, LLM calls, tool dispatch |
| MCP Client Manager | `agent/mcp_client.py` | Python | Connects to and manages all MCP servers |
| Built-in Tools | `agent/tools.py` | Python | `ask_human` and other non-MCP tools |
| System Prompt | `agent/prompts.py` | Python | LLM system prompt and templates |
| Azure DevOps MCP | `mcp-servers/azure-devops/` | TypeScript | Work items, Git branches, PRs via ADO API |
| Config | `config.json` | JSON | Single source of truth for all configuration |

---

## Key Workflow (Step by Step)

1. **Startup**: `python agent/main.py` launches the orchestrator and connects to all MCP servers defined in `config.json`.
2. **Fetch Work Items**: Agent calls `list_work_items` MCP tool with a WIQL query to get items in "Ready" state.
3. **Pick Task**: Agent presents the list to the human via `ask_human`, or auto-selects based on priority.
4. **Create Branch**: Agent calls `create_branch` MCP tool → creates `feature/ae/{id}-{slug}` from `develop` in Azure DevOps Git.
5. **Implement**: Agent uses filesystem and terminal MCP tools to make code changes.
6. **Commit & Push**: Agent uses terminal MCP to `git add`, `git commit`, and `git push`.
7. **Raise PR**: Agent calls `create_pull_request` MCP tool → PR raised to Azure DevOps targeting `develop`, title `[AB#{id}] {title}`, work item linked.
8. **Update Work Item**: Agent calls `update_work_item` to transition state to "In Progress" or "Done".
9. **Human Review**: Agent calls `ask_human` to notify the human the PR is ready.

---

## How to Add a New MCP Server

### Option A: Community / Existing MCP Server

1. Find the MCP server package (e.g., `@modelcontextprotocol/server-filesystem`).
2. Add an entry to `config.json` under `mcpServers`:
   ```json
   "my-new-server": {
     "command": "npx",
     "args": ["-y", "@some-org/mcp-server-name"],
     "enabled": true,
     "env": {}
   }
   ```
3. Restart the agent — tools from the new server are automatically discovered.

### Option B: Custom MCP Server

1. Create a new directory: `mcp-servers/my-server/`
2. Scaffold with `npm init` and install `@modelcontextprotocol/sdk`.
3. Create `src/index.ts` — see `mcp-servers/azure-devops/src/index.ts` as a template.
4. Add tool files under `src/tools/`.
5. Build: `npm run build`.
6. Add to `config.json` as above, pointing `args` to the compiled `dist/index.js`.

---

## How to Add a New Tool to the Azure DevOps MCP Server

1. Open the relevant file in `mcp-servers/azure-devops/src/tools/` (or create a new one).
2. Add a new `server.tool(...)` call inside the `register*Tools` function.
3. Define a Zod schema with `.describe()` on every field.
4. Implement the handler using the Azure DevOps REST client from `client.ts`.
5. Return a structured JSON response.
6. Rebuild: `cd mcp-servers/azure-devops && npm run build`.

---

## Important Conventions

### Branch Naming
```
feature/ae/{work_item_id}-{slugified-description}
```
- Example: `feature/ae/1234-add-user-authentication`
- Always branch from `develop`.
- Use lowercase and hyphens in the slug.

### PR Titles
```
[AB#{work_item_id}] {PR title}
```
- Example: `[AB#1234] Add user authentication`
- PRs target the `develop` branch in Azure DevOps.
- Always link the work item in the PR description.

### `ask_human` Tool
- **Always use `ask_human`** when you are unsure about scope, requirements, or approach.
- Use it to present choices to the human (e.g., which work item to implement).
- Use it to confirm before destructive actions (e.g., deleting files, pushing to protected branches).
- The human's response is fed back into the LLM context.

---

## File-by-File Guide

### `agent/main.py`
The main entry point. Initializes the MCP client manager, loads configuration, builds the system prompt, and runs the agentic tool-calling loop. The loop sends messages to Claude Opus 4.6 via the GitHub Copilot API and executes any tool calls the LLM returns.

### `agent/mcp_client.py`
Manages connections to all MCP servers defined in `config.json`. Provides a unified `call_tool(server, tool, args)` interface and handles server lifecycle (start, restart, stop).

### `agent/tools.py`
Built-in tools that are not provided by any MCP server. Currently includes `ask_human` (prompts the user via stdin/stdout and returns their response).

### `agent/prompts.py`
Contains the system prompt template and any other LLM message templates. The system prompt describes the agent's role, available tools, and key conventions.

### `mcp-servers/azure-devops/src/index.ts`
MCP server entry point. Registers all Azure DevOps tools by calling the `register*Tools` functions and starts the MCP server on stdio transport.

### `mcp-servers/azure-devops/src/client.ts`
Azure DevOps REST client helpers. Provides a `getConnection()` function that returns an authenticated `WebApi` instance using the `AZURE_DEVOPS_PAT` environment variable.

### `mcp-servers/azure-devops/src/tools/work-items.ts`
MCP tools for Azure DevOps work items: `list_work_items` (WIQL query), `get_work_item`, `update_work_item`.

### `mcp-servers/azure-devops/src/tools/git.ts`
MCP tools for Azure DevOps Git: `list_repositories`, `create_branch`, `list_branches`.

### `mcp-servers/azure-devops/src/tools/pull-requests.ts`
MCP tools for Azure DevOps pull requests: `create_pull_request`, `get_pull_request`, `list_pull_requests`.

### `config.json`
Single source of truth for all configuration. Defines MCP servers, Azure DevOps organization URL, project name, and other settings. Use `_ENV` suffix for secret references.

### `scripts/start.sh`
Starts all components: builds the TypeScript MCP server if needed, then runs `python agent/main.py`.

### `scripts/setup.sh`
First-time setup: installs Python dependencies, installs Node.js dependencies, builds the MCP server, and validates the `.env` file.

---

## Common Tasks

### Run the agent
```bash
./scripts/start.sh
# or
python agent/main.py
```

### Add a new ADO work item query filter
Edit `agent/prompts.py` to change the WIQL query used when fetching work items.

### Change the LLM model
Edit `config.json` — find the `model` key under the `llm` section.

### Enable the Figma MCP server
Set `"enabled": true` for the `figma` entry in `config.json` and ensure `FIGMA_ACCESS_TOKEN` is in your `.env`.

### Debug MCP server connections
Set `LOG_LEVEL=debug` in your `.env` and restart. MCP connection logs will appear in the console.

---

## Environment Setup

```bash
# 1. Copy env template
cp .env.example .env

# 2. Fill in your Azure DevOps PAT
# AZURE_DEVOPS_PAT=your_pat_here

# 3. Run setup
./scripts/setup.sh

# 4. Start the agent
./scripts/start.sh
```
