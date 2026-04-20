# C# / .NET Coding Instructions

Follow these conventions for all C# files in this repository.

## General

- Target **net8.0** for all projects.
- Enable `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` in every project.
- Use **records** for immutable data models; use **classes** for services and mutable state.
- Prefer `async`/`await` for all I/O operations.

## Naming and Style

- Follow Microsoft's [C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).
- Use `PascalCase` for types, methods, properties; `camelCase` for local variables and parameters.
- Prefix private fields with `_`: e.g. `_config`, `_logger`.

## Dependency Injection

- Register services via `Microsoft.Extensions.DependencyInjection` in `Program.cs`.
- Use constructor injection — no service locator pattern.
- All services that hold disposable resources should implement `IAsyncDisposable`.

## Anthropic.SDK Usage

- Use `Anthropic.SDK.Common.Tool` (not `Anthropic.SDK.Messaging.Tool`) for tool definitions.
- Create tools via `new Function(name, description, JsonNode.Parse(schemaJson))` — implicit conversion to `Tool` applies.
- `MessageParameters.SystemMessage` is a `string` (not a list).
- `ToolResultContent.ToolUseId` (not `.Id`) and `.Content` is a `string`.
- `ToolUseContent.Input` is `JsonNode`.

## MCP Client (McpClientManager)

- All MCP servers are stdio JSON-RPC 2.0 processes.
- Use `SemaphoreSlim(1,1)` to serialize request/response over stdio.
- Skip non-JSON lines when reading stdout (filter with `!line.StartsWith('{')`).
- Always send `notifications/initialized` after a successful `initialize` response.

## Azure DevOps SDK

- All Git API methods use `(project, repositoryId, ...)` parameter order — project is **first**.
- `WorkItem.Fields` is `IDictionary<string, object>` — use `TryGetValue` not `GetValueOrDefault`.
- `GetRefsAsync` takes `(project, repositoryId, filter, includeLinks, includeStatuses, includeMyBranches, latestStatusesOnly, peelTags, filterContains, userState, ct)` positionally.
- `UpdateRefsAsync` takes `(refUpdates, project, repositoryId, projectId, userState, ct)`.

## Error Handling

- Catch specific exceptions; never use bare `catch { }` without at minimum logging.
- In MCP tool handlers, catch all exceptions and return a `JsonObject { ["error"] = message }`.
- Log with `ILogger<T>` — always include structured context.

## Testing

- Use **xUnit** for all tests.
- Use **FluentAssertions** for readable assertions.
- Use **Moq** for mocking interfaces.
- Test files live in `tests/` and mirror the `src/` structure.
