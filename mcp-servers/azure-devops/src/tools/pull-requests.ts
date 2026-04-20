import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { getConnection, getProject } from "../client.js";

/**
 * Register pull-request tools on the given MCP server.
 */
export function registerPullRequestTools(server: McpServer): void {
  // ── create-pull-request ─────────────────────────────────────────────────
  server.tool(
    "create-pull-request",
    "Create a pull request in Azure DevOps and link a work item",
    {
      repository_name: z.string().describe("Name of the Git repository"),
      source_branch: z.string().describe("Source branch (e.g. feature/ae/4521-fix-login-timeout)"),
      target_branch: z.string().default("develop").describe("Target branch (default: develop)"),
      title: z.string().describe("PR title — will be prefixed with [AB#{work_item_id}] automatically"),
      description: z.string().describe("PR description / body"),
      work_item_id: z.number().describe("Azure DevOps work item ID to link"),
    },
    async ({ repository_name, source_branch, target_branch, title, description, work_item_id }) => {
      const connection = getConnection();
      const project = getProject();
      const gitApi = await connection.getGitApi();

      // Ensure branch refs have the refs/heads/ prefix
      const toRef = (branch: string) =>
        branch.startsWith("refs/") ? branch : `refs/heads/${branch}`;

      const prTitle = `[AB#${work_item_id}] ${title}`;

      // Build the work item artifact link
      const orgUrl = (process.env.AZURE_DEVOPS_ORG_URL ?? "").replace(/\/$/, "");
      const artifactId = `vstfs:///WorkItemTracking/WorkItem/${work_item_id}`;

      const pr = await gitApi.createPullRequest(
        {
          title: prTitle,
          description,
          sourceRefName: toRef(source_branch),
          targetRefName: toRef(target_branch),
          workItemRefs: [{ id: String(work_item_id), url: `${orgUrl}/_workitems/edit/${work_item_id}` }],
          artifactId,
        } as never,
        repository_name,
        project
      );

      const prUrl = `${orgUrl}/${project}/_git/${repository_name}/pullrequest/${pr.pullRequestId}`;

      return {
        content: [
          {
            type: "text" as const,
            text: JSON.stringify(
              {
                pullRequestId: pr.pullRequestId,
                title: pr.title,
                status: pr.status,
                url: prUrl,
              },
              null,
              2
            ),
          },
        ],
      };
    }
  );
}
