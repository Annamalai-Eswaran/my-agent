---
applyTo: "mcp-servers/**/tools/**"
---

# MCP Tool Development Instructions

Follow these conventions for all MCP tool files under `mcp-servers/**/tools/`.

## Tool Descriptions

- Every tool registration **must** include a clear, concise description that an LLM can understand.
- The description should explain *what the tool does*, *what its key inputs are*, and *what it returns*.
- Bad: `"Gets work item"`
- Good: `"Fetch a single Azure DevOps work item by its numeric ID. Returns the title, state, assigned-to, description, and acceptance criteria."`

## Input Schemas

- Use **Zod** for all input schemas.
- Every field must have a `.describe("...")` annotation.
- Use `.optional()` only for truly optional fields — mark required fields without it.

```typescript
const schema = z.object({
  id: z.number().describe("The numeric Azure DevOps work item ID (e.g., 1234)"),
  fields: z.array(z.string()).optional().describe(
    "List of fields to return. Defaults to common fields if omitted."
  ),
});
```

## Response Format

- Return **structured JSON** as the text content:
  ```typescript
  return {
    content: [{
      type: "text",
      text: JSON.stringify({ id, title, state, assignedTo }, null, 2),
    }],
  };
  ```
- Do not return raw HTML or unstructured prose in tool responses.

## Error Responses

- Error messages must be **human-readable** so the LLM can understand and potentially recover.
- Include the error type and the original message:
  ```typescript
  return {
    content: [{
      type: "text",
      text: `Failed to fetch work item ${id}: ${(err as Error).message}`,
    }],
  };
  ```

## File Organization

- Group related tools in the same file (e.g., all work item operations in `work-items.ts`).
- Each file exports a single `register*Tools(server: McpServer)` function.
- Keep each tool handler focused — extract complex logic into private helper functions.

## Naming Conventions

- Tool names use **snake_case**: `get_work_item`, `create_pull_request`, `list_branches`.
- File names use **kebab-case**: `work-items.ts`, `pull-requests.ts`, `git.ts`.
- Registration functions use **PascalCase** for the domain: `registerWorkItemTools`, `registerGitTools`.
