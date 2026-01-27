# Configuration Reference

This document provides a complete reference for all configuration settings in the Copilot Adoption Bot.

## Configuration Methods

The application supports multiple configuration methods, applied in this order of precedence (highest to lowest):

1. **Environment Variables** - Highest priority, used in production
2. **User Secrets** - For local development (secrets stored outside project)
3. **appsettings.json** - Base configuration file
4. **appsettings.{Environment}.json** - Environment-specific overrides

### Configuration Syntax

For nested settings, use double underscores (`__`) in environment variables or colons (`:`) in JSON:

| JSON Path | Environment Variable | User Secret Command |
|-----------|---------------------|---------------------|
| `GraphConfig.ClientId` | `GraphConfig__ClientId` | `dotnet user-secrets set "GraphConfig:ClientId" "value"` |

---

## Required Configuration

These settings are required for the application to function.

### Bot Identity

| Setting | Description | Example |
|---------|-------------|---------|
| `MicrosoftAppId` | Bot's application (client) ID from Teams Developer Portal | `12345678-1234-1234-1234-123456789abc` |
| `MicrosoftAppPassword` | Bot's client secret from Teams Developer Portal | `your-secret-value` |
| `MicrosoftAppType` | Bot authentication type | `SingleTenant` (default) or `MultiTenant` |

**JSON Example:**
```json
{
  "MicrosoftAppId": "12345678-1234-1234-1234-123456789abc",
  "MicrosoftAppPassword": "your-secret-value",
  "MicrosoftAppType": "SingleTenant"
}
```

### Microsoft Graph API (`GraphConfig`)

| Setting | Description | Required | Example |
|---------|-------------|----------|---------|
| `GraphConfig:ClientId` | Application (client) ID for Graph API access | Yes | `12345678-1234-1234-1234-123456789abc` |
| `GraphConfig:ClientSecret` | Client secret for Graph API access | Yes | `your-client-secret` |
| `GraphConfig:TenantId` | Azure AD tenant ID | Yes | `your-tenant-id` |
| `GraphConfig:Authority` | Azure AD authority URL | No | `https://login.microsoftonline.com/organizations` (default) |
| `GraphConfig:ApiAudience` | API audience for token validation | No | `api://your-app-id` |

