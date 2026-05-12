using Azure.Data.Tables;
using Engine.Models;
using Engine.Storage;
using Microsoft.Extensions.Logging;

namespace Engine.Services.UserCache;

/// <summary>
/// Handles Copilot usage statistics retrieval and storage.
/// </summary>
public class CopilotStatsService
{
    private readonly ILogger _logger;
    private readonly ICopilotStatsLoader _statsLoader;

    public CopilotStatsService(ILogger logger, ICopilotStatsLoader statsLoader)
    {
        _logger = logger;
        _statsLoader = statsLoader;
    }

    /// <summary>
    /// Update cached users with Copilot statistics via the cache storage abstraction.
    /// Prefer this overload over the <see cref="TableClient"/>-based one for new code
    /// because it is fully unit-testable without an Azure Table Storage dependency.
    /// </summary>
    public async Task<int> UpdateCachedUsersWithStatsAsync(ICacheStorage cacheStorage, List<CopilotUsageRecord> stats)
    {
        ArgumentNullException.ThrowIfNull(cacheStorage);
        ArgumentNullException.ThrowIfNull(stats);

        if (stats.Count == 0)
        {
            _logger.LogInformation("No Copilot usage records provided; nothing to update");
            return 0;
        }

        var statsByUpn = new Dictionary<string, CopilotUserStats>(StringComparer.OrdinalIgnoreCase);
        foreach (var stat in stats)
        {
            if (string.IsNullOrWhiteSpace(stat.UserPrincipalName)) continue;
            statsByUpn[stat.UserPrincipalName] = new CopilotUserStats
            {
                LastActivityDate = stat.LastActivityDate,
                CopilotChatLastActivityDate = stat.CopilotChatLastActivityDate,
                TeamsCopilotLastActivityDate = stat.TeamsCopilotLastActivityDate,
                WordCopilotLastActivityDate = stat.WordCopilotLastActivityDate,
                ExcelCopilotLastActivityDate = stat.ExcelCopilotLastActivityDate,
                PowerPointCopilotLastActivityDate = stat.PowerPointCopilotLastActivityDate,
                OutlookCopilotLastActivityDate = stat.OutlookCopilotLastActivityDate,
                OneNoteCopilotLastActivityDate = stat.OneNoteCopilotLastActivityDate,
                LoopCopilotLastActivityDate = stat.LoopCopilotLastActivityDate
            };
        }

        var updated = await cacheStorage.UpdateUsersWithCopilotStatsAsync(statsByUpn);
        _logger.LogInformation($"Updated Copilot stats for {updated} users (via ICacheStorage)");
        return updated;
    }

    /// <summary>
    /// Update cached users with Copilot statistics.
    /// </summary>
    public async Task UpdateCachedUsersWithStatsAsync(TableClient tableClient, List<CopilotUsageRecord> stats)
    {
        var updateCount = 0;

        foreach (var stat in stats)
        {
            try
            {
                var cachedUser = await tableClient.GetEntityAsync<UserCacheTableEntity>(
                    UserCacheTableEntity.PartitionKeyVal,
                    stat.UserPrincipalName);

                if (cachedUser.Value != null)
                {
                    var user = cachedUser.Value;
                    user.CopilotLastActivityDate = stat.LastActivityDate;
                    user.CopilotChatLastActivityDate = stat.CopilotChatLastActivityDate;
                    user.TeamscopilotLastActivityDate = stat.TeamsCopilotLastActivityDate;
                    user.WordCopilotLastActivityDate = stat.WordCopilotLastActivityDate;
                    user.ExcelCopilotLastActivityDate = stat.ExcelCopilotLastActivityDate;
                    user.PowerPointCopilotLastActivityDate = stat.PowerPointCopilotLastActivityDate;
                    user.OutlookCopilotLastActivityDate = stat.OutlookCopilotLastActivityDate;
                    user.OneNoteCopilotLastActivityDate = stat.OneNoteCopilotLastActivityDate;
                    user.LoopCopilotLastActivityDate = stat.LoopCopilotLastActivityDate;
                    user.LastCopilotStatsUpdate = DateTime.UtcNow;

                    await tableClient.UpdateEntityAsync(user, user.ETag, TableUpdateMode.Replace);
                    updateCount++;
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogDebug($"User {stat.UserPrincipalName} not found in cache, skipping stats update");
            }
        }

        _logger.LogInformation($"Updated Copilot stats for {updateCount} users");
    }

    /// <summary>
    /// Fetch Copilot usage stats using the configured loader.
    /// </summary>
    public async Task<CopilotStatsResult> GetCopilotUsageStatsAsync()
    {
        return await _statsLoader.GetCopilotUsageStatsAsync();
    }
}

