using Engine.Config;
using Engine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Engine.Services.UserCache;

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
        "userType",
        // Selected in bulk so license state arrives with the user. Fetching it per user via
        // /users/{id}/licenseDetails was 150,000 extra Graph calls per refresh cycle.
        "assignedLicenses"
    ];

    /// <summary>
    /// Graph returns ~100 users per page by default. Asking for the maximum turns a
    /// 150,000-user enumeration from ~1,000 sequential round trips into ~150.
    /// </summary>
    private const string MaxPageSizePreference = "odata.maxpagesize=999";

    /// <summary>
    /// Microsoft 365 Copilot SKU id. Used to derive <c>HasCopilotLicense</c> from the
    /// bulk-selected <c>assignedLicenses</c> collection.
    /// </summary>
    private static readonly Guid[] CopilotSkuIds =
    [
        Guid.Parse("639dec6b-bb19-468b-871c-c5c441c4b0cb") // Microsoft 365 Copilot
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
            requestConfiguration.Headers.Add("Prefer", MaxPageSizePreference);
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

    /// <summary>
    /// Fetch Copilot licence state for every user.
    ///
    /// <para>
    /// Derived from the bulk-selectable <c>assignedLicenses</c> property during a single
    /// paged enumeration. The previous implementation enumerated all users and then issued
    /// one <c>GET /users/{id}/licenseDetails</c> per user - 150,000 additional Graph calls
    /// per refresh cycle at target scale, on top of a second full directory enumeration.
    /// </para>
    /// </summary>
    public async Task<Dictionary<string, bool>> GetLicenseInfoAsync()
    {
        _logger.LogInformation("Fetching license information from Microsoft Graph...");

        var licenseInfo = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var usersResult = await _graphClient.Users.GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Select = new[] { "id", "userPrincipalName", "assignedLicenses" };
                requestConfiguration.QueryParameters.Filter = "accountEnabled eq true and userType eq 'Member'";
                requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                requestConfiguration.Headers.Add("Prefer", MaxPageSizePreference);
            });

            if (usersResult?.Value == null)
            {
                _logger.LogWarning("No users found when fetching license info");
                return licenseInfo;
            }

            // PageIterator iterates ALL items (including the first page already loaded into
            // usersResult), so don't pre-populate from usersResult.Value or the first page
            // would be duplicated.
            var pageIterator = PageIterator<User, UserCollectionResponse>.CreatePageIterator(
                _graphClient,
                usersResult,
                user =>
                {
                    if (!string.IsNullOrEmpty(user.UserPrincipalName))
                    {
                        licenseInfo[user.UserPrincipalName] = HasCopilotLicense(user);
                    }
                    return true;
                });

            await pageIterator.IterateAsync();

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
            HasCopilotLicense = HasCopilotLicense(user),
            IsDeleted = isDeleted
        };
    }

    /// <summary>
    /// Derive Copilot licensing from the bulk-selected <c>assignedLicenses</c> collection,
    /// avoiding a per-user <c>/licenseDetails</c> request.
    /// </summary>
    internal static bool HasCopilotLicense(User user)
    {
        var assigned = user.AssignedLicenses;
        if (assigned == null || assigned.Count == 0) return false;

        foreach (var license in assigned)
        {
            if (license.SkuId is { } skuId && Array.IndexOf(CopilotSkuIds, skuId) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
