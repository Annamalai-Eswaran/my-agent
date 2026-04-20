using System.Text.Json.Nodes;

namespace MyAgent.McpServer.AzureDevOps.Tools;

public static class WorkItemTools
{
    private static string? Field(IDictionary<string, object> fields, string key)
        => fields.TryGetValue(key, out var v) ? v?.ToString() : null;
    public static IEnumerable<ToolDefinition> GetDefinitions() =>
    [
        new ToolDefinition
        {
            Name = "list-ready-work-items",
            Description = "List all Azure DevOps work items assigned to the current user in 'Ready' state in the NAF Marketing project, ordered by priority.",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["project"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "The Azure DevOps project name. Defaults to 'NAF Marketing'."
                    }
                }
            }
        },
        new ToolDefinition
        {
            Name = "get-work-item",
            Description = "Fetch a single Azure DevOps work item by its numeric ID. Returns the title, state, assigned-to, description, and acceptance criteria.",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["id"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "The numeric Azure DevOps work item ID (e.g. 1234)."
                    }
                },
                ["required"] = new JsonArray("id")
            }
        },
        new ToolDefinition
        {
            Name = "update-work-item-state",
            Description = "Update the state of an Azure DevOps work item (e.g. Ready → Active → In Progress → Done).",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["id"] = new JsonObject { ["type"] = "integer", ["description"] = "Work item ID." },
                    ["state"] = new JsonObject { ["type"] = "string", ["description"] = "New state value (e.g. 'Active', 'In Progress', 'Done')." }
                },
                ["required"] = new JsonArray("id", "state")
            }
        }
    ];

    public static async Task<JsonObject> ListReadyWorkItemsAsync(AzureDevOpsClient client, string project = "NAF Marketing")
    {
        try
        {
            var witClient = client.GetWorkItemClient();
            var wiql = new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.Wiql
            {
                Query = $@"SELECT [System.Id], [System.Title], [System.State], [System.WorkItemType]
                           FROM WorkItems
                           WHERE [System.AssignedTo] = @Me
                             AND [System.State] = 'Ready'
                             AND [System.TeamProject] = '{project}'
                           ORDER BY [Microsoft.VSTS.Common.Priority] ASC"
            };

            var result = await witClient.QueryByWiqlAsync(wiql);
            var ids = result.WorkItems.Select(wi => wi.Id).ToArray();

            if (ids.Length == 0)
                return new JsonObject { ["workItems"] = new JsonArray(), ["count"] = 0 };

            var workItems = await witClient.GetWorkItemsAsync(ids,
                new[] { "System.Id", "System.Title", "System.State", "System.WorkItemType", "System.AssignedTo" });

            var arr = new JsonArray();
            foreach (var wi in workItems)
            {
                arr.Add(new JsonObject
                {
                    ["id"] = wi.Id,
                    ["title"] = Field(wi.Fields, "System.Title"),
                    ["state"] = Field(wi.Fields, "System.State"),
                    ["workItemType"] = Field(wi.Fields, "System.WorkItemType"),
                    ["assignedTo"] = wi.Fields.TryGetValue("System.AssignedTo", out var at) && at is Microsoft.VisualStudio.Services.WebApi.IdentityRef identity1
                        ? identity1.DisplayName : null
                });
            }
            return new JsonObject { ["workItems"] = arr, ["count"] = arr.Count };
        }
        catch (Exception ex)
        {
            return new JsonObject { ["error"] = $"Failed to list work items: {ex.Message}" };
        }
    }

    public static async Task<JsonObject> GetWorkItemAsync(AzureDevOpsClient client, int id)
    {
        try
        {
            var witClient = client.GetWorkItemClient();
            var wi = await witClient.GetWorkItemAsync(id,
                new[] { "System.Id", "System.Title", "System.State", "System.WorkItemType",
                        "System.AssignedTo", "System.Description", "Microsoft.VSTS.Common.AcceptanceCriteria" });

            return new JsonObject
            {
                ["id"] = wi.Id,
                ["title"] = Field(wi.Fields, "System.Title"),
                ["state"] = Field(wi.Fields, "System.State"),
                ["workItemType"] = Field(wi.Fields, "System.WorkItemType"),
                ["assignedTo"] = wi.Fields.TryGetValue("System.AssignedTo", out var at2) && at2 is Microsoft.VisualStudio.Services.WebApi.IdentityRef identity2
                    ? identity2.DisplayName : null,
                ["description"] = Field(wi.Fields, "System.Description"),
                ["acceptanceCriteria"] = Field(wi.Fields, "Microsoft.VSTS.Common.AcceptanceCriteria")
            };
        }
        catch (Exception ex)
        {
            return new JsonObject { ["error"] = $"Failed to get work item {id}: {ex.Message}" };
        }
    }

    public static async Task<JsonObject> UpdateWorkItemStateAsync(AzureDevOpsClient client, int id, string state)
    {
        try
        {
            var witClient = client.GetWorkItemClient();
            var patchDoc = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument
            {
                new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/System.State",
                    Value = state
                }
            };

            var wi = await witClient.UpdateWorkItemAsync(patchDoc, id);
            return new JsonObject
            {
                ["id"] = wi.Id,
                ["state"] = Field(wi.Fields, "System.State"),
                ["message"] = $"Work item {id} state updated to '{state}'"
            };
        }
        catch (Exception ex)
        {
            return new JsonObject { ["error"] = $"Failed to update work item {id}: {ex.Message}" };
        }
    }
}
