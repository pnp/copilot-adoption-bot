using Common.Engine.Storage;

namespace Common.Engine.Services;

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
