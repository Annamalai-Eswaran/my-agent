# GitHub Copilot Instructions

## Project Summary

This repository contains an **AI-powered software engineering agent** that automates the daily developer workflow. It integrates with:

- **Azure DevOps** (`https://dev.azure.com/NAF-Tech/`, project `NAF Marketing`) via MCP servers for work item management, branch creation, and pull request automation.
- **GitHub Copilot with Claude Opus 4.6** as the LLM backbone for all AI reasoning and code generation.

The agent follows a loop: fetch ADO work items → pick a task → create a feature branch → implement code → raise a PR back to Azure DevOps.

---

## Repository Structure

```
my-agent/
├── agent/                        # Python orchestrator
│   ├── main.py                   # Entry point – agentic tool-calling loop
│   ├── mcp_client.py             # MCP client manager (connects to all MCP servers)
│   ├── tools.py                  # Built-in tools (ask_human, etc.)
│   ├── prompts.py                # System prompt and message templates
│   └── requirements.txt          # Python dependencies
│
├── mcp-servers/
│   └── azure-devops/             # TypeScript MCP server for Azure DevOps
│       ├── src/
│       │   ├── index.ts          # MCP server entry point
│       │   ├── client.ts         # Azure DevOps REST client helpers
│       │   └── tools/            # Individual tool modules
│       │       ├── work-items.ts
│       │       ├── git.ts
│       │       └── pull-requests.ts
│       ├── package.json
│       └── tsconfig.json
│
├── scripts/
│   ├── start.sh                  # Start everything (MCP server + agent)
│   └── setup.sh                  # First-time environment setup
│
├── config.json                   # Single source of truth for agent configuration
├── .env                          # Environment variables (never committed)
├── AGENTS.md                     # Agent-readable instructions (root)
├── CLAUDE.md                     # Claude-specific instructions (root)
└── docs/                         # Detailed documentation
```

---

## Build Instructions

### MCP Server (TypeScript)
```bash
cd mcp-servers/azure-devops
npm install
npm run build
```

### Agent (Python)
```bash
cd agent
pip install -r requirements.txt
```

### Start Everything
```bash
./scripts/start.sh
# or directly:
python agent/main.py
```

---

## Code Standards

### Python
- Follow **PEP 8** for all Python code.
- Use **type hints** on all function signatures.
- Use **async/await** patterns for all I/O operations.
- Use **dataclasses** or **Pydantic models** for structured data.
- Catch specific exceptions and log with context (not bare `except:`).
- Use `python-dotenv` for environment variable loading.
- All MCP client calls must have **timeout handling**.

### TypeScript
- **Strict mode** enabled (`"strict": true` in `tsconfig.json`).
- Target **ES2022**.
- Use **Zod** for all MCP tool input validation schemas.
- Use the `@modelcontextprotocol/sdk` patterns for tool registration.
- All Azure DevOps API calls go through the `client.ts` connection helpers.
- Export tool registration as `register*Tools(server: McpServer)` functions.
- Handle API errors gracefully with meaningful, human-readable error messages.
- Use **ES module syntax** (`import`/`export`).

### MCP Tools
- Every tool must have a **clear, concise description** understandable by an LLM.
- Input schemas must use **Zod** with descriptive field descriptions.
- Tools should return **structured JSON** in the text content of the response.
- Error responses must be **human-readable**.
- Group related tools in the same file under a shared `register*Tools` function.

---

## Branching Convention

- All feature branches are created from `develop`.
- Branch naming: `feature/ae/{work_item_id}-{slugified-description}`
  - Example: `feature/ae/1234-fix-login-page`

---

## PR Convention

- PRs are raised to **Azure DevOps** targeting the `develop` branch.
- PR title format: `[AB#{work_item_id}] {title}`
  - Example: `[AB#1234] Fix login page redirect`
- Link the relevant work item in the PR description.

---

## Testing

- **Python**: Unit tests with `pytest`. Test files in `agent/tests/`.
- **TypeScript**: Unit tests with `vitest` or `jest`. Test files alongside source files (`*.test.ts`).
- Run Python tests: `cd agent && pytest`
- Run TypeScript tests: `cd mcp-servers/azure-devops && npm test`

---

## Environment Variables

Create a `.env` file in the root (never commit it):

```env
# Required
AZURE_DEVOPS_PAT=your_personal_access_token_here

# Optional
FIGMA_ACCESS_TOKEN=your_figma_token_here
```

The `config.json` file is the single source of truth for all non-secret configuration. Use the `_ENV` suffix pattern to reference environment variables (e.g., `"AZURE_DEVOPS_PAT_ENV"` means read from the `AZURE_DEVOPS_PAT` env var at runtime).
