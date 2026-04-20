# AI Engineering Agent

A local developer agent that automates your daily software engineering workflow. It uses **GitHub Copilot with Claude Opus 4.6** as the LLM and integrates with **Azure DevOps** and other services via MCP (Model Context Protocol) servers.

---

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│                  AI Engineering Agent                     │
│                  (Python Orchestrator)                    │
│                                                          │
│  ┌────────────────────┐   ┌──────────────────────────┐   │
│  │  GitHub Copilot    │   │   Human-in-the-Loop      │   │
│  │  Claude Opus 4.6   │   │   ask_human() prompt     │   │
│  └────────┬───────────┘   └──────────────────────────┘   │
│           │                                              │
│  ┌────────▼─────────────────────────────────────────┐    │
│  │              MCP Client Layer                    │    │
│  └──┬──────────┬──────────┬──────────┬──────────────┘    │
└─────┼──────────┼──────────┼──────────┼────────────────────┘
      │          │          │          │
      ▼          ▼          ▼          ▼
  ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐
  │ Azure  │ │  File  │ │ Termi- │ │ Figma  │
  │DevOps  │ │ System │ │  nal   │ │ (opt.) │
  │  MCP   │ │  MCP   │ │  MCP   │ │  MCP   │
  └────────┘ └────────┘ └────────┘ └────────┘
```

---

## Prerequisites

- **Node.js 18+** — for the Azure DevOps MCP server
- **Python 3.11+** — for the agent orchestrator
- **Azure DevOps PAT** — Personal Access Token with work item read/write and code read/write permissions
- **GitHub Copilot access** — with Claude Opus 4.6 model enabled in your organisation

---

## Setup Instructions

### 1. Clone the repository

```bash
git clone https://github.com/Annamalai-Eswaran/my-agent.git
cd my-agent
```

### 2. Run the setup script

```bash
chmod +x scripts/setup.sh
./scripts/setup.sh
```

This will:
- Check prerequisites (Node.js, npm, Python, pip)
- Copy `.env.example` to `.env`
- Install and build the Azure DevOps MCP server
- Install Python dependencies for the agent

### 3. Configure your environment

Edit `.env` and fill in your credentials:

```bash
AZURE_DEVOPS_PAT=your-azure-devops-personal-access-token
FIGMA_ACCESS_TOKEN=your-figma-token-optional   # optional
REPOS_BASE_PATH=/home/user/repos
```

---

## Configuration

All agent behaviour is controlled by `agent/config.json`.

### LLM Configuration

```json
"llm": {
  "provider": "copilot",
  "model": "claude-opus-4.6"
}
```

### Azure DevOps Configuration

```json
"azure_devops": {
  "org_url": "https://dev.azure.com/NAF-Tech/",
  "default_project": "NAF Marketing",
  "pat_env_var": "AZURE_DEVOPS_PAT"
}
```

### Branch & PR Strategy

```json
"branching": {
  "base_branch": "develop",
  "prefix": "feature/ae",
  "pattern": "feature/ae/{work_item_id}-{slugified_description}"
},
"pull_request": {
  "target_branch": "develop",
  "title_pattern": "[AB#{work_item_id}] {work_item_title}",
  "auto_link_work_item": true
}
```

### Adding a New MCP Server

Add an entry under `mcp_servers` in `agent/config.json`:

```json
"my-new-server": {
  "command": "node",
  "args": ["./path/to/server/dist/index.js"],
  "env": {
    "MY_API_KEY_ENV": "MY_API_KEY"
  },
  "enabled": true
}
```

The agent auto-discovers all tools from every enabled MCP server on startup.

---

## Usage

### Start the agent

```bash
./scripts/start.sh
```

### Typical daily workflow

```
Agent: Checking Azure DevOps for ready work items in "NAF Marketing"...

Agent: Here are your assigned work items in Ready state:
       1. #4521 — Fix login timeout (P1, Bug)
       2. #4530 — Update user profile API (P2, User Story)
       Which one would you like to work on?

You:   1

Agent: Got it — working on #4521 "Fix login timeout".
       Which repository should I work in?

You:   backend-api

Agent: Creating branch feature/ae/4521-fix-login-timeout from develop...
       Opening VS Code...
       Reading relevant files...

Agent: [ask_human] The timeout is currently hardcoded to 10s in AuthService.ts L45.
       Should I increase it to 30s or make it configurable via an env var?

You:   Make it configurable via env var

Agent: Implementing changes...
       Raising PR to Azure DevOps: [AB#4521] Fix login timeout
       PR created: https://dev.azure.com/NAF-Tech/NAF Marketing/_git/backend-api/pullrequest/42
```

---

## Troubleshooting

### "No ready work items found"
- Verify your `AZURE_DEVOPS_PAT` has the **Work Items (read)** scope.
- Check that work items in Azure DevOps are assigned to you and in **Ready** state.
- Confirm the project name matches exactly: `NAF Marketing`.

### MCP server fails to start
- Run `cd mcp-servers/azure-devops && npm run build` and check for TypeScript errors.
- Ensure Node.js 18+ is installed: `node --version`.

### Python import errors
- Activate the virtual environment: `source agent/.venv/bin/activate`
- Reinstall dependencies: `pip install -r agent/requirements.txt`

### Agent stuck in a loop
- The agent will call `ask_human` when it needs guidance — just answer in the terminal.
- Press `Ctrl+C` to stop the agent at any time.
