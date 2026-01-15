using Azure.Identity;
using Common.Engine.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;

namespace Common.Engine.DependencyInjection;

/// <summary>
/// Extension methods for registering Microsoft Graph services
/// </summary>
public static class GraphServiceExtensions
{
    /// <summary>
    /// Registers Microsoft Graph Service Client with client credentials authentication
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="config">Application configuration containing auth details</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddGraphServices(this IServiceCollection services, AppConfig config)
    {
        var options = new TokenCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud };
        var scopes = new[] { "https://graph.microsoft.com/.default" };
        var clientSecretCredential = new ClientSecretCredential(
            config.GraphConfig.TenantId,
            config.GraphConfig.ClientId,
            config.GraphConfig.ClientSecret,
            options);

        services.AddSingleton(sp => new GraphServiceClient(clientSecretCredential, scopes));

        return services;
    }
}
