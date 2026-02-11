# Azure Storage RBAC Configuration Guide

This guide explains how to configure Azure Role-Based Access Control (RBAC) for Azure Storage, which is the recommended authentication method for production deployments.

## Overview

The Copilot Adoption Bot supports two authentication methods for Azure Storage:

1. **RBAC Authentication (Recommended)** - Uses Azure AD and Managed Identity
2. **Connection String Authentication** - Uses storage account keys

RBAC is more secure because:
- No storage account keys to manage or rotate
- Fine-grained permissions using Azure roles
- Works seamlessly with Managed Identity in Azure
- Automatic token refresh
- Full audit logging through Azure AD

## Configuration

### Enable RBAC

Set these values in your application configuration:

```json
{
  "EnableRBAC": true,
  "StorageAccountName": "mystorageaccount",
  "ConnectionStrings": {
    "Storage": null
  }
}
```

**Configuration Properties:**

**Note:** `EnableRBAC` and `StorageAccountName` are root-level configuration properties, not part of `ConnectionStrings`.

| Property | Description | Required | Default |
|----------|-------------|----------|---------|
| `EnableRBAC` | When `true`, uses RBAC authentication | No | `true` |
| `StorageAccountName` | Name of your Azure Storage account | Yes (when RBAC enabled) | - |
| `Storage` | Connection string (only used when RBAC disabled) | Yes (when RBAC disabled) | - |

### Optional: Service Principal Authentication

If you need to use a specific service principal instead of Managed Identity or DefaultAzureCredential:

```json
{
  "EnableRBAC": true,
  "StorageAccountName": "mystorageaccount",
  "StorageAuthConfig": {
    "ClientId": "your-service-principal-client-id",
    "ClientSecret": "your-service-principal-secret",
    "TenantId": "your-tenant-id"
  }
}
```

**When to use StorageAuthConfig:**
- You need to use a specific service principal instead of Managed Identity
- DefaultAzureCredential doesn't work in your environment
- You want explicit control over which identity is used
- Running in environments without Managed Identity support

**Note:** If `StorageAuthConfig` is not provided, the application will use `DefaultAzureCredential` (Managed Identity in Azure, Visual Studio credentials locally).

## Azure Setup

### 1. Enable Managed Identity on App Service

```bash
# Enable system-assigned managed identity
az webapp identity assign \
  --name myappname \
  --resource-group myresourcegroup

# Get the identity's Object ID (save this for next step)
az webapp identity show \
  --name myappname \
  --resource-group myresourcegroup \
  --query principalId -o tsv
```

### 2. Assign Required Azure Roles

The application needs these roles on the Storage Account:

| Role | Purpose |
|------|---------|
| **Storage Blob Data Contributor** | Read/write message templates in blob containers |
| **Storage Queue Data Contributor** | Send/receive batch processing messages |
| **Storage Table Data Contributor** | Store metadata, logs, and user cache |

**Assign roles using Azure CLI:**

```bash
# Store values
APP_IDENTITY_ID="<object-id-from-step-1>"
RESOURCE_GROUP="myresourcegroup"
STORAGE_ACCOUNT="mystorageaccount"

# Get Storage Account Resource ID
STORAGE_ID=$(az storage account show \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --query id -o tsv)

# Assign Blob Data Contributor role
az role assignment create \
  --assignee $APP_IDENTITY_ID \
  --role "Storage Blob Data Contributor" \
  --scope $STORAGE_ID

# Assign Queue Data Contributor role
az role assignment create \
  --assignee $APP_IDENTITY_ID \
  --role "Storage Queue Data Contributor" \
  --scope $STORAGE_ID

# Assign Table Data Contributor role
az role assignment create \
  --assignee $APP_IDENTITY_ID \
  --role "Storage Table Data Contributor" \
  --scope $STORAGE_ID
```

**Assign roles using Azure Portal:**

1. Navigate to your **Storage Account** in Azure Portal
2. Click **Access Control (IAM)** in the left menu
3. Click **+ Add** ? **Add role assignment**
4. Select role (e.g., "Storage Blob Data Contributor")
5. Click **Next**
6. Select **Managed Identity** as assignment type
7. Click **+ Select members**
8. Find your App Service managed identity
9. Click **Review + assign**
10. Repeat for each required role

