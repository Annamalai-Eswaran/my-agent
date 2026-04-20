using System.Text.Json.Nodes;

namespace MyAgent.Orchestrator.Mcp;

public class McpToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JsonObject InputSchema { get; set; } = new();
}
