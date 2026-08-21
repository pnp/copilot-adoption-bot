using Engine.Models;
using Engine.Services;
using Engine.Storage;

namespace UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IBatchStatsSource"/> returning a configurable list of batches.
/// </summary>
public class FakeBatchStatsSource : IBatchStatsSource
{
    public List<MessageBatchTableEntity> Batches { get; set; } = new();
    public int CallCount { get; private set; }

    public Task<List<MessageBatchTableEntity>> GetAllBatches()
    {
        CallCount++;
        return Task.FromResult(Batches);
    }
}

/// <summary>
/// In-memory <see cref="ITenantUserCounter"/> returning a configurable count.
/// </summary>
public class FakeTenantUserCounter : ITenantUserCounter
{
    public int Count { get; set; }
    public int CallCount { get; private set; }

    public Task<int> GetTotalUserCount()
    {
        CallCount++;
        return Task.FromResult(Count);
    }
}

/// <summary>
/// In-memory <see cref="IBotInteractionSource"/> returning a configurable list of cached users.
/// </summary>
public class FakeBotInteractionSource : IBotInteractionSource
{
    public List<CachedUserAndConversationData> Users { get; set; } = new();
    public int CallCount { get; private set; }

    /// <summary>Overrides the count returned by <see cref="GetReachedUserCountAsync"/> when set.</summary>
    public int? ReachedUserCountOverride { get; set; }

    public Task<List<CachedUserAndConversationData>> GetCachedUsersAsync()
    {
        CallCount++;
        return Task.FromResult(Users);
    }

    public Task<int> GetReachedUserCountAsync()
    {
        CallCount++;
        return Task.FromResult(ReachedUserCountOverride ?? Users.Count);
    }
}
