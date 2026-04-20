# Adding New MCP Servers

This guide explains how to add new MCP servers to the agent, whether you're using a community server or building a custom .NET one.

---

## Overview

The agent supports any MCP-compatible server. Servers are configured in `src/MyAgent.Orchestrator/Configuration/appsettings.json` and started automatically when the agent launches. The agent automatically discovers all available tools from each enabled server.

---

## Option A: Using an Existing Community MCP Server

This is the quickest way to add new capabilities.

### Step 1: Find an MCP Server

Popular sources:
- [MCP Marketplace](https://github.com/modelcontextprotocol/servers) — official community servers
- npm packages starting with `@modelcontextprotocol/server-*`
- GitHub repositories with `mcp-server` in the name

### Step 2: Add to `appsettings.json`

```json
{
  "McpServers": {
    "existing-server": { "...": "..." },

    "new-community-server": {
      "Command": "npx",
      "Args": ["-y", "@some-org/mcp-server-name", "--option", "value"],
      "Enabled": true,
      "Env": {
        "API_KEY": "MY_SERVICE_API_KEY"
      }
    }
  }
}
```

Note: `Env` values are **environment variable names** that will be resolved at runtime.

### Step 3: Restart the Agent

```bash
dotnet run --project src/MyAgent.Orchestrator
```

The agent will automatically connect to the new server and discover its tools.

---

## Option B: Building a Custom .NET MCP Server

Use `src/MyAgent.McpServer.AzureDevOps/` as a template.

### Step 1: Create a New Project

```bash
mkdir -p src/MyAgent.McpServer.MyService
cd src/MyAgent.McpServer.MyService
dotnet new console -n MyAgent.McpServer.MyService --framework net8.0
```

### Step 2: Create the `.csproj`

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
    <!-- Add your service client packages here -->
  </ItemGroup>
</Project>
```

### Step 3: Create Tool Definitions

```csharp
// src/MyAgent.McpServer.MyService/Tools/MyServiceTools.cs
using System.Text.Json.Nodes;

namespace MyAgent.McpServer.MyService.Tools;

public static class MyServiceTools
{
    public static IEnumerable<ToolDefinition> GetDefinitions() =>
    [
        new ToolDefinition
        {
            Name = "my-service-action",
            Description = "Perform an action on MyService. Returns the result as JSON.",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["param1"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Description of param1 for the LLM."
                    }
                },
                ["required"] = new JsonArray("param1")
            }
        }
    ];

    public static async Task<JsonObject> MyServiceActionAsync(string param1)
    {
        try
        {
            // Your implementation here
            return new JsonObject { ["result"] = $"Did something with {param1}" };
        }
        catch (Exception ex)
        {
            return new JsonObject { ["error"] = $"Failed: {ex.Message}" };
        }
    }
}
```

### Step 4: Create `Program.cs`

Copy `src/MyAgent.McpServer.AzureDevOps/Program.cs` and adapt it:
- Change the server name in the `initialize` response
- Register your tool definitions instead of the ADO tools
- Add your tool dispatch cases in the `tools/call` handler

### Step 5: Add a `ToolDefinition` Helper

```csharp
// src/MyAgent.McpServer.MyService/Tools/ToolDefinition.cs
using System.Text.Json.Nodes;

namespace MyAgent.McpServer.MyService.Tools;

public class ToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JsonObject InputSchema { get; set; } = new();
}
```

### Step 6: Add to Solution

```bash
cd /path/to/my-agent
dotnet sln MyAgent.slnx add src/MyAgent.McpServer.MyService/MyAgent.McpServer.MyService.csproj
```

### Step 7: Register in `appsettings.json`

```json
{
  "McpServers": {
    "my-service": {
      "Command": "dotnet",
      "Args": ["run", "--project", "src/MyAgent.McpServer.MyService"],
      "Enabled": true,
      "Env": {
        "MY_SERVICE_API_KEY": "MY_SERVICE_API_KEY"
      }
    }
  }
}
```

### Step 8: Build and Test

```bash
dotnet build MyAgent.slnx
dotnet run --project src/MyAgent.Orchestrator
# The agent will now discover and use tools from your new server
```

---

## MCP Protocol Reference

All MCP servers communicate via **stdio JSON-RPC 2.0**. The minimum protocol to implement:

### `initialize` Request
```json
{ "jsonrpc": "2.0", "id": 1, "method": "initialize", "params": { "protocolVersion": "2024-11-05", ... } }
```
Response:
```json
{ "jsonrpc": "2.0", "id": 1, "result": { "protocolVersion": "2024-11-05", "capabilities": { "tools": {} }, "serverInfo": { "name": "...", "version": "1.0.0" } } }
```

### `tools/list` Request
```json
{ "jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {} }
```
Response:
```json
{ "jsonrpc": "2.0", "id": 2, "result": { "tools": [{ "name": "...", "description": "...", "inputSchema": { ... } }] } }
```

### `tools/call` Request
```json
{ "jsonrpc": "2.0", "id": 3, "method": "tools/call", "params": { "name": "my-tool", "arguments": { "param1": "value" } } }
```
Response:
```json
{ "jsonrpc": "2.0", "id": 3, "result": { "content": [{ "type": "text", "text": "{ \"result\": \"...\" }" }] } }
```

---

## Tips

- **Log to stderr only** — stdout is reserved for the MCP JSON-RPC protocol.
- **Return human-readable errors** — the LLM needs to understand them to recover.
- **Keep tool descriptions concise** — the LLM reads them every call.
- **Use `Enabled: false`** in `appsettings.json` to disable a server without removing it.
