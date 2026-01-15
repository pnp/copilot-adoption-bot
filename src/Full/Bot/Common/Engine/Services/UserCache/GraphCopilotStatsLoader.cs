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

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + await GetAccessTokenAsync());

            var response = await httpClient.GetAsync(requestUrl);
            result.StatusCode = (int)response.StatusCode;

            if (response.StatusCode == System.Net.HttpStatusCode.Found)
            {
                var downloadUrl = response.Headers.Location?.ToString();
                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    var csvResponse = await httpClient.GetAsync(downloadUrl);
                    result.StatusCode = (int)csvResponse.StatusCode;
                    
                    if (csvResponse.IsSuccessStatusCode)
                    {
                        var csvContent = await csvResponse.Content.ReadAsStringAsync();
                        result.Records = ParseCopilotUsageCsv(csvContent);
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
                result.Records = ParseCopilotUsageCsv(csvContent);
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

    private List<CopilotUsageRecord> ParseCopilotUsageCsv(string csvContent)
    {
        var records = new List<CopilotUsageRecord>();
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
        {
            return records;
        }

        var columnIndices = ParseCsvHeader(lines[0]);

        for (int i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            if (values.Length <= columnIndices.UpnIndex)
                continue;

            var upn = values[columnIndices.UpnIndex]?.Trim();
            if (string.IsNullOrWhiteSpace(upn))
                continue;

            if (columnIndices.LastActivityIndex >= 0 && values.Length <= columnIndices.LastActivityIndex)
                continue;

            var record = new CopilotUsageRecord
            {
                UserPrincipalName = upn,
                LastActivityDate = ParseCsvDate(values, columnIndices.LastActivityIndex),
                CopilotChatLastActivityDate = ParseCsvDate(values, columnIndices.CopilotChatIndex),
                TeamsCopilotLastActivityDate = ParseCsvDate(values, columnIndices.TeamsIndex),
                WordCopilotLastActivityDate = ParseCsvDate(values, columnIndices.WordIndex),
                ExcelCopilotLastActivityDate = ParseCsvDate(values, columnIndices.ExcelIndex),
                PowerPointCopilotLastActivityDate = ParseCsvDate(values, columnIndices.PowerPointIndex),
                OutlookCopilotLastActivityDate = ParseCsvDate(values, columnIndices.OutlookIndex),
                OneNoteCopilotLastActivityDate = ParseCsvDate(values, columnIndices.OneNoteIndex),
                LoopCopilotLastActivityDate = ParseCsvDate(values, columnIndices.LoopIndex)
            };

            records.Add(record);
        }

        return records;
    }

    private CsvColumnIndices ParseCsvHeader(string headerLine)
    {
        var headers = headerLine.Split(',').Select(h => h.Trim()).ToArray();

        return new CsvColumnIndices
        {
            UpnIndex = Array.IndexOf(headers, "User Principal Name"),
            LastActivityIndex = Array.IndexOf(headers, "Last Activity Date"),
            CopilotChatIndex = Array.IndexOf(headers, "Copilot Chat Last Activity Date"),
            TeamsIndex = Array.IndexOf(headers, "Microsoft Teams Copilot Last Activity Date"),
            WordIndex = Array.IndexOf(headers, "Word Copilot Last Activity Date"),
            ExcelIndex = Array.IndexOf(headers, "Excel Copilot Last Activity Date"),
            PowerPointIndex = Array.IndexOf(headers, "PowerPoint Copilot Last Activity Date"),
            OutlookIndex = Array.IndexOf(headers, "Outlook Copilot Last Activity Date"),
            OneNoteIndex = Array.IndexOf(headers, "OneNote Copilot Last Activity Date"),
            LoopIndex = Array.IndexOf(headers, "Loop Copilot Last Activity Date")
        };
    }

    private DateTime? ParseCsvDate(string[] values, int index)
    {
        if (index < 0 || index >= values.Length)
            return null;

        var value = values[index].Trim();
        if (string.IsNullOrEmpty(value))
            return null;

        if (DateTime.TryParse(value, out var date))
        {
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }
        
        return null;
    }
}
