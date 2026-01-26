
## User Caching and Import System

The Copilot Adoption Bot uses a sophisticated multi-layered caching system to efficiently manage user data from Microsoft Graph API. Understanding this system is crucial for troubleshooting and optimizing performance.

### Architecture Overview

The system is organized into three main layers:

```
???????????????????????????????????????????????????
?  Smart Groups / Messaging                      ?
?  (Uses CachedUserService)                      ?
???????????????????????????????????????????????????
                 ?
???????????????????????????????????????????????????
?  CachedUserService                              ?
?  • Cache-first logic                            ?
?  • Automatic fallback to external source        ?
???????????????????????????????????????????????????
                 ?
        ???????????????????
        ?                 ?
??????????????????  ?????????????????????????
? IUserCacheManager?  ? IExternalUserService  ?
? (Azure Tables)  ?  ? (GraphUserService)    ?
?                 ?  ?                       ?
? • Delta sync    ?  ? • Direct Graph API    ?
? • License cache ?  ? • License enrichment  ?
? • Stats cache   ?  ? • Manager enrichment  ?
???????????????????  ?????????????????????????
```

### Components

#### 1. **CachedUserService** (Smart Cache Layer)
- **Purpose**: Provides cache-first access to user data
- **Behavior**: Checks cache ? Falls back to external source if needed
- **Location**: `Common\Engine\Services\CachedUserService.cs`
- **Methods**:
  - `GetAllUsersWithMetadataAsync()` - Returns all users from cache
  - `GetUserWithMetadataAsync(upn)` - Returns single user from cache
  - `UpdateCopilotStatsAndLicensesAsync()` - Refreshes stats and licenses

#### 2. **IUserCacheManager** (Cache Management)
- **Purpose**: Manages cache synchronization and expiration
- **Implementation**: `UserCacheManager`
- **Storage**: Azure Table Storage (production) or In-Memory (testing)
- **Location**: `Common\Engine\Services\UserCache\UserCacheManager.cs`

**Key Operations:**
- **Full Sync**: Complete user import (happens on first run or after 7 days)
- **Delta Sync**: Incremental updates using Microsoft Graph delta queries
- **Stats Update**: Copilot usage statistics and license refresh (every 24 hours)

#### 3. **IExternalUserService** (Direct Graph Access)
- **Purpose**: Loads users directly from Microsoft Graph API
- **Implementation**: `GraphUserService`
- **When Used**: Cache misses, force refresh, or direct queries
- **Location**: `Common\Engine\Services\GraphUserService.cs`

**Capabilities:**
- Load all users with metadata
- Get users by department
- Enrich with license information
- Enrich with manager information

### Cache Synchronization

#### Initial Sync (First Run)

When the bot first starts:

1. **Full Sync** is triggered automatically
2. All users are loaded from Microsoft Graph API
3. Users are stored in Azure Table Storage with metadata:
   - Basic info (name, email, department, etc.)
   - License information (HasCopilotLicense)
   - Manager relationships
   - Initial timestamp
4. Delta token is saved for future incremental updates

**Timeline**: Typically 5-10 minutes for 1000 users

#### Delta Sync (Incremental Updates)

After initial sync, the system uses delta queries:

1. **Trigger**: Automatically on cache expiration (default: 1 hour)
2. **Process**:
   - Uses saved delta token from last sync
   - Retrieves only changed/new/deleted users
   - Updates cache with changes
   - Saves new delta token
3. **Efficiency**: Only transfers changed data

**Timeline**: 10-30 seconds for typical changes

#### Stats and License Refresh

Copilot statistics and license information are refreshed separately:

1. **Trigger**: Every 24 hours (configurable)
2. **Process**:
   - Fetches latest Copilot usage stats from Reports API
   - Fetches current license assignments
   - Updates cache entries with new data
3. **Data Updated**:
   - `HasCopilotLicense` - Current license status
   - `CopilotLastActivityDate` - Latest usage
   - Product-specific usage (Word, Excel, Teams, etc.)

**Timeline**: 2-5 minutes for 1000 users

### Configuration

Cache behavior is controlled by `UserCacheConfig`:

```csharp
public class UserCacheConfig
{
    // How long cache is considered valid before triggering delta sync
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(1);
    
    // How often to refresh Copilot stats and licenses
    public TimeSpan CopilotStatsRefreshInterval { get; set; } = TimeSpan.FromHours(24);
    
    // How often to force a complete full sync (vs delta)
    public TimeSpan FullSyncInterval { get; set; } = TimeSpan.FromDays(7);
}
```

**Location**: `Common\Engine\Config\UserCacheConfig.cs`

### Smart Groups Integration

Smart Groups use the cached data for AI-powered user matching:

1. **Force Refresh**: Clicking "Force Refresh" in the UI:
   - Loads latest user data from cache
   - If cache is expired, triggers delta sync
   - Re-runs AI matching with fresh data
   - Caches the smart group results

2. **Cache Layers**:
   - **User Cache**: Azure Table Storage (global)
   - **Smart Group Cache**: Azure Table Storage (per-group, 1 hour TTL)

### Troubleshooting Guide

#### Problem: Users Show "No License" Despite Having Copilot

**Symptoms:**
- Users with valid Copilot licenses display "No License" in Smart Groups
- License data appears correct in Azure Table Storage

**Root Cause:**
Smart Group cache contains old data from before license field was added.

**Solution:**
1. Click "Force Refresh" button in Smart Groups UI
2. This triggers a fresh resolution with updated user data
3. License information will appear correctly after refresh completes

**Prevention:**
After schema changes (like adding new fields), always force refresh cached data.

#### Problem: New Users Not Appearing

**Symptoms:**
- Recently added users don't show up in targeting lists
- Smart Groups don't include new employees

