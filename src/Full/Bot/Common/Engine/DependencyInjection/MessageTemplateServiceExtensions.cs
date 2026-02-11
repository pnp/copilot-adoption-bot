using Common.Engine.BackgroundServices;
using Common.Engine.Config;
using Common.Engine.Services;
using Common.Engine.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Common.Engine.DependencyInjection;

/// <summary>
/// Extension methods for registering message template services
/// </summary>
public static class MessageTemplateServiceExtensions
{
    /// <summary>
    /// Registers message template storage and service components
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="config">Application configuration containing connection strings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMessageTemplateServices(this IServiceCollection services, AppConfig config)
    {
        // Register storage credential factory as singleton
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<StorageCredentialFactory>>();
            return new StorageCredentialFactory(config, logger);
        });

        services.AddSingleton<MessageTemplateStorageManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MessageTemplateStorageManager>>();
            var credFactory = sp.GetRequiredService<StorageCredentialFactory>();

            if (credFactory.IsRBACEnabled)
            {
                var credential = credFactory.GetCredential()!;
                var accountName = credFactory.GetStorageAccountName();
                var tableUri = new Uri($"https://{accountName}.table.core.windows.net");
                var blobUri = new Uri($"https://{accountName}.blob.core.windows.net");
                return new MessageTemplateStorageManager(tableUri, blobUri, credential, logger);
            }
            else
            {
                return new MessageTemplateStorageManager(config.ConnectionStrings.Storage, logger);
            }
        });

        // Register queue service for batch processing
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<BatchQueueService>>();
            var credFactory = sp.GetRequiredService<StorageCredentialFactory>();

            if (credFactory.IsRBACEnabled)
            {
                var credential = credFactory.GetCredential()!;
                var accountName = credFactory.GetStorageAccountName();
                var queueUri = new Uri($"https://{accountName}.queue.core.windows.net");
                return new BatchQueueService(queueUri, credential, logger);
            }
            else
            {
                return new BatchQueueService(config.ConnectionStrings.Storage, logger);
            }
        });

        // Register message sender service
        services.AddSingleton<MessageSenderService>();

        // Register background processor
        services.AddHostedService<BatchMessageProcessorService>();

        // Register default template initialization service
        services.AddHostedService<DefaultTemplateInitializationService>();

        services.AddScoped<MessageTemplateService>();

        // Register pending card lookup service
        services.AddScoped<PendingCardLookupService>();

        return services;
    }
}
