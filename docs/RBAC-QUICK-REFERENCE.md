# Azure Storage RBAC - Quick Reference

## Configuration Quick Reference

### Production (Azure App Service with Managed Identity)

```json
{
  "EnableRBAC": true,
  "StorageAccountName": "yourstorageaccount",
  "ConnectionStrings": {
    "Storage": null
  }
}
```

**Setup Steps:**
1. Enable Managed Identity on App Service
2. Assign these roles to the Managed Identity:
   - Storage Blob Data Contributor
   - Storage Queue Data Contributor
   - Storage Table Data Contributor
3. Configure app settings
4. Deploy

---

### Production (Service Principal Authentication)

Use this when you need a specific service principal instead of Managed Identity:

```json
{
  "EnableRBAC": true,
  "StorageAccountName": "yourstorageaccount",
  "StorageAuthConfig": {
    "ClientId": "your-sp-client-id",
    "ClientSecret": "your-sp-secret",
    "TenantId": "your-tenant-id"
  }
}
```

**Setup Steps:**
1. Create service principal for storage access
2. Assign storage roles to the service principal
3. Store credentials in Azure Key Vault
4. Reference secrets in app settings
5. Deploy

---

### Local Development with RBAC (Visual Studio)

```json
{
  "EnableRBAC": true,
  "StorageAccountName": "yourstorageaccount",
  "ConnectionStrings": {
    "Storage": null
  }
}
```

**Setup Steps:**
1. Sign in to Visual Studio with Azure account
2. Request Azure admin to assign storage roles to your account
3. Update user secrets
4. Run

---

### Local Development with Azurite Emulator

```json
{
  "EnableRBAC": false,
  "ConnectionStrings": {
    "Storage": "UseDevelopmentStorage=true"
  }
}
```

**Setup Steps:**
1. Install Azurite (included with Visual Studio)
2. Update user secrets
3. Run (Azurite starts automatically)

---

### Local Development with Azure Storage Account

```json
{
  "EnableRBAC": false,
  "ConnectionStrings": {
    "Storage": "DefaultEndpointsProtocol=https;AccountName=xxx;AccountKey=xxx;EndpointSuffix=core.windows.net"
  }
}
```

**Setup Steps:**
1. Get connection string from Azure Portal
2. Update user secrets
3. Run

---

## Azure CLI Commands

### Assign Roles to App Service Managed Identity

```bash
# Variables
APP_NAME="your-app-name"
RESOURCE_GROUP="your-resource-group"
STORAGE_ACCOUNT="your-storage-account"

# Get Managed Identity ID
APP_IDENTITY_ID=$(az webapp identity show \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query principalId -o tsv)

# Get Storage Account ID
STORAGE_ID=$(az storage account show \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --query id -o tsv)

# Assign Roles
az role assignment create \
  --assignee $APP_IDENTITY_ID \
  --role "Storage Blob Data Contributor" \
  --scope $STORAGE_ID

az role assignment create \
  --assignee $APP_IDENTITY_ID \
  --role "Storage Queue Data Contributor" \
  --scope $STORAGE_ID

az role assignment create \
  --assignee $APP_IDENTITY_ID \
  --role "Storage Table Data Contributor" \
  --scope $STORAGE_ID
```

### Assign Roles to Your User Account (for local development)

```bash
# Get your user ID
USER_ID=$(az ad signed-in-user show --query id -o tsv)

# Get Storage Account ID
STORAGE_ID=$(az storage account show \
  --name your-storage-account \
  --resource-group your-resource-group \
  --query id -o tsv)

# Assign Roles
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

---

## User Secrets Commands

```bash
# Navigate to project directory
cd Web/Web.Server  # or UnitTests for integration tests

# RBAC Configuration
dotnet user-secrets set "EnableRBAC" "true"
dotnet user-secrets set "StorageAccountName" "yourstorageaccount"

# Connection String Configuration (alternative)
dotnet user-secrets set "EnableRBAC" "false"
dotnet user-secrets set "ConnectionStrings:Storage" "your-connection-string"

