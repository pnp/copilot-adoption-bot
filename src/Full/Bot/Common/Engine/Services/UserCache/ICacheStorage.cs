using Common.Engine.Models;

namespace Common.Engine.Services.UserCache;

/// <summary>
/// Metadata about the cache synchronization state.
/// </summary>
public class CacheSyncMetadata
{
    public string? DeltaToken { get; set; }
    public DateTime? LastFullSyncDate { get; set; }
    public DateTime? LastDeltaSyncDate { get; set; }
    public DateTime? LastCopilotStatsUpdate { get; set; }
    public string? LastSyncStatus { get; set; }
    public string? LastSyncError { get; set; }
    public int LastSyncUserCount { get; set; }
}

/// <summary>
/// Adapter interface for storing cached user data.
/// </summary>
public interface ICacheStorage
{
    /// <summary>
    /// Get all cached users (excluding deleted).
    /// </summary>
    Task<List<EnrichedUserInfo>> GetAllUsersAsync();

    /// <summary>
    /// Get a specific user by UPN.
    /// </summary>
    Task<EnrichedUserInfo?> GetUserByUpnAsync(string upn);

    /// <summary>
    /// Store or update a user in the cache.
    /// </summary>
    Task UpsertUserAsync(EnrichedUserInfo user);

    /// <summary>
    /// Store or update multiple users in the cache.
    /// </summary>
    Task UpsertUsersAsync(IEnumerable<EnrichedUserInfo> users);

    /// <summary>
    /// Remove all users from the cache and clear the delta token to force a full sync on next retrieval.
    /// </summary>
    Task<int> ClearAllUsersAsync();

    /// <summary>
    /// Update users with Copilot usage statistics.
    /// </summary>
    /// <param name="stats">Dictionary mapping UPN to Copilot stats.</param>
    /// <returns>Number of users updated.</returns>
    Task<int> UpdateUsersWithCopilotStatsAsync(Dictionary<string, CopilotUserStats> stats);

    /// <summary>
    /// Update users with license information.
    /// </summary>
    /// <param name="licenseInfo">Dictionary mapping UPN to whether user has Copilot license.</param>
    /// <returns>Number of users updated.</returns>
    Task<int> UpdateUsersWithLicenseInfoAsync(Dictionary<string, bool> licenseInfo);

    /// <summary>
    /// Get cache synchronization metadata.
    /// </summary>
    Task<CacheSyncMetadata> GetSyncMetadataAsync();

    /// <summary>
    /// Update cache synchronization metadata.
    /// </summary>
    Task UpdateSyncMetadataAsync(CacheSyncMetadata metadata);
}
