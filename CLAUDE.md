# CLAUDE.md — Instructions for Claude

> This file is read by Claude (Anthropic) and GitHub Copilot cloud agent.
> It contains project-specific guidance so Claude can immediately contribute to this repository.

---

## Project Summary

This is **my-agent**: an AI-powered software engineering agent that automates the daily developer workflow using Azure DevOps and MCP servers. The runtime LLM is **Claude Opus 4.6 via GitHub Copilot**.

See `AGENTS.md` for the full architecture and component guide.

---

## Critical Conventions — Always Follow These

### 1. Branch Naming
```
feature/ae/{work_item_id}-{slugified-description}
```
- Always create branches from `develop`.
- Example: `feature/ae/1234-add-login-button`

### 2. PR Titles (to Azure DevOps)
```
[AB#{work_item_id}] {PR title}
```
- Example: `[AB#1234] Add login button to navigation`
- Target branch: `develop`
- Always link the work item in the PR description.

### 3. Use `ask_human` When Unsure
- **If you are unsure about requirements, scope, or approach — use `ask_human`.**
- Do not guess or make assumptions about business logic.
- Present options and let the human decide.
- Use `ask_human` before any destructive or irreversible action.

---

## Available MCP Tools

### Azure DevOps MCP Server
| Tool | Purpose |
|------|---------|
| `list_work_items` | WIQL query to fetch work items (filter by state, type, iteration) |
| `get_work_item` | Fetch a single work item by ID with all fields |
| `update_work_item` | Update fields or transition state of a work item |
| `list_repositories` | List all Git repositories in the ADO project |
| `create_branch` | Create a new branch from a source ref |
| `list_branches` | List branches in a repository |
| `create_pull_request` | Create a PR with title, description, work item links |
| `get_pull_request` | Fetch PR details by ID |
| `list_pull_requests` | List PRs with optional filters |

### Filesystem MCP Server
Standard file operations: read, write, list directory, search.

### Terminal MCP Server
Run shell commands: `git`, `npm`, `python`, `bash`, etc.

### Figma MCP Server (optional)
Design file access — only available if `FIGMA_ACCESS_TOKEN` is set and `"enabled": true` in `config.json`.

### Built-in Tools (not MCP)
| Tool | Purpose |
|------|---------|
| `ask_human` | Prompt the human for input and return their response |

---

## Workflow Reminder

1. **List work items** in "Ready" state using WIQL.
2. **Ask the human** which item to work on (or auto-select).
3. **Create branch**: `feature/ae/{id}-{slug}` from `develop`.
4. **Implement** using filesystem + terminal tools.
5. **Commit and push** via terminal.
6. **Create PR** in Azure DevOps: `[AB#{id}] {title}` → `develop`.
7. **Update work item** state to "In Progress" / "Done".
8. **Notify human** via `ask_human`.

---

## Code Standards

### Python Files (`**/*.py`)
- PEP 8, type hints, async/await, specific exception handling, `python-dotenv`.
- MCP calls need timeout handling (`asyncio.timeout(30)`).

### TypeScript Files (`**/*.ts`)
- Strict mode, ES2022, Zod for input validation, `@modelcontextprotocol/sdk` patterns.
- Return structured JSON from tool handlers.

### MCP Tool Files (`mcp-servers/**/tools/**`)
- Clear LLM-readable descriptions, Zod schemas with `.describe()`, structured JSON responses.

---

## Build Commands

```bash
# Build MCP server
cd mcp-servers/azure-devops && npm install && npm run build

# Install Python deps
cd agent && pip install -r requirements.txt

# Start everything
./scripts/start.sh
```

---

## Key Files to Read First

1. `AGENTS.md` — full architecture and workflow guide
2. `config.json` — agent configuration
3. `agent/main.py` — orchestrator entry point
4. `agent/prompts.py` — system prompt
5. `mcp-servers/azure-devops/src/index.ts` — MCP server entry point

---

## Note on This Project's Runtime

This agent itself uses Claude Opus 4.6 via the GitHub Copilot API at runtime. If you are Claude reading this: you are both the tool being used *and* being asked to help build/maintain the tool. Keep that context in mind when making changes.
