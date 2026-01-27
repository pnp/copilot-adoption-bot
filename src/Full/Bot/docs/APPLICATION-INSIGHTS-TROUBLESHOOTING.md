# Application Insights Logging Troubleshooting Guide

## Configuration Checklist

### ? 1. Application Insights Connection String
Ensure the `AppInsightsConnectionString` is properly configured in your environment:

**Azure App Service Configuration:**
```
Name: AppInsightsConnectionString
Value: InstrumentationKey=xxxxx;IngestionEndpoint=https://...;LiveEndpoint=https://...
```

**Local Development (appsettings.Development.json or User Secrets):**
```json
{
  "AppInsightsConnectionString": "InstrumentationKey=xxxxx;..."
}
```

### ? 2. Logging Configuration (Already Updated)

**appsettings.json and appsettings.Production.json now include:**
- Proper log levels for your namespaces
- Application Insights specific log level configuration
- Information level for all Engine services

### ? 3. Application Insights SDK (Already Installed)
The following packages are installed:
- `Microsoft.ApplicationInsights` (2.23.0)
- `Microsoft.ApplicationInsights.AspNetCore` (2.23.0)

### ? 4. Program.cs Configuration (Already Updated)
- Application Insights telemetry is registered
- Sampling is disabled to ensure all logs are captured
- Dependency tracking is enabled
- Warning is logged if connection string is missing

## Verifying Logs in Production

### 1. Check Azure Portal - Application Insights
1. Go to Azure Portal
2. Navigate to your Application Insights resource
3. Go to **Logs** (under Monitoring)
4. Run this query:

```kusto
traces
| where timestamp > ago(1h)
| where customDimensions.CategoryName startswith "Common.Engine"
| order by timestamp desc
| project timestamp, message, severityLevel, customDimensions.CategoryName, operation_Name
```

### 2. Check Specific Batch Operations

```kusto
traces
| where timestamp > ago(1h)
| where message contains "batch" or message contains "Batch"
| order by timestamp desc
| project timestamp, severityLevel, message, customDimensions.CategoryName
```

### 3. Check Queue Operations

```kusto
traces
| where timestamp > ago(1h)
| where customDimensions.CategoryName == "Common.Engine.Services.BatchQueueService"
| order by timestamp desc
| project timestamp, severityLevel, message, customDimensions
```

### 4. View All Custom Logs

```kusto
traces
| where timestamp > ago(1h)
| where customDimensions.CategoryName startswith "Common.Engine" or customDimensions.CategoryName startswith "Web.Server"
| summarize count() by severityLevel, tostring(customDimensions.CategoryName)
| order by count_ desc
```

## Common Issues and Solutions

### Issue 1: No Logs Appearing at All
**Solution:**
1. Verify `AppInsightsConnectionString` is set in Azure App Service Configuration
2. Restart the App Service after configuration changes
3. Check if Application Insights resource is in the same region as your app
4. Verify no firewall rules blocking telemetry egress

### Issue 2: Only Some Logs Appearing
**Solution:**
1. Check log levels in `appsettings.Production.json` match expected severity
2. Verify the category name matches your namespace
3. Check if adaptive sampling is enabled (we disabled it)

### Issue 3: Logs Delayed
**Solution:**
- Application Insights has a ~2-5 minute ingestion delay
- Use Live Metrics (QuickPulse) for real-time monitoring
- In Azure Portal: Application Insights ? Live Metrics

### Issue 4: Structured Logging Parameters Not Showing
**Solution:**
- Ensure you're using structured logging syntax: `logger.LogInformation("Message {Param}", value)`
- NOT string interpolation: `logger.LogInformation($"Message {value}")`
- Parameters appear in `customDimensions` in Application Insights

## Log Levels Reference

Current configuration:
- **Debug**: Detailed diagnostic information (not sent to App Insights by default)
- **Information**: Important business events (batch creation, message processing)
- **Warning**: Unexpected situations that don't prevent operation (retry attempts, partial failures)
- **Error**: Failures that prevent specific operations (queue errors, serialization failures)

## Monitoring Best Practices

1. **Use Azure Monitor Alerts** for critical errors
2. **Create custom dashboards** in Application Insights for batch processing metrics
3. **Set up availability tests** if your application has public endpoints
4. **Review Application Map** to understand dependencies

## Testing Logging Locally

To verify logging works locally before deploying:

1. Set `AppInsightsConnectionString` in user secrets or `appsettings.Development.json`
2. Run the application
3. Perform batch operations
4. Check Application Insights in Azure Portal after 2-5 minutes
5. Alternatively, check console output for immediate feedback

## Quick Validation Commands

### Azure CLI - Check App Service Configuration
```bash
az webapp config appsettings list --name <app-name> --resource-group <rg-name> --query "[?name=='AppInsightsConnectionString']"
```

### Azure CLI - View Recent Logs
```bash
az monitor app-insights query --app <app-insights-name> --analytics-query "traces | where timestamp > ago(1h) | take 10"
```

## Contact Points

If logs still don't appear after following this guide:
1. Check Azure Service Health for Application Insights outages
2. Verify your Azure subscription has no issues
3. Review Azure Monitor Status page
4. Contact Azure Support if configuration is correct but telemetry is missing
