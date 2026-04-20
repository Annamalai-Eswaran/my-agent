---
applyTo: "**/*.json,**/*.yaml,**/*.yml,.env*"
---

# Configuration File Instructions

Follow these conventions for all configuration files in this repository.

## Secrets and PAT Tokens

- **Never commit secrets, PAT tokens, API keys, or passwords** to any config file.
- The `.env` file is git-ignored and is the only place secrets should be stored locally.
- If a config value is a secret, reference it via an environment variable name instead.

## Environment Variable Reference Pattern

- Use the `_ENV` suffix convention to indicate that a config value is read from an environment variable at runtime.
- Example:
  ```json
  {
    "azure_devops": {
      "pat_env": "AZURE_DEVOPS_PAT"
    }
  }
  ```
  This means: at runtime, read the PAT from the `AZURE_DEVOPS_PAT` environment variable.

## MCP Server Config (`config.json`)

- `config.json` is the **single source of truth** for all agent configuration.
- Every MCP server entry must include:
  - `command`: the executable to run (e.g., `"node"`, `"python"`)
  - `args`: array of arguments to pass to the command
  - `enabled`: boolean — set to `false` to disable without removing the entry
  - `env` (optional): key-value pairs of environment variables to pass to the server process

```json
{
  "mcpServers": {
    "azure-devops": {
      "command": "node",
      "args": ["mcp-servers/azure-devops/dist/index.js"],
      "enabled": true,
      "env": {
        "AZURE_DEVOPS_PAT": "${AZURE_DEVOPS_PAT}"
      }
    }
  }
}
```

## YAML / YML Files

- Use 2-space indentation for YAML files.
- Add comments to explain non-obvious configuration values.
- Do not use YAML anchors (`&`, `*`) unless necessary — they reduce readability.

## `.env` and `.env.example` Files

- The `applyTo` pattern above (`**/.env*`) applies to both `.env` and `.env.example`.
- `.env` is **git-ignored** — the only place real secrets may exist locally.
- `.env.example` is **committed** — it must contain **only placeholder values** (e.g., `AZURE_DEVOPS_PAT=your_pat_here`).
- Never fill in real values in `.env.example`.
- Document each variable with an inline comment explaining its purpose and where to obtain the value.
