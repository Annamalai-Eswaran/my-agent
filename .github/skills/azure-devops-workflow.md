# Skill: Azure DevOps Workflow

This skill describes the complete Azure DevOps integration workflow used by the agent.

---

## Overview

The agent integrates with Azure DevOps (ADO) at `https://dev.azure.com/NAF-Tech/`, project `NAF Marketing`, to manage the full software delivery lifecycle:
- Query work items
- Create feature branches
- Raise pull requests
- Transition work item states

The Azure DevOps MCP server is implemented in C# (`src/MyAgent.McpServer.AzureDevOps/`) using `Microsoft.TeamFoundationServer.Client`.

---

## Step-by-Step Complete Workflow

### Step 1: Query Work Items

Use the `list-ready-work-items` MCP tool (WIQL query is built in):

```json
{ "project": "NAF Marketing" }
```

Response:
```json
{
  "workItems": [
    { "id": 1234, "title": "Fix login redirect", "state": "Ready", "workItemType": "User Story" }
  ],
  "count": 1
}
```

**Common state values**: `New`, `Ready`, `Active`, `In Progress`, `Resolved`, `Done`, `Closed`

### Step 2: Select a Work Item

Present the list to the human:
```
ask_human: "Here are the Ready work items:
1. AB#1234 - Fix login redirect
2. AB#1235 - Add dashboard widget

Which would you like to work on? (enter the number)"
```

### Step 3: Create Feature Branch

Call `create-branch` MCP tool:
```json
{
  "repositoryId": "my-repo",
  "newBranchName": "feature/ae/1234-fix-login-redirect",
  "sourceBranch": "develop",
  "project": "NAF Marketing"
}
```

Branch name is generated using `BranchUtils.MakeBranchName(1234, "Fix login redirect")` → `feature/ae/1234-fix-login-redirect`.

### Step 4: Implement Changes

Use filesystem and terminal MCP tools to write code. The branch is now checked out locally.

### Step 5: Commit and Push

Via the terminal MCP:
```bash
git add .
git commit -m "[AB#1234] Fix login redirect"
git push origin feature/ae/1234-fix-login-redirect
```

### Step 6: Create Pull Request

Call `create-pull-request` MCP tool:
```json
{
  "repositoryId": "my-repo",
  "sourceBranch": "feature/ae/1234-fix-login-redirect",
  "targetBranch": "develop",
  "title": "[AB#1234] Fix login redirect",
  "description": "Fixes the login page redirect issue as described in work item AB#1234.",
  "workItemId": 1234,
  "project": "NAF Marketing"
}
```

### Step 7: Update Work Item State

Call `update-work-item-state` MCP tool:
```json
{ "id": 1234, "state": "In Progress" }
```

### Step 8: Notify Human

```
ask_human: "PR #42 has been raised: [AB#1234] Fix login redirect. It's ready for your review."
```

---

## Branch Naming Convention

```
feature/ae/{work_item_id}-{slugified-description}
```

| Input | Output |
|-------|--------|
| ID: 1234, Title: "Fix Login Page" | `feature/ae/1234-fix-login-page` |
| ID: 42, Title: "Add User Auth!" | `feature/ae/42-add-user-auth` |
| ID: 100, Title: "   spaces  " | `feature/ae/100-spaces` |

Rules:
- Lowercase
- Non-alphanumeric → `-`
- Collapse multiple `-` → single `-`
- Trim leading/trailing `-`
- Max 50 chars for the slug portion

Use `BranchUtils.MakeBranchName(id, title)` in C# code.

---

## PR Title Convention

```
[AB#{work_item_id}] {title}
```

Examples:
- `[AB#1234] Fix login page redirect`
- `[AB#42] Add dark mode toggle`

---

## Azure DevOps API Notes (.NET SDK)

### Work Items
- `WorkItemTrackingHttpClient.QueryByWiqlAsync(wiql)` — run WIQL query
- `WorkItemTrackingHttpClient.GetWorkItemsAsync(ids, fields)` — batch fetch
- `WorkItemTrackingHttpClient.UpdateWorkItemAsync(patchDoc, id)` — update fields
- `WorkItem.Fields` is `IDictionary<string, object>` — use `TryGetValue` not `GetValueOrDefault`

### Git
- **IMPORTANT**: `GitHttpClient` methods put `project` as the **first** parameter.
- `GetRefsAsync(project, repositoryId, filter)` — list refs/branches
- `UpdateRefsAsync(refUpdates, project, repositoryId)` — create/delete branches
- `CreatePullRequestAsync(pr, repositoryId, project)` — create PR

### Authentication
```csharp
var credentials = new VssBasicCredential(string.Empty, pat);
var connection = new VssConnection(new Uri(orgUrl), credentials);
var gitClient = connection.GetClient<GitHttpClient>();
```
