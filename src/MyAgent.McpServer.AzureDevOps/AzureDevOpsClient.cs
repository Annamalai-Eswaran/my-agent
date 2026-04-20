using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace MyAgent.McpServer.AzureDevOps;

public class AzureDevOpsClient
{
    private readonly VssConnection _connection;

    public AzureDevOpsClient(string orgUrl, string pat)
    {
        var credentials = new VssBasicCredential(string.Empty, pat);
        _connection = new VssConnection(new Uri(orgUrl), credentials);
    }

    public WorkItemTrackingHttpClient GetWorkItemClient()
        => _connection.GetClient<WorkItemTrackingHttpClient>();

    public GitHttpClient GetGitClient()
        => _connection.GetClient<GitHttpClient>();
}
