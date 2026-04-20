# Architecture

This document describes the architecture of **my-agent**, an AI-powered software engineering agent.

---

## System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Developer (Human)                          │
│                     Interacts via ask_human tool                    │
└───────────────────────────────┬─────────────────────────────────────┘
                                │ stdin / stdout
┌───────────────────────────────▼─────────────────────────────────────┐
│                        agent/main.py                                │
│                   Python Orchestrator                               │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                    Agentic Loop                              │   │
│  │  1. Build messages (system prompt + history)                 │   │
│  │  2. Call Claude Opus 4.6 via GitHub Copilot API              │   │
│  │  3. Parse tool calls from LLM response                       │   │
│  │  4. Dispatch tool calls (MCP or built-in)                    │   │
│  │  5. Append results to history → goto 2                       │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                     │
│  ┌─────────────────────┐  ┌─────────────────────────────────────┐   │
│  │   agent/tools.py    │  │       agent/mcp_client.py           │   │
│  │  Built-in tools:    │  │  MCP Client Manager:                │   │
│  │  - ask_human        │  │  - Starts MCP server processes      │   │
│  └─────────────────────┘  │  - Routes tool calls to servers     │   │
│                           │  - Handles server lifecycle         │   │
│                           └──────────────┬──────────────────────┘   │
└──────────────────────────────────────────┼──────────────────────────┘
                                           │ MCP protocol (stdio)
                 ┌─────────────────────────┼─────────────────────┐
                 │                         │                     │
    ┌────────────▼────────┐  ┌─────────────▼────┐  ┌────────────▼──────┐
    │  Azure DevOps MCP   │  │  Filesystem MCP  │  │  Terminal MCP     │
    │  (TypeScript)       │  │  (community)     │  │  (community)      │
    │                     │  │                  │  │                   │
    │  Tools:             │  │  Tools:          │  │  Tools:           │
    │  - list_work_items  │  │  - read_file     │  │  - run_command    │
    │  - get_work_item    │  │  - write_file    │  │  - run_script     │
    │  - update_work_item │  │  - list_dir      │  │                   │
    │  - create_branch    │  │  - search_files  │  └───────────────────┘
    │  - create_pull_req  │  └──────────────────┘
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

### Python Orchestrator (`agent/`)

The orchestrator is the brain of the agent. It:

1. **Loads configuration** from `config.json` and `.env`.
2. **Starts MCP servers** defined in `config.json` as child processes.
3. **Discovers available tools** by querying each MCP server's tool list.
4. **Runs the agentic loop**: sends messages to the LLM and executes tool calls until the task is complete.

| File | Responsibility |
|------|---------------|
| `main.py` | Entry point, loop runner, LLM client |
| `mcp_client.py` | MCP server lifecycle and tool dispatch |
| `tools.py` | Built-in tools (ask_human) |
| `prompts.py` | System prompt and message templates |
| `config.py` | Config loading and validation |
| `requirements.txt` | Python dependencies |

### Azure DevOps MCP Server (`mcp-servers/azure-devops/`)

A TypeScript MCP server that wraps the Azure DevOps REST API. Built with `@modelcontextprotocol/sdk` and uses the `azure-devops-node-api` client library.

| File | Responsibility |
|------|---------------|
| `src/index.ts` | Server entry point, tool registration |
| `src/client.ts` | Azure DevOps REST client, authentication |
| `src/tools/work-items.ts` | Work item CRUD and WIQL queries |
| `src/tools/git.ts` | Repository and branch management |
| `src/tools/pull-requests.ts` | PR creation and management |

### Configuration (`config.json`)

Central configuration file. Defines:
- LLM provider, model, and parameters
- Azure DevOps organization URL and project
- MCP server definitions (command, args, env, enabled flag)
- Agent behavior settings (timeouts, max tool calls)

---

## Data Flow Diagrams

### Work Item Implementation Flow

```
User: "Work on the next task"
         │
         ▼
[list_work_items] ──► ADO API ──► Returns: [{id:1234, title:"...", state:"Ready"}]
         │
         ▼
[ask_human] ──► "Which task? 1. AB#1234 - Fix login" ──► User: "1"
         │
         ▼
[create_branch] ──► ADO Git API ──► Creates: feature/ae/1234-fix-login
         │
         ▼
[read_file] + [write_file] + [run_command] ──► Implementation
         │
         ▼
[run_command: git add/commit/push] ──► Branch pushed to ADO
         │
         ▼
[create_pull_request] ──► ADO PR API ──► PR: "[AB#1234] Fix login"
         │
         ▼
[update_work_item] ──► ADO API ──► State: "In Progress"
         │
         ▼
[ask_human] ──► "PR #42 is ready for review at <url>"
```

---

## MCP Protocol Explanation

The Model Context Protocol (MCP) is a standard for tool-calling between LLMs and external services.

### How It Works

1. **Server** (e.g., Azure DevOps MCP) runs as a child process and communicates via **stdio** (stdin/stdout).
2. **Client** (the Python orchestrator) spawns the server and sends JSON-RPC messages.
3. **Tool Discovery**: Client sends `tools/list` → server responds with all available tools and their schemas.
4. **Tool Call**: Client sends `tools/call` with tool name and arguments → server executes and returns result.
5. **LLM Integration**: The orchestrator includes the tool list in the LLM's context. When the LLM wants to call a tool, it returns a structured tool call. The orchestrator executes it via MCP and feeds the result back to the LLM.

### Transport

This project uses **stdio transport**: the server process reads from stdin and writes to stdout. Each message is a newline-delimited JSON-RPC object.

---

## LLM Integration Details

- **Provider**: GitHub Copilot API
- **Model**: Claude Opus 4.6 (`claude-opus-4-6`)
- **API Style**: OpenAI-compatible chat completions with tool/function calling
- **Tool Format**: Anthropic tool use format (type: `tool_use` / `tool_result`)
- **Context Window**: Up to 200K tokens
- **Temperature**: 0.1 (low, for consistent/predictable behavior)

---

## Configuration System

```
config.json              ← Non-secret configuration (committed)
.env                     ← Secrets only (git-ignored)
.env.example             ← Template with placeholder values (committed)
```

The `_ENV` suffix pattern is used in `config.json` to reference environment variables:
```json
{ "patEnv": "AZURE_DEVOPS_PAT" }
```
This means: at runtime, read the value from the `AZURE_DEVOPS_PAT` environment variable.
