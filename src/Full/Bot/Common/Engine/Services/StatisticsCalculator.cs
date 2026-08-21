using Engine.Models;
using Engine.Storage;

namespace Engine.Services;

/// <summary>
/// Pure (no-IO) calculator for dashboard statistics. Extracted from <see cref="StatisticsService"/>
/// so the aggregation logic can be tested without Azure / Graph dependencies.
/// </summary>
public static class StatisticsCalculator
{
    /// <summary>
    /// Compute message-status counts from per-batch counters.
    ///
    /// <para>
    /// Reads roughly one row per send campaign rather than one row per delivery, so cost is
    /// independent of how many messages have ever been sent.
    /// </para>
    /// </summary>
    public static MessageStatusStatsDto ComputeMessageStatusStats(IEnumerable<MessageBatchTableEntity> batches)
    {
        ArgumentNullException.ThrowIfNull(batches);

        int sent = 0;
        int failed = 0;
        int total = 0;

        foreach (var batch in batches)
        {
            total += batch.TotalCount;
            sent += batch.SentCount;
            failed += batch.FailedCount;
        }

        // Anything neither delivered nor permanently failed is still in flight.
        var pending = Math.Max(0, total - sent - failed);

        return new MessageStatusStatsDto
        {
            SentCount = sent,
            FailedCount = failed,
            PendingCount = pending,
            TotalCount = total
        };
    }

    /// <summary>
    /// Compute user coverage stats.
    /// </summary>
    /// <param name="usersMessaged">
    /// Distinct users the bot has reached at least once. Sourced from the conversation
    /// cache rather than by counting distinct recipients across the delivery history,
    /// which would require scanning every row ever written.
    /// </param>
    /// <param name="totalUsersInTenant">Total users in the tenant.</param>
    public static UserCoverageStatsDto ComputeUserCoverageStats(int usersMessaged, int totalUsersInTenant)
    {
        var messaged = Math.Max(0, usersMessaged);

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

    /// <summary>
    /// Compute bot interaction stats: how many cached users have ever sent a message back
    /// to the bot. <paramref name="cachedUsers"/> is the set of users the bot has spoken to
    /// (i.e. has a conversation reference for); a non-null <see cref="CachedUserAndConversationData.LastInteractionUtc"/>
    /// means the user has at some point replied.
    /// </summary>
    public static BotInteractionStatsDto ComputeBotInteractionStats(IEnumerable<CachedUserAndConversationData> cachedUsers)
    {
        ArgumentNullException.ThrowIfNull(cachedUsers);

        int total = 0;
        int interacted = 0;
        DateTime? mostRecent = null;

        foreach (var u in cachedUsers)
        {
            total++;
            if (u.LastInteractionUtc.HasValue)
            {
                interacted++;
                if (!mostRecent.HasValue || u.LastInteractionUtc.Value > mostRecent.Value)
                {
                    mostRecent = u.LastInteractionUtc.Value;
                }
            }
        }

        return new BotInteractionStatsDto
        {
            UsersWithConversation = total,
            UsersInteracted = interacted,
            UsersNotInteracted = Math.Max(0, total - interacted),
            InteractionRatePercentage = total > 0
                ? Math.Round((double)interacted / total * 100, 2)
                : 0,
            LastInteractionUtc = mostRecent
        };
    }
}
