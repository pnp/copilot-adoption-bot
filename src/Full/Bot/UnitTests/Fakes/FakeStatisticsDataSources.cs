using Common.Engine.Services;
using Common.Engine.Storage;

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
