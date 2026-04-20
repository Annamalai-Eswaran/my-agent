# GitHub Copilot Instructions

## Project Summary

This repository contains an **AI-powered software engineering agent** built in **.NET 8 (C#)**. It integrates with:

- **Azure DevOps** (`https://dev.azure.com/NAF-Tech/`, project `NAF Marketing`) via a custom .NET MCP server for work item management, branch creation, and pull request automation.
- **Anthropic Claude (claude-opus-4.6)** as the LLM backbone via `Anthropic.SDK`.

The agent follows a loop: fetch ADO work items → pick a task → create a feature branch → implement code → raise a PR back to Azure DevOps.

---

## Repository Structure

```
my-agent/
├── MyAgent.slnx                  # .NET solution file
├── src/
│   ├── MyAgent.Orchestrator/     # Main agent brain (console app)
│   │   ├── Agent/
│   │   │   ├── EngineerAgent.cs  # Core agentic loop
│   │   │   ├── HumanLoop.cs      # ask_human: colored terminal I/O
│   │   │   └── SystemPrompts.cs  # LLM system prompt constants
│   │   ├── Configuration/
│   │   │   ├── AgentConfig.cs    # Strongly-typed config model
│   │   │   └── appsettings.json  # All config: LLM, ADO, MCP servers
│   │   ├── Mcp/
│   │   │   ├── McpClientManager.cs  # Stdio JSON-RPC MCP client
│   │   │   ├── McpServerConfig.cs
│   │   │   └── McpToolDefinition.cs
│   │   └── Utils/
│   │       ├── BranchUtils.cs    # Slugify + MakeBranchName
│   │       └── StringExtensions.cs
│   │
│   ├── MyAgent.McpServer.AzureDevOps/  # Azure DevOps MCP server (stdio)
│   │   ├── AzureDevOpsClient.cs  # VssConnection + PAT auth
│   │   └── Tools/
│   │       ├── WorkItemTools.cs
│   │       ├── BranchTools.cs
│   │       ├── PullRequestTools.cs
│   │       └── RepositoryTools.cs
│   │
│   └── MyAgent.Common/           # Shared models (WorkItem, Repository, PullRequest)
│
├── tests/
│   ├── MyAgent.Orchestrator.Tests/
│   └── MyAgent.McpServer.Tests/
│
├── scripts/
│   ├── start.sh                  # dotnet run --project src/MyAgent.Orchestrator
│   └── setup.sh                  # dotnet restore + dotnet build + env check
│
├── .env.example                  # Env variable template
├── AGENTS.md                     # Agent-readable instructions
├── CLAUDE.md                     # Claude-specific instructions
└── docs/                         # Detailed documentation
```

---

## Build Instructions

```bash
# Restore and build everything
dotnet restore MyAgent.slnx
dotnet build MyAgent.slnx

# Run tests
dotnet test MyAgent.slnx

# Start the agent
dotnet run --project src/MyAgent.Orchestrator
# or:
./scripts/start.sh
```

---

## Code Standards

### C# / .NET
- Target **net8.0** for all projects.
- Enable `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.
- Use **records** for immutable data models; **classes** for services.
- Use `async`/`await` for all I/O operations.
- Follow Microsoft C# naming conventions (PascalCase types/methods, `_camelCase` private fields).
- Register services via `Microsoft.Extensions.DependencyInjection` in `Program.cs`.
- Use `ILogger<T>` for structured logging.

### MCP Tools (C#)
- Every tool must have a **clear, concise description** understandable by an LLM.
- Input schemas are `JsonObject` with `["type"]`, `["properties"]`, and `["required"]`.
- Tool handlers return a `JsonObject` — always catch exceptions and return `{ "error": "..." }`.
- Gather all tool definitions via `GetDefinitions()` static methods.

### Anthropic.SDK Usage
- Use `Anthropic.SDK.Common.Tool` for tool definitions.
- Create tools via `new Function(name, description, JsonNode.Parse(schemaJson))`.
- `ToolResultContent.ToolUseId` (not `.Id`); `.Content` is a `string`.
- `ToolUseContent.Input` is `JsonNode`.

---

## Branching Convention

- All feature branches are created from `develop`.
- Branch naming: `feature/ae/{work_item_id}-{slugified-description}`
  - Example: `feature/ae/1234-fix-login-page`
- Use `BranchUtils.MakeBranchName(id, title)` to generate the name.

---

## PR Convention

- PRs are raised to **Azure DevOps** targeting the `develop` branch.
- PR title format: `[AB#{work_item_id}] {title}`
  - Example: `[AB#1234] Fix login page redirect`
- Always link the work item via `WorkItemRefs` on the `GitPullRequest`.

---

## Testing

- **xUnit** + **FluentAssertions** + **Moq** for all tests.
- Test files live in `tests/` mirroring `src/` structure.
- Run all tests: `dotnet test MyAgent.slnx`

---

## Environment Variables

Create a `.env` file in the root (never commit it):

```env
# Required
ANTHROPIC_API_KEY=your_anthropic_api_key_here
AZURE_DEVOPS_PAT=your_personal_access_token_here
```

All non-secret configuration is in `src/MyAgent.Orchestrator/Configuration/appsettings.json`.
