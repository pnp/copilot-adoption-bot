using Azure.Core;
using Azure.Identity;
using Common.Engine.Config;
using Common.Engine.Models;
using Microsoft.Extensions.Logging;

namespace Common.Engine.Services.UserCache;

/// <summary>
/// Loads Copilot usage statistics from Microsoft Graph API.
/// </summary>
public class GraphCopilotStatsLoader : ICopilotStatsLoader
{
    // Use a single shared HttpClient to avoid socket exhaustion from per-call allocations.
    private static readonly HttpClient s_httpClient = new HttpClient();

    private readonly ILogger _logger;
    private readonly UserCacheConfig _config;
    private readonly AzureADAuthConfig _authConfig;
    private AccessToken? _cachedToken;

    public GraphCopilotStatsLoader(ILogger logger, UserCacheConfig config, AzureADAuthConfig authConfig)
    {
        _logger = logger;
        _config = config;
        _authConfig = authConfig;
    }

    /// <summary>
    /// Fetch Copilot usage stats from Graph API.
    /// </summary>
    public async Task<CopilotStatsResult> GetCopilotUsageStatsAsync()
    {
        var result = new CopilotStatsResult();

        try
        {
            var requestUrl = $"https://graph.microsoft.com/beta/reports/getMicrosoft365CopilotUsageUserDetail(period='{_config.CopilotStatsPeriod}')?$format=text/csv";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync());

            var response = await s_httpClient.SendAsync(request);
            result.StatusCode = (int)response.StatusCode;

            if (response.StatusCode == System.Net.HttpStatusCode.Found)
            {
                var downloadUrl = response.Headers.Location?.ToString();
                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    using var csvResponse = await s_httpClient.GetAsync(downloadUrl);
                    result.StatusCode = (int)csvResponse.StatusCode;

                    if (csvResponse.IsSuccessStatusCode)
                    {
                        var csvContent = await csvResponse.Content.ReadAsStringAsync();
                        result.Records = CopilotUsageCsvParser.Parse(csvContent);
                        result.Success = true;
                    }
                    else
                    {
                        result.ErrorMessage = $"Failed to download CSV: {csvResponse.StatusCode} - {csvResponse.ReasonPhrase}";
                        _logger.LogWarning(result.ErrorMessage);
                    }
                }
                else
                {
                    result.ErrorMessage = "Redirect location URL was empty";
                    _logger.LogWarning(result.ErrorMessage);
                }
            }
            else if (response.IsSuccessStatusCode)
            {
                var csvContent = await response.Content.ReadAsStringAsync();
                result.Records = CopilotUsageCsvParser.Parse(csvContent);
                result.Success = true;
            }
            else
            {
                result.ErrorMessage = $"Copilot stats API returned {response.StatusCode} - {response.ReasonPhrase}";
                _logger.LogWarning(result.ErrorMessage);
            }
        }
        catch (Azure.Identity.AuthenticationFailedException)
        {
            // Re-throw authentication exceptions so callers can handle them appropriately
            throw;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Error fetching Copilot usage stats: {ex.Message}";
            _logger.LogError(ex, result.ErrorMessage);
        }

        return result;
    }

    private async Task<string> GetAccessTokenAsync()
    {
        if (_cachedToken.HasValue && _cachedToken.Value.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return _cachedToken.Value.Token;
        }

        _logger.LogDebug("Acquiring new access token for Microsoft Graph API...");

        try
        {
            var credential = new ClientSecretCredential(
                _authConfig.TenantId,
                _authConfig.ClientId,
                _authConfig.ClientSecret);

            var tokenRequestContext = new TokenRequestContext(
                new[] { "https://graph.microsoft.com/.default" });

            _cachedToken = await credential.GetTokenAsync(tokenRequestContext);

            _logger.LogDebug("Successfully acquired access token");
            return _cachedToken.Value.Token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire access token for Microsoft Graph API");
            throw;
        }
    }
}
