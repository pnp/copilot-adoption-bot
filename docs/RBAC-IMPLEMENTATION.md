# Azure Storage RBAC Implementation

This document describes the Azure Storage RBAC (Role-Based Access Control) implementation added to the Copilot Adoption Bot.

## Overview

The application now supports Azure RBAC for authenticating to Azure Storage, which is more secure than using connection strings with storage account keys. This implementation supports both production (with Managed Identity) and local development (with Visual Studio credentials).

## What Changed

### New Configuration

Added `EnableRBAC` and `StorageAccountName` to the root `AppConfig` configuration:

```json
{
  "EnableRBAC": true,
  "StorageAccountName": "mystorageaccount",
  "ConnectionStrings": {
    "Storage": null
  }
}
```

**Default Behavior:** `EnableRBAC` defaults to `true`, meaning RBAC is enabled by default for new deployments.

### Code Changes

#### 1. Configuration (`Common/Engine/Config/AppConfig.cs`)
- Moved `EnableRBAC` property (default: true) to root `AppConfig` class
- Moved `StorageAccountName` property to root `AppConfig` class
- Removed these from `AppConnectionStrings` class

#### 2. Credential Factory (`Common/Engine/Storage/StorageCredentialFactory.cs`)
- New factory class to manage credential creation
- Accepts full `AppConfig` instead of just `AppConnectionStrings`
- Uses `DefaultAzureCredential` for RBAC authentication
- Supports both connection string and RBAC modes
- Automatically detects storage account name from connection string

#### 3. Storage Managers
Updated to support both authentication methods:
- `TableStorageManager` - Base class with dual constructors
- `MessageTemplateStorageManager` - Supports RBAC for tables and blobs
- `SmartGroupStorageManager` - Supports RBAC for tables
- `SettingsStorageManager` - Supports RBAC for tables
- `BotConversationCache` - Supports RBAC for tables
- `BatchQueueService` - Supports RBAC for queues
- `AzureTableCacheStorage` - Supports RBAC for user cache

#### 4. Dependency Injection
Updated all service registrations in:
- `DependencyInjection.cs`
- `BotServiceExtensions.cs`
- `MessageTemplateServiceExtensions.cs`

Each registration now uses `StorageCredentialFactory` to determine whether to use RBAC or connection strings.

#### 5. Package Dependencies
- Added `Azure.Identity` v1.17.1 to `Engine.csproj`

### Documentation

Created comprehensive documentation:
- **docs/STORAGE-RBAC-SETUP.md** - Complete setup guide for RBAC
- Updated **UnitTests/IntegrationTests/README.md** - Test configuration instructions
- Updated **secrets.json** example
- Updated **appsettings.example.json**

## How It Works

### Production (Azure App Service with Managed Identity)

1. App Service has a system-assigned managed identity
2. Managed identity is granted Storage roles (Blob/Queue/Table Data Contributor)
3. `DefaultAzureCredential` automatically uses the managed identity
4. No secrets needed in configuration

### Local Development (Visual Studio)

1. Developer signs in to Visual Studio with their Azure account
2. Developer's account is granted Storage roles on the storage account
3. `DefaultAzureCredential` uses Visual Studio credentials
4. No secrets needed in configuration

### Local Development (Azurite Emulator)

1. Set `EnableRBAC` to `false`
2. Use `Storage` connection string: `"UseDevelopmentStorage=true"`
3. Azurite must be running

## Migration Path

For existing deployments using connection strings:

1. **Add roles** to the App Service Managed Identity
2. **Wait 10 minutes** for role propagation
3. **Update configuration** to enable RBAC
4. **Restart** the application
5. **Verify** everything works
6. **Optionally remove** the connection string from configuration

## Security Benefits

? **No storage account keys to manage or rotate**
- Keys can be disabled entirely in the storage account
- Reduces risk of key leakage

? **Fine-grained permissions**
- Assign only required roles (Blob/Queue/Table Data Contributor)
- Can scope to specific containers/queues/tables if needed

? **Audit trail**
- All storage access logged through Azure AD
- Can track who accessed what and when

