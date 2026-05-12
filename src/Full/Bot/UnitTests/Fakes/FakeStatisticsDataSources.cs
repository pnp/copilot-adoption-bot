using Engine.Models;
using Engine.Services;
using Engine.Storage;

namespace UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IMessageLogReader"/> returning a configurable list of logs.
/// </summary>
public class FakeMessageLogReader : IMessageLogReader
{
    public List<MessageLogTableEntity> Logs { get; set; } = new();
    public int CallCount { get; private set; }

    public Task<List<MessageLogTableEntity>> GetAllMessageLogs()
    {
        CallCount++;
        return Task.FromResult(Logs);
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

    public Task<List<CachedUserAndConversationData>> GetCachedUsersAsync()
    {
        CallCount++;
        return Task.FromResult(Users);
    }
}
