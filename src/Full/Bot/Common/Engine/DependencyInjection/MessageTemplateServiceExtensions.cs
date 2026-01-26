using Common.Engine.BackgroundServices;
using Common.Engine.Config;
using Common.Engine.Services;
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
        services.AddSingleton<MessageTemplateStorageManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MessageTemplateStorageManager>>();

            // Use new StorageAuthConfig if configured, otherwise fallback to legacy ConnectionStrings.Storage
            var storageAuthConfig = GetStorageAuthConfig(config);
            return new MessageTemplateStorageManager(storageAuthConfig, logger);
        });

        // Register queue service for batch processing
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<BatchQueueService>>();

            // Use new StorageAuthConfig if configured, otherwise fallback to legacy ConnectionStrings.Storage
            var storageAuthConfig = GetStorageAuthConfig(config);
            return new BatchQueueService(storageAuthConfig, logger);
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

    /// <summary>
    /// Helper method to get StorageAuthConfig with fallback to legacy configuration
    /// </summary>
    private static StorageAuthConfig GetStorageAuthConfig(AppConfig config)
    {
        // If StorageAuthConfig is properly configured, use it
        if (config.StorageAuthConfig != null &&
            (config.StorageAuthConfig.UseRBAC || !string.IsNullOrEmpty(config.StorageAuthConfig.ConnectionString)))
        {
            return config.StorageAuthConfig;
        }

        // Fallback to legacy ConnectionStrings.Storage
        return new StorageAuthConfig
        {
            UseRBAC = false,
            ConnectionString = config.ConnectionStrings.Storage
        };
    }
}
