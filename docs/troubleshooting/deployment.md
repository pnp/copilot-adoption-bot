# Deployment Issues

[← Back to Troubleshooting Guide](../TROUBLESHOOTING.md)

> For deployment-method-specific guidance, see also:
> - [Manual Deployment troubleshooting](../DEPLOYMENT-MANUAL.md)
> - [GitHub Actions troubleshooting](../DEPLOYMENT-GITHUB-ACTIONS.md)
> - [Azure DevOps troubleshooting](../DEPLOYMENT-AZURE-DEVOPS.md)
> - [Copilot CLI troubleshooting](../DEPLOYMENT-COPILOT-CLI.md#troubleshooting)

## Deployment Fails

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
   ```powershell
   cd src/Full/Bot/Web/web.client
   npm install
   npm run build
   ```

## App Service Not Starting

**Symptoms:**
- App Service shows as stopped
- Cannot access application
- HTTP 503 errors

**Solutions:**

1. **Check Application Logs:**
   - Go to Azure Portal → App Service → Log stream
   - Look for startup errors

2. **Verify Runtime:**
   - Ensure .NET 10 runtime is available
   - Check app service configuration

3. **Health Check:**
   - Configure health check endpoint
   - Verify endpoint responds

4. **Restart App Service:**
   ```powershell
   az webapp restart --name your-app --resource-group your-rg
   ```
