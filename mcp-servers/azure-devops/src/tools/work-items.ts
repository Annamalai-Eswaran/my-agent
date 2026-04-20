import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { getConnection, getProject } from "../client.js";

/**
 * Register work-item tools on the given MCP server.
 */
export function registerWorkItemTools(server: McpServer): void {
  // ── list-ready-work-items ───────────────────────────────────────────────
  server.tool(
    "list-ready-work-items",
    "List work items assigned to me in Ready state in the configured Azure DevOps project",
    {},
    async () => {
      const connection = getConnection();
      const project = getProject();
      const witApi = await connection.getWorkItemTrackingApi();

      const wiql = {
        query: `
          SELECT [System.Id], [System.Title], [System.WorkItemType],
                 [Microsoft.VSTS.Common.Priority], [System.State]
          FROM WorkItems
          WHERE [System.AssignedTo] = @Me
            AND [System.State] = 'Ready'
            AND [System.TeamProject] = '${project}'
          ORDER BY [Microsoft.VSTS.Common.Priority] ASC
        `,
      };

      const queryResult = await witApi.queryByWiql(wiql, { project });
      const ids = (queryResult.workItems ?? []).map((wi) => wi.id!).filter(Boolean);

      if (ids.length === 0) {
        return { content: [{ type: "text" as const, text: "No ready work items found." }] };
      }

      const items = await witApi.getWorkItems(ids);
      const formatted = (items ?? []).map((item) => ({
        id: item.id,
        title: item.fields?.["System.Title"],
        type: item.fields?.["System.WorkItemType"],
        priority: item.fields?.["Microsoft.VSTS.Common.Priority"],
        state: item.fields?.["System.State"],
      }));

      return {
        content: [{ type: "text" as const, text: JSON.stringify(formatted, null, 2) }],
      };
    }
  );

  // ── get-work-item ───────────────────────────────────────────────────────
  server.tool(
    "get-work-item",
    "Get full details of a specific work item by ID",
    { id: z.number().describe("Azure DevOps work item ID") },
    async ({ id }) => {
      const connection = getConnection();
      const witApi = await connection.getWorkItemTrackingApi();
      // WorkItemExpand.All = 4
      const item = await witApi.getWorkItem(id, undefined, undefined, 4);

      return {
        content: [
          { type: "text" as const, text: JSON.stringify(item?.fields ?? {}, null, 2) },
        ],
      };
    }
  );

  // ── update-work-item-state ──────────────────────────────────────────────
  server.tool(
    "update-work-item-state",
    "Update the state of an Azure DevOps work item",
    {
      id: z.number().describe("Work item ID"),
      state: z.string().describe("New state value, e.g. 'Active' or 'In Progress'"),
    },
    async ({ id, state }) => {
      const connection = getConnection();
      const witApi = await connection.getWorkItemTrackingApi();

      const patchDoc = [
        { op: "replace", path: "/fields/System.State", value: state },
      ];

      await witApi.updateWorkItem(null as never, patchDoc as never, id);

      return {
        content: [
          { type: "text" as const, text: `Work item ${id} state updated to "${state}".` },
        ],
      };
    }
  );
}
