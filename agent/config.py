"""Load and resolve agent configuration."""

import json
import os
from pathlib import Path


def load_config() -> dict:
    """Read config.json from the agent directory and return a dict.

    Environment variables referenced in ``pat_env_var`` fields are resolved
    so downstream code can use the actual token values directly.
    """
    config_path = Path(__file__).parent / "config.json"
    with open(config_path, "r", encoding="utf-8") as fh:
        config = json.load(fh)

    # Resolve Azure DevOps PAT from environment
    ado_cfg = config.get("azure_devops", {})
    pat_env_var = ado_cfg.get("pat_env_var", "AZURE_DEVOPS_PAT")
    ado_cfg["pat"] = os.environ.get(pat_env_var, "")

    # Resolve PAT env vars declared inside mcp_servers entries
    for _name, server_cfg in config.get("mcp_servers", {}).items():
        env_section = server_cfg.get("env", {})
        for key, value in list(env_section.items()):
            if key.endswith("_ENV"):
                resolved_key = key[:-4]  # strip trailing _ENV
                env_section[resolved_key] = os.environ.get(value, "")

    return config
