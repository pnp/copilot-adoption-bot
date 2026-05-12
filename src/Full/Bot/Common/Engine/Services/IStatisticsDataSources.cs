using Engine.Models;
using Engine.Storage;

namespace Engine.Services;

/// <summary>
/// Narrow read-only abstraction over message logs used by <see cref="StatisticsService"/>.
/// Decouples statistics calculation from the concrete <see cref="MessageTemplateStorageManager"/>
/// so it can be unit-tested without Azure Table Storage.
/// </summary>
public interface IMessageLogReader
{
    /// <summary>
    /// Retrieve every message log entry. Implementations may stream from underlying storage.
    /// </summary>
    Task<List<MessageLogTableEntity>> GetAllMessageLogs();
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
}
