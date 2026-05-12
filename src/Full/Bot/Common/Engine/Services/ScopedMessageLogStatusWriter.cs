using Microsoft.Extensions.DependencyInjection;

namespace Engine.Services;

/// <summary>
/// Default <see cref="IMessageLogStatusWriter"/> implementation that resolves the
/// scoped <see cref="MessageTemplateService"/> from the DI container on each call.
/// Keeps the singleton <see cref="MessageSenderService"/> free of scope-resolution
/// logic while preserving the original runtime behavior.
/// </summary>
internal sealed class ScopedMessageLogStatusWriter : IMessageLogStatusWriter
{
    private readonly IServiceProvider _serviceProvider;

    public ScopedMessageLogStatusWriter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task UpdateMessageLogStatusAsync(string logId, string status, string? lastError = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var templateService = scope.ServiceProvider.GetRequiredService<MessageTemplateService>();
        await templateService.UpdateMessageLogStatus(logId, status, lastError);
    }
}
