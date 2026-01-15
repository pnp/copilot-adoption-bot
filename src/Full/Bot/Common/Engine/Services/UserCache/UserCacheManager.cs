using Common.Engine.Config;
using Common.Engine.Models;
using Microsoft.Extensions.Logging;

namespace Common.Engine.Services.UserCache;

/// <summary>
/// Manages user caching with pluggable data loader and storage adapters.
/// Orchestrates synchronization, expiration, and Copilot stats updates.
/// </summary>
public class UserCacheManager : IUserCacheManager
{
    private readonly IUserDataLoader _dataLoader;
    private readonly ICacheStorage _storage;
    private readonly UserCacheConfig _config;
    private readonly ILogger<UserCacheManager> _logger;

    public UserCacheManager(
        IUserDataLoader dataLoader,
        ICacheStorage storage,
        UserCacheConfig config,
        ILogger<UserCacheManager> logger)
    {
        _dataLoader = dataLoader;
        _storage = storage;
        _config = config;
        _logger = logger;
    }

    public async Task<List<EnrichedUserInfo>> GetAllCachedUsersAsync(bool forceRefresh = false, bool skipAutoSync = false)
    {
        var metadata = await _storage.GetSyncMetadataAsync();
        var needsSync = forceRefresh ||
                        (metadata.LastDeltaSyncDate == null ||
                        DateTime.UtcNow - metadata.LastDeltaSyncDate.Value > _config.CacheExpiration);

        if (needsSync && !skipAutoSync)
        {
            await SyncUsersAsync();
        }

        return await _storage.GetAllUsersAsync();
    }

    public async Task<EnrichedUserInfo?> GetCachedUserAsync(string upn)
    {
        return await _storage.GetUserByUpnAsync(upn);
    }

    public async Task ClearCacheAsync()
    {
        _logger.LogInformation("Clearing user cache...");

        var userCount = await _storage.ClearAllUsersAsync();

        // Clear sync metadata
        var metadata = new CacheSyncMetadata();
        await _storage.UpdateSyncMetadataAsync(metadata);

        _logger.LogInformation($"Cleared {userCount} users from cache");
    }

    public async Task SyncUsersAsync()
    {
        var metadata = await _storage.GetSyncMetadataAsync();
        var needsFullSync = metadata.LastFullSyncDate == null ||
                            DateTime.UtcNow - metadata.LastFullSyncDate.Value > _config.FullSyncInterval;

        try
        {
            metadata.LastSyncStatus = "InProgress";
            await _storage.UpdateSyncMetadataAsync(metadata);

            UserLoadResult result;

            if (needsFullSync || string.IsNullOrEmpty(metadata.DeltaToken))
            {
                _logger.LogInformation("Performing full user sync...");
                result = await _dataLoader.LoadAllUsersAsync();
                metadata.LastFullSyncDate = DateTime.UtcNow;
            }
            else
            {
                _logger.LogInformation("Performing delta sync...");
                result = await _dataLoader.LoadDeltaChangesAsync(metadata.DeltaToken);
            }

            // Store users
            await _storage.UpsertUsersAsync(result.Users);

            // Update metadata
            metadata.DeltaToken = result.DeltaToken;
            metadata.LastDeltaSyncDate = DateTime.UtcNow;
            metadata.LastSyncUserCount = result.Users.Count;
            metadata.LastSyncStatus = "Success";
            metadata.LastSyncError = null;

            await _storage.UpdateSyncMetadataAsync(metadata);

            _logger.LogInformation($"User sync completed successfully: {result.Users.Count} users processed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user sync");
            metadata.LastSyncStatus = "Failed";
            metadata.LastSyncError = ex.Message;
            await _storage.UpdateSyncMetadataAsync(metadata);
            throw;
        }
    }

    public async Task UpdateCopilotStatsAsync()
    {
        var metadata = await _storage.GetSyncMetadataAsync();

        // Check if we need to update stats
        if (metadata.LastCopilotStatsUpdate != null &&
            DateTime.UtcNow - metadata.LastCopilotStatsUpdate.Value < _config.CopilotStatsRefreshInterval)
        {
            _logger.LogInformation("Copilot stats are still fresh, skipping update");
            return;
        }

        _logger.LogInformation("Fetching Copilot usage statistics...");

        try
        {
            var stats = await _dataLoader.GetCopilotStatsAsync();
            
            if (stats.Count == 0)
            {
                _logger.LogWarning("No Copilot stats retrieved - stats dictionary is empty. This may indicate an API error or no Copilot activity data available.");
            }
            else
            {
                // Update users in storage with Copilot stats
                var updateCount = await _storage.UpdateUsersWithCopilotStatsAsync(stats);
                _logger.LogInformation($"Updated Copilot stats in storage for {updateCount} users");
            }

            metadata.LastCopilotStatsUpdate = DateTime.UtcNow;
            await _storage.UpdateSyncMetadataAsync(metadata);

            _logger.LogInformation("Copilot stats update completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Copilot stats");
            throw;
        }
    }

    public async Task<CacheSyncMetadata> GetSyncMetadataAsync()
    {
        return await _storage.GetSyncMetadataAsync();
    }

    public async Task UpdateSyncMetadataAsync(CacheSyncMetadata metadata)
    {
        await _storage.UpdateSyncMetadataAsync(metadata);
    }
}
