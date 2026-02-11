using Common.Engine.Config;
using Common.Engine.Storage;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Engine.DependencyInjection;

/// <summary>
/// Extension methods for registering Bot Framework services
/// </summary>
public static class BotServiceExtensions
{
    /// <summary>
    /// Registers Bot Framework infrastructure services including adapter and authentication
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddBotFrameworkServices(this IServiceCollection services)
    {
        // Bot Framework adapter with error handling
        services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

        // Bot Framework authentication
        services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

        // Bot conversation cache and management
        services.AddSingleton<BotConversationCache>(sp =>
        {
            var graphClient = sp.GetRequiredService<Microsoft.Graph.GraphServiceClient>();
            var credFactory = sp.GetRequiredService<StorageCredentialFactory>();
            var config = sp.GetRequiredService<AppConfig>();

            if (credFactory.IsRBACEnabled)
            {
                var credential = credFactory.GetCredential()!;
                var accountName = credFactory.GetStorageAccountName();
                var tableUri = new Uri($"https://{accountName}.table.core.windows.net");
                return new BotConversationCache(graphClient, tableUri, credential);
            }
            else
            {
                return new BotConversationCache(graphClient, config);
            }
        });

        return services;
    }

    /// <summary>
    /// Registers Bot notification and conversation resume services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddBotNotificationServices(this IServiceCollection services)
    {
        services.AddSingleton<Notifications.IBotConvoResumeManager, Notifications.BotConvoResumeManager>();

        return services;
    }
}
