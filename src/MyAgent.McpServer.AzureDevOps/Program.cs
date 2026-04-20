using System.Text.Json;
using System.Text.Json.Nodes;
using MyAgent.McpServer.AzureDevOps;
using MyAgent.McpServer.AzureDevOps.Tools;

var log = Console.Error;

var orgUrl = Environment.GetEnvironmentVariable("AZURE_DEVOPS_ORG_URL") ?? "https://dev.azure.com/NAF-Tech/";
var patEnvVar = Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT_ENV") ?? "AZURE_DEVOPS_PAT";
var pat = Environment.GetEnvironmentVariable(patEnvVar) ?? "";

if (string.IsNullOrEmpty(pat))
    log.WriteLine($"Warning: {patEnvVar} environment variable not set. ADO API calls will fail.");

AzureDevOpsClient? adoClient = null;
try
{
    adoClient = new AzureDevOpsClient(orgUrl, pat);
}
catch (Exception ex)
{
    log.WriteLine($"Failed to create Azure DevOps client: {ex.Message}");
}

var allTools = new List<ToolDefinition>();
allTools.AddRange(WorkItemTools.GetDefinitions());
allTools.AddRange(BranchTools.GetDefinitions());
allTools.AddRange(PullRequestTools.GetDefinitions());
allTools.AddRange(RepositoryTools.GetDefinitions());

using var stdin = new StreamReader(Console.OpenStandardInput());
using var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

while (true)
{
    var line = await stdin.ReadLineAsync();
    if (line == null) break;
    line = line.Trim();
    if (string.IsNullOrEmpty(line)) continue;

    JsonObject? request;
    try { request = JsonNode.Parse(line)?.AsObject(); }
    catch { continue; }
    if (request == null) continue;

    var id = request["id"];
    var method = request["method"]?.GetValue<string>();

    JsonObject response;
    try
    {
        response = method switch
        {
            "initialize" => HandleInitialize(id),
            "tools/list" => HandleToolsList(id, allTools),
            "tools/call" => await HandleToolCallAsync(id, request["params"]?.AsObject(), adoClient),
            _ => CreateErrorResponse(id, -32601, $"Method not found: {method}")
        };
    }
    catch (Exception ex)
    {
        response = CreateErrorResponse(id, -32603, ex.Message);
    }

    await stdout.WriteLineAsync(response.ToJsonString());
}

static JsonObject HandleInitialize(JsonNode? id) => new()
{
    ["jsonrpc"] = "2.0",
    ["id"] = id?.DeepClone(),
    ["result"] = new JsonObject
    {
        ["protocolVersion"] = "2024-11-05",
        ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
        ["serverInfo"] = new JsonObject { ["name"] = "MyAgent.McpServer.AzureDevOps", ["version"] = "1.0.0" }
    }
};

static JsonObject HandleToolsList(JsonNode? id, List<ToolDefinition> tools)
{
    var toolsArray = new JsonArray();
    foreach (var t in tools)
    {
        toolsArray.Add(new JsonObject
        {
            ["name"] = t.Name,
            ["description"] = t.Description,
            ["inputSchema"] = t.InputSchema.DeepClone()
        });
    }
    return new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["result"] = new JsonObject { ["tools"] = toolsArray }
    };
}

static async Task<JsonObject> HandleToolCallAsync(JsonNode? id, JsonObject? p, AzureDevOpsClient? adoClient)
{
    var toolName = p?["name"]?.GetValue<string>() ?? "";
    var args = p?["arguments"]?.AsObject() ?? new JsonObject();

    if (adoClient == null)
        return CreateToolErrorResponse(id, "Azure DevOps client not initialized. Check AZURE_DEVOPS_PAT environment variable.");

    JsonObject result;
    try
    {
        result = toolName switch
        {
            "list-ready-work-items" => await WorkItemTools.ListReadyWorkItemsAsync(adoClient,
                args["project"]?.GetValue<string>() ?? "NAF Marketing"),
            "get-work-item" => await WorkItemTools.GetWorkItemAsync(adoClient,
                args["id"]?.GetValue<int>() ?? 0),
            "update-work-item-state" => await WorkItemTools.UpdateWorkItemStateAsync(adoClient,
                args["id"]?.GetValue<int>() ?? 0,
                args["state"]?.GetValue<string>() ?? ""),
            "create-branch" => await BranchTools.CreateBranchAsync(adoClient,
                args["repositoryId"]?.GetValue<string>() ?? "",
                args["newBranchName"]?.GetValue<string>() ?? "",
                args["sourceBranch"]?.GetValue<string>() ?? "develop",
                args["project"]?.GetValue<string>() ?? "NAF Marketing"),
            "list-branches" => await BranchTools.ListBranchesAsync(adoClient,
                args["repositoryId"]?.GetValue<string>() ?? "",
                args["project"]?.GetValue<string>() ?? "NAF Marketing"),
            "create-pull-request" => await PullRequestTools.CreatePullRequestAsync(adoClient,
                args["repositoryId"]?.GetValue<string>() ?? "",
                args["sourceBranch"]?.GetValue<string>() ?? "",
                args["title"]?.GetValue<string>() ?? "",
                args["targetBranch"]?.GetValue<string>() ?? "develop",
                args["description"]?.GetValue<string>() ?? "",
                args["workItemId"] != null ? args["workItemId"]?.GetValue<int>() : null,
                args["project"]?.GetValue<string>() ?? "NAF Marketing"),
            "get-pull-request" => await PullRequestTools.GetPullRequestAsync(adoClient,
                args["repositoryId"]?.GetValue<string>() ?? "",
                args["pullRequestId"]?.GetValue<int>() ?? 0,
                args["project"]?.GetValue<string>() ?? "NAF Marketing"),
            "list-pull-requests" => await PullRequestTools.ListPullRequestsAsync(adoClient,
                args["repositoryId"]?.GetValue<string>() ?? "",
                args["status"]?.GetValue<string>() ?? "active",
                args["project"]?.GetValue<string>() ?? "NAF Marketing"),
            "list-repositories" => await RepositoryTools.ListRepositoriesAsync(adoClient,
                args["project"]?.GetValue<string>() ?? "NAF Marketing"),
            _ => new JsonObject { ["error"] = $"Unknown tool: {toolName}" }
        };
    }
    catch (Exception ex)
    {
        result = new JsonObject { ["error"] = $"Tool '{toolName}' failed: {ex.Message}" };
    }

    var content = new JsonArray
    {
        new JsonObject
        {
            ["type"] = "text",
            ["text"] = result.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
        }
    };

    return new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["result"] = new JsonObject { ["content"] = content }
    };
}

static JsonObject CreateErrorResponse(JsonNode? id, int code, string message) => new()
{
    ["jsonrpc"] = "2.0",
    ["id"] = id?.DeepClone(),
    ["error"] = new JsonObject { ["code"] = code, ["message"] = message }
};

static JsonObject CreateToolErrorResponse(JsonNode? id, string message)
{
    var content = new JsonArray
    {
        new JsonObject { ["type"] = "text", ["text"] = $"Error: {message}" }
    };
    return new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["result"] = new JsonObject { ["content"] = content }
    };
}
