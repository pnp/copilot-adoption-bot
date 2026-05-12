# Deployment Overview

This guide provides an overview of deployment options for the Copilot Adoption Bot Teams application. For detailed instructions, see the specific deployment guides linked below.

## Choose Your Deployment Path

Pick the guide that matches how you work:

| Guide | Best for | Automation | Recommended when |
|-------|----------|-----------|------------------|
| **[Copilot CLI](DEPLOYMENT-COPILOT-CLI.md)** | One-off / first-time deployments from your terminal | Fully agent-driven | You want a guided walkthrough, or you have an existing `deployment-config.json` |
| **[Manual Deployment](DEPLOYMENT-MANUAL.md)** | Learning the moving parts, locked-down environments | Hand-run `az` commands | You can't install Copilot CLI, or you want full visibility of every step |
| **[GitHub Actions](DEPLOYMENT-GITHUB-ACTIONS.md)** | Teams using GitHub for source control | Push-to-deploy CI/CD | Code lives on GitHub and you want automated rollouts on `main` |
| **[Azure DevOps](DEPLOYMENT-AZURE-DEVOPS.md)** | Teams using Azure DevOps for source control | Pipeline-based CI/CD | Code lives in Azure Repos / you already use Azure Pipelines |

> First time deploying? Start with **Copilot CLI** if you have it installed, otherwise **Manual Deployment**. Move to **GitHub Actions** or **Azure DevOps** once you want repeatable rollouts.

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

The storage account requires Table Storage, Blob Storage **and Queue Storage**:

```
Storage Account
├── Table Storage
│   ├── messagetemplates       (template metadata)
│   ├── messagebatches         (recipient batch metadata)
│   ├── messagelogs            (per-recipient delivery tracking)
│   ├── ConversationCache      (bot conversation references)
│   ├── usercache              (cached user / Copilot data)
│   ├── usersyncmetadata       (delta-sync watermark)
│   ├── smartgroups            (AI-resolved group definitions)
│   ├── smartgroupmembers      (cached members per smart group)
│   └── appsettings            (key/value app settings)
├── Blob Storage
│   └── message-templates      (container for adaptive-card JSON payloads)
└── Queue Storage
    └── batch-messages         (queue used by the background sender)
```

> **Note**: All containers, tables and queues are automatically created by the application on first run.

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
```powershell
# Get your App Service managed identity principal ID
$APP_PRINCIPAL_ID = az webapp identity show `
  --name myAppName `
  --resource-group myResourceGroup `
  --query principalId -o tsv

# Get storage account ID
$STORAGE_ID = az storage account show `
  --name mystorageaccount `
  --resource-group myResourceGroup `
  --query id -o tsv

# Assign roles to the managed identity
az role assignment create `
  --assignee $APP_PRINCIPAL_ID `
  --role "Storage Blob Data Contributor" `
  --scope $STORAGE_ID

az role assignment create `
  --assignee $APP_PRINCIPAL_ID `
  --role "Storage Table Data Contributor" `
  --scope $STORAGE_ID

az role assignment create `
  --assignee $APP_PRINCIPAL_ID `
  --role "Storage Queue Data Contributor" `
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
4. **Grant the App Service Managed Identity (or your local identity) access** to the AI Foundry resource. Azure AI Foundry is configured to use **Azure RBAC only** - API key authentication is not supported. Assign a role such as:
   - `Cognitive Services OpenAI User` (data-plane access to call the deployed model), or
   - `Azure AI Developer`

### Assigning the AI Foundry Role

These commands target the App Service Managed Identity (for production). Run them in **PowerShell** with the **Azure CLI (`az`)** installed and signed in (`az login`). Make sure you've enabled the Managed Identity first - see [Enable Managed Identity](#3-enable-managed-identity) below.

```powershell
# Get the App Service Managed Identity principal ID
$APP_PRINCIPAL_ID = az webapp identity show `
  --name myAppName `
  --resource-group myResourceGroup `
  --query principalId -o tsv

# Get the AI Foundry (Azure OpenAI / Cognitive Services) resource ID
$FOUNDRY_ID = az cognitiveservices account show `
  --name yourFoundryResource `
  --resource-group yourResourceGroup `
  --query id -o tsv

# Assign the data-plane role
az role assignment create `
  --assignee $APP_PRINCIPAL_ID `
  --role "Cognitive Services OpenAI User" `
  --scope $FOUNDRY_ID
