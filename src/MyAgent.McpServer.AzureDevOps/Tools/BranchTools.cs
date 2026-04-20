using Microsoft.TeamFoundation.SourceControl.WebApi;
using System.Text.Json.Nodes;

namespace MyAgent.McpServer.AzureDevOps.Tools;

public static class BranchTools
{
    public static IEnumerable<ToolDefinition> GetDefinitions() =>
    [
        new ToolDefinition
        {
            Name = "create-branch",
            Description = "Create a new Git branch in an Azure DevOps repository from a source branch (defaults to 'develop').",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["repositoryId"] = new JsonObject { ["type"] = "string", ["description"] = "The Azure DevOps Git repository ID or name." },
                    ["newBranchName"] = new JsonObject { ["type"] = "string", ["description"] = "Name of the new branch to create (e.g. 'feature/ae/1234-my-feature')." },
                    ["sourceBranch"] = new JsonObject { ["type"] = "string", ["description"] = "Source branch to create from. Defaults to 'develop'." },
                    ["project"] = new JsonObject { ["type"] = "string", ["description"] = "Azure DevOps project name. Defaults to 'NAF Marketing'." }
                },
                ["required"] = new JsonArray("repositoryId", "newBranchName")
            }
        },
        new ToolDefinition
        {
            Name = "list-branches",
            Description = "List all branches in an Azure DevOps Git repository.",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["repositoryId"] = new JsonObject { ["type"] = "string", ["description"] = "The repository ID or name." },
                    ["project"] = new JsonObject { ["type"] = "string", ["description"] = "Azure DevOps project name. Defaults to 'NAF Marketing'." }
                },
                ["required"] = new JsonArray("repositoryId")
            }
        }
    ];

    public static async Task<JsonObject> CreateBranchAsync(AzureDevOpsClient client,
        string repositoryId, string newBranchName, string sourceBranch = "develop", string project = "NAF Marketing")
    {
        try
        {
            var gitClient = client.GetGitClient();
            // GetRefsAsync(project, repositoryId, filter, includeLinks, includeStatuses, includeMyBranches, latestStatusesOnly, peelTags, filterContains, userState, ct)
            var refs = await gitClient.GetRefsAsync(project, repositoryId, null, null, null, null, null, null, sourceBranch, null, default);
            var sourceRef = refs.FirstOrDefault(r => r.Name == $"refs/heads/{sourceBranch}");

            if (sourceRef == null)
                return new JsonObject { ["error"] = $"Source branch '{sourceBranch}' not found." };

            var refUpdate = new GitRefUpdate
            {
                Name = $"refs/heads/{newBranchName}",
                NewObjectId = sourceRef.ObjectId,
                OldObjectId = new string('0', 40)
            };

            // UpdateRefsAsync(refUpdates, project, repositoryId, projectId, userState, ct)
            var result = await gitClient.UpdateRefsAsync(new[] { refUpdate }, project, repositoryId, null, null, default);
            var update = result.FirstOrDefault();

            if (update?.Success == true)
                return new JsonObject
                {
                    ["branchName"] = newBranchName,
                    ["sourceBranch"] = sourceBranch,
                    ["objectId"] = sourceRef.ObjectId,
                    ["message"] = $"Branch '{newBranchName}' created from '{sourceBranch}' successfully."
                };

            return new JsonObject { ["error"] = $"Failed to create branch: {update?.CustomMessage ?? "Unknown error"}" };
        }
        catch (Exception ex)
        {
            return new JsonObject { ["error"] = $"Failed to create branch '{newBranchName}': {ex.Message}" };
        }
    }

    public static async Task<JsonObject> ListBranchesAsync(AzureDevOpsClient client,
        string repositoryId, string project = "NAF Marketing")
    {
        try
        {
            var gitClient = client.GetGitClient();
            var refs = await gitClient.GetRefsAsync(project, repositoryId, null, null, null, null, null, null, "heads/", null, default);

            var arr = new JsonArray();
            foreach (var r in refs)
            {
                arr.Add(new JsonObject
                {
                    ["name"] = r.Name.Replace("refs/heads/", ""),
                    ["objectId"] = r.ObjectId,
                    ["creator"] = r.Creator?.DisplayName
                });
            }
            return new JsonObject { ["branches"] = arr, ["count"] = arr.Count };
        }
        catch (Exception ex)
        {
            return new JsonObject { ["error"] = $"Failed to list branches: {ex.Message}" };
        }
    }
}
