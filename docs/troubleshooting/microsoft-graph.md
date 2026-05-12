# Microsoft Graph Errors

[← Back to Troubleshooting Guide](../TROUBLESHOOTING.md)

## Error: "Insufficient privileges to complete the operation"

**Symptoms:**
- Cannot read users
- Cannot access Graph API
- Permission denied errors

**Solutions:**

1. **Verify Application Permissions:**
   - Go to Azure Portal → Entra ID → App registrations → Your app
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

## Error: "The tenant for tenant guid does not exist"

**Symptoms:**
- Tenant not found errors
- Cannot access Graph API

**Solutions:**

1. **Verify Tenant ID:**
   ```powershell
   # Get tenant ID from Azure Portal
   az account show --query tenantId -o tsv
   ```

2. **Update Configuration:**
   ```powershell
   dotnet user-secrets set "GraphConfig:TenantId" "your-actual-tenant-id"
   ```

3. **Check for Whitespace:**
   - Ensure no extra spaces or characters in tenant ID
   - Verify it's a valid GUID format
