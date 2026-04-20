"""AI Engineering Agent — main orchestrator."""

import asyncio
import json
import logging
import os
import sys

from dotenv import load_dotenv

from config import load_config
from human_loop import ask_human
from mcp_client import MCPClientManager

load_dotenv()

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s  %(levelname)-8s  %(name)s — %(message)s",
)
logger = logging.getLogger(__name__)

# ANSI helpers
_BOLD = "\033[1m"
_GREEN = "\033[92m"
_RESET = "\033[0m"

SYSTEM_PROMPT = """You are an AI software engineering agent. Your job is to automate the daily \
software engineering workflow for the human engineer.

## Workflow

1. **Check Azure DevOps** — Use the `list-ready-work-items` tool to fetch work items assigned \
to the current user in "Ready" state inside the "NAF Marketing" project.
2. **Present items** — Display the list and use `ask_human` to ask which work item to work on.
3. **Ask for the repository** — Use `ask_human` to ask which Git repository to work in.
4. **Create a feature branch** — Use the `create-branch` tool to create a branch named \
`feature/ae/{work_item_id}-{slugified_description}` from `develop`.
5. **Open VS Code** — Run `code /path/to/repo` via the terminal MCP to open the editor.
6. **Implement changes** — Read files with the filesystem MCP, reason about the required \
changes, and write code using the filesystem MCP tools.
7. **Raise a PR to Azure DevOps** — Use `create-pull-request` with:
   - `source_branch`: the feature branch you created
   - `target_branch`: `develop`
   - `title`: `[AB#{work_item_id}] {work_item_title}`
   - `work_item_id`: the Azure DevOps work item ID to link
8. **Ask the human whenever stuck** — Call `ask_human` with your question any time you are \
uncertain, need clarification, or need approval before making a destructive change.

## Important Rules

- Always confirm with the human before committing code or changing work item state.
- Never guess credentials or URLs — use `ask_human` if you need them.
- Keep the human informed of progress at each step.
- If an MCP tool call fails, report the error to the human and ask how to proceed.
"""


class EngineerAgent:
    """Orchestrates the agentic loop: LLM ↔ MCP tools ↔ human."""

    def __init__(self) -> None:
        self._config = load_config()
        self._mcp = MCPClientManager(self._config)
        self._messages: list[dict] = []
        self._all_tools: list[dict] = []

        # Import Anthropic lazily so startup errors are clear
        try:
            from anthropic import Anthropic  # type: ignore
        except ImportError as exc:
            sys.exit(f"anthropic package not installed — run: pip install anthropic\n{exc}")

        self._anthropic = Anthropic()
        llm_cfg = self._config.get("llm", {})
        self._model = llm_cfg.get("model", "claude-opus-4-5")

    async def _setup(self) -> None:
        """Connect to MCP servers and discover tools."""
        await self._mcp.connect_all()
        mcp_tools = await self._mcp.discover_tools()

        # Add the built-in ask_human tool
        ask_human_tool = {
            "name": "ask_human",
            "description": (
                "Ask the human engineer a question when you need clarification, approval, "
                "or are stuck. Use this tool freely — the human is always available."
            ),
            "input_schema": {
                "type": "object",
                "properties": {
                    "question": {
                        "type": "string",
                        "description": "The question or prompt to present to the human.",
                    }
                },
                "required": ["question"],
            },
        }
        self._all_tools = mcp_tools + [ask_human_tool]
        logger.info("Total tools available: %d", len(self._all_tools))

    async def _handle_tool_call(self, tool_name: str, tool_input: dict) -> str:
        """Dispatch a single tool call and return the string result."""
        if tool_name == "ask_human":
            return ask_human(tool_input.get("question", ""))

        print(f"  {_GREEN}🔧  {tool_name}{_RESET}({json.dumps(tool_input)[:120]}…)")
        try:
            return await self._mcp.call_tool(tool_name, tool_input)
        except Exception as exc:
            error_msg = f"Tool '{tool_name}' failed: {exc}"
            logger.error(error_msg)
            return error_msg

    async def run(self) -> None:
        """Main agentic loop."""
        await self._setup()

        # Seed the conversation
        self._messages = [
            {
                "role": "user",
                "content": "Start my workday. Check Azure DevOps for my ready work items.",
            }
        ]

        print(f"\n{_BOLD}{_GREEN}AI Engineering Agent started. Press Ctrl+C to quit.{_RESET}\n")

        try:
            while True:
                response = self._anthropic.messages.create(
                    model=self._model,
                    max_tokens=4096,
                    system=SYSTEM_PROMPT,
                    tools=self._all_tools,
                    messages=self._messages,
                )

                if response.stop_reason == "end_turn":
                    # Agent finished its turn — print any text and wait for user input
                    for block in response.content:
                        if hasattr(block, "text"):
                            print(f"\n{_BOLD}🤖  Agent:{_RESET} {block.text}")

                    self._messages.append(
                        {"role": "assistant", "content": response.content}
                    )

                    user_input = input(f"\n{_BOLD}👉  You (or 'quit'): {_RESET}").strip()
                    if user_input.lower() in {"quit", "exit", "q"}:
                        print("\nGoodbye! 👋")
                        break
                    self._messages.append({"role": "user", "content": user_input})

                elif response.stop_reason == "tool_use":
                    # Agent wants to call one or more tools
                    self._messages.append(
                        {"role": "assistant", "content": response.content}
                    )

                    tool_results = []
                    for block in response.content:
                        if block.type == "tool_use":
                            result_text = await self._handle_tool_call(
                                block.name, block.input
                            )
                            tool_results.append(
                                {
                                    "type": "tool_result",
                                    "tool_use_id": block.id,
                                    "content": result_text,
                                }
                            )

                    self._messages.append({"role": "user", "content": tool_results})

                else:
                    logger.warning("Unexpected stop_reason: %s", response.stop_reason)
                    break

        except KeyboardInterrupt:
            print("\n\nInterrupted — shutting down.")
        finally:
            await self._mcp.disconnect_all()


def main() -> None:
    asyncio.run(EngineerAgent().run())


if __name__ == "__main__":
    main()
