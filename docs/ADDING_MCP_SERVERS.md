# Adding New MCP Servers

This guide explains how to add new MCP servers to the agent, whether you're using a community server or building a custom one.

---

## Overview

The agent supports any MCP-compatible server. Servers are configured in `config.json` and started automatically when the agent launches. The agent automatically discovers all available tools from each enabled server.

---

## Option A: Using an Existing Community MCP Server

This is the quickest way to add new capabilities.

### Step 1: Find an MCP Server

Popular sources:
- [MCP Marketplace](https://github.com/modelcontextprotocol/servers) — official community servers
- npm packages starting with `@modelcontextprotocol/server-*`
- GitHub repositories with `mcp-server` in the name

### Step 2: Add to `config.json`

```json
{
  "mcpServers": {
    "existing-server": { "...": "..." },

    "new-community-server": {
      "command": "npx",
      "args": ["-y", "@some-org/mcp-server-name", "--option", "value"],
      "enabled": true,
      "env": {
        "API_KEY": "${MY_SERVICE_API_KEY}"
      }
    }
  }
}
```

### Step 3: Add Environment Variables

Add any required secrets to `.env`:
```env
MY_SERVICE_API_KEY=your_key_here
```

Add placeholders to `.env.example`:
```env
MY_SERVICE_API_KEY=your_key_here
```

### Step 4: Test

Restart the agent. The new server's tools will appear in the tool list automatically.

---

## Option B: Building a Custom MCP Server

Use this when you need deep integration with a service that doesn't have an existing MCP server, or when you need custom tool behavior.

### Step 1: Scaffold the Project

```bash
mkdir -p mcp-servers/my-service/src/tools
cd mcp-servers/my-service
npm init -y
npm install @modelcontextprotocol/sdk zod
npm install -D typescript @types/node
```

### Step 2: Configure TypeScript

Create `tsconfig.json`:
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
    "skipLibCheck": true
  },
  "include": ["src/**/*"],
  "exclude": ["node_modules", "dist"]
}
```

Update `package.json`:
```json
{
  "type": "module",
  "scripts": {
    "build": "tsc",
    "start": "node dist/index.js",
    "dev": "tsc --watch"
  }
}
```

### Step 3: Create the Service Client (`src/client.ts`)

```typescript
import MyServiceClient from "my-service-sdk";

let _client: MyServiceClient | null = null;

export function getClient(): MyServiceClient {
  if (!_client) {
    const apiKey = process.env.MY_SERVICE_API_KEY;
    if (!apiKey) throw new Error("MY_SERVICE_API_KEY environment variable is required");
    _client = new MyServiceClient({ apiKey });
  }
  return _client;
}
```

### Step 4: Create Tool Files (`src/tools/my-tools.ts`)

```typescript
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { getClient } from "../client.js";

export function registerMyServiceTools(server: McpServer): void {
  server.tool(
    "get_item",
    "Fetch an item by ID from My Service. Returns the item's title, description, and metadata.",
    {
      id: z.string().describe("The unique item identifier"),
    },
    async ({ id }) => {
      try {
        const client = getClient();
        const item = await client.getItem(id);
        return {
          content: [{ type: "text", text: JSON.stringify(item, null, 2) }],
        };
      } catch (err) {
        return {
          content: [{ type: "text", text: `Failed to get item ${id}: ${(err as Error).message}` }],
        };
      }
    }
  );

  // Add more tools here...
}
```

### Step 5: Create the Server Entry Point (`src/index.ts`)

```typescript
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { registerMyServiceTools } from "./tools/my-tools.js";

const server = new McpServer({
  name: "my-service",
  version: "1.0.0",
});

registerMyServiceTools(server);

const transport = new StdioServerTransport();
await server.connect(transport);
console.error("My Service MCP server started");
```

### Step 6: Build and Add to Config

```bash
npm run build
```

Add to `config.json`:
```json
"my-service": {
  "command": "node",
  "args": ["mcp-servers/my-service/dist/index.js"],
  "enabled": true,
  "env": {
    "MY_SERVICE_API_KEY": "${MY_SERVICE_API_KEY}"
  }
}
```

### Step 7: Test

```bash
# Test the server directly
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' | node dist/index.js

# Or use the MCP inspector
npx @modelcontextprotocol/inspector node dist/index.js
```

---

## Examples

### Figma MCP Server

Already supported in `config.json` — just enable it:

```json
"figma": {
  "command": "npx",
  "args": ["-y", "@figma/mcp-server"],
  "enabled": true,
  "env": {
    "FIGMA_ACCESS_TOKEN": "${FIGMA_ACCESS_TOKEN}"
  }
}
```

Add to `.env`:
```env
FIGMA_ACCESS_TOKEN=your_figma_token
```

### Slack MCP Server

```json
"slack": {
  "command": "npx",
  "args": ["-y", "@modelcontextprotocol/server-slack"],
  "enabled": true,
  "env": {
    "SLACK_BOT_TOKEN": "${SLACK_BOT_TOKEN}",
    "SLACK_TEAM_ID": "${SLACK_TEAM_ID}"
  }
}
```

### Jira MCP Server

```json
"jira": {
  "command": "npx",
  "args": ["-y", "@some-org/mcp-server-jira"],
  "enabled": true,
  "env": {
    "JIRA_URL": "https://your-org.atlassian.net",
    "JIRA_API_TOKEN": "${JIRA_API_TOKEN}",
    "JIRA_EMAIL": "${JIRA_EMAIL}"
  }
}
```

### GitHub MCP Server

```json
"github": {
  "command": "npx",
  "args": ["-y", "@modelcontextprotocol/server-github"],
  "enabled": true,
  "env": {
    "GITHUB_PERSONAL_ACCESS_TOKEN": "${GITHUB_PAT}"
  }
}
```

---

## Troubleshooting

| Problem | Solution |
|---------|---------|
| Server fails to start | Check stderr logs; ensure `dist/index.js` exists (run `npm run build`) |
| Tools not appearing | Verify `"enabled": true` in `config.json` and no JSON syntax errors |
| `ENOENT` when starting | Check that `command` and `args` paths are correct |
| Authentication errors | Verify environment variables are set in `.env` and referenced correctly |
| Tool calls failing | Enable debug logging: `LOG_LEVEL=debug` in `.env` |
