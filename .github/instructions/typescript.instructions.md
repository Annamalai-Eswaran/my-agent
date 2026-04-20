---
applyTo: "**/*.ts,**/*.tsx"
---

# TypeScript Coding Instructions

Follow these conventions for all TypeScript files in this repository.

## Zod Schemas

- Use **Zod** for all MCP tool input validation.
- Every schema field must have a `.describe("...")` annotation so the LLM understands its purpose.

```typescript
import { z } from "zod";

const MyToolInput = z.object({
  workItemId: z.number().describe("The Azure DevOps work item ID"),
  comment: z.string().optional().describe("Optional comment to add to the work item"),
});
```

## MCP SDK Patterns

- Use `@modelcontextprotocol/sdk` for all server/tool registration.
- Register tools via the `server.tool(name, description, schema, handler)` pattern.
- Return `{ content: [{ type: "text", text: JSON.stringify(result) }] }` from tool handlers.

## Azure DevOps API

- All Azure DevOps REST API calls go through the helpers in `client.ts`.
- Never create a raw `fetch`/`axios` call to the ADO API outside of `client.ts`.
- Use the `getConnection()` helper to obtain an authenticated `WebApi` instance.

## Tool Registration

- Export tool registration as a named function: `register*Tools(server: McpServer)`.
- Example:
  ```typescript
  export function registerWorkItemTools(server: McpServer): void {
    server.tool("get_work_item", "Fetch a work item by ID", { ... }, handler);
  }
  ```
- Call all `register*Tools` functions from `index.ts`.

## Error Handling

- Wrap all ADO API calls in try/catch.
- Return a human-readable error message as the tool response text on failure:
  ```typescript
  catch (err) {
    return { content: [{ type: "text", text: `Error: ${(err as Error).message}` }] };
  }
  ```

## ES Modules

- Use ES module syntax: `import` / `export` (no `require`).
- The project targets **ES2022** — you can use optional chaining (`?.`), nullish coalescing (`??`), `Array.at()`, etc.

## Strict Mode

- `"strict": true` is enabled in `tsconfig.json`. Do not disable it.
- Handle `null` / `undefined` explicitly — do not use the non-null assertion operator (`!`) unless you are certain the value cannot be null.
