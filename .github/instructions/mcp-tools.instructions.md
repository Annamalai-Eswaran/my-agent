---
applyTo: "**/Tools/**"
---

# MCP Tool Development Instructions (C#)

Follow these conventions for all MCP tool files under `**/Tools/`.

## Tool Descriptions

- Every tool definition **must** include a clear, concise description that an LLM can understand.
- The description should explain *what the tool does*, *what its key inputs are*, and *what it returns*.
- Bad: `"Gets work item"`
- Good: `"Fetch a single Azure DevOps work item by its numeric ID. Returns the title, state, assigned-to, description, and acceptance criteria."`

## Input Schemas

- Input schemas are `JsonObject` instances with `"type"`, `"properties"`, and `"required"` keys.
- Every property must have a `"description"` key so the LLM understands its purpose.
- Use `["required"] = new JsonArray("field1", "field2")` for required fields.

```csharp
InputSchema = new JsonObject
{
    ["type"] = "object",
    ["properties"] = new JsonObject
    {
        ["id"] = new JsonObject
        {
            ["type"] = "integer",
            ["description"] = "The numeric Azure DevOps work item ID (e.g. 1234)."
        },
        ["fields"] = new JsonObject
        {
            ["type"] = "array",
            ["description"] = "List of fields to return. Defaults to common fields if omitted.",
            ["items"] = new JsonObject { ["type"] = "string" }
        }
    },
    ["required"] = new JsonArray("id")
}
```

## Response Format

- Return **structured JSON** as a `JsonObject` from the handler:
  ```csharp
  return new JsonObject
  {
      ["id"] = wi.Id,
      ["title"] = title,
      ["state"] = state
  };
  ```
- The MCP server's `Program.cs` wraps this in `{ "content": [{ "type": "text", "text": "..." }] }`.
- Do not return raw HTML or unstructured prose.

## Error Responses

- Error messages must be **human-readable** so the LLM can understand and potentially recover.
- Return a `JsonObject` with an `"error"` key on failure:
  ```csharp
  catch (Exception ex)
  {
      return new JsonObject { ["error"] = $"Failed to fetch work item {id}: {ex.Message}" };
  }
  ```

## File Organization

- Group related tools in the same file (e.g., all work item operations in `WorkItemTools.cs`).
- Each file has a `GetDefinitions()` static method returning `IEnumerable<ToolDefinition>`.
- Each tool handler is a `public static async Task<JsonObject>` method.
- Keep each tool handler focused — extract complex logic into private helper methods.

## Naming Conventions

- Tool names use **kebab-case**: `get-work-item`, `create-pull-request`, `list-branches`.
- File names use **PascalCase**: `WorkItemTools.cs`, `PullRequestTools.cs`, `BranchTools.cs`.
- Handler methods use **PascalCase**: `GetWorkItemAsync`, `CreatePullRequestAsync`.
