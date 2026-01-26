# Troubleshooting Guide

Common issues and solutions for the Copilot Adoption Bot.

## Table of Contents

- [Authentication Issues](#authentication-issues)
- [Microsoft Graph Errors](#microsoft-graph-errors)
- [Bot Connection Issues](#bot-connection-issues)
- [Storage Issues](#storage-issues)
- [Deployment Issues](#deployment-issues)
- [Copilot Stats Issues](#copilot-stats-issues)
- [Performance Issues](#performance-issues)

## Authentication Issues

### Error: "Current authenticated context is not valid for this request"

**Symptoms:**
- Cannot access Microsoft Graph API
- Authentication errors in logs
- Users cannot log in to web interface

**Causes:**
- Invalid or expired client secret
- Incorrect tenant ID
- Missing or incorrect configuration

**Solutions:**

1. **Verify Configuration:**
   ```bash
   # Check user secrets
   cd src/Full/Bot/Web/Web.Server
   dotnet user-secrets list
   
   # Verify values
   # GraphConfig:TenantId should be a GUID without extra characters
   # GraphConfig:ClientId should be a GUID
   # GraphConfig:ClientSecret should be set
   ```

2. **Check Tenant ID:**
   - Ensure `TenantId` doesn't have curly braces or extra whitespace
   - Verify it matches your Azure AD tenant

3. **Regenerate Client Secret:**
   - Go to Azure Portal ? Entra ID ? App registrations
   - Find your app
   - Go to Certificates & secrets
   - Create new client secret
   - Update configuration with new secret

4. **Wait for Propagation:**
   - After making changes, wait 5-10 minutes
   - Restart the application

### Error: "AADSTS700016: Application not found in the directory"

**Symptoms:**
- Cannot authenticate
- App registration not found error

**Solutions:**

1. **Verify App Registration:**
   - Go to Azure Portal ? Entra ID ? App registrations
   - Search for your app by Client ID
   - Ensure it exists and is enabled

2. **Check Multi-Tenant Settings:**
   - If app is single-tenant, ensure `TenantId` is set correctly
   - If multi-tenant, use `organizations` in Authority URL

3. **Verify Authority URL:**
   ```bash
   # Single-tenant
   dotnet user-secrets set "GraphConfig:Authority" "https://login.microsoftonline.com/{your-tenant-id}"
   
   # Multi-tenant
   dotnet user-secrets set "GraphConfig:Authority" "https://login.microsoftonline.com/organizations"
   ```

### Error: "Invalid redirect URI"

**Symptoms:**
- Authentication redirects fail
- CORS errors in browser

**Solutions:**

1. **Add Redirect URIs in App Registration:**
   - Go to Azure Portal ? Entra ID ? App registrations ? Your app
   - Go to Authentication
   - Add redirect URIs:
     - For local: `https://localhost:5001/auth-end.html`
     - For production: `https://your-app.azurewebsites.net/auth-end.html`

2. **Configure CORS:**
   - Ensure frontend origin is allowed in backend CORS policy
   - Check `Program.cs` for CORS configuration

## Microsoft Graph Errors

### Error: "Insufficient privileges to complete the operation"

**Symptoms:**
- Cannot read users
- Cannot access Graph API
- Permission denied errors

**Solutions:**

1. **Verify Application Permissions:**
   - Go to Azure Portal ? Entra ID ? App registrations ? Your app
   - Click **API permissions**
   - Ensure all required permissions are listed:
     - `User.Read.All` (Application)
     - `TeamsActivity.Send` (Application)
     - `TeamsAppInstallation.ReadWriteForUser.All` (Application)
     - `Reports.Read.All` (Application, optional)

2. **Grant Admin Consent:**
   - Click **Grant admin consent for [Your Tenant]**
   - Wait 5-10 minutes for changes to propagate

3. **Check Permission Type:**
   - Ensure permissions are **Application** type, not Delegated
   - Application permissions require admin consent

4. **Verify Token Scopes:**
   ```csharp
   // Add diagnostic logging
   _logger.LogInformation("Token scopes: {Scopes}", string.Join(", ", token.Scopes));
   ```

5. **Test Graph Connection:**
   - Navigate to `/api/Diagnostics/TestGraphConnection`
   - Check logs for detailed error messages

### Error: "The tenant for tenant guid does not exist"

**Symptoms:**
- Tenant not found errors
- Cannot access Graph API

**Solutions:**

1. **Verify Tenant ID:**
   ```bash
   # Get tenant ID from Azure Portal
   az account show --query tenantId -o tsv
   ```

2. **Update Configuration:**
   ```bash
   dotnet user-secrets set "GraphConfig:TenantId" "your-actual-tenant-id"
   ```

3. **Check for Whitespace:**
   - Ensure no extra spaces or characters in tenant ID
   - Verify it's a valid GUID format

## Bot Connection Issues

### Bot Not Responding in Teams

**Symptoms:**
- Bot doesn't respond to messages
- Bot appears offline
- Messages sent but no response

**Solutions:**

1. **Verify Bot Endpoint:**
   - Go to Teams Developer Portal ? Bot management ? Your bot
   - Check endpoint address: `https://your-app.azurewebsites.net/api/messages`
   - Ensure URL is correct and uses HTTPS

2. **Check App Service Status:**
   - Go to Azure Portal ? App Service
   - Verify app is running
   - Check Application Insights logs for errors

3. **Verify Bot Credentials:**
   ```bash
   # Check configuration
   dotnet user-secrets list
   
   # Verify MicrosoftAppId matches bot ID
   # Verify MicrosoftAppPassword is correct
   ```

4. **Check Bot Installation:**
   - Ensure `TeamsAppInstallation.ReadWriteForUser.All` permission is granted
   - Try uninstalling and reinstalling the bot

5. **Review Logs:**
   ```bash
   # Check Application Insights
   # Look for errors in the /api/messages endpoint
   ```

### Bot Installation Fails

**Symptoms:**
- Cannot install bot to Teams
- Installation error messages

**Solutions:**

1. **Verify Permissions:**
   - Ensure `TeamsAppInstallation.ReadWriteForUser.All` is granted
   - Admin consent must be given

2. **Check Bot Manifest:**
   - Verify bot ID in manifest matches MicrosoftAppId
   - Ensure manifest is valid JSON

3. **App Catalog Issues:**
   - If using app catalog, verify app is uploaded
   - Check AppCatalogTeamAppId configuration

## Storage Issues

### Error: "Unable to connect to storage account"

**Symptoms:**
- Cannot access templates
- Storage connection errors
- Timeout errors

**Solutions:**

1. **Verify Storage Configuration:**
   ```bash
   # Check storage configuration
   dotnet user-secrets list | grep Storage
   
   # For Connection String (Legacy):
   # ConnectionStrings:Storage = DefaultEndpointsProtocol=https;AccountName=...
   
   # For RBAC:
   # StorageAuthConfig:UseRBAC = true
   # StorageAuthConfig:StorageAccountName = yourstorageaccount
   ```

2. **Check Storage Account:**
   - Go to Azure Portal ? Storage Account
   - Verify account exists and is accessible
   - Check if account key is correct

3. **Firewall Rules:**
   - Go to Storage Account ? Networking
   - Add your IP address or App Service IPs
   - Ensure firewall allows necessary connections

4. **Test Connectivity:**
   ```bash
   # Use Azure Storage Explorer
   # Try to connect with same connection string or RBAC credentials
   ```

5. **RBAC Authentication Issues (when UseRBAC = true):**
   - Verify RBAC role assignments on the storage account:
     - `Storage Blob Data Contributor`
     - `Storage Table Data Contributor`
     - `Storage Queue Data Contributor`
   - Check identity being used:
     - Local dev: Azure CLI (`az account show`)
     - Azure: Managed Identity or service principal
   - If using override credentials, verify ClientId, ClientSecret, and TenantId are correct
   - Test RBAC permissions:
     ```bash
     az storage blob list --account-name yourstorageaccount --container-name message-templates --auth-mode login
     ```

### Error: "Blob container not found"

**Symptoms:**
- Templates cannot be saved
- Blob storage errors

**Solutions:**

1. **Container Auto-Creation:**
   - The `message-templates` container is auto-created on first use
   - Check logs for container creation errors

2. **Manual Creation:**
   ```bash
   az storage container create \
     --name message-templates \
     --connection-string "your-connection-string"
   ```

3. **Permissions:**
   - Ensure storage account has blob service enabled
   - Verify connection string has blob access

### Table Storage Errors

**Symptoms:**
- Cannot save templates
- User cache errors

**Solutions:**

1. **Verify Table Service:**
   - Go to Azure Portal ? Storage Account
   - Ensure Table service is enabled

2. **Check Table Names:**
   - MessageTemplates
   - MessageLogs
   - UserCache
   - SyncMetadata

3. **Test Table Access:**
   ```bash
   az storage table list \
     --connection-string "your-connection-string"
   ```

## Deployment Issues

### Deployment Fails

**Symptoms:**
- Cannot deploy to Azure
- Build errors during deployment

**Solutions:**

1. **Check Build Logs:**
   - Review GitHub Actions or Azure DevOps logs
   - Look for build errors

2. **Verify App Service Plan:**
   - Ensure App Service Plan supports .NET 10
   - Check pricing tier (B1 or higher recommended)

3. **Configuration Issues:**
   - Verify all required app settings are configured
   - Check Key Vault references if used

4. **Frontend Build:**
   ```bash
   cd src/Full/Bot/Web/web.client
   npm install
   npm run build
   ```

### App Service Not Starting

**Symptoms:**
- App Service shows as stopped
- Cannot access application
- HTTP 503 errors

**Solutions:**

1. **Check Application Logs:**
   - Go to Azure Portal ? App Service ? Log stream
   - Look for startup errors

2. **Verify Runtime:**
   - Ensure .NET 10 runtime is available
   - Check app service configuration

3. **Health Check:**
   - Configure health check endpoint
   - Verify endpoint responds

4. **Restart App Service:**
   ```bash
   az webapp restart --name your-app --resource-group your-rg
   ```

## Copilot Stats Issues

### Error: "Reports.Read.All permission not granted"

**Symptoms:**
- Cannot update Copilot stats
- Permission errors in logs

**Solutions:**

1. **Add Permission:**
   - Go to Azure Portal ? Entra ID ? App registrations ? Your app
   - API permissions ? Add permission
   - Microsoft Graph ? Application permissions
   - Select `Reports.Read.All`

2. **Grant Admin Consent:**
   - Click "Grant admin consent"
   - Wait 5-10 minutes

3. **Verify Permission:**
   - Check that green checkmark appears next to permission

### No Copilot Data Returned

**Symptoms:**
- Update completes but no data
- Empty statistics

**Solutions:**

1. **Verify Copilot Licenses:**
   - Ensure your tenant has Microsoft 365 Copilot licenses
   - Check that licenses are assigned to users

2. **Check Reporting Period:**
   - Microsoft Graph reports have 24-48 hour delay
   - Ensure users have activity in selected period (D7, D30, etc.)

3. **Verify Active Usage:**
   - Reports only show users with recent activity
   - Test with known active Copilot users

4. **Regional Availability:**
   - Verify Copilot is available in your tenant's region

### API Rate Limiting

**Symptoms:**
- Stats update fails with rate limit errors
- Throttling messages in logs

**Solutions:**

1. **Increase Refresh Interval:**
   ```json
   {
     "CopilotStatsRefreshInterval": "48:00:00"  // 48 hours
   }
   ```

2. **Retry Logic:**
   - Application includes automatic retry with exponential backoff
   - Check logs for retry attempts

3. **Schedule Updates:**
   - Run stats updates during off-peak hours
   - Stagger updates if multiple tenants

## Performance Issues

### Slow User Cache Updates

**Symptoms:**
- User cache updates take too long
- Timeouts during updates

**Solutions:**

1. **Use Delta Queries:**
   ```json
   {
     "UserCacheConfig": {
       "EnableDeltaQueries": true
     }
   }
   ```

2. **Reduce Update Frequency:**
   ```json
   {
     "UserCacheConfig": {
       "RefreshInterval": "12:00:00"  // 12 hours
     }
   }
   ```

3. **Monitor Graph API:**
   - Check for throttling
   - Review Application Insights metrics

### High Memory Usage

**Symptoms:**
- App Service running out of memory
- Frequent restarts

**Solutions:**

1. **Scale Up App Service:**
   - Increase to higher pricing tier
   - Add more memory

2. **Optimize Queries:**
   - Use pagination for large datasets
   - Implement caching

3. **Check for Memory Leaks:**
   - Review Application Insights
   - Look for growing memory usage patterns

### Slow API Responses

**Symptoms:**
- API calls take too long
- Timeout errors

**Solutions:**

1. **Enable Caching:**
   - User cache should reduce Graph API calls
   - Verify cache is being used

2. **Optimize Queries:**
   - Use pagination
   - Filter data at source
   - Reduce data returned

3. **Check Storage Performance:**
   - Verify storage account performance tier
   - Consider Premium storage for high traffic

4. **Application Insights:**
   - Review dependency timing
   - Identify slow operations

## Getting Help

### Diagnostic Endpoints

For local development, diagnostic endpoints can help:

```http
GET /api/Diagnostics/TestGraphConnection
GET /api/Diagnostics/TestStorageConnection
GET /api/Diagnostics/GetConfiguration
```

### Logs to Check

1. **Application Insights:**
   - Traces
   - Exceptions
   - Dependencies
   - Custom events

2. **App Service Logs:**
   - Application logs
   - Web server logs
   - Deployment logs

3. **Azure Storage Logs:**
   - Storage analytics
   - Diagnostic logs

### Enable Detailed Logging

In `appsettings.json` or app configuration:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.Graph": "Debug"
    }
  }
}
```

### Support Resources

- **GitHub Issues**: Report bugs or request features
- **Documentation**: Check other docs in this repository
- **Azure Support**: Contact Azure support for infrastructure issues
- **Microsoft 365 Admin**: Contact for Graph API or Teams issues

## Common Error Codes

| Error Code | Description | Solution |
|------------|-------------|----------|
| AADSTS50011 | Invalid redirect URI | Add redirect URI to app registration |
| AADSTS700016 | Application not found | Verify client ID and tenant ID |
| AADSTS65001 | User consent required | Grant admin consent |
| 401 Unauthorized | Authentication failed | Check credentials and tokens |
| 403 Forbidden | Insufficient permissions | Verify and grant required permissions |
| 404 Not Found | Resource not found | Check IDs and URLs |
| 429 Too Many Requests | Rate limiting | Implement backoff and retry |
| 500 Internal Server Error | Server error | Check application logs |
| 503 Service Unavailable | Service down | Check App Service status |

## Next Steps

If you're still experiencing issues after trying these solutions:

1. Review the [Setup Guide](SETUP.md) to ensure correct configuration
2. Check the [Security Guide](SECURITY.md) for security-related issues
3. Review Application Insights logs for detailed error messages
4. Open a GitHub issue with:
   - Error messages
   - Steps to reproduce
   - Configuration (without secrets)
   - Logs (sanitized)


