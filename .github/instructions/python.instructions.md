---
applyTo: "**/*.py"
---

# Python Coding Instructions

Follow these conventions for all Python files in this repository.

## Async / Await

- Use `async`/`await` for **all** I/O operations (file reads, network calls, MCP client calls, subprocess execution).
- Never use blocking I/O inside an async function without `asyncio.to_thread` or equivalent.

## Type Hints

- Add **type hints to all function signatures**, including return types.
- Use `Optional[T]` (or `T | None` in Python ≥ 3.10) for nullable values.
- Import types from `typing` or use built-in generics (`list[str]`, `dict[str, Any]`).

## Structured Data

- Use `dataclasses` or **Pydantic models** for structured data objects.
- Prefer Pydantic when data comes from external sources (ADO API responses, config files) for automatic validation.

## Error Handling

- **Always catch specific exceptions** — never use a bare `except:` or `except Exception:` without logging.
- Log errors with full context: which tool was called, what the inputs were, what the error was.
- Use structured logging (e.g., `logging.getLogger(__name__)`).

## Environment Variables

- Load environment variables using **`python-dotenv`**:
  ```python
  from dotenv import load_dotenv
  load_dotenv()
  ```
- Never hardcode secrets or tokens in source code.

## MCP Client Calls

- All MCP client calls must have **timeout handling**:
  ```python
  async with asyncio.timeout(30):
      result = await mcp_client.call_tool("tool_name", args)
  ```
- Handle `TimeoutError` and surface a clear error message to the LLM.

## Existing Patterns

- Follow the patterns already established in the `agent/` directory.
- Use the same logging setup, config loading, and error handling style as `agent/main.py`.
- Keep tool functions small and focused — one responsibility per function.

## Imports

- Group imports: standard library → third-party → local, with a blank line between groups.
- Use absolute imports within the `agent/` package.