? **Works with existing Azure security features**
- Conditional Access policies
- Multi-factor authentication
- Identity Protection

## Backward Compatibility

The implementation is **fully backward compatible**:
- Setting `EnableRBAC: false` uses the traditional connection string method
- Existing deployments continue to work without changes
- Migration can be done gradually

## Testing

Integration tests support both modes:
- Set `EnableRBAC: true` and provide `StorageAccountName` for RBAC testing
- Set `EnableRBAC: false` and provide `Storage` connection string for traditional testing

## Required Azure Roles

When `EnableRBAC` is enabled, the identity (Managed Identity or user account) needs these roles on the Storage Account:

| Role | Purpose |
|------|---------|
| **Storage Blob Data Contributor** | Read/write message template blobs |
| **Storage Queue Data Contributor** | Send/receive batch processing messages |
| **Storage Table Data Contributor** | Read/write tables (templates, logs, cache, etc.) |

## Troubleshooting

### "This request is not authorized to perform this operation"
- Verify all three roles are assigned
- Wait 5-10 minutes for role propagation
- Check role assignment scope (should be storage account level)

### "DefaultAzureCredential failed to retrieve a token"
- **In Azure:** Ensure managed identity is enabled on App Service
- **Locally:** Sign in to Visual Studio with your Azure account
- Verify your account has the required roles

### "No service connection string or service account information provided"
- Ensure `StorageAccountName` is configured when `EnableRBAC: true`
- Check configuration is loaded correctly

## Implementation Details

### DefaultAzureCredential Chain

The `DefaultAzureCredential` tries these authentication methods in order:

1. **EnvironmentCredential** - Environment variables (for Docker/containers)
2. **ManagedIdentityCredential** - Managed Identity (in Azure)
3. **VisualStudioCredential** - Visual Studio signed-in account
4. **AzureCliCredential** - Azure CLI logged-in account
5. **AzurePowerShellCredential** - Azure PowerShell logged-in account (excluded)
6. **SharedTokenCacheCredential** - Shared token cache (excluded)

This means the implementation "just works" in most environments without configuration changes.

### URI Construction

For RBAC, the application constructs service URIs from the storage account name:
- Tables: `https://{accountName}.table.core.windows.net`
- Blobs: `https://{accountName}.blob.core.windows.net`
- Queues: `https://{accountName}.queue.core.windows.net`

### Credential Caching

The `StorageCredentialFactory` caches the `DefaultAzureCredential` instance to avoid recreating it for each service.

## Future Enhancements

Possible future improvements:
- Support for user-assigned managed identities
- Support for Azure Government / Azure China cloud endpoints
- Per-service RBAC configuration (enable RBAC only for blobs, not tables)
- Automatic role assignment during deployment

## Related Files

### Code Files
- `Common/Engine/Config/AppConfig.cs`
- `Common/Engine/Storage/StorageCredentialFactory.cs`
- `Common/Engine/TableStorageManager.cs`
- `Common/Engine/MessageTemplateStorageManager.cs`
- `Common/Engine/SmartGroupStorageManager.cs`
- `Common/Engine/SettingsStorageManager.cs`
- `Common/Engine/BotConversationCache.cs`
- `Common/Engine/Services/BatchQueueService.cs`
- `Common/Engine/Services/UserCache/AzureTableCacheStorage.cs`
- `Common/Engine/DependencyInjection.cs`
- `Common/Engine/DependencyInjection/BotServiceExtensions.cs`
- `Common/Engine/DependencyInjection/MessageTemplateServiceExtensions.cs`

### Documentation Files
- `docs/STORAGE-RBAC-SETUP.md` - Setup guide
- `UnitTests/IntegrationTests/README.md` - Testing instructions
- `UnitTests/appsettings.example.json` - Configuration example
- `secrets.json` - Developer configuration example

## Support

For questions or issues with RBAC setup:
1. Check the [Storage RBAC Setup Guide](docs/STORAGE-RBAC-SETUP.md)
2. Review application logs for authentication errors
3. Verify role assignments in Azure Portal
4. Open an issue in the GitHub repository
