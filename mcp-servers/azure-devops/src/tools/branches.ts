import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { getConnection, getProject } from "../client.js";

/**
 * Register branch tools on the given MCP server.
 */
export function registerBranchTools(server: McpServer): void {
  // ── create-branch ───────────────────────────────────────────────────────
  server.tool(
    "create-branch",
    "Create a new Git branch from a source branch in an Azure DevOps repository",
    {
      repository_name: z.string().describe("Name of the Git repository"),
      new_branch_name: z
        .string()
        .describe("Name of the new branch, e.g. feature/ae/4521-fix-login-timeout"),
      source_branch: z
        .string()
        .default("develop")
        .describe("Source branch to branch from (default: develop)"),
    },
    async ({ repository_name, new_branch_name, source_branch }) => {
      const connection = getConnection();
      const project = getProject();
      const gitApi = await connection.getGitApi();

      const toRef = (branch: string) =>
        branch.startsWith("refs/") ? branch : `refs/heads/${branch}`;

      // Get the latest commit SHA of the source branch
      const sourceRefName = toRef(source_branch);
      const refs = await gitApi.getRefs(repository_name, project, `heads/${source_branch}`);

      if (!refs || refs.length === 0) {
        throw new Error(
          `Source branch "${source_branch}" not found in repository "${repository_name}"`
        );
      }

      const sourceObjectId = refs[0].objectId!;
      const newRefName = toRef(new_branch_name);

      // Create the new ref pointing to the same commit as the source
      const refUpdates = [
        {
          name: newRefName,
          oldObjectId: "0000000000000000000000000000000000000000",
          newObjectId: sourceObjectId,
        },
      ];

      const results = await gitApi.updateRefs(refUpdates, repository_name, project);

      const success = results?.every((r) => r.success);
      const message = success
        ? `Branch "${new_branch_name}" created from "${source_branch}" in "${repository_name}".`
        : `Failed to create branch. Details: ${JSON.stringify(results)}`;

      return {
        content: [{ type: "text" as const, text: message }],
      };
    }
  );
}
