# Configuration Migration Guide

## Overview

This guide helps you migrate from the old configuration structure (where `EnableRBAC` and `StorageAccountName` were under `ConnectionStrings`) to the new structure (where they are at the root level).

## Quick Migration Checklist

- [ ] Update Azure App Service application settings
- [ ] Update local user secrets
- [ ] Update any appsettings.json files
- [ ] Update deployment scripts
- [ ] Update CI/CD pipeline variables
- [ ] Update documentation/runbooks

## Migration Steps

### 1. Azure App Service Configuration

#### Option A: Using Azure Portal

1. Navigate to your **App Service** in Azure Portal
2. Go to **Configuration** ? **Application settings**
3. **Add new settings:**
   - Name: `EnableRBAC`, Value: `true` (or `false`)
   - Name: `StorageAccountName`, Value: your storage account name
4. **Delete old settings:**
   - `ConnectionStrings__EnableRBAC`
   - `ConnectionStrings__StorageAccountName`
5. Click **Save**
6. **Restart** your App Service

#### Option B: Using Azure CLI

```bash
# Variables
APP_NAME="your-app-name"
RESOURCE_GROUP="your-resource-group"
STORAGE_ACCOUNT="your-storage-account"

# Remove old settings
az webapp config appsettings delete \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --setting-names ConnectionStrings__EnableRBAC ConnectionStrings__StorageAccountName

# Add new settings
az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings EnableRBAC=true \
             StorageAccountName=$STORAGE_ACCOUNT

# Restart app
az webapp restart \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP
```

### 2. Local Development (User Secrets)

#### For Web.Server Project:

```bash
cd src/Full/Bot/Web/Web.Server

# Remove old settings
dotnet user-secrets remove "ConnectionStrings:EnableRBAC"
dotnet user-secrets remove "ConnectionStrings:StorageAccountName"

# Add new settings (RBAC enabled)
dotnet user-secrets set "EnableRBAC" "true"
dotnet user-secrets set "StorageAccountName" "your-storage-account"

# OR for connection string mode (Azurite)
dotnet user-secrets set "EnableRBAC" "false"
```

#### For UnitTests Project:

```bash
cd src/Full/Bot/UnitTests

# Remove old settings
dotnet user-secrets remove "ConnectionStrings:EnableRBAC"
dotnet user-secrets remove "ConnectionStrings:StorageAccountName"

# Add new settings
dotnet user-secrets set "EnableRBAC" "true"
dotnet user-secrets set "StorageAccountName" "your-storage-account"
```

### 3. Configuration Files (appsettings.json)

If you have any appsettings.json files with RBAC configuration, update them:

#### Before:
```json
{
  "ConnectionStrings": {
    "Storage": "...",
    "EnableRBAC": true,
    "StorageAccountName": "mystorageaccount"
  }
}
```

#### After:
```json
{
  "EnableRBAC": true,
  "StorageAccountName": "mystorageaccount",
  "ConnectionStrings": {
    "Storage": "..."
  }
}
```

### 4. Environment Variables

If you're using environment variables (e.g., in Docker, Kubernetes):

#### Before:
```bash
ConnectionStrings__EnableRBAC=true
ConnectionStrings__StorageAccountName=mystorageaccount
```

#### After:
```bash
EnableRBAC=true
StorageAccountName=mystorageaccount
```

### 5. Azure DevOps / GitHub Actions

Update your pipeline variables:

#### Azure DevOps Variables:

**Remove:**
- `ConnectionStrings.EnableRBAC`
- `ConnectionStrings.StorageAccountName`

**Add:**
- `EnableRBAC`
- `StorageAccountName`

#### GitHub Actions Secrets/Variables:

**Remove:**
- `CONNECTIONSTRINGS_ENABLERBAC`
- `CONNECTIONSTRINGS_STORAGEACCOUNTNAME`

**Add:**
- `ENABLERBAC`
- `STORAGEACCOUNTNAME`

### 6. ARM Templates / Bicep

If you're using Infrastructure as Code:

#### ARM Template - Before:
```json
{
  "name": "ConnectionStrings__EnableRBAC",
  "value": "true"
},
{
  "name": "ConnectionStrings__StorageAccountName",
  "value": "[parameters('storageAccountName')]"
}
```

#### ARM Template - After:
```json
{
  "name": "EnableRBAC",
  "value": "true"
},
{
  "name": "StorageAccountName",
  "value": "[parameters('storageAccountName')]"
}
```

#### Bicep - Before:
```bicep
{
  name: 'ConnectionStrings__EnableRBAC'
  value: 'true'
}
{
  name: 'ConnectionStrings__StorageAccountName'
  value: storageAccountName
}
```

