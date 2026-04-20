# Skill: Azure DevOps Workflow

This skill describes the complete Azure DevOps integration workflow used by the agent.

---

## Overview

The agent integrates with Azure DevOps (ADO) at `https://dev.azure.com/NAF-Tech/`, project `NAF Marketing`, to manage the full software delivery lifecycle:
- Query work items
- Create feature branches
- Raise pull requests
- Transition work item states

---

## Step-by-Step Complete Workflow

### Step 1: Query Work Items

Use the `list_work_items` MCP tool with a WIQL query:

```sql
SELECT [System.Id], [System.Title], [System.State], [System.AssignedTo], [System.WorkItemType]
FROM WorkItems
WHERE [System.TeamProject] = 'NAF Marketing'
  AND [System.State] = 'Ready'
  AND [System.WorkItemType] IN ('User Story', 'Task', 'Bug')
ORDER BY [Microsoft.VSTS.Common.Priority] ASC, [System.CreatedDate] ASC
```

**Common state values**: `New`, `Ready`, `Active`, `In Progress`, `Resolved`, `Done`, `Closed`

### Step 2: Select a Work Item

Present the list to the human:
```
ask_human: "Here are the Ready work items:\n1. [AB#1234] Fix login redirect\n2. [AB#1235] Add dashboard widget\n\nWhich would you like to work on? (enter the number)"
```

### Step 3: Create Feature Branch

Call `create_branch` MCP tool:
```json
{
  "repositoryId": "your-repo-id-or-name",
  "branchName": "feature/ae/1234-fix-login-redirect",
  "sourceBranch": "develop"
}
```

**Branch naming rule**: `feature/ae/{work_item_id}-{slugified-title}`
- Slugify: lowercase, spaces → hyphens, remove special chars
- Example: "Fix login redirect (urgent!)" → `fix-login-redirect-urgent`

### Step 4: Implement Changes

Use filesystem and terminal MCP tools to:
1. `git checkout feature/ae/1234-fix-login-redirect`
2. Make code changes
3. `git add .`
4. `git commit -m "[AB#1234] Fix login redirect"`
5. `git push origin feature/ae/1234-fix-login-redirect`

### Step 5: Create Pull Request

Call `create_pull_request` MCP tool:
```json
{
  "repositoryId": "your-repo-id-or-name",
  "title": "[AB#1234] Fix login redirect",
  "description": "## Summary\n\nFixes the login redirect issue.\n\n## Work Item\nAB#1234\n\n## Changes\n- Updated auth middleware\n- Fixed redirect URL construction",
  "sourceBranch": "feature/ae/1234-fix-login-redirect",
  "targetBranch": "develop",
  "workItemIds": [1234]
}
```

### Step 6: Update Work Item State

Call `update_work_item` MCP tool:
```json
{
  "id": 1234,
  "fields": {
    "System.State": "In Progress"
  }
}
```

---

## WIQL Query Patterns

### All Ready User Stories
```sql
SELECT [System.Id], [System.Title], [System.State]
FROM WorkItems
WHERE [System.TeamProject] = 'NAF Marketing'
  AND [System.WorkItemType] = 'User Story'
  AND [System.State] = 'Ready'
ORDER BY [Microsoft.VSTS.Common.Priority] ASC
```

### Work Items Assigned to Me
```sql
SELECT [System.Id], [System.Title], [System.State]
FROM WorkItems
WHERE [System.TeamProject] = 'NAF Marketing'
  AND [System.AssignedTo] = @Me
  AND [System.State] NOT IN ('Done', 'Closed')
```

### Work Items in Current Sprint
```sql
SELECT [System.Id], [System.Title], [System.State]
FROM WorkItems
WHERE [System.TeamProject] = 'NAF Marketing'
  AND [System.IterationPath] UNDER @CurrentIteration('[NAF Marketing]\Team')
  AND [System.State] NOT IN ('Done', 'Closed')
```

### High Priority Bugs
```sql
SELECT [System.Id], [System.Title], [System.State], [Microsoft.VSTS.Common.Priority]
FROM WorkItems
WHERE [System.TeamProject] = 'NAF Marketing'
  AND [System.WorkItemType] = 'Bug'
  AND [Microsoft.VSTS.Common.Priority] <= 2
  AND [System.State] NOT IN ('Done', 'Closed')
ORDER BY [Microsoft.VSTS.Common.Priority] ASC
```

---

## State Transitions

```
New → Ready → Active → In Progress → Resolved → Done
                                              ↘ Closed
```

| From | To | When |
|------|----|------|
| `Ready` | `Active` | Work item is picked up |
| `Active` | `In Progress` | Branch created, implementation started |
| `In Progress` | `Resolved` | PR raised, awaiting review |
| `Resolved` | `Done` | PR merged |

---

## How Branch Creation Works via ADO Git Refs API

Azure DevOps creates branches via the Git Refs API. The MCP `create_branch` tool wraps this:

1. Fetch the latest commit SHA from the source branch (`develop`).
2. POST to `/_apis/git/repositories/{repoId}/refs` with:
   ```json
   [
     {
       "name": "refs/heads/feature/ae/1234-fix-login-redirect",
       "oldObjectId": "0000000000000000000000000000000000000000",
       "newObjectId": "{sha-of-develop-HEAD}"
     }
   ]
   ```

---

## PR Work Item Linking

When creating a PR via the ADO REST API, link work items using the `workItemRefs` array:
```json
{
  "title": "[AB#1234] Fix login redirect",
  "sourceRefName": "refs/heads/feature/ae/1234-fix-login-redirect",
  "targetRefName": "refs/heads/develop",
  "workItemRefs": [
    { "id": "1234" }
  ]
}
```

---

## Error Handling Patterns

| Error | Cause | Recovery |
|-------|-------|---------|
| `401 Unauthorized` | Invalid or expired PAT | Ask human to update `AZURE_DEVOPS_PAT` in `.env` |
| `404 Not Found` | Wrong project/repo name | Verify `config.json` project and repo names |
| `409 Conflict` (branch) | Branch already exists | Use existing branch or ask human |
| `TF400898` | Work item not found | Check the work item ID |
| Rate limit (`429`) | Too many requests | Wait and retry with exponential backoff |

---

## Authentication

All ADO API calls use a **Personal Access Token (PAT)** with the following scopes:
- `Work Items: Read & Write`
- `Code: Read & Write` (for Git operations)
- `Pull Request Threads: Read & Write`

The PAT is loaded from the `AZURE_DEVOPS_PAT` environment variable at runtime.
