using Common.Engine.Storage;

namespace Common.Engine.Services;

/// <summary>
/// Pure (no-IO) calculator for dashboard statistics. Extracted from <see cref="StatisticsService"/>
/// so the aggregation logic can be tested without Azure / Graph dependencies.
/// </summary>
public static class StatisticsCalculator
{
    /// <summary>
    /// Compute message-status counts in a single pass.
    /// </summary>
    public static MessageStatusStatsDto ComputeMessageStatusStats(IEnumerable<MessageLogTableEntity> logs)
    {
        ArgumentNullException.ThrowIfNull(logs);

        int sent = 0;
        int failed = 0;
        int pending = 0;
        int total = 0;

        foreach (var log in logs)
        {
            total++;

            var status = log.Status ?? string.Empty;
            if (status.Equals("Sent", StringComparison.OrdinalIgnoreCase) ||
                status.Equals("Success", StringComparison.OrdinalIgnoreCase))
            {
                sent++;
            }
            else if (status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
            {
                failed++;
            }
            else if (status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                pending++;
            }
        }

        return new MessageStatusStatsDto
        {
            SentCount = sent,
            FailedCount = failed,
            PendingCount = pending,
            TotalCount = total
        };
    }

    /// <summary>
    /// Compute user coverage stats given message logs and the total user count in the tenant.
    /// </summary>
    public static UserCoverageStatsDto ComputeUserCoverageStats(IEnumerable<MessageLogTableEntity> logs, int totalUsersInTenant)
    {
        ArgumentNullException.ThrowIfNull(logs);

        var uniqueRecipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var log in logs)
        {
            if (!string.IsNullOrWhiteSpace(log.RecipientUpn))
            {
                uniqueRecipients.Add(log.RecipientUpn!);
            }
        }

        var messaged = uniqueRecipients.Count;

        return new UserCoverageStatsDto
        {
            UsersMessaged = messaged,
            TotalUsersInTenant = totalUsersInTenant,
            UsersNotMessaged = Math.Max(0, totalUsersInTenant - messaged),
            CoveragePercentage = totalUsersInTenant > 0
                ? Math.Round((double)messaged / totalUsersInTenant * 100, 2)
                : 0
        };
    }
}
