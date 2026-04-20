# Workflow Guide

This document describes the complete daily workflow for using **my-agent** to automate software development tasks with Azure DevOps.

---

## Overview

The agent automates the following workflow:

```
ADO Work Items (Ready) → Pick Task → Create Branch → Implement → Raise PR → Update State
```

All steps are performed by the AI agent with human oversight via the `ask_human` tool.

---

## Daily Workflow Walkthrough

### 1. Start the Agent

```bash
# Ensure your .env is configured
cat .env  # Should contain ANTHROPIC_API_KEY and AZURE_DEVOPS_PAT

# Start the agent
./scripts/start.sh
# or
dotnet run --project src/MyAgent.Orchestrator
```

The agent will:
1. Load configuration from `appsettings.json` and `.env`.
2. Start all enabled MCP servers defined in `appsettings.json`.
3. Print available tools.
4. Await your first message.

### 2. Ask for Work Items

The agent automatically starts by calling `list-ready-work-items`, which runs a WIQL query to fetch all work items assigned to you in "Ready" state in the NAF Marketing project.

### 3. Pick a Work Item

The agent calls `ask_human` to present the list and ask which item to work on:

```
❓  Agent asks: Here are your Ready work items:
    1. AB#1234 - Fix login page redirect
    2. AB#1235 - Add dark mode toggle
    Which would you like to work on?
👤  Your answer: 1
```

### 4. Create Feature Branch

The agent calls `create-branch`:
- Repository: (asks via `ask_human`)
- New branch: `feature/ae/1234-fix-login-page-redirect`
- From: `develop`

### 5. Implement Changes

The agent uses filesystem and terminal MCP tools to:
- Read existing files
- Write code changes
- Run tests / build checks

### 6. Commit & Push

Via the terminal MCP:
```bash
git add .
git commit -m "[AB#1234] Fix login page redirect"
git push origin feature/ae/1234-fix-login-page-redirect
```

### 7. Raise Pull Request

The agent calls `create-pull-request`:
- Title: `[AB#1234] Fix login page redirect`
- Source: `feature/ae/1234-fix-login-page-redirect`
- Target: `develop`
- Work item linked: `1234`

### 8. Update Work Item State

The agent calls `update-work-item-state` to set state to "In Progress" or "Done".

### 9. Human Review

The agent calls `ask_human` to notify you the PR is ready for review.

---

## Configuration

All non-secret configuration is in `src/MyAgent.Orchestrator/Configuration/appsettings.json`.

Secrets go in `.env` (never committed):
```env
ANTHROPIC_API_KEY=sk-ant-...
AZURE_DEVOPS_PAT=your_pat_here
```

---

## Troubleshooting

### Agent can't connect to Azure DevOps
- Check `AZURE_DEVOPS_PAT` is set in `.env`
- Verify the PAT has work items + Git read/write permissions
- Check `OrgUrl` in `appsettings.json` is correct

### MCP server won't start
- Check the `Command` and `Args` in `appsettings.json`
- Set `"Enabled": false` to disable a server for debugging
- Check stderr output (MCP servers log to stderr)

### Build fails
```bash
dotnet restore MyAgent.slnx
dotnet build MyAgent.slnx
```
