namespace Engine.Services;

/// <summary>
/// Narrow abstraction for updating the status of a message log entry.
/// Allows <see cref="MessageSenderService"/> to be unit-tested without
/// constructing a full <see cref="MessageTemplateService"/> or pulling
/// scoped services out of an <see cref="IServiceProvider"/>.
/// </summary>
public interface IMessageLogStatusWriter
{
    /// <summary>
    /// Update the status (and optional last-error message) for the given message log id.
    /// </summary>
    Task UpdateMessageLogStatusAsync(string logId, string status, string? lastError = null);
}
