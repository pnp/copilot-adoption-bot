using Azure.Data.Tables;
using Common.Engine.Storage;
using Microsoft.Extensions.Logging;
using Common.Engine.Models;

namespace Common.Engine.Services.UserCache;

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

