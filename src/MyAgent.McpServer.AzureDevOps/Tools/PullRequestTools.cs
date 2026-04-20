using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using System.Text.Json.Nodes;

namespace MyAgent.McpServer.AzureDevOps.Tools;

public static class PullRequestTools
{
    public static IEnumerable<ToolDefinition> GetDefinitions() =>
    [
        new ToolDefinition
        {
            Name = "create-pull-request",
            Description = "Create a pull request in Azure DevOps Git. Links the specified work item. Title format: [AB#{workItemId}] {title}.",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["repositoryId"] = new JsonObject { ["type"] = "string", ["description"] = "Azure DevOps Git repository ID or name." },
                    ["sourceBranch"] = new JsonObject { ["type"] = "string", ["description"] = "Source branch (feature branch)." },
                    ["targetBranch"] = new JsonObject { ["type"] = "string", ["description"] = "Target branch. Defaults to 'develop'." },
                    ["title"] = new JsonObject { ["type"] = "string", ["description"] = "PR title. Use format: [AB#{workItemId}] {description}." },
                    ["description"] = new JsonObject { ["type"] = "string", ["description"] = "PR description/body." },
                    ["workItemId"] = new JsonObject { ["type"] = "integer", ["description"] = "Azure DevOps work item ID to link to this PR." },
                    ["project"] = new JsonObject { ["type"] = "string", ["description"] = "Azure DevOps project name. Defaults to 'NAF Marketing'." }
                },
                ["required"] = new JsonArray("repositoryId", "sourceBranch", "title")
            }
        },
        new ToolDefinition
        {
            Name = "get-pull-request",
            Description = "Get details of an Azure DevOps pull request by ID.",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["repositoryId"] = new JsonObject { ["type"] = "string", ["description"] = "Repository ID or name." },
                    ["pullRequestId"] = new JsonObject { ["type"] = "integer", ["description"] = "Pull request ID." },
                    ["project"] = new JsonObject { ["type"] = "string", ["description"] = "Azure DevOps project name." }
                },
                ["required"] = new JsonArray("repositoryId", "pullRequestId")
            }
        },
        new ToolDefinition
        {
            Name = "list-pull-requests",
            Description = "List pull requests in an Azure DevOps Git repository.",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["repositoryId"] = new JsonObject { ["type"] = "string", ["description"] = "Repository ID or name." },
                    ["status"] = new JsonObject { ["type"] = "string", ["description"] = "Filter by status: active, completed, abandoned. Defaults to 'active'." },
                    ["project"] = new JsonObject { ["type"] = "string", ["description"] = "Azure DevOps project name." }
                },
                ["required"] = new JsonArray("repositoryId")
            }
        }
    ];

    public static async Task<JsonObject> CreatePullRequestAsync(AzureDevOpsClient client,
        string repositoryId, string sourceBranch, string title,
        string targetBranch = "develop", string description = "",
        int? workItemId = null, string project = "NAF Marketing")
    {
        try
        {
            var gitClient = client.GetGitClient();
            var pr = new GitPullRequest
            {
                Title = title,
                Description = description,
                SourceRefName = $"refs/heads/{sourceBranch}",
                TargetRefName = $"refs/heads/{targetBranch}"
            };

            if (workItemId.HasValue)
            {
                pr.WorkItemRefs = new[]
                {
                    new ResourceRef { Id = workItemId.Value.ToString() }
                };
            }

            // CreatePullRequestAsync(gitPullRequest, project, repositoryId, supportsIterations, userState, ct)
            var created = await gitClient.CreatePullRequestAsync(pr, project, repositoryId, null, null, default);
            return new JsonObject
            {
                ["pullRequestId"] = created.PullRequestId,
                ["title"] = created.Title,
                ["status"] = created.Status.ToString(),
                ["sourceBranch"] = sourceBranch,
                ["targetBranch"] = targetBranch,
                ["url"] = created.Url,
                ["message"] = $"Pull request #{created.PullRequestId} created successfully."
            };
        }
        catch (Exception ex)
        {
            return new JsonObject { ["error"] = $"Failed to create pull request: {ex.Message}" };
        }
    }

    public static async Task<JsonObject> GetPullRequestAsync(AzureDevOpsClient client,
        string repositoryId, int pullRequestId, string project = "NAF Marketing")
    {
        try
        {
            var gitClient = client.GetGitClient();
            // GetPullRequestAsync(project, repositoryId, pullRequestId, ...)
            var pr = await gitClient.GetPullRequestAsync(project, repositoryId, pullRequestId, null, null, null, null, null, null, default);
            return new JsonObject
            {
                ["pullRequestId"] = pr.PullRequestId,
                ["title"] = pr.Title,
                ["status"] = pr.Status.ToString(),
                ["sourceBranch"] = pr.SourceRefName?.Replace("refs/heads/", ""),
                ["targetBranch"] = pr.TargetRefName?.Replace("refs/heads/", ""),
                ["createdBy"] = pr.CreatedBy?.DisplayName,
                ["url"] = pr.Url
            };
        }
        catch (Exception ex)
        {
            return new JsonObject { ["error"] = $"Failed to get pull request {pullRequestId}: {ex.Message}" };
        }
    }

    public static async Task<JsonObject> ListPullRequestsAsync(AzureDevOpsClient client,
        string repositoryId, string status = "active", string project = "NAF Marketing")
    {
        try
        {
            var gitClient = client.GetGitClient();
            var prStatus = status.ToLower() switch
            {
                "completed" => PullRequestStatus.Completed,
                "abandoned" => PullRequestStatus.Abandoned,
                _ => PullRequestStatus.Active
            };

            // GetPullRequestsAsync(project, repositoryId, searchCriteria, ...)
            var prs = await gitClient.GetPullRequestsAsync(project, repositoryId,
                new GitPullRequestSearchCriteria { Status = prStatus }, null, null, null, null, default);

            var arr = new JsonArray();
            foreach (var pr in prs)
            {
                arr.Add(new JsonObject
                {
                    ["pullRequestId"] = pr.PullRequestId,
                    ["title"] = pr.Title,
                    ["status"] = pr.Status.ToString(),
                    ["sourceBranch"] = pr.SourceRefName?.Replace("refs/heads/", ""),
                    ["targetBranch"] = pr.TargetRefName?.Replace("refs/heads/", ""),
                    ["createdBy"] = pr.CreatedBy?.DisplayName
                });
            }
            return new JsonObject { ["pullRequests"] = arr, ["count"] = arr.Count };
        }
        catch (Exception ex)
        {
            return new JsonObject { ["error"] = $"Failed to list pull requests: {ex.Message}" };
        }
    }
}
