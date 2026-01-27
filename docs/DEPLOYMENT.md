# Deployment Overview

This guide provides an overview of deployment options for the Copilot Adoption Bot Teams application. For detailed instructions, see the specific deployment guides linked below.

## Deployment Guides

| Guide | Description |
|-------|-------------|
| **[Manual Deployment](DEPLOYMENT-MANUAL.md)** | Step-by-step Azure resource setup and manual deployment |
| **[GitHub Actions](DEPLOYMENT-GITHUB-ACTIONS.md)** | Automated CI/CD using GitHub Actions with OIDC authentication |
| **[Azure DevOps](DEPLOYMENT-AZURE-DEVOPS.md)** | Automated CI/CD using Azure DevOps Pipelines |

> **Looking for local development setup?** See the [Development Environment Guide](DEVELOPMENT.md) for tools, secrets management, and tunneling setup.
>
> **Looking for all configuration options?** See the [Configuration Reference](CONFIGURATION.md) for a complete list of settings.

---

## Prerequisites

Before deploying, ensure you have:

- **Azure Subscription** with Contributor access to create resources
- **Microsoft 365 Tenant** with Teams enabled
- **Admin Consent** capability for Graph API application permissions
- **Teams Bot** created in the [Teams Developer Portal](https://dev.teams.microsoft.com/) (see [Setup Guide](SETUP.md))

---

## Azure Resources Required

### Required Resources

| Resource | SKU/Tier | Purpose |
|----------|----------|---------|
| **Azure App Service** | B1 or higher | Host the .NET web application |
| **App Service Plan** | Basic or higher | Compute for App Service |
| **Azure Storage Account** | Standard LRS | Table Storage (metadata) + Blob Storage (JSON payloads) |
| **Azure AD App Registration** | N/A | Authentication and bot identity |

### Optional Resources

| Resource | SKU/Tier | Purpose |
|----------|----------|---------|
| **Azure Key Vault** | Standard | Secure secret storage (recommended for production) |
| **Application Insights** | Basic | Telemetry, logging, and monitoring |
| **Azure AI Foundry** | Pay-as-you-go | AI-powered bot conversations (Copilot Connected mode) |

### Storage Account Structure

The storage account requires both Table Storage and Blob Storage:

```
Storage Account
├── Table Storage
│   ├── MessageTemplates (template metadata)
│   └── MessageLogs (delivery tracking)
└── Blob Storage
    └── message-templates (container for JSON payloads)
```

> **Note**: The `message-templates` blob container is automatically created by the application on first run.

---

## Storage Authentication Options

The application supports two authentication methods for Azure Storage:

### Option 1: RBAC with Managed Identity (Recommended)

Uses Azure role-based access control with no storage keys in configuration.

**Configuration:**
```json
{
  "StorageAuthConfig": {
    "UseRBAC": true,
    "StorageAccountName": "yourstorageaccount"
  }
}
```

**Required RBAC Role Assignments:**

| Role | Purpose |
|------|---------|
| `Storage Blob Data Contributor` | Blob storage access |
| `Storage Table Data Contributor` | Table storage access |
| `Storage Queue Data Contributor` | Queue storage access |

**Azure CLI Commands:**
```bash
# Get your App Service managed identity principal ID
APP_PRINCIPAL_ID=$(az webapp identity show \
  --name myAppName \
  --resource-group myResourceGroup \
  --query principalId -o tsv)

# Get storage account ID
STORAGE_ID=$(az storage account show \
  --name mystorageaccount \
  --resource-group myResourceGroup \
  --query id -o tsv)

# Assign roles to the managed identity
az role assignment create \
  --assignee $APP_PRINCIPAL_ID \
  --role "Storage Blob Data Contributor" \
  --scope $STORAGE_ID

az role assignment create \
  --assignee $APP_PRINCIPAL_ID \
  --role "Storage Table Data Contributor" \
  --scope $STORAGE_ID

az role assignment create \
  --assignee $APP_PRINCIPAL_ID \
  --role "Storage Queue Data Contributor" \
  --scope $STORAGE_ID
```

### Option 2: Connection String (Legacy)

Uses storage account access keys. Less secure but simpler to set up.

**Configuration:**
```json
{
  "ConnectionStrings": {
    "Storage": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=..."
  }
}
```

---

## Azure AI Foundry Configuration (Optional)

For AI-powered bot conversations, you need an Azure AI Foundry deployment:

1. **Create an Azure AI Foundry resource** in the Azure Portal
2. **Deploy a model** (e.g., GPT-4o, GPT-4o-mini)
3. **Note the following values** for configuration:
   - Endpoint URL (e.g., `https://your-resource.openai.azure.com/`)
   - Deployment name
   - API key

**Configuration:**
```json
{
  "AIFoundryConfig": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini",
    "ApiKey": "your-api-key",
    "MaxTokens": 2000,
    "Temperature": "0.7"
  }
}
```

---

## Estimated Costs

| Resource | Estimated Monthly Cost |
|----------|----------------------|
| App Service (B1) | ~$13 USD |
| Storage Account (minimal usage) | ~$1-5 USD |
| Application Insights (basic) | Free tier available |
| Azure AI Foundry | Pay-per-token (varies by usage) |

**Total**: ~$15-25 USD/month for basic deployment (excluding AI Foundry usage)

---

## Azure Key Vault Integration

For production deployments, store all secrets in Azure Key Vault.

### 1. Create Azure Key Vault

```bash
az keyvault create \
  --name my-copilot-bot-kv \
  --resource-group myResourceGroup \
  --location eastus
```

### 2. Add Secrets

```bash
# Required secrets
az keyvault secret set --vault-name my-copilot-bot-kv \
  --name GraphClientSecret --value "<your-client-secret>"
az keyvault secret set --vault-name my-copilot-bot-kv \
  --name BotAppPassword --value "<your-bot-password>"

# Optional secrets
az keyvault secret set --vault-name my-copilot-bot-kv \
  --name StorageConnectionString --value "<your-connection-string>"
az keyvault secret set --vault-name my-copilot-bot-kv \
  --name ApplicationInsightsConnectionString --value "<your-appinsights-connection-string>"
az keyvault secret set --vault-name my-copilot-bot-kv \
  --name AIFoundryApiKey --value "<your-ai-foundry-api-key>"
```

### 3. Enable Managed Identity

```bash
az webapp identity assign \
  --resource-group myResourceGroup \
  --name myAppName
```

### 4. Grant Key Vault Access

```bash
# Get the principal ID from the previous command output
PRINCIPAL_ID=$(az webapp identity show \
  --name myAppName \
  --resource-group myResourceGroup \
  --query principalId -o tsv)

az keyvault set-policy \
  --name my-copilot-bot-kv \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list
```

### 5. Reference Secrets in App Settings

In the Azure Portal, configure your App Service application settings:

```
GraphConfig__ClientSecret=@Microsoft.KeyVault(SecretUri=https://my-copilot-bot-kv.vault.azure.net/secrets/GraphClientSecret/)
MicrosoftAppPassword=@Microsoft.KeyVault(SecretUri=https://my-copilot-bot-kv.vault.azure.net/secrets/BotAppPassword/)
APPLICATIONINSIGHTS_CONNECTION_STRING=@Microsoft.KeyVault(SecretUri=https://my-copilot-bot-kv.vault.azure.net/secrets/ApplicationInsightsConnectionString/)
AIFoundryConfig__ApiKey=@Microsoft.KeyVault(SecretUri=https://my-copilot-bot-kv.vault.azure.net/secrets/AIFoundryApiKey/)
```

---

## Post-Deployment Configuration

After deploying the application, complete these steps:

### 1. Configure Bot Messaging Endpoint

1. Go to the [Teams Developer Portal](https://dev.teams.microsoft.com/)
2. Navigate to **Tools** → **Bot management**
3. Click on your bot
4. Under **Configure** → **Endpoint address**, enter:
   ```
   https://your-app-name.azurewebsites.net/api/messages
   ```
5. Click **Save**

### 2. Verify Application Health

| Endpoint | Purpose |
|----------|---------|
| `https://your-app-name.azurewebsites.net` | Main application |
| `https://your-app-name.azurewebsites.net/swagger` | API documentation |
| `https://your-app-name.azurewebsites.net/api/Diagnostics/TestGraphConnection` | Graph connectivity test |

### 3. Configure Application Insights (Optional)

If configured, set up monitoring in the Azure Portal:
- Enable **Live Metrics** for real-time monitoring
- Review **Failures** for any deployment issues
- Set up **Alerts** for critical errors

---

## Next Steps

Choose your deployment method:

- **[Manual Deployment](DEPLOYMENT-MANUAL.md)** - Best for learning and small deployments
- **[GitHub Actions](DEPLOYMENT-GITHUB-ACTIONS.md)** - Best for GitHub-hosted repositories
- **[Azure DevOps](DEPLOYMENT-AZURE-DEVOPS.md)** - Best for enterprise environments using Azure DevOps

---

## Troubleshooting

### Common Deployment Issues

| Issue | Solution |
|-------|----------|
| Build fails on Node.js step | Ensure Node.js 18+ is being used and `package-lock.json` is committed |
| Build fails on .NET step | Ensure .NET 10 SDK is available and solution builds locally |
| Azure deployment fails with 401/403 | Verify service principal permissions and credentials |
| App starts but bot doesn't respond | Check messaging endpoint configuration and Application Insights logs |

See the [Troubleshooting Guide](TROUBLESHOOTING.md) for additional guidance.