**Root Cause:**
Cache hasn't synced since users were added to tenant.

**Solution:**
```csharp
// Option 1: Wait for automatic delta sync (up to 1 hour)
// Cache expires automatically and syncs on next request

// Option 2: Manual sync via API
POST /api/UserCache/Update

// Option 3: Clear cache and force full sync
await _userCacheManager.ClearCacheAsync();
await _userCacheManager.SyncUsersAsync();
```

**Check Sync Status:**
```csharp
var metadata = await _userCacheManager.GetSyncMetadataAsync();
Console.WriteLine($"Last sync: {metadata.LastDeltaSyncDate}");
Console.WriteLine($"Status: {metadata.LastSyncStatus}");
Console.WriteLine($"Users: {metadata.LastSyncUserCount}");
```

#### Problem: Outdated Copilot Usage Statistics

**Symptoms:**
- Usage dates are stale (more than 24 hours old)
- New Copilot activity not reflected

**Root Cause:**
Stats refresh hasn't run or is failing.

**Solution:**
```csharp
// Manual stats and license refresh
await _cachedUserService.UpdateCopilotStatsAndLicensesAsync();

// Or via API
POST /api/UserCache/UpdateCopilotStats
```

**Verify Permissions:**
Ensure app registration has `Reports.Read.All` permission for Copilot stats.

#### Problem: Cache Taking Too Long to Sync

**Symptoms:**
- Initial sync takes 30+ minutes
- Delta sync times out

**Root Cause:**
- Large tenant (5000+ users)
- Network latency to Azure
- Graph API throttling

**Solutions:**

1. **Optimize batch size** - In GraphUserDataLoader, reduce page size to 100 vs 999
2. **Check Graph API throttling** - Look for HTTP 429 responses in logs
3. **Verify Azure region** - Ensure resources are in same region for best performance

#### Problem: JSON Property Name Mismatch

**Symptoms:**
- Data exists in backend but UI shows default values
- Network tab shows PascalCase instead of camelCase properties

**Root Cause:**
ASP.NET Core isn't serializing to camelCase for JavaScript compatibility.

**Solution:**
Ensure `Program.cs` has camelCase policy:
```csharp
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = 
        System.Text.Json.JsonNamingPolicy.CamelCase;
});
```

After changing, restart the application for changes to take effect.

#### Problem: Schema Changes Not Reflected

**Symptoms:**
- Added new property to DTO but it's always null/default
- UI doesn't show new field even after refresh

**Checklist:**
1. ? Added property to C# DTO (`SmartGroupMemberDto`)
2. ? Added property to cache entity (`SmartGroupMemberCacheEntity`)
3. ? Added property to storage entity (`UserCacheTableEntity`)
4. ? Updated all mapping methods (`MapMemberToDto`, `MapToEntity`)
5. ? Added property to TypeScript interface (`SmartGroupMemberDto`)
6. ? Rebuilt solution
7. ? Restarted application
8. ? Force refresh in UI to reload cached data

### Performance Optimization Tips

#### 1. Tune Cache Expiration
```csharp
// For mostly static user base (small company)
CacheExpiration = TimeSpan.FromHours(6);

// For dynamic environment (frequent changes)
CacheExpiration = TimeSpan.FromMinutes(30);
```

#### 2. Optimize Smart Group Queries
```csharp
// Limit user count for preview/testing
var users = await _userService.GetAllUsersWithMetadataAsync(maxUsers: 100);

// Use full dataset only for production
var users = await _userService.GetAllUsersWithMetadataAsync();
```

#### 3. Monitor Cache Health
```csharp
// Regular health checks
var metadata = await _userCacheManager.GetSyncMetadataAsync();

if (metadata.LastSyncStatus == "Failed")
{
    _logger.LogError($"Sync failed: {metadata.LastSyncError}");
}

if (DateTime.UtcNow - metadata.LastCopilotStatsUpdate > TimeSpan.FromDays(2))
{
    _logger.LogWarning("Copilot stats are stale");
}
```

### Monitoring and Diagnostics

#### Log Levels

Set appropriate logging for troubleshooting:

```json
{
  "Logging": {
    "LogLevel": {
      "Common.Engine.Services.UserCache": "Debug",
      "Common.Engine.Services.GraphUserService": "Information",
      "Microsoft.Graph": "Warning"
    }
  }
}
```

#### Key Metrics to Monitor

1. **Cache Hit Rate** - Target: >95% for normal operations
2. **Sync Duration** - Full sync: <10 min, Delta: <1 min, Stats: <5 min
3. **Error Rate** - Graph API: <1%, Storage: <0.1%
4. **Data Freshness** - User data: <1 hr, Copilot stats: <24 hrs

### Best Practices

1. **Regular Maintenance**
   - Monitor sync status weekly
   - Review error logs for patterns
   - Test force refresh functionality monthly

2. **Schema Changes**
   - Document new properties in all DTOs
   - Update mappings in all layers
   - Clear cache after deployment
   - Force refresh cached smart groups

3. **Performance**
   - Use delta sync instead of full sync when possible
   - Limit user counts during testing/preview
   - Schedule stats refresh during off-peak hours

4. **Testing**
   - Use `InMemoryCacheStorage` for unit tests
   - Test both cache hit and cache miss scenarios
   - Verify delta sync with mock data
   - Test force refresh with stale cache

### Additional Resources

- [Graph API Delta Queries](https://learn.microsoft.com/graph/delta-query-overview)
- [Azure Table Storage Performance Guide](https://learn.microsoft.com/azure/storage/tables/storage-performance-checklist)
- [Copilot Usage Reports API](https://learn.microsoft.com/graph/api/reportroot-getm365copilotusageuserdetail)