> **Note**: Typically `GraphConfig:ClientId` and `MicrosoftAppId` are the same value (the bot's app registration).

**JSON Example:**
```json
{
  "GraphConfig": {
    "ClientId": "12345678-1234-1234-1234-123456789abc",
    "ClientSecret": "your-client-secret",
    "TenantId": "your-tenant-id"
  }
}
```

### Storage Configuration

The application requires Azure Storage (Table Storage + Blob Storage). Choose one authentication method:

#### Option 1: Connection String (Simpler)

| Setting | Description | Example |
|---------|-------------|---------|
| `ConnectionStrings:Storage` | Full storage connection string | `DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...` |

**JSON Example:**
```json
{
  "ConnectionStrings": {
    "Storage": "DefaultEndpointsProtocol=https;AccountName=mystorageaccount;AccountKey=abc123...;EndpointSuffix=core.windows.net"
  }
}
```

#### Option 2: RBAC with Managed Identity (Recommended for Production)

| Setting | Description | Required | Example |
|---------|-------------|----------|---------|
| `StorageAuthConfig:UseRBAC` | Enable RBAC authentication | Yes | `true` |
| `StorageAuthConfig:StorageAccountName` | Storage account name | Yes | `mystorageaccount` |

**JSON Example:**
```json
{
  "StorageAuthConfig": {
    "UseRBAC": true,
    "StorageAccountName": "mystorageaccount"
  }
}
```

#### Option 2b: RBAC with Service Principal Override

If you need to use specific credentials instead of Managed Identity:

| Setting | Description | Example |
|---------|-------------|---------|
| `StorageAuthConfig:UseRBAC` | Enable RBAC authentication | `true` |
| `StorageAuthConfig:StorageAccountName` | Storage account name | `mystorageaccount` |
| `StorageAuthConfig:RBACOverrideCredentials:ClientId` | Service principal client ID | `sp-client-id` |
| `StorageAuthConfig:RBACOverrideCredentials:ClientSecret` | Service principal secret | `sp-secret` |
| `StorageAuthConfig:RBACOverrideCredentials:TenantId` | Tenant ID | `tenant-id` |

**JSON Example:**
```json
{
  "StorageAuthConfig": {
    "UseRBAC": true,
    "StorageAccountName": "mystorageaccount",
    "RBACOverrideCredentials": {
      "ClientId": "sp-client-id",
      "ClientSecret": "sp-secret",
      "TenantId": "tenant-id"
    }
  }
}
```

---

## Optional Configuration

### Web Authentication (`WebAuthConfig`)

Configuration for the web interface authentication (Teams SSO).

| Setting | Description | Required | Example |
|---------|-------------|----------|---------|
| `WebAuthConfig:ClientId` | Client ID for web auth | No | Same as `MicrosoftAppId` |
| `WebAuthConfig:ClientSecret` | Client secret for web auth | No | Same as `MicrosoftAppPassword` |
| `WebAuthConfig:TenantId` | Tenant ID | No | Your tenant ID |
| `WebAuthConfig:ApiAudience` | API audience | No | `api://your-app-id` |

**JSON Example:**
```json
{
  "WebAuthConfig": {
    "ClientId": "12345678-1234-1234-1234-123456789abc",
    "ClientSecret": "your-client-secret",
    "TenantId": "your-tenant-id",
    "ApiAudience": "api://12345678-1234-1234-1234-123456789abc"
  }
}
```

### Teams App Configuration

| Setting | Description | Required | Example |
|---------|-------------|----------|---------|
| `AppCatalogTeamAppId` | Teams app ID from the app catalog | No | `com.contoso.copilotbot` |

This is used to install the bot app for users who haven't interacted with it yet.

### Azure AI Foundry (`AIFoundryConfig`)

Enables "Copilot Connected" mode with AI-powered features including smart groups and follow-up conversations.

| Setting | Description | Required | Default | Example |
|---------|-------------|----------|---------|---------|
| `AIFoundryConfig:Endpoint` | Azure AI Foundry endpoint URL | Yes* | - | `https://your-resource.openai.azure.com/` |
| `AIFoundryConfig:DeploymentName` | Model deployment name | Yes* | - | `gpt-4o-mini` |
| `AIFoundryConfig:ApiKey` | API key for authentication | Yes* | - | `your-api-key` |
| `AIFoundryConfig:MaxTokens` | Maximum tokens for responses | No | `2000` | `4000` |
| `AIFoundryConfig:Temperature` | Response creativity (0.0-1.0) | No | `0.7` | `0.5` |

\* Required only if enabling AI features.

**JSON Example:**
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

### Application Insights

| Setting | Description | Example |
|---------|-------------|---------|
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Connection string for telemetry | `InstrumentationKey=...;IngestionEndpoint=...` |

### User Cache Configuration (`UserCacheConfig`)

Controls user data synchronization and caching behavior.

| Setting | Description | Default | Example |
|---------|-------------|---------|---------|
| `UserCacheConfig:CacheExpiration` | How long cached user data is valid | `01:00:00` (1 hour) | `02:00:00` |
| `UserCacheConfig:CopilotStatsRefreshInterval` | How often to refresh Copilot stats | `1.00:00:00` (24 hours) | `12:00:00` |
| `UserCacheConfig:FullSyncInterval` | How often to force full sync | `7.00:00:00` (7 days) | `3.00:00:00` |
| `UserCacheConfig:CopilotStatsPeriod` | Copilot stats period | `D30` | `D7`, `D30`, `D90`, `D180` |
| `UserCacheConfig:UserCacheTableName` | Table name for user cache | `usercache` | `usercache` |
| `UserCacheConfig:SyncMetadataTableName` | Table name for sync metadata | `usersyncmetadata` | `usersyncmetadata` |

**JSON Example:**
```json
{
  "UserCacheConfig": {
    "CacheExpiration": "01:00:00",
    "CopilotStatsRefreshInterval": "1.00:00:00",
    "CopilotStatsPeriod": "D30"
  }
}
```

### Development Settings

| Setting | Description | Default | Example |
|---------|-------------|---------|---------|
| `DevMode` | Enable development mode features | `false` | `true` |
| `TestUPN` | Test user principal name for development | - | `testuser@contoso.com` |

---

## Logging Configuration

Standard ASP.NET Core logging configuration.

**JSON Example:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.Bot": "Debug"
    }
  }
}
```

| Log Level | Use Case |
|-----------|----------|
| `Trace` | Most detailed logs |
| `Debug` | Development debugging |
| `Information` | General operational logs (default) |
| `Warning` | Unexpected but handled situations |
| `Error` | Errors and exceptions |
| `Critical` | System failures |

---

## Complete Configuration Examples

### Local Development (User Secrets)

```bash
cd src/Full/Bot/Web/Web.Server

