using System.Text.Json.Nodes;
using MyAgent.Orchestrator.Mcp;

namespace MyAgent.Orchestrator.Agent;

public static class HumanLoop
{
    private const string Bold = "\x1b[1m";
    private const string Yellow = "\x1b[93m";
    private const string Reset = "\x1b[0m";

    public static string Ask(string question)
    {
        Console.Write($"\n{Bold}{Yellow}❓  Agent asks: {Reset}{question}\n{Bold}👤  Your answer: {Reset}");
        return Console.ReadLine() ?? "";
    }

    public static McpToolDefinition GetToolDefinition() => new()
    {
        Name = "ask_human",
        Description = "Ask the human engineer a question when you need clarification, approval, or are stuck. Use this tool freely — the human is always available.",
        InputSchema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["question"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "The question or prompt to present to the human."
                }
            },
            ["required"] = new JsonArray("question")
        }
    };
}
