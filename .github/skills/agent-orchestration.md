# Skill: Agent Orchestration (.NET)

This skill explains how the .NET orchestrator works and how to extend it.

---

## The Agentic Tool-Calling Loop

The orchestrator in `src/MyAgent.Orchestrator/Agent/EngineerAgent.cs` runs a continuous loop:

```
┌─────────────────────────────────────────────────────┐
│                     AGENT LOOP                      │
│                                                     │
│  1. Build message history (system + user + history) │
│     │                                               │
│  2. Call LLM (Claude Opus 4.6 via Anthropic.SDK)    │
│     │                                               │
│  3. Receive response                                │
│     │                                               │
│  4a. If stop_reason == "tool_use":                  │
│     ├── Execute each tool (MCP or HumanLoop)        │
│     ├── Append ToolResultContent to message history │
│     └── Go back to step 2                          │
│     │                                               │
│  4b. If stop_reason == "end_turn":                  │
│     └── Print text, await user input or quit        │
└─────────────────────────────────────────────────────┘
```

### Key Design Principles

1. **All context is in the message history** — the LLM has no persistent memory other than the conversation.
2. **Tool results are always fed back** — the LLM sees the result of every tool call before deciding what to do next.
3. **The loop continues until `end_turn`** — the LLM produces a final text response with no tool calls.
4. **Human input is just another tool** — `ask_human` pauses the loop and waits for user input.

---

## How to Add New Tools

### Option A: Add an MCP Tool (recommended for external services)

See `docs/ADDING_MCP_SERVERS.md` for the full MCP server creation guide.

After creating and registering the tool in the MCP server and adding the server to `appsettings.json`, the agent automatically discovers and can use the new tool — no changes to the orchestrator needed.

### Option B: Add a Built-in Tool (for local operations)

Built-in tools live in `src/MyAgent.Orchestrator/Agent/HumanLoop.cs`. Use this pattern:

```csharp
// In HumanLoop.cs or a new BuiltInTools.cs
public static string MyBuiltInTool(string param1, int param2 = 0)
{
    // Implementation
    return JsonSerializer.Serialize(new { success = true, result = $"Done: {param1}" });
}

// Tool definition for the LLM
public static McpToolDefinition GetMyToolDefinition() => new()
{
    Name = "my_built_in_tool",
    Description = "Clear description of what this tool does.",
    InputSchema = new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["param1"] = new JsonObject { ["type"] = "string", ["description"] = "Description." }
        },
        ["required"] = new JsonArray("param1")
    }
};
```

Then in `EngineerAgent.RunAsync()`:
1. Add the tool definition to the `tools` list.
2. Add a dispatch case in the `tool_use` handler.

---

## System Prompt Design Principles

The system prompt in `Agent/SystemPrompts.cs` is the most important piece of the agent. Key principles:

### 1. Be Explicit About Role and Goal
```
You are an AI software engineering agent. Your goal is to pick up Azure DevOps work items and
implement them fully, including creating feature branches, writing code, and raising pull requests.
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

---

## Message History Management

### Structure

```csharp
var messages = new List<Message>
{
    new() { Role = RoleType.User, Content = new List<ContentBase>
        { new TextContent { Text = "Please work on the next Ready work item." } } },
    new() { Role = RoleType.Assistant, Content = new List<ContentBase>
        { new ToolUseContent { Id = "call_1", Name = "list-ready-work-items", Input = JsonNode.Parse("{}") } } },
    new() { Role = RoleType.User, Content = new List<ContentBase>
        { new ToolResultContent { ToolUseId = "call_1", Content = "[{...}]" } } },
    // ... continues
};
```

### Preventing Context Window Overflow

For long-running sessions, the message history can grow large. Strategies:
1. **Task boundaries**: Start a fresh message history for each new work item.
2. **Summarize old history**: After completing a task, summarize into a single message.

---

## Error Recovery Patterns

### Tool Call Failure

When a tool returns an error response, append the error to the message history and let the LLM decide how to recover. The LLM may:
- Retry with different parameters.
- Try an alternative approach.
- Ask the human for help via `ask_human`.

### LLM API Failure

The `Anthropic.SDK` throws on API failures. Wrap the call with retry logic:

```csharp
for (int attempt = 0; attempt < maxRetries; attempt++)
{
    try
    {
        var response = await client.Messages.GetClaudeMessageAsync(parameters, ct);
        return response;
    }
    catch (Exception ex) when (attempt < maxRetries - 1)
    {
        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
    }
}
```

---

## Configuration Reference

All non-secret configuration lives in `appsettings.json`:

```json
{
  "Llm": { "Model": "claude-opus-4.6" },
  "AzureDevOps": { "OrgUrl": "...", "DefaultProject": "NAF Marketing" },
  "Branching": { "BaseBranch": "develop", "Prefix": "feature/ae" },
  "PullRequest": { "TargetBranch": "develop", "TitlePattern": "[AB#{WorkItemId}] {WorkItemTitle}" },
  "McpServers": { "azure-devops": { ... }, "filesystem": { ... }, "terminal": { ... } }
}
```

Access in C# via the `AgentConfig` strongly-typed config model.
