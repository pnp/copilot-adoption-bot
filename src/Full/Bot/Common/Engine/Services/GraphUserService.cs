using Azure.Identity;
using Common.Engine.Config;
using Common.Engine.Models;
using Common.Engine.Services.UserCache;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Common.Engine.Services;

/// <summary>
/// Service for loading users from Microsoft Graph with extended metadata.
/// Used for AI-driven smart group resolution.
/// Requires a cache manager implementation for efficient data retrieval.
/// </summary>
public class GraphUserService
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<GraphUserService> _logger;
    private readonly IUserCacheManager _cacheManager;

    // Properties to request for enriched user data
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
        "employeeHireDate"
    ];

    /// <summary>
    /// Constructor with cache manager for optimized user data retrieval.
    /// </summary>
    public GraphUserService(
        AzureADAuthConfig config,
        ILogger<GraphUserService> logger,
        IUserCacheManager cacheManager)
    {
        _logger = logger;
        _cacheManager = cacheManager;

        var clientSecretCredential = new ClientSecretCredential(
            config.TenantId,
            config.ClientId,
            config.ClientSecret);

        var scopes = new[] { "https://graph.microsoft.com/.default" };
        _graphClient = new GraphServiceClient(clientSecretCredential, scopes);
    }

    /// <summary>
    /// Get all users from the tenant with extended metadata.
    /// Uses cache manager for optimized retrieval.
    /// </summary>
    /// <param name="maxUsers">Maximum number of users to retrieve (default 999)</param>
    /// <param name="forceRefresh">Force a refresh from Graph API instead of using cache</param>
    public async Task<List<EnrichedUserInfo>> GetAllUsersWithMetadataAsync(int maxUsers = 999, bool forceRefresh = false)
    {
        try
        {
            _logger.LogInformation("Fetching users from cache...");
            var cachedUsers = await _cacheManager.GetAllCachedUsersAsync(forceRefresh);
            
            if (maxUsers < int.MaxValue)
            {
                cachedUsers = cachedUsers.Take(maxUsers).ToList();
            }
            
            _logger.LogInformation($"Retrieved {cachedUsers.Count} users from cache");
            return cachedUsers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving from cache");
            throw;
        }
    }

    /// <summary>
    /// Get all users directly from Graph API (bypasses cache).
    /// Use this sparingly as it's less efficient than cached retrieval.
    /// </summary>
    /// <param name="maxUsers">Maximum number of users to retrieve (default 999)</param>
    public async Task<List<EnrichedUserInfo>> GetAllUsersDirectFromGraphAsync(int maxUsers = 999)
    {
        var users = new List<EnrichedUserInfo>();

        try
        {
            _logger.LogInformation("Fetching users with extended metadata from Graph...");

            var result = await _graphClient.Users.GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Select = UserSelectProperties;
                requestConfiguration.QueryParameters.Top = Math.Min(maxUsers, 999);
                requestConfiguration.QueryParameters.Filter = "accountEnabled eq true and userType eq 'Member'";
                requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
            });

            if (result?.Value != null)
            {
                foreach (var user in result.Value)
                {
                    var enrichedUser = MapToEnrichedUser(user);
                    users.Add(enrichedUser);
                }
            }

            // Handle paging for large tenants
            var pageIterator = PageIterator<User, UserCollectionResponse>.CreatePageIterator(
                _graphClient,
                result!,
                user =>
                {
                    if (users.Count >= maxUsers)
                        return false;

                    users.Add(MapToEnrichedUser(user));
                    return true;
                });

            await pageIterator.IterateAsync();

            _logger.LogInformation($"Retrieved {users.Count} users with metadata from Graph");

            // Enrich users with license information in parallel
            await EnrichUsersWithLicenseInfoAsync(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users with metadata from Graph");
            throw;
        }

        return users;
    }

    /// <summary>
    /// Get users filtered by department.
    /// </summary>
    public async Task<List<EnrichedUserInfo>> GetUsersByDepartmentAsync(string department)
    {
        var users = new List<EnrichedUserInfo>();

        try
        {
            _logger.LogInformation($"Fetching users in department '{department}' from Graph...");

            var result = await _graphClient.Users.GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Select = UserSelectProperties;
                requestConfiguration.QueryParameters.Filter = $"accountEnabled eq true and department eq '{department}'";
                requestConfiguration.QueryParameters.Top = 999;
                requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
            });

            if (result?.Value != null)
            {
                users.AddRange(result.Value.Select(MapToEnrichedUser));
            }

            _logger.LogInformation($"Retrieved {users.Count} users in department '{department}'");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting users by department from Graph");
            throw;
        }

        return users;
    }

    /// <summary>
    /// Get a single user with extended metadata.
    /// Uses cache manager for optimized retrieval.
    /// </summary>
    public async Task<EnrichedUserInfo?> GetUserWithMetadataAsync(string upn)
    {
        try
        {
            var cachedUser = await _cacheManager.GetCachedUserAsync(upn);
            if (cachedUser != null)
            {
                _logger.LogDebug($"Retrieved user {upn} from cache");
                return cachedUser;
            }
            
            // User not in cache, fetch from Graph API
            _logger.LogDebug($"User {upn} not in cache, fetching from Graph API");
            return await GetUserDirectFromGraphAsync(upn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving user {upn}");
            throw;
        }
    }

    /// <summary>
    /// Get a single user directly from Graph API (bypasses cache).
    /// </summary>
    private async Task<EnrichedUserInfo?> GetUserDirectFromGraphAsync(string upn)
    {
        try
        {
            var user = await _graphClient.Users[upn].GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Select = UserSelectProperties;
            });

            if (user != null)
            {
                var enrichedUser = MapToEnrichedUser(user);
                
                // Try to get manager info
                try
                {
                    var manager = await _graphClient.Users[upn].Manager.GetAsync();
                    if (manager is User managerUser)
                    {
                        enrichedUser.ManagerUpn = managerUser.UserPrincipalName;
                        enrichedUser.ManagerDisplayName = managerUser.DisplayName;
                    }
                }
                catch
                {
                    // Manager not found or not accessible - continue without it
                }

                // Try to get license info
                try
                {
                    const string copilotSkuId = "Microsoft_365_Copilot";
                    var licenses = await _graphClient.Users[user.Id].LicenseDetails.GetAsync();
                    
                    if (licenses?.Value != null)
                    {
                        enrichedUser.HasCopilotLicense = licenses.Value.Any(license => 
                            license.SkuPartNumber?.Equals(copilotSkuId, StringComparison.OrdinalIgnoreCase) == true);
                    }
                }
                catch
                {
                    // License info not accessible - continue without it
                }

                return enrichedUser;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting user {upn} with metadata from Graph");
            return null;
        }
    }

    /// <summary>
    /// Get managers for batch of users (for enrichment).
    /// </summary>
    public async Task EnrichUsersWithManagersAsync(List<EnrichedUserInfo> users)
    {
        _logger.LogInformation($"Enriching {users.Count} users with manager information...");
        
        var tasks = users.Select(async user =>
        {
            try
            {
                var manager = await _graphClient.Users[user.UserPrincipalName].Manager.GetAsync();
                if (manager is User managerUser)
                {
                    user.ManagerUpn = managerUser.UserPrincipalName;
                    user.ManagerDisplayName = managerUser.DisplayName;
                }
            }
            catch
            {
                // Manager not found or not accessible - continue
            }
        });

        await Task.WhenAll(tasks);
        _logger.LogInformation("Manager enrichment completed");
    }

    /// <summary>
    /// Enrich users with Microsoft 365 Copilot license information in parallel.
    /// </summary>
    private async Task EnrichUsersWithLicenseInfoAsync(List<EnrichedUserInfo> users)
    {
        _logger.LogInformation($"Enriching {users.Count} users with license information...");
        
        const string copilotSkuId = "Microsoft_365_Copilot";
        
        var tasks = users.Select(async user =>
        {
            try
            {
                var licenses = await _graphClient.Users[user.Id]
                    .LicenseDetails
                    .GetAsync();

                if (licenses?.Value != null)
                {
                    user.HasCopilotLicense = licenses.Value.Any(license => 
                        license.SkuPartNumber?.Equals(copilotSkuId, StringComparison.OrdinalIgnoreCase) == true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Could not retrieve license info for user {user.UserPrincipalName}: {ex.Message}");
                // Continue without license info
            }
        });

        await Task.WhenAll(tasks);
        
        var usersWithCopilot = users.Count(u => u.HasCopilotLicense);
        _logger.LogInformation($"License enrichment completed. {usersWithCopilot} users have Copilot licenses");
    }

    private static EnrichedUserInfo MapToEnrichedUser(User user)
    {
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
            HireDate = user.EmployeeHireDate
        };
    }
}
