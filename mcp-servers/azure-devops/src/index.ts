import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { registerWorkItemTools } from "./tools/work-items.js";
import { registerPullRequestTools } from "./tools/pull-requests.js";
import { registerRepositoryTools } from "./tools/repositories.js";
import { registerBranchTools } from "./tools/branches.js";

const server = new McpServer({
  name: "azure-devops",
  version: "1.0.0",
});

registerWorkItemTools(server);
registerPullRequestTools(server);
registerRepositoryTools(server);
registerBranchTools(server);

const transport = new StdioServerTransport();
await server.connect(transport);
