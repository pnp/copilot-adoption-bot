# Getting Help

[← Back to Troubleshooting Guide](../TROUBLESHOOTING.md)

## Diagnostic Endpoints

For local development, diagnostic endpoints can help:

```http
GET /api/Diagnostics/TestGraphConnection
GET /api/Diagnostics/TestStorageConnection
GET /api/Diagnostics/GetConfiguration
```

## Logs to Check

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

## Enable Detailed Logging

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

## Support Resources

- **GitHub Issues**: Report bugs or request features
- **Documentation**: Check other docs in this repository
- **Azure Support**: Contact Azure support for infrastructure issues
- **Microsoft 365 Admin**: Contact for Graph API or Teams issues