```

> **Local development / service principal**: If you're running locally with `az login`, or using `AIFoundryConfig:RBACOverrideCredentials` with a service principal, assign the same role to that identity instead. See [SETUP.md → Assigning RBAC Roles to Azure AI Foundry](SETUP.md#assigning-rbac-roles-to-azure-ai-foundry-optional---for-copilot-connected-mode) for the local-dev variant.

> **Note**: Role assignments can take up to 5 minutes to propagate. If you see `401`/`403` errors from AI Foundry immediately after assignment, wait a few minutes.

**Configuration:**
```json
{
  "AIFoundryConfig": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini",
    "MaxTokens": 2000,
    "Temperature": "0.7"
  }
}
```

---

## Estimated Costs

| Resource | SKU | Estimated Monthly Cost |
|----------|-----|----------------------|
| App Service Plan | B1 (Windows or Linux) | ~$13 USD |
| Storage Account | Standard LRS, minimal usage | ~$1-5 USD |
| Application Insights | Basic / pay-as-you-go | Free tier available |
| Azure AI Foundry | Pay-per-token | Varies by usage |

**Total**: ~$15-25 USD/month for basic deployment (excluding AI Foundry usage).

> **Sizing note**: B1 is fine for a few hundred users and the background sender. For
> tenant-wide rollouts (10k+ users with periodic delta syncs) prefer **B2** (3.5 GB RAM)
> or **P1v3** so the user cache sync and the parallel batch sender do not contend for memory.

---

## Azure Key Vault Integration

For production deployments, store all secrets in Azure Key Vault.

### 1. Create Azure Key Vault

```powershell
az keyvault create `
  --name my-copilot-bot-kv `
  --resource-group myResourceGroup `
  --location eastus
```

### 2. Add Secrets

```powershell
# Required secrets
az keyvault secret set --vault-name my-copilot-bot-kv `
  --name GraphClientSecret --value "<your-client-secret>"
az keyvault secret set --vault-name my-copilot-bot-kv `
  --name BotAppPassword --value "<your-bot-password>"

# Optional secrets
az keyvault secret set --vault-name my-copilot-bot-kv `
  --name StorageConnectionString --value "<your-connection-string>"
az keyvault secret set --vault-name my-copilot-bot-kv `
  --name ApplicationInsightsConnectionString --value "<your-appinsights-connection-string>"

# Note: Azure AI Foundry uses Azure RBAC only and does NOT use an API key.
# Grant the App Service Managed Identity a role such as "Cognitive Services OpenAI User"
# on the AI Foundry resource instead of storing a key here.
```

### 3. Enable Managed Identity

```powershell
az webapp identity assign `
  --resource-group myResourceGroup `
  --name myAppName
```

### 4. Grant Key Vault Access

Azure now recommends the **RBAC** authorization model for Key Vault. The commands below
use RBAC; the older access-policy commands (`az keyvault set-policy ...`) still work for
vaults created with the legacy model.

```powershell
# Get the principal ID from the previous command output
$PRINCIPAL_ID = az webapp identity show `
  --name myAppName `
  --resource-group myResourceGroup `
  --query principalId -o tsv

# Recommended: RBAC model
az keyvault update --name my-copilot-bot-kv --enable-rbac-authorization true

$VAULT_ID = az keyvault show --name my-copilot-bot-kv --query id -o tsv

az role assignment create `
  --assignee $PRINCIPAL_ID `
  --role "Key Vault Secrets User" `
  --scope $VAULT_ID

# Legacy alternative (only if the vault was created with the access-policy model):
# az keyvault set-policy --name my-copilot-bot-kv `
#   --object-id $PRINCIPAL_ID --secret-permissions get list
```

### 5. Reference Secrets in App Settings

In the Azure Portal, configure your App Service application settings:

```
GraphConfig__ClientSecret=@Microsoft.KeyVault(SecretUri=https://my-copilot-bot-kv.vault.azure.net/secrets/GraphClientSecret/)
MicrosoftAppPassword=@Microsoft.KeyVault(SecretUri=https://my-copilot-bot-kv.vault.azure.net/secrets/BotAppPassword/)
APPLICATIONINSIGHTS_CONNECTION_STRING=@Microsoft.KeyVault(SecretUri=https://my-copilot-bot-kv.vault.azure.net/secrets/ApplicationInsightsConnectionString/)
```

> **Note**: Azure AI Foundry uses Azure RBAC only - configure `AIFoundryConfig__Endpoint` and `AIFoundryConfig__DeploymentName` directly as App Service application settings and grant the App Service Managed Identity a role such as `Cognitive Services OpenAI User` on the AI Foundry resource. No AI Foundry secret needs to be stored in Key Vault.


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
| Build fails on Node.js step | Ensure Node.js 20 LTS is being used and `package-lock.json` is committed |
| Build fails on .NET step | Ensure .NET 10 SDK is available and solution builds locally |
| Azure deployment fails with 401/403 | Verify service principal permissions and credentials |
| App starts but bot doesn't respond | Check messaging endpoint configuration and Application Insights logs |

See the [Troubleshooting Guide](TROUBLESHOOTING.md) for additional guidance.
