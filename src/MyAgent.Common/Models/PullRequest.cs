namespace MyAgent.Common.Models;

public record PullRequest(int PullRequestId, string Title, string Status, string SourceBranch, string TargetBranch, string Url);
