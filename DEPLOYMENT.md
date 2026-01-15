# Deployment Guide

This document covers all deployment options for the Copilot Adoption Bot Teams application, including prerequisites, Azure resource setup, CI/CD pipelines, and infrastructure configuration.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Azure Resources Required](#azure-resources-required)
- [Manual Deployment](#manual-deployment)
- [CI/CD Pipelines](#cicd-pipelines)
  - [GitHub Actions](#github-actions)
  - [Azure DevOps](#azure-devops)
- [Azure Key Vault Integration](#azure-key-vault-integration)
- [Post-Deployment Configuration](#post-deployment-configuration)

## Prerequisites

### Development Tools

| Tool | Version | Purpose |
|------|---------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10.0+ | Backend development |
| [Node.js](https://nodejs.org/) | 18+ | Frontend build tools |
| [npm](https://www.npmjs.com/) | 9+ | Package management |
| [Git](https://git-scm.com/) | Latest | Version control |
| [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/) | Latest | IDE (optional) |

### Azure Tools

| Tool | Purpose |
|------|---------|
| [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli) | Command-line deployment and management |
| [Azure Storage Explorer](https://azure.microsoft.com/features/storage-explorer/) | Storage account management (optional) |

### Azure Subscriptions and Tenants

- **Azure Subscription** - For hosting Azure resources
- **Microsoft 365 Tenant** - With Teams enabled for bot deployment
- **Admin Consent** - Required for Graph API application permissions

### Accounts and Access

- Azure subscription with Contributor access to create resources
- Microsoft 365 admin access (or Global Admin) for:
  - Granting admin consent for Graph API permissions
  - Publishing the bot to the Teams app catalog (optional)

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

### Storage Account Configuration

The storage account requires both Table Storage and Blob Storage:

```
Storage Account
├── Table Storage
│   ├── MessageTemplates (template metadata)
│   └── MessageLogs (delivery tracking)
└── Blob Storage
    └── message-templates (container for JSON payloads)
```

**Note**: The `message-templates` blob container is automatically created by the application on first run.

### Azure AI Foundry Configuration (Optional)

For AI-powered bot conversations, you need an Azure AI Foundry (formerly Azure OpenAI) deployment:

1. **Create an Azure AI Foundry resource** in the Azure Portal
2. **Deploy a model** (e.g., GPT-4o, GPT-4o-mini)
3. **Note the following values** for configuration:
   - Endpoint URL (e.g., `https://your-resource.openai.azure.com/`)
   - Deployment name
   - API key

Configure these in your application settings:
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

### Estimated Costs

| Resource | Estimated Monthly Cost |
|----------|----------------------|
| App Service (B1) | ~$13 USD |
| Storage Account (minimal usage) | ~$1-5 USD |
| Application Insights (basic) | Free tier available |
| Azure AI Foundry | Pay-per-token (varies by usage) |

**Total**: ~$15-25 USD/month for basic deployment (excluding AI Foundry usage)

---

## Manual Deployment

### Azure App Service

1. **Create an Azure App Service** (Windows or Linux, .NET 10)

2. **Configure Application Settings** in Azure Portal (Configuration - Application Settings):
   - Add all the settings from your user secrets
   - Use Azure Key Vault references for sensitive values (recommended)

3. **Deploy using Azure CLI**:

   ```bash
   # Navigate to the solution directory
   cd src/Full

   # Build and publish
   dotnet publish Web/Web.Server/Web.Server.csproj -c Release -o ./publish

   # Deploy to Azure App Service (replace with your app name)
   az webapp deploy --resource-group myResourceGroup --name myAppName --src-path ./publish
   ```

4. **Or deploy using Visual Studio**:
   - Right-click on `Web.Server` project - Publish
   - Follow the wizard to publish to Azure App Service

### Azure Static Web Apps (Frontend Only)

If separating frontend and backend:

```bash
cd src/Full/Web/web.client
npm run build
# Deploy the dist folder to Azure Static Web Apps
```

---

## CI/CD Pipelines

This project includes pre-configured CI/CD pipelines for both GitHub Actions and Azure DevOps.

### GitHub Actions

**Location:** `.github/workflows/azure-deploy.yml`

#### Features

- Triggers on push to `main` branch and pull requests
- Builds both .NET backend and React frontend
- Runs unit tests with test result publishing
- Deploys to Azure Web App using OIDC authentication (no secrets stored)

#### Setup Instructions

1. **Create an Azure AD App Registration for GitHub Actions**:

   ```bash
   # Create the app registration
   az ad app create --display-name "GitHub-Actions-OfficeNudge"
   
   # Note the appId (client ID) from the output
   ```

2. **Configure Federated Credentials** for OIDC authentication:

   In the Azure Portal:
   - Go to **Microsoft Entra ID** - **App registrations** - Your app
   - Navigate to **Certificates & secrets** - **Federated credentials**
   - Click **+ Add credential**
   - Select **GitHub Actions deploying Azure resources**
     - Configure:
       - **Organization**: Your GitHub organization/username
       - **Repository**: `copilot-adoption-bot`
       - **Entity type**: Branch
     - **Branch**: `main`
     - **Name**: `github-actions-main`

3. **Grant Azure Permissions**:

   ```bash
   # Get the app's service principal object ID
   az ad sp create --id <app-id>
   
   # Assign Contributor role on your resource group
   az role assignment create \
     --assignee <app-id> \
     --role Contributor \
     --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>
   ```

4. **Configure GitHub Secrets and Variables**:

   In your GitHub repository, go to **Settings** - **Secrets and variables** - **Actions**.

   **Secrets tab** (for sensitive values):

   | Secret Name | Value |
   |------------|-------|
   | `AZURE_CLIENT_ID` | The Application (client) ID from step 1 |
   | `AZURE_TENANT_ID` | Your Azure AD tenant ID |
   | `AZURE_SUBSCRIPTION_ID` | Your Azure subscription ID |

   **Variables tab** (for configuration values):

   | Variable Name | Value |
   |--------------|-------|
   | `AZURE_WEBAPP_NAME` | Your Azure App Service name (e.g., `my-office-nudge-app`) |

5. **Create GitHub Environment**:

   - Go to **Settings** - **Environments** - **New environment**
   - Name it `Production`
   - Optionally add required reviewers for deployment approvals

#### Workflow Triggers

| Trigger | Build | Deploy |
|---------|-------|--------|
| Push to `main` | Yes | Yes |
| Pull request to `main` | Yes | No |
| Manual (`workflow_dispatch`) | Yes | Yes |

---

### Azure DevOps

**Location:** `.azure-pipelines/azure-deploy.yml`

#### Features

- Multi-stage pipeline (Build - Deploy)
- Builds both .NET backend and React frontend
- Runs unit tests with automatic result publishing
- Deploys to Azure Web App using service connection

#### Setup Instructions

1. **Create an Azure DevOps Project** (if not already done)

2. **Create an Azure Service Connection**:

   - Go to **Project Settings** - **Service connections**
   - Click **New service connection** - **Azure Resource Manager**
   - Select **Service principal (automatic)** or **Workload Identity federation (automatic)**
   - Configure:
     - **Scope level**: Subscription or Resource Group
     - **Subscription**: Select your Azure subscription
     - **Resource group**: Select your resource group (if using resource group scope)
     - **Service connection name**: `AzureServiceConnection`

3. **Create a Pipeline**:

   - Go to **Pipelines** - **New Pipeline**
   - Select your repository source (GitHub, Azure Repos, etc.)
   - Choose **Existing Azure Pipelines YAML file**
   - Select `.azure-pipelines/azure-deploy.yml`
   - Click **Run** to save and execute

4. **Configure Pipeline Variables**:

   In Azure DevOps, go to **Pipelines** - Your pipeline - **Edit** - **Variables** and add:

   | Variable Name | Value |
   |--------------|-------|
   | `webAppName` | Your Azure App Service name |
   | `azureSubscription` | Your service connection name (default: `AzureServiceConnection`) |

   Alternatively, you can edit the variables directly in the YAML file.

5. **Create Deployment Environment**:

   - Go to **Pipelines** - **Environments**
   - The `Production` environment will be created automatically on first run
   - Optionally configure approvals and checks

#### Pipeline Stages

| Stage | Condition | Actions |
|-------|-----------|---------|
| Build | Always | Restore, build, test, publish artifacts |
| Deploy | `main` branch only, build succeeded | Deploy to Azure Web App |

---

## Azure Key Vault Integration

For production deployments, store all secrets in Azure Key Vault.

### Setup

1. **Create an Azure Key Vault**:

   ```bash
   az keyvault create \
     --name my-office-nudge-kv \
     --resource-group myResourceGroup \
     --location eastus
   ```

2. **Add secrets to Key Vault**:

   ```bash
   # Required secrets
   az keyvault secret set --vault-name my-office-nudge-kv --name GraphClientSecret --value "<your-client-secret>"
   az keyvault secret set --vault-name my-office-nudge-kv --name StorageConnectionString --value "<your-connection-string>"
   az keyvault secret set --vault-name my-office-nudge-kv --name BotAppPassword --value "<your-bot-password>"
   
   # Optional secrets
   az keyvault secret set --vault-name my-office-nudge-kv --name ApplicationInsightsConnectionString --value "<your-appinsights-connection-string>"
   az keyvault secret set --vault-name my-office-nudge-kv --name AIFoundryApiKey --value "<your-ai-foundry-api-key>"
   ```

3. **Enable Managed Identity on your App Service**:

   ```bash
   az webapp identity assign \
     --resource-group myResourceGroup \
     --name myAppName
   ```

4. **Grant the managed identity access to Key Vault**:

   ```bash
   # Get the principal ID from the previous command output
   az keyvault set-policy \
     --name my-office-nudge-kv \
     --object-id <principal-id> \
     --secret-permissions get list
   ```

5. **Reference secrets in Application Settings**:

   In the Azure Portal, configure your App Service application settings:

   ```
   GraphConfig__ClientSecret=@Microsoft.KeyVault(SecretUri=https://my-office-nudge-kv.vault.azure.net/secrets/GraphClientSecret/)
   ConnectionStrings__Storage=@Microsoft.KeyVault(SecretUri=https://my-office-nudge-kv.vault.azure.net/secrets/StorageConnectionString/)
   MicrosoftAppPassword=@Microsoft.KeyVault(SecretUri=https://my-office-nudge-kv.vault.azure.net/secrets/BotAppPassword/)
   APPLICATIONINSIGHTS_CONNECTION_STRING=@Microsoft.KeyVault(SecretUri=https://my-office-nudge-kv.vault.azure.net/secrets/ApplicationInsightsConnectionString/)
   AIFoundryConfig__ApiKey=@Microsoft.KeyVault(SecretUri=https://my-office-nudge-kv.vault.azure.net/secrets/AIFoundryApiKey/)
   ```

---

## Post-Deployment Configuration

After deploying the application:

### 1. Configure Bot Messaging Endpoint

1. Go to the [Teams Developer Portal](https://dev.teams.microsoft.com/)
2. Navigate to **Tools** - **Bot management**
3. Click on your bot
4. Under **Configure** - **Endpoint address**, enter:
   ```
   https://your-app-name.azurewebsites.net/api/messages
   ```
5. Click **Save**

### 2. Verify Application Health

1. Navigate to your App Service URL: `https://your-app-name.azurewebsites.net`
2. Check the Swagger documentation: `https://your-app-name.azurewebsites.net/swagger`
3. Test Graph connectivity: `https://your-app-name.azurewebsites.net/api/Diagnostics/TestGraphConnection`

### 3. Monitor with Application Insights

If configured, view logs and telemetry in the Azure Portal:
- Go to your Application Insights resource
- Check **Live Metrics** for real-time monitoring
- Review **Failures** for any deployment issues
- Set up **Alerts** for critical errors

---

## Troubleshooting

### Deployment Failures

**Build fails on Node.js step:**
- Ensure Node.js 18+ is being used
- Check that `npm ci` can resolve all dependencies
- Verify `package-lock.json` is committed to the repository

**Build fails on .NET step:**
- Ensure .NET 10 SDK is available
- Check for any missing NuGet packages
- Verify solution builds locally with `dotnet build`

**.NET 10 runtime not available:**
- For Azure DevOps, use `UseDotNet@2` task with version `10.0.x`
- For GitHub Actions, use `actions/setup-dotnet@v4` with version `10.0.x`
- Ensure your App Service is configured with .NET 10 runtime stack

**Azure deployment fails with 401/403:**
- Verify service connection/federated credentials are configured correctly
- Check that the service principal has Contributor access to the resource group
- For GitHub Actions, ensure all three secrets are set correctly

**App starts but bot doesn't respond:**
- Verify the messaging endpoint is configured in Teams Developer Portal
- Check Application Insights for errors
- Ensure `MicrosoftAppId` and `MicrosoftAppPassword` are correctly configured

### Common Issues

See the [Troubleshooting section](README.md#troubleshooting) in the main README for additional guidance on authentication, Graph API, storage, and bot issues.
