# Skill: Agent Orchestration

This skill explains how the Python orchestrator works and how to extend it.

---

## The Agentic Tool-Calling Loop

The orchestrator in `agent/main.py` runs a continuous loop:

```
┌─────────────────────────────────────────────────────┐
│                     AGENT LOOP                      │
│                                                     │
│  1. Build message history (system + user + history) │
│     │                                               │
│  2. Call LLM (Claude Opus 4.6 via Copilot API)      │
│     │                                               │
│  3. Receive response                                │
│     │                                               │
│  4a. If response has TOOL CALLS:                    │
│     ├── Execute each tool (MCP or built-in)         │
│     ├── Append tool results to message history      │
│     └── Go back to step 2                          │
│     │                                               │
│  4b. If response is FINAL TEXT (no tool calls):     │
│     └── Present to user, end or await next input    │
└─────────────────────────────────────────────────────┘
```

### Key Design Principles

1. **All context is in the message history** — the LLM has no persistent memory other than the conversation.
2. **Tool results are always fed back** — the LLM sees the result of every tool call before deciding what to do next.
3. **The loop continues until the LLM produces a final text response** (no tool calls).
4. **Human input is just another tool** — `ask_human` pauses the loop and waits for user input.

---

## How to Add New Tools

### Option A: Add an MCP Tool (recommended for external services)

See `.github/skills/mcp-development.md` for the full MCP tool creation guide.

After creating and registering the tool in the MCP server and adding the server to `config.json`, the agent automatically discovers and can use the new tool — no changes to the orchestrator needed.

### Option B: Add a Built-in Tool (for local operations)

Built-in tools live in `agent/tools.py`. Use this pattern:

```python
from typing import Any

async def my_built_in_tool(param1: str, param2: int = 0) -> dict[str, Any]:
    """
    Clear description of what this tool does.
    This description is shown to the LLM.
    """
    # Implementation
    result = do_something(param1, param2)
    return {"success": True, "result": result}

# Tool registration metadata (used to build the tools list for the LLM)
MY_TOOL_SPEC = {
    "name": "my_built_in_tool",
    "description": "Clear description of what this tool does.",
    "parameters": {
        "type": "object",
        "properties": {
            "param1": {"type": "string", "description": "Description of param1"},
            "param2": {"type": "integer", "description": "Optional: description of param2"},
        },
        "required": ["param1"],
    },
}
```

Then register it in `agent/main.py` by adding it to the built-in tools list and the dispatch table.

---

## System Prompt Design Principles

The system prompt in `agent/prompts.py` is the most important piece of the agent. Key principles:

### 1. Be Explicit About Role and Goal
```
You are an AI software engineering agent. Your goal is to pick up Azure DevOps work items and implement them fully, including creating feature branches, writing code, and raising pull requests.
```

### 2. List Available Tools and Their Purposes
Include a brief summary of every tool category so the LLM knows what it can do.

### 3. Embed Conventions
State naming conventions, PR title formats, and branch naming rules directly in the system prompt so the LLM follows them consistently.

### 4. Instruct on `ask_human` Usage
```
If you are unsure about requirements or scope, ALWAYS use ask_human before proceeding.
Never make assumptions about business logic — ask the human.
```

### 5. Keep It Focused
Avoid including too much detail in the system prompt. Long system prompts can dilute the LLM's attention. Use skill files and documentation for detailed references; keep the system prompt action-oriented.

---

## Message History Management

### Structure

The message history is a list of messages in the OpenAI/Anthropic format:

```python
messages = [
    {"role": "user", "content": "Please work on the next Ready work item."},
    {"role": "assistant", "content": [
        {"type": "tool_use", "id": "call_1", "name": "list_work_items", "input": {...}}
    ]},
    {"role": "user", "content": [
        {"type": "tool_result", "tool_use_id": "call_1", "content": "[{...}]"}
    ]},
    # ... continues
]
```

### Preventing Context Window Overflow

For long-running sessions, the message history can grow large. Strategies:

1. **Summarize old history**: After completing a task, summarize the completed work into a single message and truncate the detailed history.
2. **Task boundaries**: Start a fresh message history for each new work item.
3. **Max tokens**: Track token usage and trigger a summary when approaching the context limit.

### Parallel Tool Calls

Claude supports parallel tool calls (multiple tool calls in a single response). Handle them by:

1. Executing all tool calls concurrently using `asyncio.gather`.
2. Appending all tool results in a single `tool_result` message.

```python
tool_results = await asyncio.gather(*[execute_tool(call) for call in tool_calls])
```

---

## Error Recovery Patterns

### Tool Call Failure

When a tool returns an error response, append the error to the message history and let the LLM decide how to recover. The LLM may:
- Retry with different parameters.
- Try an alternative approach.
- Ask the human for help via `ask_human`.

### LLM API Failure

Implement exponential backoff for transient LLM API failures:
```python
for attempt in range(MAX_RETRIES):
    try:
        response = await llm_client.call(messages)
        break
    except RateLimitError:
        await asyncio.sleep(2 ** attempt)
    except AuthenticationError:
        raise  # Non-recoverable, surface to user
```

### Infinite Loop Detection

If the LLM keeps calling the same tool with the same parameters repeatedly, detect this and break the loop:
```python
if tool_call_count > MAX_TOOL_CALLS:
    ask_human("The agent seems stuck. Please provide guidance.")
    break
```

---

## Human-in-the-Loop Integration

### `ask_human` Tool

The `ask_human` tool is the primary human-in-the-loop mechanism. It:
1. Prints the question/options to stdout.
2. Waits for the user to type a response (via stdin).
3. Returns the user's response as the tool result.
4. The LLM reads the response and continues.

### When to Use `ask_human`

- **Always** when picking which work item to implement.
- **Always** when the requirements are ambiguous or incomplete.
- **Always** before destructive actions (deleting files, force-pushing).
- **Optionally** to confirm the implementation plan before writing code.

### Non-Interactive Mode

For fully automated pipelines, `ask_human` can be configured to read from a pre-supplied answers file instead of stdin. Set `AUTO_APPROVE=true` in `.env` to auto-accept all `ask_human` prompts (use with caution).

---

## Configuration System

The agent reads its configuration from `config.json`. Key sections:

```json
{
  "llm": {
    "provider": "github-copilot",
    "model": "claude-opus-4-6",
    "maxTokens": 8192,
    "temperature": 0.1
  },
  "azureDevOps": {
    "orgUrl": "https://dev.azure.com/NAF-Tech/",
    "project": "NAF Marketing",
    "patEnv": "AZURE_DEVOPS_PAT"
  },
  "mcpServers": {
    "azure-devops": { ... },
    "filesystem": { ... },
    "terminal": { ... }
  },
  "agent": {
    "maxToolCalls": 50,
    "taskTimeoutMinutes": 30,
    "askHumanAutoApprove": false
  }
}
```

Access config in Python:
```python
from agent.config import get_config
config = get_config()
org_url = config["azureDevOps"]["orgUrl"]
```
