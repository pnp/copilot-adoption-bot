# Troubleshooting Guide

Common issues and solutions for the Copilot Adoption Bot. Each section is now a dedicated page under [`troubleshooting/`](troubleshooting/).

> **Looking for deployment-method-specific issues?** See also:
> - [Manual Deployment troubleshooting](DEPLOYMENT-MANUAL.md)
> - [GitHub Actions troubleshooting](DEPLOYMENT-GITHUB-ACTIONS.md)
> - [Azure DevOps troubleshooting](DEPLOYMENT-AZURE-DEVOPS.md)
> - [Copilot CLI troubleshooting](DEPLOYMENT-COPILOT-CLI.md#troubleshooting)
>
> **Setting issues?** Check the [Configuration Reference cheat sheet](CONFIGURATION.md#cheat-sheet) for the exact key names.

## Sections

| Topic | When to read |
|-------|--------------|
| [Authentication Issues](troubleshooting/authentication.md) | Cannot log in, invalid client secret, AADSTS errors, redirect URI errors |
| [Microsoft Graph Errors](troubleshooting/microsoft-graph.md) | "Insufficient privileges", tenant not found, missing scopes |
| [Bot Connection Issues](troubleshooting/bot-connection.md) | Bot not responding in Teams, installation fails |
| [Storage Issues](troubleshooting/storage.md) | Cannot connect to storage, table/blob/queue errors, RBAC issues, `TableBeingDeleted` |
| [Deployment Issues](troubleshooting/deployment.md) | Deployment fails, App Service won't start |
| [Copilot Stats Issues](troubleshooting/copilot-stats.md) | Missing usage data, rate limits, **how the stats cache works and why it can lag** |
| [Smart Groups](troubleshooting/smart-groups.md) | Resolution is slow, members look stale, **how the smart group cache works** |
| [Performance Issues](troubleshooting/performance.md) | Slow user cache updates, high memory, slow API responses |
| [Getting Help](troubleshooting/getting-help.md) | Diagnostic endpoints, logs to check, enabling detailed logging |
| [Common Error Codes](troubleshooting/common-error-codes.md) | Quick reference table of error codes and fixes |

## Next Steps

If you're still experiencing issues after trying these solutions:

1. Review the [Setup Guide](SETUP.md) to ensure correct configuration
2. Check the [Security Guide](SECURITY.md) for security-related issues
3. Review Application Insights logs for detailed error messages
4. Open a GitHub issue with:
   - Error messages
   - Steps to reproduce
   - Configuration (without secrets)
   - Logs (sanitized)
