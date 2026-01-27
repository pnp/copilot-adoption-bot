# Manual Deployment Guide

This guide walks you through manually deploying the Copilot Adoption Bot to Azure App Service.

## Prerequisites

Before starting, ensure you have:

- [ ] Completed [Teams Bot Setup](SETUP.md#teams-bot-setup)
- [ ] [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli) installed
- [ ] [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed
- [ ] [Node.js 18+](https://nodejs.org/) installed
- [ ] Azure subscription with Contributor access

---

## Step 1: Create Azure Resources

### 1.1 Login to Azure

```bash
az login
az account set --subscription "Your Subscription Name"
```

### 1.2 Create Resource Group

```bash
az group create \
  --name rg-copilot-adoption-bot \
  --location eastus
```

### 1.3 Create App Service Plan

```bash
az appservice plan create \
  --name asp-copilot-adoption-bot \
  --resource-group rg-copilot-adoption-bot \
  --sku B1 \
  --is-linux false
```

> **Note**: Use `--is-linux true` for Linux hosting if preferred.

### 1.4 Create App Service

```bash
az webapp create \
  --name copilot-adoption-bot-app \
  --resource-group rg-copilot-adoption-bot \
  --plan asp-copilot-adoption-bot \
  --runtime "DOTNET|10.0"
```

### 1.5 Create Storage Account

```bash
az storage account create \
  --name copilotbotsa \
  --resource-group rg-copilot-adoption-bot \
  --location eastus \
  --sku Standard_LRS \
  --kind StorageV2
```

### 1.6 Enable Managed Identity

```bash
az webapp identity assign \
  --name copilot-adoption-bot-app \
  --resource-group rg-copilot-adoption-bot
```

---

## Step 2: Configure Storage Access

### Option A: RBAC (Recommended)

```bash
# Get the managed identity principal ID
PRINCIPAL_ID=$(az webapp identity show \
  --name copilot-adoption-bot-app \
  --resource-group rg-copilot-adoption-bot \
  --query principalId -o tsv)

# Get storage account ID
STORAGE_ID=$(az storage account show \
  --name copilotbotsa \
  --resource-group rg-copilot-adoption-bot \
  --query id -o tsv)

# Assign required roles
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Storage Blob Data Contributor" \
  --scope $STORAGE_ID

az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Storage Table Data Contributor" \
  --scope $STORAGE_ID

az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Storage Queue Data Contributor" \
  --scope $STORAGE_ID
```

> **Note**: Role assignments can take up to 5 minutes to propagate.

### Option B: Connection String

```bash
# Get the storage connection string
STORAGE_CONN=$(az storage account show-connection-string \
  --name copilotbotsa \
  --resource-group rg-copilot-adoption-bot \
  --query connectionString -o tsv)

echo "Connection String: $STORAGE_CONN"
```

---

## Step 3: Configure Application Settings

### 3.1 Set Required App Settings

```bash
# Bot configuration (from Teams Developer Portal)
az webapp config appsettings set \
  --name copilot-adoption-bot-app \
  --resource-group rg-copilot-adoption-bot \
  --settings \
    MicrosoftAppId="your-bot-app-id" \
    MicrosoftAppPassword="your-bot-app-password"

# Graph API configuration
az webapp config appsettings set \
  --name copilot-adoption-bot-app \
  --resource-group rg-copilot-adoption-bot \
  --settings \
    GraphConfig__ClientId="your-client-id" \
    GraphConfig__ClientSecret="your-client-secret" \
    GraphConfig__TenantId="your-tenant-id"
```

### 3.2 Set Storage Configuration

**For RBAC:**
```bash
az webapp config appsettings set \
  --name copilot-adoption-bot-app \
  --resource-group rg-copilot-adoption-bot \
  --settings \
    StorageAuthConfig__UseRBAC="true" \
    StorageAuthConfig__StorageAccountName="copilotbotsa"
```

**For Connection String:**
```bash
az webapp config connection-string set \
  --name copilot-adoption-bot-app \
  --resource-group rg-copilot-adoption-bot \
  --settings Storage="$STORAGE_CONN" \
  --connection-string-type Custom
```

### 3.3 Set Optional Settings

```bash
# Application Insights (if using)
az webapp config appsettings set \
  --name copilot-adoption-bot-app \
  --resource-group rg-copilot-adoption-bot \
  --settings \
    APPLICATIONINSIGHTS_CONNECTION_STRING="your-appinsights-connection-string"

# Azure AI Foundry (if using)
az webapp config appsettings set \
  --name copilot-adoption-bot-app \
  --resource-group rg-copilot-adoption-bot \
  --settings \
    AIFoundryConfig__Endpoint="https://your-resource.openai.azure.com/" \
    AIFoundryConfig__DeploymentName="gpt-4o-mini" \
    AIFoundryConfig__ApiKey="your-api-key" \
    AIFoundryConfig__MaxTokens="2000" \
    AIFoundryConfig__Temperature="0.7"
```

---

## Step 4: Build the Application

### 4.1 Navigate to Solution Directory

```bash
cd src/Full/Bot
```

### 4.2 Build Frontend

```bash
cd Web/web.client
npm ci
npm run build
cd ../..
```

### 4.3 Build and Publish Backend

```bash
dotnet publish Web/Web.Server/Web.Server.csproj \
  -c Release \
  -o ./publish
```

---

## Step 5: Deploy to Azure

### Option A: Using Azure CLI

```bash
# Create a zip file of the publish folder
cd publish
zip -r ../deploy.zip .
cd ..

# Deploy the zip file
az webapp deploy \
  --resource-group rg-copilot-adoption-bot \
  --name copilot-adoption-bot-app \
  --src-path ./deploy.zip \
  --type zip
```

### Option B: Using Visual Studio

1. Open `Adoption Bot.sln` in Visual Studio 2022
2. Right-click on `Web.Server` project
3. Select **Publish**
4. Choose **Azure** → **Azure App Service (Windows)**
5. Select your App Service and click **Publish**

### Option C: Using VS Code

1. Install the [Azure App Service extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azureappservice)
2. Open the `publish` folder
3. Right-click and select **Deploy to Web App**
4. Select your App Service

---

## Step 6: Configure Bot Endpoint

1. Go to the [Teams Developer Portal](https://dev.teams.microsoft.com/)
2. Navigate to **Tools** → **Bot management**
3. Click on your bot
4. Under **Configure** → **Endpoint address**, enter:
   ```
   https://copilot-adoption-bot-app.azurewebsites.net/api/messages
   ```
5. Click **Save**

---

## Step 7: Verify Deployment

### 7.1 Check Application Health

Open your browser and navigate to:

| URL | Expected Result |
|-----|-----------------|
| `https://copilot-adoption-bot-app.azurewebsites.net` | Application loads |
| `https://copilot-adoption-bot-app.azurewebsites.net/swagger` | Swagger UI loads |
| `https://copilot-adoption-bot-app.azurewebsites.net/api/Diagnostics/TestGraphConnection` | Returns success |

### 7.2 Check Logs

```bash
# Stream live logs
az webapp log tail \
  --name copilot-adoption-bot-app \
  --resource-group rg-copilot-adoption-bot

# Download logs
az webapp log download \
  --name copilot-adoption-bot-app \
  --resource-group rg-copilot-adoption-bot \
  --log-file logs.zip
```

### 7.3 Test Bot in Teams

1. Install the Teams app (see [SETUP.md](SETUP.md) for Teams app package creation)
2. Send a message to the bot
3. Verify the bot responds

---

## Updating the Deployment

To deploy updates:

```bash
# Navigate to solution directory
cd src/Full/Bot

# Build frontend
cd Web/web.client
npm ci
npm run build
cd ../..

# Build and publish
dotnet publish Web/Web.Server/Web.Server.csproj -c Release -o ./publish

# Deploy
cd publish
zip -r ../deploy.zip .
cd ..

az webapp deploy \
  --resource-group rg-copilot-adoption-bot \
  --name copilot-adoption-bot-app \
  --src-path ./deploy.zip \
  --type zip
```

---

## Cleanup (Optional)

To remove all resources:

```bash
az group delete \
  --name rg-copilot-adoption-bot \
  --yes \
  --no-wait
```

---

## Troubleshooting

### Application won't start

1. Check the application logs:
   ```bash
   az webapp log tail --name copilot-adoption-bot-app --resource-group rg-copilot-adoption-bot
   ```
2. Verify all required app settings are configured
3. Check that the .NET 10 runtime is available

### Storage access denied

1. Verify role assignments are complete (allow 5 minutes for propagation)
2. Check storage account name is correct
3. For connection string: verify the connection string is valid

### Bot doesn't respond

1. Verify the messaging endpoint is configured correctly in Teams Developer Portal
2. Check that `MicrosoftAppId` and `MicrosoftAppPassword` are correct
3. Review Application Insights for errors

### Graph API errors

1. Verify Graph permissions have admin consent
2. Check `GraphConfig` settings are correct
3. Test Graph connectivity: `/api/Diagnostics/TestGraphConnection`

---

## Next Steps

- Set up [Azure Key Vault Integration](DEPLOYMENT.md#azure-key-vault-integration) for production
- Configure [Application Insights](DEPLOYMENT.md#estimated-costs) for monitoring
- Review [Security Best Practices](SECURITY.md)
