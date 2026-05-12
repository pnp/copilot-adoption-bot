using Microsoft.Extensions.Logging;

namespace Engine.Services;

/// <summary>
/// Service for calculating dashboard statistics
/// </summary>
public class StatisticsService
{
    private readonly IMessageLogReader _logReader;
    private readonly ITenantUserCounter _tenantUserCounter;
    private readonly IBotInteractionSource _interactionSource;
    private readonly ILogger<StatisticsService> _logger;

    public StatisticsService(
        IMessageLogReader logReader,
        ITenantUserCounter tenantUserCounter,
        IBotInteractionSource interactionSource,
        ILogger<StatisticsService> logger)
    {
        _logReader = logReader;
        _tenantUserCounter = tenantUserCounter;
        _interactionSource = interactionSource;
        _logger = logger;
    }

    /// <summary>
    /// Get message status statistics
    /// </summary>
    public async Task<MessageStatusStatsDto> GetMessageStatusStats()
    {
        try
        {
            var logs = await _logReader.GetAllMessageLogs();

            var stats = StatisticsCalculator.ComputeMessageStatusStats(logs);

            _logger.LogInformation(
                "Message stats - Sent: {Sent}, Failed: {Failed}, Pending: {Pending}",
                stats.SentCount, stats.FailedCount, stats.PendingCount);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating message status stats");
            throw;
        }
    }

    /// <summary>
    /// Get user coverage statistics
    /// </summary>
    public async Task<UserCoverageStatsDto> GetUserCoverageStats()
    {
        try
        {
            var logs = await _logReader.GetAllMessageLogs();
            var totalUsersInTenant = await _tenantUserCounter.GetTotalUserCount();

            var stats = StatisticsCalculator.ComputeUserCoverageStats(logs, totalUsersInTenant);

            _logger.LogInformation(
                "User coverage - Messaged: {Messaged}, Total in tenant: {Total}",
                stats.UsersMessaged, stats.TotalUsersInTenant);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating user coverage stats");
            throw;
        }
    }

    /// <summary>
    /// Get bot interaction (engagement) statistics: how many users the bot has spoken to
    /// have ever sent a message back.
    /// </summary>
    public async Task<BotInteractionStatsDto> GetBotInteractionStats()
    {
        try
        {
            var users = await _interactionSource.GetCachedUsersAsync();
            var stats = StatisticsCalculator.ComputeBotInteractionStats(users);

            _logger.LogInformation(
                "Bot interaction - Cached: {Cached}, Interacted: {Interacted}",
                stats.UsersWithConversation, stats.UsersInteracted);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating bot interaction stats");
            throw;
        }
    }
}

public class MessageStatusStatsDto
{
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int PendingCount { get; set; }
    public int TotalCount { get; set; }
}

public class UserCoverageStatsDto
{
    public int UsersMessaged { get; set; }
    public int TotalUsersInTenant { get; set; }
    public int UsersNotMessaged { get; set; }
    public double CoveragePercentage { get; set; }
}

public class BotInteractionStatsDto
{
    /// <summary>
    /// Number of users the bot has at least once held a conversation reference for
    /// (i.e. the bot has been installed for / spoken to them).
    /// </summary>
    public int UsersWithConversation { get; set; }

    /// <summary>
    /// Number of those users who have sent at least one message back to the bot.
    /// </summary>
    public int UsersInteracted { get; set; }

    /// <summary>
    /// Convenience: <see cref="UsersWithConversation"/> minus <see cref="UsersInteracted"/>.
    /// </summary>
    public int UsersNotInteracted { get; set; }

    /// <summary>
    /// Percentage of cached users who have replied at least once.
    /// </summary>
    public double InteractionRatePercentage { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent reply from any user (null if no replies yet).
    /// </summary>
    public DateTime? LastInteractionUtc { get; set; }
}
