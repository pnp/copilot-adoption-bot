# Bot Connection Issues

[← Back to Troubleshooting Guide](../TROUBLESHOOTING.md)

## Bot Not Responding in Teams

**Symptoms:**
- Bot doesn't respond to messages
- Bot appears offline
- Messages sent but no response

**Solutions:**

1. **Verify Bot Endpoint:**
   - Go to Teams Developer Portal → Bot management → Your bot
   - Check endpoint address: `https://your-app.azurewebsites.net/api/messages`
   - Ensure URL is correct and uses HTTPS

2. **Check App Service Status:**
   - Go to Azure Portal → App Service
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

## Bot Installation Fails

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
