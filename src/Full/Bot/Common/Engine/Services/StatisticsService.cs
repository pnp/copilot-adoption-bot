using Common.Engine.Storage;
using Microsoft.Extensions.Logging;

namespace Common.Engine.Services;

/// <summary>
/// Service for calculating dashboard statistics
/// </summary>
public class StatisticsService
{
    private readonly MessageTemplateStorageManager _storageManager;
    private readonly GraphService _graphService;
    private readonly ILogger<StatisticsService> _logger;

    public StatisticsService(
        MessageTemplateStorageManager storageManager,
        GraphService graphService,
        ILogger<StatisticsService> logger)
    {
        _storageManager = storageManager;
        _graphService = graphService;
        _logger = logger;
    }

    /// <summary>
    /// Get message status statistics
    /// </summary>
    public async Task<MessageStatusStatsDto> GetMessageStatusStats()
    {
        try
        {
            var logs = await _storageManager.GetAllMessageLogs();

            // Count both "Sent" and "Success" as sent messages
            var sentCount = logs.Count(l => l.Status.Equals("Sent", StringComparison.OrdinalIgnoreCase) || 
                                            l.Status.Equals("Success", StringComparison.OrdinalIgnoreCase));
            var failedCount = logs.Count(l => l.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase));
            var pendingCount = logs.Count(l => l.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase));

            _logger.LogInformation($"Message stats - Sent: {sentCount}, Failed: {failedCount}, Pending: {pendingCount}");

            return new MessageStatusStatsDto
            {
                SentCount = sentCount,
                FailedCount = failedCount,
                PendingCount = pendingCount,
                TotalCount = logs.Count
            };
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
            // Get all message logs
            var logs = await _storageManager.GetAllMessageLogs();

            // Get unique users messaged (distinct recipient UPNs)
            var uniqueUsersMessaged = logs
                .Where(l => !string.IsNullOrWhiteSpace(l.RecipientUpn))
                .Select(l => l.RecipientUpn!)
                .Distinct()
                .Count();

            // Get total users in tenant from Graph
            var totalUsersInTenant = await _graphService.GetTotalUserCount();

            _logger.LogInformation($"User coverage - Messaged: {uniqueUsersMessaged}, Total in tenant: {totalUsersInTenant}");

            return new UserCoverageStatsDto
            {
                UsersMessaged = uniqueUsersMessaged,
                TotalUsersInTenant = totalUsersInTenant,
                UsersNotMessaged = totalUsersInTenant - uniqueUsersMessaged,
                CoveragePercentage = totalUsersInTenant > 0 
                    ? Math.Round((double)uniqueUsersMessaged / totalUsersInTenant * 100, 2) 
                    : 0
            };
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
