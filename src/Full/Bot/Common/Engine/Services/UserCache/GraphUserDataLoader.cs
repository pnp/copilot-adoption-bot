using Common.Engine.Config;
using Common.Engine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Common.Engine.Services.UserCache;

/// <summary>
/// Loads user data from Microsoft Graph API with delta query support.
/// </summary>
public class GraphUserDataLoader : IUserDataLoader
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger _logger;
    private readonly UserCacheConfig _config;
    private readonly ICopilotStatsLoader _copilotStatsLoader;

    private static readonly string[] UserSelectProperties =
    [
        "id",
        "userPrincipalName",
        "displayName",
        "givenName",
        "surname",
        "mail",
        "department",
        "jobTitle",
        "officeLocation",
        "city",
        "country",
        "state",
        "companyName",
        "employeeType",
        "employeeHireDate",
        "accountEnabled",
        "userType"
    ];

    public GraphUserDataLoader(
        GraphServiceClient graphClient,
        ILogger<GraphUserDataLoader> logger,
        ICopilotStatsLoader copilotStatsLoader,
        UserCacheConfig? config = null)
    {
        _graphClient = graphClient;
        _logger = logger;
        _copilotStatsLoader = copilotStatsLoader;
        _config = config ?? new UserCacheConfig();
    }

    public async Task<UserLoadResult> LoadAllUsersAsync()
    {
        _logger.LogInformation("Loading all users from Microsoft Graph with delta query initialization...");

        var users = new List<EnrichedUserInfo>();

        // Initial delta query request
        var deltaRequest = await _graphClient.Users.Delta.GetAsDeltaGetResponseAsync(requestConfiguration =>
        {
            requestConfiguration.QueryParameters.Select = UserSelectProperties;
        });

        // Collect first page
        if (deltaRequest?.Value != null)
        {
            foreach (var user in deltaRequest.Value.Where(u => u.AccountEnabled == true && u.UserType == "Member"))
            {
                users.Add(MapToEnrichedUser(user));
            }
        }

        // Handle pagination
        while (!string.IsNullOrEmpty(deltaRequest?.OdataNextLink))
        {
            deltaRequest = await _graphClient.Users.Delta.WithUrl(deltaRequest.OdataNextLink).GetAsDeltaGetResponseAsync();
            if (deltaRequest?.Value != null)
            {
                foreach (var user in deltaRequest.Value.Where(u => u.AccountEnabled == true && u.UserType == "Member"))
                {
                    users.Add(MapToEnrichedUser(user));
                }
            }
        }

        _logger.LogInformation($"Loaded {users.Count} users from Microsoft Graph");

        return new UserLoadResult
        {
            Users = users,
            DeltaToken = deltaRequest?.OdataDeltaLink
        };
    }

    public async Task<UserLoadResult> LoadDeltaChangesAsync(string deltaToken)
    {
        _logger.LogInformation("Loading delta changes from Microsoft Graph...");

        var users = new List<EnrichedUserInfo>();

        // Use the delta token to get only changes
        var deltaResponse = await _graphClient.Users.Delta.WithUrl(deltaToken).GetAsDeltaGetResponseAsync();

        // Collect first page of changes
        if (deltaResponse?.Value != null)
        {
            foreach (var user in deltaResponse.Value)
            {
                users.Add(MapToEnrichedUser(user));
            }
        }

        // Handle pagination for delta changes
        while (!string.IsNullOrEmpty(deltaResponse?.OdataNextLink))
        {
            deltaResponse = await _graphClient.Users.Delta.WithUrl(deltaResponse.OdataNextLink).GetAsDeltaGetResponseAsync();
            if (deltaResponse?.Value != null)
            {
                foreach (var user in deltaResponse.Value)
                {
                    users.Add(MapToEnrichedUser(user));
                }
            }
        }

        _logger.LogInformation($"Loaded {users.Count} changes from Microsoft Graph");

        return new UserLoadResult
        {
            Users = users,
            DeltaToken = deltaResponse?.OdataDeltaLink
        };
    }

    public async Task<Dictionary<string, CopilotUserStats>> GetCopilotStatsAsync()
    {
        _logger.LogInformation("Fetching Copilot usage statistics from Microsoft Graph...");

        var stats = new Dictionary<string, CopilotUserStats>();

        try
        {
            var result = await _copilotStatsLoader.GetCopilotUsageStatsAsync();

            if (!result.Success)
            {
                _logger.LogWarning($"Failed to fetch Copilot stats: {result.ErrorMessage} (Status: {result.StatusCode})");
                return stats;
            }

            foreach (var record in result.Records)
            {
                stats[record.UserPrincipalName] = new CopilotUserStats
                {
                    LastActivityDate = record.LastActivityDate,
                    CopilotChatLastActivityDate = record.CopilotChatLastActivityDate,
                    TeamsCopilotLastActivityDate = record.TeamsCopilotLastActivityDate,
                    WordCopilotLastActivityDate = record.WordCopilotLastActivityDate,
                    ExcelCopilotLastActivityDate = record.ExcelCopilotLastActivityDate,
                    PowerPointCopilotLastActivityDate = record.PowerPointCopilotLastActivityDate,
                    OutlookCopilotLastActivityDate = record.OutlookCopilotLastActivityDate,
                    OneNoteCopilotLastActivityDate = record.OneNoteCopilotLastActivityDate,
                    LoopCopilotLastActivityDate = record.LoopCopilotLastActivityDate
                };
            }

            _logger.LogInformation($"Retrieved Copilot stats for {stats.Count} users");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Copilot usage stats");
        }

        return stats;
    }

    public async Task<Dictionary<string, bool>> GetLicenseInfoAsync()
    {
        _logger.LogInformation("Fetching license information from Microsoft Graph...");

        var licenseInfo = new Dictionary<string, bool>();
        const string copilotSkuId = "Microsoft_365_Copilot";

        try
        {
            // Get all users first
            var usersResult = await _graphClient.Users.GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Select = new[] { "id", "userPrincipalName" };
                requestConfiguration.QueryParameters.Filter = "accountEnabled eq true and userType eq 'Member'";
                requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
            });

            if (usersResult?.Value == null)
            {
                _logger.LogWarning("No users found when fetching license info");
                return licenseInfo;
            }

            var users = new List<User>(usersResult.Value);

            // Handle pagination
            var pageIterator = PageIterator<User, UserCollectionResponse>.CreatePageIterator(
                _graphClient,
                usersResult,
                user =>
                {
                    users.Add(user);
                    return true;
                });

            await pageIterator.IterateAsync();

            _logger.LogInformation($"Checking license info for {users.Count} users...");

            // Fetch license info for each user in parallel
            var tasks = users.Select(async user =>
            {
                try
                {
                    var licenses = await _graphClient.Users[user.Id].LicenseDetails.GetAsync();
                    
                    var hasCopilot = licenses?.Value?.Any(license => 
                        license.SkuPartNumber?.Equals(copilotSkuId, StringComparison.OrdinalIgnoreCase) == true) ?? false;

                    lock (licenseInfo)
                    {
                        if (!string.IsNullOrEmpty(user.UserPrincipalName))
                        {
                            licenseInfo[user.UserPrincipalName] = hasCopilot;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Could not retrieve license info for user {user.UserPrincipalName}: {ex.Message}");
                    // Continue without license info for this user
                }
            });

            await Task.WhenAll(tasks);

            var usersWithCopilot = licenseInfo.Count(kvp => kvp.Value);
            _logger.LogInformation($"License info retrieved for {licenseInfo.Count} users. {usersWithCopilot} have Copilot licenses");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching license information");
        }

        return licenseInfo;
    }

    private static EnrichedUserInfo MapToEnrichedUser(User user)
    {
        var isDeleted = user.AdditionalData?.ContainsKey("@removed") == true;

        return new EnrichedUserInfo
        {
            Id = user.Id ?? string.Empty,
            UserPrincipalName = user.UserPrincipalName ?? string.Empty,
            DisplayName = user.DisplayName,
            GivenName = user.GivenName,
            Surname = user.Surname,
            Mail = user.Mail,
            Department = user.Department,
            JobTitle = user.JobTitle,
            OfficeLocation = user.OfficeLocation,
            City = user.City,
            Country = user.Country,
            State = user.State,
            CompanyName = user.CompanyName,
            EmployeeType = user.EmployeeType,
            HireDate = user.EmployeeHireDate,
            IsDeleted = isDeleted
        };
    }
}
