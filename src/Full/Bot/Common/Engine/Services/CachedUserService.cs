using Engine.Models;
using Engine.Services.UserCache;
using Microsoft.Extensions.Logging;

namespace Engine.Services;

/// <summary>
/// Service for loading user data with cache-first logic.
/// Checks cache first, then falls back to external user service if data is not available or expired.
/// </summary>
public class CachedUserService
{
    private readonly IUserCacheManager _cacheManager;
    private readonly IExternalUserService _externalUserService;
    private readonly ILogger<CachedUserService> _logger;

    public CachedUserService(
        IUserCacheManager cacheManager,
        IExternalUserService externalUserService,
        ILogger<CachedUserService> logger)
    {
        _cacheManager = cacheManager;
        _externalUserService = externalUserService;
        _logger = logger;
    }

    /// <summary>
    /// Get all users with extended metadata.
    /// Uses cache manager for optimized retrieval, falls back to Graph API if cache is empty/expired.
    /// </summary>
    /// <param name="maxUsers">
    /// Maximum number of users to retrieve. Defaults to unbounded. This previously defaulted
    /// to 999, which silently truncated every caller that didn't pass a value - including
    /// smart-group resolution, so groups were matched against the first 999 users of the
    /// tenant and the other 99% were invisible.
    /// </param>
    /// <param name="forceRefresh">Force a refresh from Graph API instead of using cache</param>
    public async Task<List<EnrichedUserInfo>> GetAllUsersWithMetadataAsync(int maxUsers = int.MaxValue, bool forceRefresh = false)
    {
        try
        {
            _logger.LogInformation("Fetching users from cache...");
            var cachedUsers = await _cacheManager.GetAllCachedUsersAsync(forceRefresh);

            if (maxUsers < int.MaxValue && cachedUsers.Count > maxUsers)
            {
                _logger.LogWarning(
                    "Truncating user list to {MaxUsers} of {TotalUsers} cached users. Callers that need the full " +
                    "directory (e.g. smart-group resolution) must not pass maxUsers.",
                    maxUsers, cachedUsers.Count);
                cachedUsers = cachedUsers.Take(maxUsers).ToList();
            }

            _logger.LogInformation($"Retrieved {cachedUsers.Count} users from cache");
            return cachedUsers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving from cache");
            throw;
        }
    }

    /// <summary>
    /// Get a single user with extended metadata.
    /// Uses cache manager for optimized retrieval, falls back to Graph API if not in cache.
    /// </summary>
    public async Task<EnrichedUserInfo?> GetUserWithMetadataAsync(string upn)
    {
        try
        {
            var cachedUser = await _cacheManager.GetCachedUserAsync(upn);
            if (cachedUser != null)
            {
                _logger.LogDebug($"Retrieved user {upn} from cache");
                return cachedUser;
            }

            // User not in cache, fetch from external source
            _logger.LogDebug($"User {upn} not in cache, fetching from external source");
            return await _externalUserService.GetUserAsync(upn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving user {upn}");
            throw;
        }
    }

    /// <summary>
    /// Get users filtered by department directly from external source.
    /// This operation bypasses cache as it's a filtered query.
    /// </summary>
    public async Task<List<EnrichedUserInfo>> GetUsersByDepartmentAsync(string department)
    {
        return await _externalUserService.GetUsersByDepartmentAsync(department);
    }

    /// <summary>
    /// Enrich users with manager information using external source.
    /// </summary>
    public async Task EnrichUsersWithManagersAsync(List<EnrichedUserInfo> users)
    {
        await _externalUserService.EnrichUsersWithManagersAsync(users);
    }

    /// <summary>
    /// Enrich users with license information using external source.
    /// </summary>
    public async Task EnrichUsersWithLicenseInfoAsync(List<EnrichedUserInfo> users)
    {
        await _externalUserService.EnrichUsersWithLicenseInfoAsync(users);
    }

    /// <summary>
    /// Update Copilot usage statistics and license information for all cached users.
    /// This will fetch fresh data from the external source and update the cache.
    /// </summary>
    public async Task UpdateCopilotStatsAndLicensesAsync()
    {
        await _cacheManager.UpdateCopilotStatsAsync();
    }

    /// <summary>
    /// Get all users directly from external source (bypasses cache completely).
    /// Use this sparingly as it's less efficient than cached retrieval.
    /// </summary>
    /// <param name="maxUsers">Maximum number of users to retrieve (default 999)</param>
    public async Task<List<EnrichedUserInfo>> GetAllUsersDirectFromGraphAsync(int maxUsers = 999)
    {
        return await _externalUserService.GetAllUsersAsync(maxUsers);
    }
}
