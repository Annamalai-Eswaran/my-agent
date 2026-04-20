"""MCP client manager — connects to all configured MCP servers."""

import logging
import os
from contextlib import AsyncExitStack
from typing import Any

from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

logger = logging.getLogger(__name__)


class MCPClientManager:
    """Manages connections to one or more MCP servers.

    Usage::

        manager = MCPClientManager(config)
        await manager.connect_all()
        tools = await manager.discover_tools()
        result = await manager.call_tool("list-ready-work-items", {"project": "NAF Marketing"})
        await manager.disconnect_all()
    """

    def __init__(self, config: dict) -> None:
        self._config = config
        self._sessions: dict[str, ClientSession] = {}
        self._tool_to_session: dict[str, ClientSession] = {}
        self._tools: list[dict] = []
        self._exit_stack = AsyncExitStack()

    async def connect_all(self) -> None:
        """Spawn every enabled MCP server and initialise its session."""
        servers: dict[str, dict] = self._config.get("mcp_servers", {})
        for name, server_cfg in servers.items():
            if not server_cfg.get("enabled", True):
                logger.info("Skipping disabled MCP server: %s", name)
                continue

            env = {**os.environ}
            for key, value in server_cfg.get("env", {}).items():
                env[key] = value

            params = StdioServerParameters(
                command=server_cfg["command"],
                args=server_cfg.get("args", []),
                env=env,
            )

            try:
                read, write = await self._exit_stack.enter_async_context(
                    stdio_client(params)
                )
                session = await self._exit_stack.enter_async_context(
                    ClientSession(read, write)
                )
                await session.initialize()
                self._sessions[name] = session
                logger.info("Connected to MCP server: %s", name)
            except Exception:
                logger.exception("Failed to connect to MCP server: %s", name)

    async def discover_tools(self) -> list[dict]:
        """Gather tool definitions from all connected sessions.

        Returns:
            A list of tool dicts compatible with the Anthropic ``tools`` parameter.
        """
        self._tools = []
        self._tool_to_session = {}

        for name, session in self._sessions.items():
            try:
                response = await session.list_tools()
                for tool in response.tools:
                    self._tools.append(
                        {
                            "name": tool.name,
                            "description": tool.description or "",
                            "input_schema": tool.inputSchema,
                        }
                    )
                    self._tool_to_session[tool.name] = session
                    logger.debug("Discovered tool '%s' from server '%s'", tool.name, name)
            except Exception:
                logger.exception("Failed to list tools from MCP server: %s", name)

        return self._tools

    async def call_tool(self, tool_name: str, arguments: dict[str, Any]) -> str:
        """Route a tool call to the correct MCP session.

        Args:
            tool_name: The name of the tool to invoke.
            arguments: Keyword arguments for the tool.

        Returns:
            The text content of the tool result.

        Raises:
            KeyError: If no session is registered for *tool_name*.
        """
        if tool_name not in self._tool_to_session:
            raise KeyError(f"Unknown tool: {tool_name!r}")

        session = self._tool_to_session[tool_name]
        result = await session.call_tool(tool_name, arguments)

        parts = []
        for content in result.content:
            if hasattr(content, "text"):
                parts.append(content.text)
        return "\n".join(parts)

    async def disconnect_all(self) -> None:
        """Cleanly close all MCP server connections."""
        await self._exit_stack.aclose()
        self._sessions.clear()
        self._tool_to_session.clear()
        logger.info("Disconnected from all MCP servers")
