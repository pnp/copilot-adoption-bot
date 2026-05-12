# Authentication Issues

[← Back to Troubleshooting Guide](../TROUBLESHOOTING.md)

## Error: "Current authenticated context is not valid for this request"

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
   - Go to Azure Portal → Entra ID → App registrations
   - Find your app
   - Go to Certificates & secrets
   - Create new client secret
   - Update configuration with new secret

4. **Wait for Propagation:**
   - After making changes, wait 5-10 minutes
   - Restart the application

## Error: "AADSTS700016: Application not found in the directory"

**Symptoms:**
- Cannot authenticate
- App registration not found error

**Solutions:**

1. **Verify App Registration:**
   - Go to Azure Portal → Entra ID → App registrations
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

## Error: "Invalid redirect URI"

**Symptoms:**
- Authentication redirects fail
- CORS errors in browser

**Solutions:**

1. **Add Redirect URIs in App Registration:**
   - Go to Azure Portal → Entra ID → App registrations → Your app
   - Go to Authentication
   - Add redirect URIs:
     - For local: `https://localhost:5173/auth-end.html`
     - For production: `https://your-app.azurewebsites.net/auth-end.html`

2. **Configure CORS:**
   - Ensure frontend origin is allowed in backend CORS policy
   - Check `Program.cs` for CORS configuration
