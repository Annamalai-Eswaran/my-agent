import * as azdev from "azure-devops-node-api";

/**
 * Create and return an authenticated Azure DevOps WebApi connection.
 * Reads credentials from environment variables:
 *   - AZURE_DEVOPS_ORG_URL  — e.g. https://dev.azure.com/NAF-Tech/
 *   - AZURE_DEVOPS_PAT      — personal access token
 */
export function getConnection(): azdev.WebApi {
  const orgUrl = process.env.AZURE_DEVOPS_ORG_URL;
  const pat = process.env.AZURE_DEVOPS_PAT;

  if (!orgUrl) {
    throw new Error("AZURE_DEVOPS_ORG_URL environment variable is not set");
  }
  if (!pat) {
    throw new Error("AZURE_DEVOPS_PAT environment variable is not set");
  }

  const authHandler = azdev.getPersonalAccessTokenHandler(pat);
  return new azdev.WebApi(orgUrl, authHandler);
}

/**
 * Return the default Azure DevOps project name.
 * Uses AZURE_DEVOPS_PROJECT env var or falls back to "NAF Marketing".
 */
export function getProject(): string {
  return process.env.AZURE_DEVOPS_PROJECT ?? "NAF Marketing";
}
