using Common.Engine.Models;

namespace Common.Engine.Services.UserCache;

/// <summary>
/// Interface for user cache management.
/// </summary>
public interface IUserCacheManager
{
    /// <summary>
    /// Get all cached users. If cache is expired or empty, performs a sync (unless skipAutoSync is true).
    /// </summary>
    /// <param name="forceRefresh">Force a refresh regardless of cache state.</param>
    /// <param name="skipAutoSync">Skip automatic sync even if cache appears expired or empty.</param>
    /// <returns>List of enriched user information from cache.</returns>
    Task<List<EnrichedUserInfo>> GetAllCachedUsersAsync(bool forceRefresh = false, bool skipAutoSync = false);

    /// <summary>
    /// Get a specific user from cache by UPN.
    /// </summary>
    /// <param name="upn">User Principal Name.</param>
    /// <returns>Enriched user information if found, null otherwise.</returns>
    Task<EnrichedUserInfo?> GetCachedUserAsync(string upn);

    /// <summary>
    /// Clear all cached user data (useful for forcing a full resync).
    /// </summary>
    Task ClearCacheAsync();

    /// <summary>
    /// Synchronize users from the data source using delta query for incremental updates.
    /// </summary>
    Task SyncUsersAsync();

    /// <summary>
    /// Update Copilot usage statistics for all cached users.
    /// </summary>
    Task UpdateCopilotStatsAsync();

    /// <summary>
    /// Get the current synchronization metadata.
    /// </summary>
    Task<CacheSyncMetadata> GetSyncMetadataAsync();

    /// <summary>
    /// Update the synchronization metadata.
    /// </summary>
    Task UpdateSyncMetadataAsync(CacheSyncMetadata metadata);
}