### 3. Update App Service Configuration

Update application settings in Azure Portal:

1. Navigate to your **App Service**
2. Click **Configuration** ? **Application settings**
3. Add or update these settings:
   - `EnableRBAC` = `true`
   - `StorageAccountName` = `mystorageaccount`
   - Remove or set `ConnectionStrings__Storage` to empty
4. Click **Save**
5. Restart the app service

### Alternative: Service Principal Authentication

If you prefer to use a specific service principal instead of Managed Identity:

#### 1. Create Service Principal

```bash
# Create service principal
SP_NAME="storage-access-sp"
SP_OUTPUT=$(az ad sp create-for-rbac \
  --name $SP_NAME \
  --skip-assignment \
  --output json)

# Extract credentials (save these securely)
SP_CLIENT_ID=$(echo $SP_OUTPUT | jq -r '.appId')
SP_CLIENT_SECRET=$(echo $SP_OUTPUT | jq -r '.password')
SP_TENANT_ID=$(echo $SP_OUTPUT | jq -r '.tenant')

echo "Client ID: $SP_CLIENT_ID"
echo "Client Secret: $SP_CLIENT_SECRET"
echo "Tenant ID: $SP_TENANT_ID"
```

#### 2. Assign Storage Roles to Service Principal

```bash
# Get Storage Account ID
STORAGE_ID=$(az storage account show \
  --name mystorageaccount \
  --resource-group myresourcegroup \
  --query id -o tsv)

# Assign roles
az role assignment create \
  --assignee $SP_CLIENT_ID \
  --role "Storage Blob Data Contributor" \
  --scope $STORAGE_ID

az role assignment create \
  --assignee $SP_CLIENT_ID \
  --role "Storage Queue Data Contributor" \
  --scope $STORAGE_ID

az role assignment create \
  --assignee $SP_CLIENT_ID \
  --role "Storage Table Data Contributor" \
  --scope $STORAGE_ID
```

#### 3. Configure Application Settings

Store the service principal credentials in Azure Key Vault (recommended):

```bash
az keyvault secret set \
  --vault-name mykeyvault \
  --name StorageAuthClientId \
  --value $SP_CLIENT_ID

az keyvault secret set \
  --vault-name mykeyvault \
  --name StorageAuthClientSecret \
  --value $SP_CLIENT_SECRET
```

Then reference in App Service:

```
EnableRBAC=true
StorageAccountName=mystorageaccount
StorageAuthConfig__ClientId=@Microsoft.KeyVault(SecretUri=https://mykeyvault.vault.azure.net/secrets/StorageAuthClientId/)
StorageAuthConfig__ClientSecret=@Microsoft.KeyVault(SecretUri=https://mykeyvault.vault.azure.net/secrets/StorageAuthClientSecret/)
StorageAuthConfig__TenantId=your-tenant-id
```

## Local Development Setup

### Option 1: RBAC with Visual Studio (Recommended)

When developing locally, the application can use your Visual Studio credentials:

**Prerequisites:**
- Signed in to Visual Studio with your Azure account
- Your account has the required storage roles assigned

**Configuration (user secrets):**

```json
{
  "EnableRBAC": true,
  "StorageAccountName": "mystorageaccount",
  "ConnectionStrings": {
    "Storage": null
  }
}
```

**Assign roles to your user account:**

```bash
# Get your user's Object ID
USER_ID=$(az ad signed-in-user show --query id -o tsv)

# Get Storage Account Resource ID
STORAGE_ID=$(az storage account show \
  --name mystorageaccount \
  --resource-group myresourcegroup \
  --query id -o tsv)

# Assign roles
az role assignment create \
  --assignee $USER_ID \
  --role "Storage Blob Data Contributor" \
  --scope $STORAGE_ID

az role assignment create \
  --assignee $USER_ID \
  --role "Storage Queue Data Contributor" \
  --scope $STORAGE_ID

az role assignment create \
  --assignee $USER_ID \
  --role "Storage Table Data Contributor" \
  --scope $STORAGE_ID
```

