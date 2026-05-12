# Performance Issues

[← Back to Troubleshooting Guide](../TROUBLESHOOTING.md)

## Slow User Cache Updates

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

## High Memory Usage

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

## Slow API Responses

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
