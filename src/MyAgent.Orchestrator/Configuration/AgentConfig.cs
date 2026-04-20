using MyAgent.Orchestrator.Mcp;

namespace MyAgent.Orchestrator.Configuration;

public class AgentConfig
{
    public LlmConfig Llm { get; set; } = new();
    public AzureDevOpsConfig AzureDevOps { get; set; } = new();
    public BranchingConfig Branching { get; set; } = new();
    public PullRequestConfig PullRequest { get; set; } = new();
    public Dictionary<string, McpServerConfig> McpServers { get; set; } = new();
    public string ReposBasePath { get; set; } = "/home/user/repos";
}

public class LlmConfig
{
    public string Provider { get; set; } = "copilot";
    public string Model { get; set; } = "claude-opus-4-5";
}

public class AzureDevOpsConfig
{
    public string OrgUrl { get; set; } = "https://dev.azure.com/NAF-Tech/";
    public string DefaultProject { get; set; } = "NAF Marketing";
    public string PatEnvVar { get; set; } = "AZURE_DEVOPS_PAT";
}

public class BranchingConfig
{
    public string BaseBranch { get; set; } = "develop";
    public string Prefix { get; set; } = "feature/ae";
    public string Pattern { get; set; } = "feature/ae/{WorkItemId}-{SlugifiedDescription}";
}

public class PullRequestConfig
{
    public string TargetBranch { get; set; } = "develop";
    public string TitlePattern { get; set; } = "[AB#{WorkItemId}] {WorkItemTitle}";
    public bool AutoLinkWorkItem { get; set; } = true;
}
