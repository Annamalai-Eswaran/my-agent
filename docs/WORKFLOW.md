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
cat .env  # Should contain AZURE_DEVOPS_PAT

# Start the agent
./scripts/start.sh
# or
python agent/main.py
```

The agent will:
1. Load configuration from `config.json`.
2. Start all enabled MCP servers.
3. Print available tools.
4. Await your first message.

### 2. Ask for Work Items

Type to the agent:
```
Please show me the Ready work items.
```

The agent will query Azure DevOps using WIQL and display results like:
```
Found 3 Ready work items:

1. [AB#1234] Fix login redirect (User Story, Priority 1)
   Assigned to: Annamalai Eswaran
   
2. [AB#1235] Add dashboard export (User Story, Priority 2)
   Assigned to: Unassigned

3. [AB#1236] Update API documentation (Task, Priority 3)
   Assigned to: Annamalai Eswaran

Which would you like to work on? (enter 1, 2, or 3, or "skip" to exit)
```

### 3. Select a Work Item

Type the number of the task you want to work on:
```
1
```

The agent will:
1. Fetch the full work item details (description, acceptance criteria, etc.).
2. Create the feature branch: `feature/ae/1234-fix-login-redirect` from `develop`.
3. Present its implementation plan and ask for confirmation.

### 4. Review and Confirm the Plan

The agent presents:
```
Work Item: [AB#1234] Fix login redirect

Description:
Users are redirected to the wrong page after login when they had a deep link.

Acceptance Criteria:
- User is redirected to the originally requested page after login
- Fallback to dashboard if no redirect URL is present
- Regression tests pass

My Plan:
1. Read the current authentication middleware
2. Identify the redirect logic
3. Fix the redirect URL handling
4. Add/update tests
5. Create PR

Shall I proceed? (yes/no)
```

Type `yes` to proceed.

### 5. Implementation

The agent will:
1. Use the filesystem MCP to read relevant files.
2. Make code changes.
3. Use the terminal MCP to run tests: `pytest`, `npm test`, etc.
4. Ask for clarification if needed via `ask_human`.
5. Iterate until the implementation is complete.

During implementation, you may see messages like:
```
I've updated agent/auth/middleware.py to fix the redirect logic. 
Running tests to verify...

Tests passed! ✓ 12 tests, 0 failures

Ready to commit and push. Confirm? (yes/no)
```

### 6. Commit and Raise PR

After confirmation:
1. Agent commits: `git commit -m "[AB#1234] Fix login redirect"`
2. Agent pushes: `git push origin feature/ae/1234-fix-login-redirect`
3. Agent creates the PR in Azure DevOps:
   - Title: `[AB#1234] Fix login redirect`
   - Target: `develop`
   - Work item linked: `AB#1234`
4. Agent updates work item state to `In Progress`.
5. Agent reports: `PR #42 is ready for review at: https://dev.azure.com/NAF-Tech/...`

---

## Configuration Options

### `config.json` Key Settings

```json
{
  "llm": {
    "model": "claude-opus-4-6",      // LLM model to use
    "maxTokens": 8192,               // Max tokens per response
    "temperature": 0.1               // Lower = more deterministic
  },
  "azureDevOps": {
    "orgUrl": "https://dev.azure.com/NAF-Tech/",
    "project": "NAF Marketing",
    "defaultTargetBranch": "develop"
  },
  "agent": {
    "maxToolCalls": 50,              // Max tool calls per session
    "taskTimeoutMinutes": 30,        // Timeout per task
    "workItemStates": {
      "ready": "Ready",             // State to query for available work
      "inProgress": "In Progress",  // State to set when starting
      "done": "Done"                // State to set on PR raise
    }
  }
}
```

### Enabling/Disabling MCP Servers

In `config.json`, set `"enabled": false` to disable a server without removing it:
```json
"figma": {
  "enabled": false  // Disabled until FIGMA_ACCESS_TOKEN is available
}
```

### Customizing the Work Item Query

Edit `agent/prompts.py` to change the default WIQL query. For example, to only work on bugs:
```python
WORK_ITEM_QUERY = """
SELECT [System.Id], [System.Title], [System.State]
FROM WorkItems
WHERE [System.WorkItemType] = 'Bug'
  AND [System.State] = 'Ready'
ORDER BY [Microsoft.VSTS.Common.Priority] ASC
"""
```

---

## Troubleshooting Common Issues

### Agent can't connect to Azure DevOps

**Symptoms**: `401 Unauthorized` or `Authentication failed`

**Fix**:
1. Check that `AZURE_DEVOPS_PAT` is set in `.env`.
2. Verify the PAT hasn't expired (create a new one in ADO user settings).
3. Ensure the PAT has `Work Items: Read & Write` and `Code: Read & Write` scopes.

### No work items returned

**Symptoms**: Agent says "No work items found"

**Fix**:
1. Verify items exist in ADO with state "Ready" (or your configured state).
2. Check the project name in `config.json` matches exactly (case-sensitive).
3. Try running the WIQL query directly in ADO's query editor.

### Branch already exists

**Symptoms**: `create_branch` fails with "branch already exists"

**Fix**: The agent will ask if you want to use the existing branch. Type `yes` to continue, or manually delete the branch in ADO.

### MCP server won't start

**Symptoms**: "Failed to start MCP server: azure-devops"

**Fix**:
```bash
# Check if the server is built
ls mcp-servers/azure-devops/dist/

# If not, build it
cd mcp-servers/azure-devops && npm run build

# Check for build errors
npm run build 2>&1
```

### Agent seems stuck in a loop

**Symptoms**: Agent keeps calling the same tool repeatedly

**Fix**: Press `Ctrl+C` to interrupt and restart. Consider:
1. Restarting with `./scripts/start.sh`.
2. Checking `LOG_LEVEL=debug` output for the root cause.
3. Simplifying the task or breaking it into smaller steps.

### LLM API rate limits

**Symptoms**: `429 Too Many Requests` errors

**Fix**: The agent automatically retries with backoff. If it persists:
1. Wait 60 seconds and try again.
2. Consider reducing `maxTokens` in `config.json`.
3. Check your GitHub Copilot usage limits.

---

## Advanced Usage

### Running Without Human Interaction

For CI/CD or automated pipelines, set `AUTO_APPROVE=true` in `.env`:
```env
AUTO_APPROVE=true
```

This makes `ask_human` auto-accept all prompts. Use with caution.

### Providing a Specific Work Item ID

Instead of letting the agent query for work items, you can direct it:
```
Please work on work item 1234.
```

### Running Multiple Tasks

After completing a task, the agent can continue to the next:
```
Great, please move on to the next Ready work item.
```

### Asking the Agent to Explain Its Plan First

```
Please review work item 1234 and tell me your implementation plan before making any changes.
```
