using Azure.Identity;
using Common.Engine.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Common.Engine.Services;

/// <summary>
/// Service for interacting with Microsoft Graph API
/// </summary>
public class GraphService
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<GraphService> _logger;

    public GraphService(AzureADAuthConfig config, ILogger<GraphService> logger)
    {
        _logger = logger;

        var clientSecretCredential = new ClientSecretCredential(
            config.TenantId,
            config.ClientId,
            config.ClientSecret);

        var scopes = new[] { "https://graph.microsoft.com/.default" };
        _graphClient = new GraphServiceClient(clientSecretCredential, scopes);
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