# View all secrets
dotnet user-secrets list

# Clear a secret
dotnet user-secrets remove "ConnectionStrings:Storage"
```

---

## Troubleshooting

### Error: "This request is not authorized to perform this operation"

**Cause:** Missing role assignments

**Solution:**
1. Verify all three roles are assigned
2. Wait 5-10 minutes for propagation
3. Restart the application

---

### Error: "DefaultAzureCredential failed to retrieve a token"

**Cause:** No valid credentials found

**Solution (Azure):**
- Enable Managed Identity on App Service
- Restart App Service after enabling

**Solution (Local):**
- Sign in to Visual Studio with Azure account
- Ensure your account has storage roles
- Close and reopen Visual Studio

---

### Error: "StorageAccountName must be configured"

**Cause:** Missing storage account name when RBAC is enabled

**Solution:**
```json
{
  "EnableRBAC": true,
  "StorageAccountName": "yourstorageaccount",  // Add this
  "ConnectionStrings": {
    "Storage": null
  }
}
```

---

### Application works locally but not in Azure

**Cause:** Managed Identity not enabled or roles not assigned

**Solution:**
1. Check Managed Identity is enabled: Azure Portal ? App Service ? Identity ? System assigned = On
2. Verify role assignments: Storage Account ? Access Control (IAM) ? Role assignments
3. Check application logs for specific error messages

---

## Verification Checklist

### Azure Deployment
- [ ] System-assigned Managed Identity is enabled on App Service
- [ ] Storage Blob Data Contributor role assigned
- [ ] Storage Queue Data Contributor role assigned
- [ ] Storage Table Data Contributor role assigned
- [ ] `EnableRBAC` = `true` in App Settings
- [ ] `StorageAccountName` is set in App Settings
- [ ] Application restarted after configuration changes

### Local Development (RBAC)
- [ ] Signed in to Visual Studio with Azure account
- [ ] Storage Blob Data Contributor role assigned to your account
- [ ] Storage Queue Data Contributor role assigned to your account
- [ ] Storage Table Data Contributor role assigned to your account
- [ ] `EnableRBAC: true` in user secrets
- [ ] `StorageAccountName` is set in user secrets

### Local Development (Connection String)
- [ ] `EnableRBAC: false` in user secrets
- [ ] `Storage` connection string is set in user secrets
- [ ] Azurite is running (if using emulator)

---

## Quick Links

- **Detailed Setup Guide:** [docs/STORAGE-RBAC-SETUP.md](docs/STORAGE-RBAC-SETUP.md)
- **Implementation Details:** [docs/RBAC-IMPLEMENTATION.md](docs/RBAC-IMPLEMENTATION.md)
- **Azure RBAC Documentation:** https://learn.microsoft.com/azure/storage/common/storage-auth-aad
- **DefaultAzureCredential:** https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential

---

## Decision Matrix: Which Auth Method?

| Scenario | Recommended | Configuration |
|----------|-------------|---------------|
| Production deployment in Azure | **RBAC (Managed Identity)** | `EnableRBAC: true` + Managed Identity |
| Production with service principal | **RBAC (Service Principal)** | `EnableRBAC: true` + `StorageAuthConfig` |
| Staging/Test in Azure | **RBAC (Managed Identity)** | `EnableRBAC: true` + Managed Identity |
| Local development (online) | **RBAC (Visual Studio)** | `EnableRBAC: true` + Visual Studio auth |
| Local development (service principal) | **RBAC (Service Principal)** | `EnableRBAC: true` + `StorageAuthConfig` |
| Local development (offline) | **Connection String** | `EnableRBAC: false` + Azurite |
| CI/CD pipelines | **RBAC (Managed Identity or SP)** | Depends on environment |
| Integration tests | **Either** | All methods supported |

**Recommendation:** 
- Use Managed Identity (RBAC without StorageAuthConfig) in Azure for best security
- Use Service Principal (RBAC with StorageAuthConfig) when Managed Identity isn't available
- Use Connection String only for local development with Azurite
