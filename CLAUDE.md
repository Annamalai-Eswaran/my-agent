# CLAUDE.md — Instructions for Claude

> This file is read by Claude (Anthropic) and GitHub Copilot cloud agent.
> It contains project-specific guidance so Claude can immediately contribute to this repository.

---

## Project Summary

This is **my-agent**: an AI-powered software engineering agent built in **.NET 8 (C#)** that automates the daily developer workflow using Azure DevOps and MCP servers. The runtime LLM is **Claude Opus 4.6 via `Anthropic.SDK`**.

See `AGENTS.md` for the full architecture and component guide.

---

## Critical Conventions — Always Follow These

### 1. Branch Naming
```
feature/ae/{work_item_id}-{slugified-description}
```
- Always create branches from `develop`.
- Example: `feature/ae/1234-add-login-button`
- Use `BranchUtils.MakeBranchName(id, title)` in C# code.

### 2. PR Titles (to Azure DevOps)
```
[AB#{work_item_id}] {PR title}
```
- Example: `[AB#1234] Add login button to navigation`
- Target branch: `develop`
- Always link the work item via `WorkItemRefs` on `GitPullRequest`.

### 3. Use `ask_human` When Unsure
- **If you are unsure about requirements, scope, or approach — use `ask_human`.**
- Do not guess or make assumptions about business logic.
- Present options and let the human decide.
- Use `ask_human` before any destructive or irreversible action.

---

## Available MCP Tools

### Azure DevOps MCP Server (`MyAgent.McpServer.AzureDevOps`)
| Tool | Purpose |
|------|---------|
| `list-ready-work-items` | WIQL query to fetch work items in "Ready" state |
| `get-work-item` | Fetch a single work item by ID with all fields |
| `update-work-item-state` | Transition work item state (e.g. Ready → Active → Done) |
| `list-repositories` | List all Git repositories in the ADO project |
| `create-branch` | Create a new branch from `develop` |
| `list-branches` | List branches in a repository |
| `create-pull-request` | Create a PR with title, description, work item link |
| `get-pull-request` | Fetch PR details by ID |
| `list-pull-requests` | List PRs with optional status filter |

### Filesystem MCP Server
Standard file operations: read, write, list directory, search.

### Terminal MCP Server
Run shell commands: `git`, `dotnet`, `bash`, etc.

### Built-in Tools (not MCP)
| Tool | Purpose |
|------|---------|
| `ask_human` | Prompt the human for input via colored terminal I/O |

---

## Workflow Reminder

1. **List work items** in "Ready" state using `list-ready-work-items`.
2. **Ask the human** which item to work on (or auto-select).
3. **Create branch**: `feature/ae/{id}-{slug}` from `develop`.
4. **Implement** using filesystem + terminal tools.
5. **Commit and push** via terminal.
6. **Create PR** in Azure DevOps: `[AB#{id}] {title}` → `develop`.
7. **Update work item** state to "In Progress" / "Done".
8. **Notify human** via `ask_human`.

---

## Code Standards

### C# Files (`**/*.cs`)
- net8.0, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`.
- Records for models, classes for services.
- `async`/`await` for I/O, `ILogger<T>` for logging.
- Constructor injection, no service locator pattern.

### MCP Tool Files (`**/Tools/**`)
- `GetDefinitions()` returns `IEnumerable<ToolDefinition>`.
- Each handler is a static `async Task<JsonObject>` method.
- Always catch exceptions, return `{ "error": "..." }` JsonObject.

### Anthropic.SDK
- Use `Common.Tool` (not `Messaging.Tool`).
- `ToolResultContent.ToolUseId` (not `.Id`); `.Content` is a `string`.
- `MessageParameters.SystemMessage` is a `string`.

---

## Build Commands

```bash
# Build everything
dotnet build MyAgent.slnx

# Run tests
dotnet test MyAgent.slnx

# Start the agent
dotnet run --project src/MyAgent.Orchestrator
# or:
./scripts/start.sh
```

---

## Key Files to Read First

1. `AGENTS.md` — full architecture and workflow guide
2. `src/MyAgent.Orchestrator/Configuration/appsettings.json` — agent configuration
3. `src/MyAgent.Orchestrator/Agent/EngineerAgent.cs` — main agentic loop
4. `src/MyAgent.Orchestrator/Agent/SystemPrompts.cs` — LLM system prompt
5. `src/MyAgent.McpServer.AzureDevOps/Program.cs` — MCP server entry point

---

## Note on This Project's Runtime

This agent uses Claude Opus 4.6 via `Anthropic.SDK` at runtime. If you are Claude reading this: you are both the tool being used *and* being asked to help build/maintain the tool. Keep that context in mind when making changes.
