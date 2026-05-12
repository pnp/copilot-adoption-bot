using Engine.Services;

namespace UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IMessageLogStatusWriter"/> recording every status update so
/// tests can assert on the recorded sequence without touching real storage.
/// </summary>
public class FakeMessageLogStatusWriter : IMessageLogStatusWriter
{
    public record StatusUpdate(string LogId, string Status, string? LastError);

    public List<StatusUpdate> Updates { get; } = new();
    public Exception? ThrowOnUpdate { get; set; }

    public Task UpdateMessageLogStatusAsync(string logId, string status, string? lastError = null)
    {
        Updates.Add(new StatusUpdate(logId, status, lastError));

        if (ThrowOnUpdate != null)
        {
            throw ThrowOnUpdate;
        }

        return Task.CompletedTask;
    }
}
