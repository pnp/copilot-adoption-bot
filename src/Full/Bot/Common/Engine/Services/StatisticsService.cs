using Common.Engine.Storage;
using Microsoft.Extensions.Logging;

namespace Common.Engine.Services;

/// <summary>
/// Service for calculating dashboard statistics
/// </summary>
public class StatisticsService
{
    private readonly IMessageLogReader _logReader;
    private readonly ITenantUserCounter _tenantUserCounter;
    private readonly ILogger<StatisticsService> _logger;

    public StatisticsService(
        IMessageLogReader logReader,
        ITenantUserCounter tenantUserCounter,
        ILogger<StatisticsService> logger)
    {
        _logReader = logReader;
        _tenantUserCounter = tenantUserCounter;
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
