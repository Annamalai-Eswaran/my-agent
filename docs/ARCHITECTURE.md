# Architecture

This document describes the architecture of **my-agent**, an AI-powered software engineering agent built in **.NET 8 (C#)**.

---

## System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Developer (Human)                          │
│                     Interacts via ask_human tool                    │
└───────────────────────────────┬─────────────────────────────────────┘
                                │ stdin / stdout
┌───────────────────────────────▼─────────────────────────────────────┐
│              src/MyAgent.Orchestrator/Program.cs                    │
│                   .NET 8 Orchestrator (C#)                          │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                    Agentic Loop (EngineerAgent)              │   │
│  │  1. Build messages (system prompt + history)                 │   │
│  │  2. Call Claude Opus 4.6 via Anthropic.SDK                   │   │
│  │  3. Parse tool calls from LLM response                       │   │
│  │  4. Dispatch tool calls (MCP or HumanLoop)                   │   │
│  │  5. Append results to history → goto 2                       │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                     │
│  ┌─────────────────────┐  ┌─────────────────────────────────────┐   │
│  │   HumanLoop.cs      │  │       McpClientManager.cs           │   │
│  │  Built-in tools:    │  │  Stdio JSON-RPC 2.0 client:         │   │
│  │  - ask_human        │  │  - Starts MCP server processes      │   │
│  └─────────────────────┘  │  - Routes tool calls to servers     │   │
│                           │  - Handles server lifecycle         │   │
│                           └──────────────┬──────────────────────┘   │
└──────────────────────────────────────────┼──────────────────────────┘
                                           │ MCP protocol (stdio JSON-RPC)
                 ┌─────────────────────────┼─────────────────────┐
                 │                         │                     │
    ┌────────────▼────────┐  ┌─────────────▼────┐  ┌────────────▼──────┐
    │  Azure DevOps MCP   │  │  Filesystem MCP  │  │  Terminal MCP     │
    │  (C# custom)        │  │  (community)     │  │  (community)      │
    │                     │  │                  │  │                   │
    │  Tools:             │  │  Tools:          │  │  Tools:           │
    │  - list-ready-…     │  │  - read_file     │  │  - run_command    │
    │  - get-work-item    │  │  - write_file    │  │  - run_script     │
    │  - update-…-state   │  │  - list_dir      │  │                   │
    │  - create-branch    │  │  - search_files  │  └───────────────────┘
    │  - create-pull-req  │  └──────────────────┘
    │  - list-repos       │
    └─────────────────────┘
                 │
    ┌────────────▼────────────────────────────────┐
    │         Azure DevOps REST API               │
    │         https://dev.azure.com/NAF-Tech/     │
    │         Project: NAF Marketing              │
    └─────────────────────────────────────────────┘
```

---

## Component Descriptions

### .NET Orchestrator (`src/MyAgent.Orchestrator/`)

The orchestrator is the brain of the agent. It:

1. **Loads configuration** from `appsettings.json` and `.env`.
2. **Starts MCP servers** defined in `appsettings.json` as child processes.
3. **Discovers available tools** by querying each MCP server's tool list.
4. **Runs the agentic loop**: sends messages to the LLM and executes tool calls until the task is complete.

| File | Responsibility |
|------|---------------|
| `Program.cs` | Entry point, DI setup, cancellation |
| `Agent/EngineerAgent.cs` | Core agentic loop with Anthropic.SDK |
| `Agent/HumanLoop.cs` | `ask_human` built-in tool |
| `Agent/SystemPrompts.cs` | LLM system prompt constants |
| `Mcp/McpClientManager.cs` | MCP server lifecycle and tool dispatch |
| `Utils/BranchUtils.cs` | Branch name slugification |
| `Configuration/appsettings.json` | All non-secret configuration |

### Azure DevOps MCP Server (`src/MyAgent.McpServer.AzureDevOps/`)

A C# stdio JSON-RPC MCP server that wraps the Azure DevOps REST API using `Microsoft.TeamFoundationServer.Client`.

| File | Responsibility |
|------|---------------|
| `Program.cs` | Stdio JSON-RPC dispatcher loop |
| `AzureDevOpsClient.cs` | VssConnection + PAT auth |
| `Tools/WorkItemTools.cs` | Work item CRUD and WIQL queries |
| `Tools/BranchTools.cs` | Repository and branch management |
| `Tools/PullRequestTools.cs` | PR creation and management |
| `Tools/RepositoryTools.cs` | Repository listing |

### Shared Models (`src/MyAgent.Common/`)

C# record types shared across projects: `WorkItem`, `Repository`, `PullRequest`. Also holds shared `Constants`.

### Configuration (`appsettings.json`)

Central configuration file. Defines:
- LLM provider and model
- Azure DevOps organization URL and project
- MCP server definitions (command, args, env, enabled flag)
- Branching and PR conventions

---

## Data Flow Diagrams

### Work Item Implementation Flow

```
User: "Work on the next task"
         │
         ▼
[list-ready-work-items] ──► ADO API ──► Returns: [{id:1234, title:"...", state:"Ready"}]
         │
         ▼
[ask_human] ──► "Which task? 1. AB#1234 - Fix login" ──► User: "1"
         │
         ▼
[create-branch] ──► ADO Git API ──► Creates: feature/ae/1234-fix-login
         │
         ▼
[read_file] + [write_file] + [run_command] ──► Implementation
         │
         ▼
[run_command: git add/commit/push] ──► Branch pushed to ADO
         │
         ▼
[create-pull-request] ──► ADO PR API ──► PR: "[AB#1234] Fix login"
         │
         ▼
[update-work-item-state] ──► ADO API ──► State: "In Progress"
         │
         ▼
[ask_human] ──► "PR #42 is ready for review at <url>"
```

---

## MCP Protocol Explanation

The Model Context Protocol (MCP) is a standard for tool-calling between LLMs and external services.

### How It Works

1. **Server** (e.g., Azure DevOps MCP) runs as a child process and communicates via **stdio** (stdin/stdout).
2. **Client** (the .NET orchestrator) spawns the server and sends JSON-RPC 2.0 messages.
3. **Tool Discovery**: Client sends `tools/list` → server responds with all available tools and their schemas.
4. **Tool Call**: Client sends `tools/call` with tool name and arguments → server executes and returns result.
5. **LLM Integration**: The orchestrator includes the tool list in the LLM's context. When the LLM wants to call a tool, it returns a structured tool call. The orchestrator executes it via MCP and feeds the result back to the LLM.

### Transport

This project uses **stdio transport**: the server process reads from stdin and writes to stdout. Each message is a newline-delimited JSON-RPC 2.0 object. The `McpClientManager` uses a `SemaphoreSlim(1,1)` to serialize requests over the shared stdio pipe.

---

## LLM Integration Details

- **SDK**: `Anthropic.SDK` (NuGet)
- **Model**: `claude-opus-4.6`
- **Tool Format**: Anthropic tool use format — `stop_reason: "tool_use"` with `ToolUseContent` blocks
- **Context Window**: Up to 200K tokens

---

## Configuration System

```
src/MyAgent.Orchestrator/Configuration/appsettings.json   ← Non-secret config (committed)
.env                                                       ← Secrets only (git-ignored)
.env.example                                               ← Template (committed)
```
