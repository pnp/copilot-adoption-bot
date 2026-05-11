using Common.Engine.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Common.Engine.Services;

/// <summary>
/// Service for interacting with Microsoft Graph API
/// </summary>
public class GraphService : ITenantUserCounter
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<GraphService> _logger;

    /// <summary>
    /// Preferred constructor: reuses the singleton <see cref="GraphServiceClient"/> registered in DI
    /// so that all callers share a single underlying HttpClient and credential pipeline.
    /// </summary>
    public GraphService(GraphServiceClient graphClient, ILogger<GraphService> logger)
    {
        _graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
        _logger = logger;
    }

    /// <summary>
    /// Legacy constructor for callers that still build their own credential. Prefer the DI overload.
    /// </summary>
    public GraphService(AzureADAuthConfig config, ILogger<GraphService> logger)
        : this(BuildClient(config), logger)
    {
    }

    private static GraphServiceClient BuildClient(AzureADAuthConfig config)
    {
        var clientSecretCredential = new Azure.Identity.ClientSecretCredential(
            config.TenantId,
            config.ClientId,
            config.ClientSecret);

        return new GraphServiceClient(clientSecretCredential, new[] { "https://graph.microsoft.com/.default" });
    }

    /// <summary>
    /// Get total count of users in the tenant
    /// </summary>
    public async Task<int> GetTotalUserCount()
    {
        try
        {
            var result = await _graphClient.Users.GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Count = true;
                requestConfiguration.QueryParameters.Top = 1;
                requestConfiguration.QueryParameters.Select = new[] { "id" };
                requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
            });

            var count = result?.OdataCount ?? 0;
            _logger.LogInformation($"Retrieved user count from Graph: {count}");
            return (int)count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user count from Graph");
            throw;
        }
    }

    /// <summary>
    /// Get a user by UPN
    /// </summary>
    public async Task<User?> GetUserByUpn(string upn)
    {
        try
        {
            return await _graphClient.Users[upn].GetAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting user {upn} from Graph");
            return null;
        }
    }
}
