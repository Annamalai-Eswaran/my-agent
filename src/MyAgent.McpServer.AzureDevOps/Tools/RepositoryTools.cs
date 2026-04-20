using System.Text.Json.Nodes;

namespace MyAgent.McpServer.AzureDevOps.Tools;

public static class RepositoryTools
{
    public static IEnumerable<ToolDefinition> GetDefinitions() =>
    [
        new ToolDefinition
        {
            Name = "list-repositories",
            Description = "List all Git repositories in the Azure DevOps project.",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["project"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Azure DevOps project name. Defaults to 'NAF Marketing'."
                    }
                }
            }
        }
    ];

    public static async Task<JsonObject> ListRepositoriesAsync(AzureDevOpsClient client, string project = "NAF Marketing")
    {
        try
        {
            var gitClient = client.GetGitClient();
            var repos = await gitClient.GetRepositoriesAsync(project, null, null, default);

            var arr = new JsonArray();
            foreach (var r in repos)
            {
                arr.Add(new JsonObject
                {
                    ["id"] = r.Id.ToString(),
                    ["name"] = r.Name,
                    ["defaultBranch"] = r.DefaultBranch?.Replace("refs/heads/", ""),
                    ["remoteUrl"] = r.RemoteUrl
                });
            }
            return new JsonObject { ["repositories"] = arr, ["count"] = arr.Count };
        }
        catch (Exception ex)
        {
            return new JsonObject { ["error"] = $"Failed to list repositories: {ex.Message}" };
        }
    }
}
