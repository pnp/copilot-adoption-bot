using Engine.Services;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Engine.DependencyInjection;

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
        services.AddSingleton<BotConversationCache>();

        // The conversation cache also serves as the engagement-stats source. Register it as
        // the IBotInteractionSource here so it wins over the Null fallback registered by
        // AddStatisticsServices when both are present.
        services.AddSingleton<IBotInteractionSource>(sp => sp.GetRequiredService<BotConversationCache>());

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