# Bot identity
dotnet user-secrets set "MicrosoftAppId" "your-bot-app-id"
dotnet user-secrets set "MicrosoftAppPassword" "your-bot-password"

# Graph API
dotnet user-secrets set "GraphConfig:ClientId" "your-bot-app-id"
dotnet user-secrets set "GraphConfig:ClientSecret" "your-bot-password"
dotnet user-secrets set "GraphConfig:TenantId" "your-tenant-id"

# Storage (connection string for simplicity)
dotnet user-secrets set "ConnectionStrings:Storage" "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=..."

# Optional: Development mode
dotnet user-secrets set "DevMode" "true"
dotnet user-secrets set "TestUPN" "your-email@company.com"

# Optional: AI Foundry
dotnet user-secrets set "AIFoundryConfig:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AIFoundryConfig:DeploymentName" "gpt-4o-mini"
dotnet user-secrets set "AIFoundryConfig:ApiKey" "your-api-key"
```

### Production (Azure App Service)

Configure these settings in the Azure Portal under **Configuration** → **Application settings**, or via Azure CLI.

#### Using Azure Portal

Add these as **Application settings** (not Connection strings):

| Name | Value |
|------|-------|
| `MicrosoftAppId` | `your-bot-app-id` |
| `MicrosoftAppPassword` | `@Microsoft.KeyVault(SecretUri=https://your-kv.vault.azure.net/secrets/BotAppPassword/)` |
| `MicrosoftAppType` | `SingleTenant` |
| `GraphConfig__ClientId` | `your-bot-app-id` |
| `GraphConfig__ClientSecret` | `@Microsoft.KeyVault(SecretUri=https://your-kv.vault.azure.net/secrets/GraphClientSecret/)` |
| `GraphConfig__TenantId` | `your-tenant-id` |
| `StorageAuthConfig__UseRBAC` | `true` |
| `StorageAuthConfig__StorageAccountName` | `yourstorageaccount` |
| `AppCatalogTeamAppId` | `your-teams-app-id` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | `@Microsoft.KeyVault(SecretUri=https://your-kv.vault.azure.net/secrets/AppInsightsConnectionString/)` |
| `AIFoundryConfig__Endpoint` | `https://your-resource.openai.azure.com/` |
| `AIFoundryConfig__DeploymentName` | `gpt-4o-mini` |
| `AIFoundryConfig__ApiKey` | `@Microsoft.KeyVault(SecretUri=https://your-kv.vault.azure.net/secrets/AIFoundryApiKey/)` |

> **Note**: Use double underscores (`__`) for nested settings in App Service configuration. This works on both Windows and Linux App Services.

#### Using Azure CLI (Windows App Service)

```powershell
# PowerShell
az webapp config appsettings set `
  --name your-app-name `
  --resource-group your-resource-group `
  --settings `
    MicrosoftAppId="your-bot-app-id" `
    MicrosoftAppType="SingleTenant" `
    GraphConfig__ClientId="your-bot-app-id" `
    GraphConfig__TenantId="your-tenant-id" `
    StorageAuthConfig__UseRBAC="true" `
    StorageAuthConfig__StorageAccountName="yourstorageaccount"

# Secrets (use Key Vault references)
az webapp config appsettings set `
  --name your-app-name `
  --resource-group your-resource-group `
  --settings `
    "MicrosoftAppPassword=@Microsoft.KeyVault(SecretUri=https://your-kv.vault.azure.net/secrets/BotAppPassword/)" `
    "GraphConfig__ClientSecret=@Microsoft.KeyVault(SecretUri=https://your-kv.vault.azure.net/secrets/GraphClientSecret/)"
```

#### Using Azure CLI (Linux App Service)

```bash
# Bash
az webapp config appsettings set \
  --name your-app-name \
  --resource-group your-resource-group \
  --settings \
    MicrosoftAppId="your-bot-app-id" \
    MicrosoftAppType="SingleTenant" \
    GraphConfig__ClientId="your-bot-app-id" \
    GraphConfig__TenantId="your-tenant-id" \
    StorageAuthConfig__UseRBAC="true" \
    StorageAuthConfig__StorageAccountName="yourstorageaccount"

# Secrets (use Key Vault references)
az webapp config appsettings set \
  --name your-app-name \
  --resource-group your-resource-group \
  --settings \
    'MicrosoftAppPassword=@Microsoft.KeyVault(SecretUri=https://your-kv.vault.azure.net/secrets/BotAppPassword/)' \
    'GraphConfig__ClientSecret=@Microsoft.KeyVault(SecretUri=https://your-kv.vault.azure.net/secrets/GraphClientSecret/)'
```

#### Platform-Specific Notes

| Aspect | Windows App Service | Linux App Service |
|--------|--------------------|--------------------|
| **Runtime** | .NET 10 (Windows) | .NET 10 (Linux) |
| **Config syntax** | `__` for nested settings | `__` for nested settings |
| **Case sensitivity** | Case-insensitive | Case-sensitive |
| **File paths** | Backslashes `\` | Forward slashes `/` |
| **Startup command** | Automatic | May need `dotnet Web.Server.dll` |

> **Important for Linux**: Environment variable names are case-sensitive. Ensure `GraphConfig__ClientId` matches exactly (including capitalization).

### Production (appsettings.json with Key Vault References)

```json
{
  "MicrosoftAppId": "your-bot-app-id",
  "MicrosoftAppType": "SingleTenant",
  
  "GraphConfig": {
    "ClientId": "your-bot-app-id",
    "TenantId": "your-tenant-id"
  },
  
  "StorageAuthConfig": {
    "UseRBAC": true,
    "StorageAccountName": "yourstorageaccount"
  },
  
  "UserCacheConfig": {
    "CopilotStatsPeriod": "D30",
    "CacheExpiration": "01:00:00"
  },
  
  "AIFoundryConfig": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini",
    "MaxTokens": 2000,
    "Temperature": "0.7"
  },
  
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

> **Note**: Secrets like `MicrosoftAppPassword`, `GraphConfig:ClientSecret`, `AIFoundryConfig:ApiKey` should be stored in Azure Key Vault and referenced via App Service configuration.

---

## Frontend Configuration

The React frontend uses environment variables defined in `.env.local`:

| Variable | Description | Example |
|----------|-------------|---------|
| `VITE_MSAL_CLIENT_ID` | Client ID for MSAL authentication | `your-bot-app-id` |
| `VITE_MSAL_AUTHORITY` | Azure AD authority | `https://login.microsoftonline.com/your-tenant-id` |
| `VITE_MSAL_SCOPES` | API scopes for access token | `api://your-app-id/access_as_user` |
| `VITE_TEAMSFX_START_LOGIN_PAGE_URL` | Login redirect URL | `https://localhost:5001/auth-start.html` |

**Example `.env.local`:**
```env
VITE_MSAL_CLIENT_ID=12345678-1234-1234-1234-123456789abc
VITE_MSAL_AUTHORITY=https://login.microsoftonline.com/your-tenant-id
VITE_MSAL_SCOPES=api://12345678-1234-1234-1234-123456789abc/access_as_user
VITE_TEAMSFX_START_LOGIN_PAGE_URL=https://localhost:5001/auth-start.html
```

---

## Configuration Validation

The application validates required configuration on startup. Missing required values will cause the application to fail with a `ConfigurationMissingException`.

To test your configuration:

1. **Check startup logs** for configuration errors
2. **Use the diagnostics endpoint**: `GET /api/Diagnostics/TestGraphConnection`
3. **Review Application Insights** for configuration-related exceptions

---

## Related Documentation

- **[Development Guide](DEVELOPMENT.md)** - Local development setup with User Secrets
- **[Deployment Guide](DEPLOYMENT.md)** - Production deployment configuration
- **[Security Guide](SECURITY.md)** - Security best practices for secrets management
- **[Troubleshooting Guide](TROUBLESHOOTING.md)** - Common configuration issues
