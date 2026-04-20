import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { getConnection, getProject } from "../client.js";

/**
 * Register repository tools on the given MCP server.
 */
export function registerRepositoryTools(server: McpServer): void {
  // ── list-repositories ───────────────────────────────────────────────────
  server.tool(
    "list-repositories",
    "List all Git repositories in the configured Azure DevOps project",
    {},
    async () => {
      const connection = getConnection();
      const project = getProject();
      const gitApi = await connection.getGitApi();

      const repos = await gitApi.getRepositories(project);

      const formatted = (repos ?? []).map((repo) => ({
        name: repo.name,
        id: repo.id,
        defaultBranch: repo.defaultBranch,
        remoteUrl: repo.remoteUrl,
      }));

      return {
        content: [{ type: "text" as const, text: JSON.stringify(formatted, null, 2) }],
      };
    }
  );
}
