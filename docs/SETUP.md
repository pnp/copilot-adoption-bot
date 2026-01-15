# Setup Guide

This guide covers setting up the Copilot Adoption Bot for local development and production deployment.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Teams Bot Setup](#teams-bot-setup)
- [Configuration](#configuration)
- [Installation](#installation)
- [Running the Application](#running-the-application)

## Prerequisites

### Development Tools

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 18+](https://nodejs.org/)
- [Azure Subscription](https://azure.microsoft.com/free/)
- [Microsoft 365 tenant](https://developer.microsoft.com/microsoft-365/dev-program) with Teams

### Azure Resources

For complete Azure resource requirements and deployment options, see the [Deployment Guide](../DEPLOYMENT.md).

## Teams Bot Setup

This section covers creating a Teams bot and configuring the required Microsoft Graph permissions.

### 1. Create a Bot in Teams Developer Portal

1. Navigate to the [Teams Developer Portal](https://dev.teams.microsoft.com/)
2. Sign in with your Microsoft 365 account
3. Go to **Tools** ? **Bot management** in the left navigation
4. Click **+ New Bot**
5. Enter a name for your bot (e.g., "Copilot Adoption Bot")
6. Click **Add**
7. Once created, note down the **Bot ID** - this is your `MicrosoftAppId`
8. Click on your newly created bot to open its settings
9. Under **Client secrets**, click **Add a client secret**
10. Copy and securely store the generated secret - this is your `MicrosoftAppPassword`

> **Important**: The client secret is only shown once. Store it securely immediately.

### 2. Configure Graph Permissions for the Bot's Entra ID App

When you create a bot in the Teams Developer Portal, an Entra ID (Azure AD) app registration is automatically created. You need to add the required Microsoft Graph permissions to this app.

1. Go to the [Azure Portal](https://portal.azure.com/)
2. Navigate to **Microsoft Entra ID** ? **App registrations**
3. Search for your bot by name or Bot ID (the app will have the same name as your bot)
4. Click on the app registration to open it
5. Go to **API permissions** in the left menu
6. Click **+ Add a permission**
7. Select **Microsoft Graph** ? **Application permissions**
8. Add the following permissions:
   - `User.Read.All` - Required for reading user information and statistics
   - `Reports.Read.All` - Required for Copilot usage statistics (optional, enables Copilot stats features)
   - `TeamsActivity.Send` - Required for sending activity feed notifications
   - `TeamsAppInstallation.ReadWriteForUser.All` - Required for the bot to install itself to user conversations and send messages

9. After adding all permissions, click **Grant admin consent for [Your Tenant]**
10. Confirm by clicking **Yes**

> **Note**: All Application permissions require admin consent. Without granting admin consent, the bot will not be able to send messages or access user information.

#### Required Permissions Summary

| Permission | Type | Required | Description |
|------------|------|----------|-------------|
| `User.Read.All` | Application | Yes | Read all users' full profiles |
| `Reports.Read.All` | Application | No* | Read Copilot usage reports for statistics |
| `TeamsActivity.Send` | Application | Yes | Send activity feed notifications |
| `TeamsAppInstallation.ReadWriteForUser.All` | Application | Yes | Manage Teams app installations for users |

\* Required only for Copilot usage statistics features

#### Important: Refresh Token Cache

After adding any new Microsoft Graph permissions:

- **Rebuild and redeploy** your application
- **Refresh the Microsoft Graph token cache** by signing out and back in via the Teams bot

This ensures the bot receives an updated token with the new permissions.

### 3. Configure Bot Messaging Endpoint (After Deployment)

Once your application is deployed to Azure App Service, you need to configure the bot's messaging endpoint so Teams knows where to send messages.

1. Deploy your application to Azure App Service (see [Deployment Guide](../DEPLOYMENT.md))
2. Note your App Service URL (e.g., `https://your-app-name.azurewebsites.net`)
3. Go back to the [Teams Developer Portal](https://dev.teams.microsoft.com/)
4. Navigate to **Tools** ? **Bot management**
5. Click on your bot to open its settings
6. Under **Configure** ? **Endpoint address**, enter:
   ```
   https://your-app-name.azurewebsites.net/api/messages
   ```
7. Click **Save**

> **Tip**: The messaging endpoint must be HTTPS and publicly accessible. Azure App Service provides this by default.

#### Testing the Bot Connection

After configuring the endpoint:
1. Open Microsoft Teams
2. Search for your bot by name in the search bar
3. Start a conversation with the bot
4. The bot should respond, confirming the connection is working

If the bot doesn't respond, see the [Troubleshooting Guide](TROUBLESHOOTING.md).

## Configuration

This section covers configuring the application for local development and production.

> :memo: **Note**: When you create a bot in the Teams Developer Portal, an Entra ID (Azure AD) app registration is automatically created. This same app registration is used for both the bot identity and Microsoft Graph API access. You will use the Bot ID as your `MicrosoftAppId` / `GraphConfig:ClientId` and the bot client secret as your `MicrosoftAppPassword` / `GraphConfig:ClientSecret`.

### 1. Backend Configuration (User Secrets)

**Important**: For local development, use **User Secrets** to store sensitive configuration. Never commit secrets to source control.

#### Setting Up User Secrets

Navigate to the Web.Server project directory and initialize user secrets:

```bash
cd src/Full/Bot/Web/Web.Server
dotnet user-secrets init
```

Then add your configuration values:

```bash
# Graph API Configuration (Required)
dotnet user-secrets set "GraphConfig:ClientId" "your-app-registration-client-id"
dotnet user-secrets set "GraphConfig:ClientSecret" "your-app-registration-client-secret"
dotnet user-secrets set "GraphConfig:TenantId" "your-tenant-id"
dotnet user-secrets set "GraphConfig:Authority" "https://login.microsoftonline.com/organizations"

# Web Auth Configuration (Required)
dotnet user-secrets set "WebAuthConfig:ClientId" "your-web-app-registration-client-id"
dotnet user-secrets set "WebAuthConfig:ClientSecret" "your-web-app-registration-client-secret"
dotnet user-secrets set "WebAuthConfig:TenantId" "your-tenant-id"
dotnet user-secrets set "WebAuthConfig:Authority" "https://login.microsoftonline.com/organizations"

# Storage Connection (Required)
dotnet user-secrets set "ConnectionStrings:Storage" "DefaultEndpointsProtocol=https;AccountName=yourstorageaccount;AccountKey=your-storage-key;EndpointSuffix=core.windows.net"

# Bot Configuration (Required)
dotnet user-secrets set "MicrosoftAppId" "your-bot-app-id"
dotnet user-secrets set "MicrosoftAppPassword" "your-bot-app-password"

# Teams App Catalog ID (Optional)
dotnet user-secrets set "AppCatalogTeamAppId" "your-teams-app-catalog-id"

# AI Foundry Configuration (Optional - for Copilot Connected mode)
dotnet user-secrets set "AIFoundryConfig:Endpoint" "https://your-project.openai.azure.com/"
dotnet user-secrets set "AIFoundryConfig:DeploymentName" "your-deployment-name"
dotnet user-secrets set "AIFoundryConfig:ApiKey" "your-api-key"
dotnet user-secrets set "AIFoundryConfig:MaxTokens" "2000"
dotnet user-secrets set "AIFoundryConfig:Temperature" "0.7"

# Application Insights (Optional)
dotnet user-secrets set "APPLICATIONINSIGHTS_CONNECTION_STRING" "InstrumentationKey=your-key;IngestionEndpoint=https://..."

# Development Settings (Optional)
dotnet user-secrets set "DevMode" "true"
dotnet user-secrets set "TestUPN" "your-test-user@yourtenant.onmicrosoft.com"
```

#### Configuration Details

**Required Settings:**

- **GraphConfig**: Azure AD configuration for Microsoft Graph API access
  - `ClientId`: Your app registration client ID
  - `ClientSecret`: Your app registration client secret
  - `TenantId`: Your Azure AD tenant ID
  - `Authority`: OAuth authority URL (defaults to organizations)

- **WebAuthConfig**: Azure AD configuration for web interface authentication
  - Uses same structure as GraphConfig
  - Required for user authentication to the web portal

- **ConnectionStrings.Storage**: Azure Storage connection string for table and blob storage

- **MicrosoftAppId**: Bot application ID (same as GraphConfig.ClientId)

- **MicrosoftAppPassword**: Bot application secret (same as GraphConfig.ClientSecret)

**Optional Settings:**

- **AppCatalogTeamAppId**: Teams app catalog ID for the bot

- **AIFoundryConfig**: Azure AI Foundry configuration for Copilot Connected mode
  - Enables smart groups and AI-powered conversations
  - `Endpoint`: Azure AI Foundry endpoint URL
  - `DeploymentName`: AI model deployment name
  - `ApiKey`: API key for authentication
  - `MaxTokens`: Maximum tokens for AI responses (default: 2000)
  - `Temperature`: Temperature for AI responses 0.0-1.0 (default: 0.7)

- **APPLICATIONINSIGHTS_CONNECTION_STRING**: Application Insights connection string for telemetry

- **DevMode**: Enable development mode features (default: false)

- **TestUPN**: Test user principal name for development testing

### 2. Frontend Configuration

Create `src/Full/Bot/Web/web.client/.env.local`:

```env
VITE_MSAL_CLIENT_ID=your-app-registration-client-id
VITE_MSAL_AUTHORITY=https://login.microsoftonline.com/your-tenant-id
VITE_MSAL_SCOPES=api://your-app-registration-client-id/access_as_user
VITE_TEAMSFX_START_LOGIN_PAGE_URL=https://your-domain.com/auth-start.html
```

### 3. Production Configuration

For production deployments to Azure App Service:

1. **Azure App Service**: Configure application settings in the Azure Portal under Configuration ? Application Settings
2. **Azure Key Vault**: For enhanced security, store secrets in Azure Key Vault and reference them:
   ```
   @Microsoft.KeyVault(SecretUri=https://your-keyvault.vault.azure.net/secrets/StorageConnectionString/)
   ```
3. **Managed Identity**: Use system-assigned or user-assigned managed identities to access Azure resources without storing credentials

See the [Deployment Guide](../DEPLOYMENT.md) for detailed production deployment instructions.

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/pnp/copilot-adoption-bot.git
cd copilot-adoption-bot/src/Full/Bot
```

### 2. Restore Backend Dependencies

```bash
dotnet restore
```

### 3. Install Frontend Dependencies

```bash
cd Web/web.client
npm install
cd ../../..
```

### 4. Build the Solution

```bash
dotnet build
```

### 5. Build the Frontend

```bash
cd Web/web.client
npm run build
cd ../../..
```

## Running the Application

### Development Mode

**Backend:**
```bash
cd Web/Web.Server
dotnet run
```

The API will be available at `https://localhost:5001` (or configured port)

**Frontend:**
```bash
cd Web/web.client
npm run dev
```

The React app will be available at `http://localhost:5173`

### Production Mode

Build and run the backend (frontend is served from backend):

```bash
cd Web/Web.Server
dotnet publish -c Release -o ./publish
cd publish
dotnet Web.Server.dll
```

## Next Steps

- **Usage Guide**: See [USAGE.md](USAGE.md) for how to use the application
- **Features**: Learn about all features in [FEATURES.md](FEATURES.md)
- **Deployment**: Deploy to Azure using [../DEPLOYMENT.md](../DEPLOYMENT.md)
- **Security**: Review security best practices in [SECURITY.md](SECURITY.md)
- **Troubleshooting**: Get help with [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