### Option 2: RBAC with Service Principal

For local development using a service principal:

**Configuration (user secrets):**

```json
{
  "EnableRBAC": true,
  "StorageAccountName": "mystorageaccount",
  "StorageAuthConfig": {
    "ClientId": "your-service-principal-client-id",
    "ClientSecret": "your-service-principal-secret",
    "TenantId": "your-tenant-id"
  }
}
```

**Setup:**
1. Create a service principal (see "Alternative: Service Principal Authentication" above)
2. Assign storage roles to the service principal
3. Add credentials to user secrets
4. Run

### Option 3: Connection String with Azurite (Local Emulator)

For offline development or testing:

**Configuration (user secrets):**

```json
{
  "EnableRBAC": false,
  "ConnectionStrings": {
    "Storage": "UseDevelopmentStorage=true"
  }
}
```

**Prerequisites:**
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) installed and running
- Visual Studio will start Azurite automatically when debugging

### Option 4: Connection String with Azure Storage

Use an actual Azure Storage account with connection string:

**Configuration (user secrets):**

```json
{
  "EnableRBAC": false,
  "ConnectionStrings": {
    "Storage": "DefaultEndpointsProtocol=https;AccountName=mystorageaccount;AccountKey=xxx;EndpointSuffix=core.windows.net"
  }
}
```

?? **Not recommended for production** - Only use for development/testing.

## Testing RBAC Configuration

### Verify Roles are Assigned

```bash
# List role assignments for your managed identity
az role assignment list \
  --assignee $APP_IDENTITY_ID \
  --scope $STORAGE_ID \
  --output table
```

### Test Application Connectivity

1. Deploy or start the application
2. Check application logs in Azure Portal:
   - Navigate to **App Service** ? **Log stream**
   - Look for: `"Using RBAC authentication for Azure Storage"`
3. Verify storage operations work:
   - Create a message template
   - Send a message batch
   - Check storage containers/tables/queues

### Common Issues

**Error: "This request is not authorized to perform this operation"**
- Solution: Verify all three roles are assigned to the managed identity
- Check role assignment scope is on the storage account
- Wait 5-10 minutes for role assignments to propagate

**Error: "No service connection string or service account information provided"**
- Solution: Ensure `StorageAccountName` is configured
- Verify `EnableRBAC` is set to `true`

**Error: "DefaultAzureCredential failed to retrieve a token"**
- Solution (Azure): Enable managed identity on App Service
- Solution (Local): Sign in to Visual Studio with Azure account

## Migration from Connection Strings

To migrate an existing deployment from connection strings to RBAC:

1. **Assign roles** to the managed identity (Step 2 above)
2. **Wait 10 minutes** for role propagation
3. **Update configuration**:
```bash
az webapp config appsettings set \
  --name myappname \
  --resource-group myresourcegroup \
  --settings EnableRBAC=true \
             StorageAccountName=mystorageaccount
```
4. **Restart app service**:
   ```bash
   az webapp restart \
     --name myappname \
     --resource-group myresourcegroup
   ```
5. **Verify** application works correctly
6. **Remove connection string** (optional but recommended):
   ```bash
   az webapp config appsettings delete \
     --name myappname \
     --resource-group myresourcegroup \
     --setting-names ConnectionStrings__Storage
   ```

## Security Best Practices

When using RBAC:

? **DO:**
- Use system-assigned managed identity for App Service
- Assign roles at the storage account level
- Enable storage account firewall and allow only required networks
- Monitor role assignments and access through Azure Monitor
- Use separate storage accounts for dev/test/prod environments

? **DON'T:**
- Grant broader roles than necessary (e.g., "Contributor" instead of "Storage Blob Data Contributor")
- Assign roles at the subscription or resource group level unless required
- Share managed identity credentials
- Use connection strings in production

## Additional Resources

- [Azure Storage RBAC Documentation](https://learn.microsoft.com/azure/storage/common/storage-auth-aad)
- [Managed Identity Overview](https://learn.microsoft.com/azure/active-directory/managed-identities-azure-resources/overview)
- [Azure Built-in Roles for Storage](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#storage)
- [DefaultAzureCredential](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential)
