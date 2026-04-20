# Skill: MCP Server Development (.NET)

This skill describes how to create, test, and integrate MCP (Model Context Protocol) servers in this project using .NET 8 C#.

---

## Overview

MCP (Model Context Protocol) is a standard protocol that allows LLMs to call tools provided by external processes (servers). This project uses MCP to expose Azure DevOps operations, file system access, terminal commands, and more as tools that Claude Opus 4.6 can call.

All custom MCP servers in this project are written in **C# (.NET 8)** and use **stdio JSON-RPC 2.0** transport.

---

## Creating a New .NET MCP Server

### 1. Scaffold the Project

```bash
mkdir -p src/MyAgent.McpServer.MyService/Tools
cd src/MyAgent.McpServer.MyService
dotnet new console --framework net8.0
```

### 2. Create the `.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\MyAgent.Common\MyAgent.Common.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="System.Text.Json" Version="8.0.5" />
  </ItemGroup>
</Project>
```

### 3. Create `Tools/ToolDefinition.cs`

```csharp
using System.Text.Json.Nodes;

namespace MyAgent.McpServer.MyService.Tools;

public class ToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JsonObject InputSchema { get; set; } = new();
}
```

### 4. Create a Tool File

```csharp
// Tools/MyTools.cs
using System.Text.Json.Nodes;

namespace MyAgent.McpServer.MyService.Tools;

public static class MyTools
{
    public static IEnumerable<ToolDefinition> GetDefinitions() =>
    [
        new ToolDefinition
        {
            Name = "my-action",
            Description = "Perform my action. Returns result JSON with {status, result}.",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["param1"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "The input parameter."
                    }
                },
                ["required"] = new JsonArray("param1")
            }
        }
    ];

    public static async Task<JsonObject> MyActionAsync(string param1)
    {
        try
        {
            // Your implementation
            return new JsonObject { ["status"] = "ok", ["result"] = param1 };
        }
        catch (Exception ex)
        {
            return new JsonObject { ["error"] = $"my-action failed: {ex.Message}" };
        }
    }
}
```

### 5. Create `Program.cs`

Use the stdio JSON-RPC loop pattern from `src/MyAgent.McpServer.AzureDevOps/Program.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using MyAgent.McpServer.MyService.Tools;

var log = Console.Error; // always log to stderr

var allTools = new List<ToolDefinition>();
allTools.AddRange(MyTools.GetDefinitions());

using var stdin = new StreamReader(Console.OpenStandardInput());
using var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

while (true)
{
    var line = await stdin.ReadLineAsync();
    if (line == null) break;
    line = line.Trim();
    if (string.IsNullOrEmpty(line)) continue;

    JsonObject? request;
    try { request = JsonNode.Parse(line)?.AsObject(); }
    catch { continue; }
    if (request == null) continue;

    var id = request["id"];
    var method = request["method"]?.GetValue<string>();

    var response = method switch
    {
        "initialize" => HandleInitialize(id),
        "tools/list" => HandleToolsList(id, allTools),
        "tools/call" => await HandleToolCallAsync(id, request["params"]?.AsObject()),
        _ => CreateErrorResponse(id, -32601, $"Method not found: {method}")
    };

    await stdout.WriteLineAsync(response.ToJsonString());
}

// ... helper methods (HandleInitialize, HandleToolsList, etc.)
```

### 6. Add to Solution and `appsettings.json`

```bash
dotnet sln MyAgent.slnx add src/MyAgent.McpServer.MyService/MyAgent.McpServer.MyService.csproj
```

```json
"McpServers": {
  "my-service": {
    "Command": "dotnet",
    "Args": ["run", "--project", "src/MyAgent.McpServer.MyService"],
    "Enabled": true,
    "Env": {}
  }
}
```

---

## MCP Protocol Rules

1. **All logging goes to `stderr`** — `stdout` is the JSON-RPC channel.
2. **Every request/response uses the same `id`** — echo it back exactly.
3. **Tool responses use content array**: `{ "content": [{ "type": "text", "text": "..." }] }`.
4. **Errors in tool calls are NOT JSON-RPC errors** — return `{ "content": [{ "type": "text", "text": "Error: ..." }] }`.
5. **Send `notifications/initialized` after `initialize`** (client does this; the server just awaits it).

---

## Tool Design Guidelines

- **Name**: Use `kebab-case` (e.g., `list-work-items`, `create-branch`).
- **Description**: Should answer "What does this do?" AND "What does it return?" in one sentence.
- **Input Schema**: Every field must have a `"description"` property.
- **Response**: Return structured JSON so the LLM can parse it.
- **Errors**: Return `{ "error": "Human-readable message" }` — never throw uncaught exceptions.

---

## Testing MCP Servers Manually

You can test a .NET MCP server by piping JSON-RPC requests manually:

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}' | dotnet run --project src/MyAgent.McpServer.AzureDevOps
```

Expected response:
```json
{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2024-11-05","capabilities":{"tools":{}},"serverInfo":{"name":"MyAgent.McpServer.AzureDevOps","version":"1.0.0"}}}
```
