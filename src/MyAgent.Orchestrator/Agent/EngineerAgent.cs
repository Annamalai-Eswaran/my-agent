using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;
using MyAgent.Orchestrator.Configuration;
using MyAgent.Orchestrator.Mcp;
using AnthropicTool = Anthropic.SDK.Common.Tool;

namespace MyAgent.Orchestrator.Agent;

public class EngineerAgent
{
    private readonly AgentConfig _config;
    private readonly McpClientManager _mcpManager;
    private readonly ILogger<EngineerAgent> _logger;

    private const string Bold = "\x1b[1m";
    private const string Green = "\x1b[92m";
    private const string Reset = "\x1b[0m";

    public EngineerAgent(AgentConfig config, McpClientManager mcpManager, ILogger<EngineerAgent> logger)
    {
        _config = config;
        _mcpManager = mcpManager;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        await _mcpManager.ConnectAllAsync(ct);
        var mcpTools = await _mcpManager.DiscoverToolsAsync(ct);

        var humanToolDef = HumanLoop.GetToolDefinition();

        var tools = new List<AnthropicTool>();
        foreach (var t in mcpTools)
            tools.Add(BuildTool(t.Name, t.Description, t.InputSchema));
        tools.Add(BuildTool(humanToolDef.Name, humanToolDef.Description, humanToolDef.InputSchema));

        _logger.LogInformation("Total tools available: {Count}", tools.Count);

        var messages = new List<Message>
        {
            new()
            {
                Role = RoleType.User,
                Content = new List<ContentBase>
                {
                    new TextContent { Text = "Start my workday. Check Azure DevOps for my ready work items." }
                }
            }
        };

        var client = new AnthropicClient();
        Console.WriteLine($"\n{Bold}{Green}AI Engineering Agent started. Press Ctrl+C to quit.{Reset}\n");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var parameters = new MessageParameters
                {
                    Model = _config.Llm.Model,
                    MaxTokens = 4096,
                    SystemMessage = SystemPrompts.EngineerAgent,
                    Tools = tools,
                    Messages = messages
                };

                var response = await client.Messages.GetClaudeMessageAsync(parameters, ct);

                if (response.StopReason == "end_turn")
                {
                    foreach (var block in response.Content)
                    {
                        if (block is TextContent textBlock)
                            Console.WriteLine($"\n{Bold}🤖  Agent:{Reset} {textBlock.Text}");
                    }

                    messages.Add(new Message { Role = RoleType.Assistant, Content = response.Content });

                    Console.Write($"\n{Bold}👉  You (or 'quit'): {Reset}");
                    var userInput = Console.ReadLine()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(userInput) || userInput.ToLower() is "quit" or "exit" or "q")
                    {
                        Console.WriteLine("\nGoodbye! 👋");
                        break;
                    }
                    messages.Add(new Message
                    {
                        Role = RoleType.User,
                        Content = new List<ContentBase> { new TextContent { Text = userInput } }
                    });
                }
                else if (response.StopReason == "tool_use")
                {
                    messages.Add(new Message { Role = RoleType.Assistant, Content = response.Content });

                    var toolResults = new List<ContentBase>();
                    foreach (var block in response.Content)
                    {
                        if (block is ToolUseContent toolUse)
                        {
                            string resultText;
                            try
                            {
                                if (toolUse.Name == "ask_human")
                                {
                                    var inputObj = toolUse.Input?.AsObject();
                                    var question = inputObj?["question"]?.GetValue<string>() ?? "";
                                    resultText = HumanLoop.Ask(question);
                                }
                                else
                                {
                                    var inputJson = toolUse.Input?.ToJsonString() ?? "{}";
                                    var preview = inputJson.Length > 120 ? inputJson[..120] + "…" : inputJson;
                                    Console.WriteLine($"  {Green}🔧  {toolUse.Name}{Reset}({preview})");
                                    var inputObj = JsonNode.Parse(inputJson)?.AsObject() ?? new JsonObject();
                                    resultText = await _mcpManager.CallToolAsync(toolUse.Name, inputObj, ct);
                                }
                            }
                            catch (Exception ex)
                            {
                                resultText = $"Tool '{toolUse.Name}' failed: {ex.Message}";
                                _logger.LogError(ex, "Tool call failed: {Tool}", toolUse.Name);
                            }

                            toolResults.Add(new ToolResultContent
                            {
                                ToolUseId = toolUse.Id,
                                Content = resultText
                            });
                        }
                    }

                    messages.Add(new Message { Role = RoleType.User, Content = toolResults });
                }
                else
                {
                    _logger.LogWarning("Unexpected stop reason: {StopReason}", response.StopReason);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n\nInterrupted — shutting down.");
        }
        finally
        {
            await _mcpManager.DisposeAsync();
        }
    }

    private static AnthropicTool BuildTool(string name, string description, JsonObject inputSchema)
    {
        var schemaJson = inputSchema.ToJsonString();
        var function = new Function(name, description, JsonNode.Parse(schemaJson));
        return function;
    }
}
