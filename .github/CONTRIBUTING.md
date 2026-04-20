# Contributing Guide

Thank you for contributing to **my-agent**! This guide covers everything you need to get started.

---

## Development Environment Setup

### Prerequisites

- **Python 3.11+**
- **Node.js 20+** and **npm**
- **Git**
- An **Azure DevOps PAT** with Work Items (Read/Write) and Code (Read/Write) scopes

### First-Time Setup

```bash
# 1. Clone the repository
git clone https://github.com/Annamalai-Eswaran/my-agent.git
cd my-agent

# 2. Copy environment template
cp .env.example .env

# 3. Edit .env and fill in your PAT
# AZURE_DEVOPS_PAT=your_pat_here

# 4. Run the setup script
./scripts/setup.sh

# 5. Verify setup
python agent/main.py --check-config
```

### Manual Setup (if setup.sh is unavailable)

```bash
# Python dependencies
cd agent
python -m venv .venv
source .venv/bin/activate  # Windows: .venv\Scripts\activate
pip install -r requirements.txt
cd ..

# TypeScript MCP server
cd mcp-servers/azure-devops
npm install
npm run build
cd ../..
```

---

## How to Add New MCP Servers

### Option A: Community MCP Server

1. Find an MCP-compatible server (e.g., from the MCP marketplace or npm).
2. Add an entry to `config.json` under `mcpServers`:
   ```json
   "my-server": {
     "command": "npx",
     "args": ["-y", "@some-org/mcp-server-name", "--arg1", "value1"],
     "enabled": true,
     "env": {
       "API_KEY": "${MY_API_KEY}"
     }
   }
   ```
3. Add any required environment variables to `.env.example` (with placeholder values).
4. Update `docs/ADDING_MCP_SERVERS.md` with the new server.
5. Test: restart the agent and verify the new tools appear.

### Option B: Custom MCP Server

See `.github/skills/mcp-development.md` for the complete guide on building a custom MCP server.

Summary:
1. Create `mcp-servers/your-server/` directory.
2. Scaffold with `npm init` + `@modelcontextprotocol/sdk`.
3. Implement tools in `src/tools/`.
4. Build and add to `config.json`.
5. Write tests.

---

## How to Add New Tools

### To the Azure DevOps MCP Server

1. Open `mcp-servers/azure-devops/src/tools/` and find the relevant file (or create a new one).
2. Add a new `server.tool(...)` call inside the `register*Tools` function.
3. Use Zod for the input schema, with `.describe()` on every field.
4. Return structured JSON.
5. Rebuild: `cd mcp-servers/azure-devops && npm run build`.
6. Write a test in `mcp-servers/azure-devops/src/tools/*.test.ts`.

### As a Built-in Python Tool

1. Add your tool function to `agent/tools.py`.
2. Add the tool spec (name, description, JSON schema) to the tool registry.
3. Add the tool to the dispatch table in `agent/main.py`.
4. Write a test in `agent/tests/test_tools.py`.

---

## Code Review Checklist

Before submitting a PR, verify:

### General
- [ ] The code follows the patterns in the existing codebase.
- [ ] No secrets, PAT tokens, or credentials are hardcoded.
- [ ] All new environment variables are documented in `.env.example`.
- [ ] `config.json` is not broken (valid JSON, no secrets).

### Python
- [ ] Type hints on all function signatures.
- [ ] Async/await for all I/O operations.
- [ ] Specific exception handling (no bare `except:`).
- [ ] Tests written for new functionality.
- [ ] `cd agent && pytest` passes.

### TypeScript
- [ ] Zod schema with `.describe()` on every field.
- [ ] Structured JSON returned from all tool handlers.
- [ ] Human-readable error messages on failure.
- [ ] `cd mcp-servers/azure-devops && npm test` passes.
- [ ] `cd mcp-servers/azure-devops && npm run build` succeeds.

### Documentation
- [ ] `AGENTS.md` updated if architecture changed.
- [ ] Relevant skill files updated if workflows changed.
- [ ] `docs/` updated if APIs or configs changed.

---

## PR Conventions

### For Work on This Repository (GitHub)

- Branch from `main`: `feature/{short-description}`
- PR title: descriptive, imperative mood (e.g., `Add Slack MCP server integration`)
- Reference any related issues in the PR description.

### For Work Produced by the Agent (Azure DevOps)

When the agent itself raises PRs for ADO work items:
- Branch from `develop`: `feature/ae/{work_item_id}-{slugified-title}`
- PR title: `[AB#{work_item_id}] {title}`
- Target branch: `develop`
- Work item linked in `workItemRefs`

---

## Running Tests

```bash
# Python tests
cd agent
pytest
pytest --cov=. --cov-report=term-missing  # With coverage

# TypeScript tests
cd mcp-servers/azure-devops
npm test
npm run test:coverage  # With coverage
```

---

## Getting Help

- Read `AGENTS.md` for architecture context.
- Read `.github/skills/` for detailed how-to guides.
- Open a GitHub issue for bugs or feature requests.
- Use `ask_human` in the agent if you're unsure about something at runtime.
