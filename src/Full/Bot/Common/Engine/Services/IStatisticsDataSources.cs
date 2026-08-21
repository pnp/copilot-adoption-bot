using Engine.Models;
using Engine.Storage;

namespace Engine.Services;

/// <summary>
/// Narrow read-only abstraction over message batches used by <see cref="StatisticsService"/>.
///
/// <para>
/// Statistics are derived from per-batch counters (roughly one row per send campaign),
/// never from delivery rows. Scanning deliveries is not viable: at 150,000 nudges/day the
/// delivery table grows by ~55 million rows a year, and the previous
/// <c>GetAllMessageLogs()</c> approach materialised all of them into memory on every
/// dashboard load.
/// </para>
/// </summary>
public interface IBatchStatsSource
{
    /// <summary>
    /// Retrieve every message batch, including its running delivery counters.
    /// </summary>
    Task<List<MessageBatchTableEntity>> GetAllBatches();
}

/// <summary>
/// Narrow abstraction over the tenant-wide user count used by <see cref="StatisticsService"/>.
/// Decouples coverage statistics from the concrete <see cref="GraphService"/>.
/// </summary>
public interface ITenantUserCounter
{
    /// <summary>
    /// Get the total number of users in the tenant.
    /// </summary>
    Task<int> GetTotalUserCount();
}

/// <summary>
/// Narrow abstraction over the bot conversation cache used by <see cref="StatisticsService"/>
/// to compute "users who have replied to the bot" engagement statistics.
/// </summary>
public interface IBotInteractionSource
{
    /// <summary>
    /// Retrieve every cached user with their last-interaction timestamp (or null if they
    /// have never sent a message back to the bot).
    /// </summary>
    Task<List<CachedUserAndConversationData>> GetCachedUsersAsync();

    /// <summary>
    /// Number of distinct users the bot has ever established a conversation with — i.e.
    /// users successfully reached at least once. Used for coverage statistics in place of
    /// counting distinct recipients across the whole delivery history.
    /// </summary>
    Task<int> GetReachedUserCountAsync();
}

/// <summary>
/// No-op <see cref="IBotInteractionSource"/> used by Teams-only apps that have no
/// bot framework registered (and therefore no <see cref="BotConversationCache"/>).
/// Returns an empty list so interaction stats render cleanly as zeroes.
/// </summary>
public sealed class NullBotInteractionSource : IBotInteractionSource
{
    public Task<List<CachedUserAndConversationData>> GetCachedUsersAsync() =>
        Task.FromResult(new List<CachedUserAndConversationData>());

    public Task<int> GetReachedUserCountAsync() => Task.FromResult(0);
}
