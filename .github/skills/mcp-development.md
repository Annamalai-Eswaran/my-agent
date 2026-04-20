# Skill: MCP Server Development

This skill describes how to create, test, and integrate MCP (Model Context Protocol) servers in this project.

---

## Overview

MCP (Model Context Protocol) is a standard protocol that allows LLMs to call tools provided by external processes (servers). This project uses MCP to expose Azure DevOps operations, file system access, terminal commands, and more as tools that Claude Opus 4.6 can call.

---

## Creating a New MCP Server from Scratch

### 1. Scaffold the Project

```bash
mkdir -p mcp-servers/my-server/src/tools
cd mcp-servers/my-server
npm init -y
npm install @modelcontextprotocol/sdk zod
npm install -D typescript @types/node ts-node
```

### 2. Create `tsconfig.json`

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "Node16",
    "moduleResolution": "Node16",
    "outDir": "./dist",
    "rootDir": "./src",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "declaration": true
  },
  "include": ["src/**/*"],
  "exclude": ["node_modules", "dist"]
}
```

### 3. Create the Entry Point (`src/index.ts`)

```typescript
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { registerMyTools } from "./tools/my-tools.js";

const server = new McpServer({
  name: "my-server",
  version: "1.0.0",
});

// Register all tool groups
registerMyTools(server);

// Start on stdio transport
const transport = new StdioServerTransport();
await server.connect(transport);
console.error("My MCP server running on stdio");
```

### 4. Create a Tool File (`src/tools/my-tools.ts`)

```typescript
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";

export function registerMyTools(server: McpServer): void {
  server.tool(
    "my_tool_name",
    "Clear description of what this tool does, what it takes, and what it returns.",
    {
      param1: z.string().describe("Description of param1"),
      param2: z.number().optional().describe("Optional: description of param2"),
    },
    async ({ param1, param2 }) => {
      try {
        // Tool implementation
        const result = { success: true, data: `Processed: ${param1}` };
        return {
          content: [{ type: "text", text: JSON.stringify(result, null, 2) }],
        };
      } catch (err) {
        return {
          content: [{ type: "text", text: `Error: ${(err as Error).message}` }],
        };
      }
    }
  );
}
```

### 5. Add Build Script to `package.json`

```json
{
  "scripts": {
    "build": "tsc",
    "dev": "ts-node src/index.ts",
    "start": "node dist/index.js"
  }
}
```

### 6. Build and Test

```bash
npm run build
node dist/index.js  # Should start and wait on stdio
```

---

## Tool Registration Patterns

### Pattern 1: Simple Tool

```typescript
server.tool(
  "get_data",
  "Fetch data by ID",
  { id: z.string().describe("The resource ID") },
  async ({ id }) => {
    const data = await fetchData(id);
    return { content: [{ type: "text", text: JSON.stringify(data) }] };
  }
);
```

### Pattern 2: Tool with Complex Output

```typescript
server.tool(
  "search_items",
  "Search for items matching a query. Returns an array of matching items with id, title, and score.",
  {
    query: z.string().describe("Search query string"),
    limit: z.number().min(1).max(100).default(10).describe("Max results to return"),
  },
  async ({ query, limit }) => {
    const results = await search(query, limit);
    return {
      content: [{
        type: "text",
        text: JSON.stringify({
          count: results.length,
          items: results,
        }, null, 2),
      }],
    };
  }
);
```

### Pattern 3: Tool with Side Effects

```typescript
server.tool(
  "create_resource",
  "Create a new resource. Returns the created resource with its assigned ID.",
  {
    name: z.string().describe("Name of the resource"),
    type: z.enum(["typeA", "typeB"]).describe("Resource type: 'typeA' or 'typeB'"),
  },
  async ({ name, type }) => {
    try {
      const created = await createResource({ name, type });
      return {
        content: [{
          type: "text",
          text: JSON.stringify({ success: true, resource: created }),
        }],
      };
    } catch (err) {
      return {
        content: [{
          type: "text",
          text: `Failed to create resource: ${(err as Error).message}`,
        }],
      };
    }
  }
);
```

---

## Input/Output Schema Design

### Good Schema Design
- Every field has `.describe("...")` — this is what the LLM sees.
- Use `z.enum()` when values are constrained to a known set.
- Use `.optional()` only for truly optional fields.
- Use `.default()` for fields with sensible defaults.
- Use `.min()`, `.max()`, `.email()`, `.url()` etc. for validation.

### Output Schema Design
- Always return JSON — structured, machine-readable.
- Include `success: boolean` for mutation operations.
- Include `count` and `items` for list operations.
- Include the created/updated object for create/update operations.

---

## Testing MCP Servers Locally

### Method 1: MCP Inspector

```bash
npx @modelcontextprotocol/inspector node dist/index.js
```

This opens a web UI where you can invoke tools manually.

### Method 2: Manual stdin Test

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' | node dist/index.js
```

### Method 3: Integration Test via Agent

Run the agent with only your new server enabled in `config.json` and ask it to use your new tool.

---

## Adding a New Server to the Agent Config

Edit `config.json`:

```json
{
  "mcpServers": {
    "my-server": {
      "command": "node",
      "args": ["mcp-servers/my-server/dist/index.js"],
      "enabled": true,
      "env": {
        "MY_API_KEY": "${MY_API_KEY}"
      }
    }
  }
}
```

Environment variables in `env` are passed directly to the server process. Use `${VAR_NAME}` to reference variables from the host environment.

---

## Debugging MCP Connections

### Enable Debug Logging

Set `LOG_LEVEL=debug` in `.env`. The MCP client manager will log:
- Server startup and shutdown events
- Each tool call and its response
- Connection errors and reconnection attempts

### Common Issues

| Issue | Cause | Fix |
|-------|-------|-----|
| Server won't start | Missing `dist/index.js` | Run `npm run build` |
| Tools not appearing | Server crashes on startup | Check stderr for errors |
| `ENOENT` error | Wrong `command` or `args` path | Verify paths in `config.json` |
| Tool returns error | API credentials missing | Check `.env` and `config.json` env vars |
| Timeout | Server unresponsive | Check server logs, increase timeout in `mcp_client.py` |

### Read Server Stderr

The agent captures server stderr and logs it at debug level. Run with `LOG_LEVEL=debug` to see it.

### Test a Tool Call Directly

```typescript
// Add to your tool file temporarily:
if (process.argv[2] === "test") {
  const result = await myToolHandler({ param1: "test" });
  console.log(JSON.stringify(result, null, 2));
}
```

Then: `node dist/index.js test`