#### Bicep - After:
```bicep
{
  name: 'EnableRBAC'
  value: 'true'
}
{
  name: 'StorageAccountName'
  value: storageAccountName
}
```

## Verification

After migration, verify the configuration is correct:

### 1. Check Application Logs

Start the application and look for:
```
Using RBAC authentication for Azure Storage
```

or

```
Using connection string authentication for Azure Storage
```

### 2. Test Storage Operations

Verify that:
- Message templates can be created and retrieved
- Blobs are accessible
- Queue messages can be sent/received
- Table operations work

### 3. Check Azure App Service Configuration

In Azure Portal ? App Service ? Configuration ? Application settings:

Should see:
- ? `EnableRBAC` = `true` (or `false`)
- ? `StorageAccountName` = your account name
- ? `ConnectionStrings__Storage` (if using connection string mode)
- ? No `ConnectionStrings__EnableRBAC`
- ? No `ConnectionStrings__StorageAccountName`

## Troubleshooting

### Error: "StorageAccountName must be configured"

**Problem:** Application can't find `StorageAccountName` configuration.

**Solution:**
```bash
# Verify the setting exists
az webapp config appsettings list \
  --name your-app \
  --resource-group your-group \
  --query "[?name=='StorageAccountName']"

# Add if missing
az webapp config appsettings set \
  --name your-app \
  --resource-group your-group \
  --settings StorageAccountName=your-storage-account
```

### Application Still Looking for Old Configuration

**Problem:** Old configuration paths are still in use.

**Solution:**
1. Ensure you've pulled the latest code changes
2. Rebuild the application: `dotnet build`
3. Verify you're running the updated version
4. Clear any cached configurations

### Configuration Not Loading

**Problem:** Settings don't seem to take effect.

**Solution:**
1. Restart the application/app service
2. Clear browser cache if using web interface
3. Check configuration order (user secrets ? appsettings ? environment variables ? Azure app settings)
4. Verify no conflicting configurations exist

## Rollback Procedure

If you need to rollback to the old configuration structure:

### 1. Revert Code Changes
```bash
git revert <commit-hash>
```

### 2. Restore Old Configuration
```bash
# In Azure App Service
az webapp config appsettings set \
  --name your-app \
  --resource-group your-group \
  --settings ConnectionStrings__EnableRBAC=true \
             ConnectionStrings__StorageAccountName=your-account

# Remove new settings
az webapp config appsettings delete \
  --name your-app \
  --resource-group your-group \
  --setting-names EnableRBAC StorageAccountName

# Restart
az webapp restart \
  --name your-app \
  --resource-group your-group
```

## Common Scenarios

### Scenario 1: Development with Azurite

**Old Configuration:**
```json
{
  "ConnectionStrings": {
    "Storage": "UseDevelopmentStorage=true",
    "EnableRBAC": false
  }
}
```

**New Configuration:**
```json
{
  "EnableRBAC": false,
  "ConnectionStrings": {
    "Storage": "UseDevelopmentStorage=true"
  }
}
```

### Scenario 2: Production with RBAC

**Old Configuration:**
```json
{
  "ConnectionStrings": {
    "EnableRBAC": true,
    "StorageAccountName": "prodstorageaccount"
  }
}
```

**New Configuration:**
```json
{
  "EnableRBAC": true,
  "StorageAccountName": "prodstorageaccount",
  "ConnectionStrings": {
    "Storage": null
  }
}
```

### Scenario 3: Staging with Connection String

**Old Configuration:**
```json
{
  "ConnectionStrings": {
    "Storage": "DefaultEndpointsProtocol=https;AccountName=...",
    "EnableRBAC": false
  }
}
```

**New Configuration:**
```json
{
  "EnableRBAC": false,
  "ConnectionStrings": {
    "Storage": "DefaultEndpointsProtocol=https;AccountName=..."
  }
}
```

## Need Help?

If you encounter issues during migration:

1. **Check the Logs**: Azure Portal ? App Service ? Log stream
2. **Review Documentation**: See `docs/STORAGE-RBAC-SETUP.md`
3. **Verify Configuration**: Use Azure CLI to list all settings
4. **Test Locally First**: Migrate local development setup before production
5. **Use Staging Slots**: Test configuration in staging before swapping to production

## Best Practices

? **Do:**
- Migrate development environment first
- Test thoroughly before migrating production
- Document your specific configuration
- Update runbooks and documentation
- Use Azure App Service deployment slots for zero-downtime migration

? **Don't:**
- Migrate production without testing
- Delete old configuration until new one is verified
- Forget to update CI/CD pipelines
- Skip restarting the application after configuration changes
