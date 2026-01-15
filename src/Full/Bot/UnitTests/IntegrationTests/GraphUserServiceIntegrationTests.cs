using Common.Engine.Services;
using Common.Engine.Services.UserCache;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Azure.Identity;
using Microsoft.Graph;
using Common.Engine.Config;
using UnitTests.Fakes;

namespace UnitTests.IntegrationTests;

/// <summary>
/// Integration tests for GraphUserService.
/// NOTE: These tests require actual Microsoft Graph connectivity and will query real data.
/// Ensure your test configuration has valid Azure AD credentials.
/// </summary>
[TestClass]
public class GraphUserServiceIntegrationTests : AbstractTest
{
    private GraphUserService _service = null!;
    private IUserCacheManager _cacheManager = null!;

    [TestInitialize]
    public void Initialize()
    {
        var clientSecretCredential = new ClientSecretCredential(
            _config.GraphConfig.TenantId,
            _config.GraphConfig.ClientId,
            _config.GraphConfig.ClientSecret);

        var scopes = new[] { "https://graph.microsoft.com/.default" };
        var graphClient = new GraphServiceClient(clientSecretCredential, scopes);

        // Use in-memory cache for testing
        var cacheConfig = new UserCacheConfig();
        var copilotStatsLoader = new GraphCopilotStatsLoader(
            GetLogger<GraphCopilotStatsLoader>(),
            cacheConfig,
            _config.GraphConfig);
        var dataLoader = new GraphUserDataLoader(graphClient, GetLogger<GraphUserDataLoader>(), copilotStatsLoader, cacheConfig);
        var storage = new InMemoryCacheStorage();
        _cacheManager = new UserCacheManager(dataLoader, storage, cacheConfig, GetLogger<UserCacheManager>());

        _service = new GraphUserService(
            _config.GraphConfig,
            GetLogger<GraphUserService>(),
            _cacheManager
        );
    }

    [TestMethod]
    public async Task GetAllUsersWithMetadataAsync_ReturnsUsers()
    {
        // Act
        var users = await _service.GetAllUsersWithMetadataAsync(maxUsers: 10);

        // Assert
        Assert.IsNotNull(users);
        Assert.IsTrue(users.Count > 0, "Should return at least one user");
        
        var firstUser = users.First();
        Assert.IsFalse(string.IsNullOrEmpty(firstUser.UserPrincipalName));
        Assert.IsFalse(string.IsNullOrEmpty(firstUser.DisplayName));

        _logger.LogInformation($"Retrieved {users.Count} users from Graph");
    }

    [TestMethod]
    public async Task GetAllUsersWithMetadataAsync_IncludesExpectedProperties()
    {
        // Act
        var users = await _service.GetAllUsersWithMetadataAsync(maxUsers: 5);

        // Assert
        Assert.IsTrue(users.Count > 0, "Should return at least one user");

        var user = users.First();
        
        // Required properties
        Assert.IsFalse(string.IsNullOrEmpty(user.Id));
        Assert.IsFalse(string.IsNullOrEmpty(user.UserPrincipalName));
        Assert.IsFalse(string.IsNullOrEmpty(user.DisplayName));

        // Log which optional properties are available
        _logger.LogInformation($"User {user.UserPrincipalName} properties:");
        _logger.LogInformation($"  Department: {user.Department ?? "Not set"}");
        _logger.LogInformation($"  JobTitle: {user.JobTitle ?? "Not set"}");
        _logger.LogInformation($"  OfficeLocation: {user.OfficeLocation ?? "Not set"}");
        _logger.LogInformation($"  City: {user.City ?? "Not set"}");
        _logger.LogInformation($"  State: {user.State ?? "Not set"}");
        _logger.LogInformation($"  Country: {user.Country ?? "Not set"}");
    }

    [TestMethod]
    public async Task GetUserWithMetadataAsync_ValidUser_ReturnsUser()
    {
        // Arrange - First get a valid UPN from the tenant
        var allUsers = await _service.GetAllUsersWithMetadataAsync(maxUsers: 1);
        Assert.IsTrue(allUsers.Count > 0, "Need at least one user in tenant");
        var testUpn = allUsers.First().UserPrincipalName;

        // Act
        var user = await _service.GetUserWithMetadataAsync(testUpn);

        // Assert
        Assert.IsNotNull(user);
        Assert.AreEqual(testUpn, user.UserPrincipalName);
        Assert.IsFalse(string.IsNullOrEmpty(user.DisplayName));

        _logger.LogInformation($"Retrieved user: {user.DisplayName} ({user.UserPrincipalName})");
    }

    [TestMethod]
    public async Task GetUserWithMetadataAsync_InvalidUser_ReturnsNull()
    {
        // Arrange
        var invalidUpn = "nonexistent.user@invalid-domain-12345.com";

        // Act
        var user = await _service.GetUserWithMetadataAsync(invalidUpn);

        // Assert
        Assert.IsNull(user);
    }

    [TestMethod]
    public async Task GetUsersByDepartmentAsync_ValidDepartment_ReturnsUsers()
    {
        // Arrange - First find a department that exists
        var allUsers = await _service.GetAllUsersWithMetadataAsync(maxUsers: 50);
        var usersWithDepartments = allUsers.Where(u => !string.IsNullOrEmpty(u.Department)).ToList();
        
        if (usersWithDepartments.Count == 0)
        {
            Assert.Inconclusive("No users with department information found in tenant");
            return;
        }

        var testDepartment = usersWithDepartments.First().Department!;

        // Act
        var departmentUsers = await _service.GetUsersByDepartmentAsync(testDepartment);

        // Assert
        Assert.IsTrue(departmentUsers.Count > 0, $"Should find users in department '{testDepartment}'");
        Assert.IsTrue(departmentUsers.All(u => u.Department == testDepartment));

        _logger.LogInformation($"Found {departmentUsers.Count} users in department '{testDepartment}'");
    }

    [TestMethod]
    public async Task EnrichUsersWithManagersAsync_AddsManagerInfo()
    {
        // Arrange
        var users = await _service.GetAllUsersWithMetadataAsync(maxUsers: 5);
        Assert.IsTrue(users.Count > 0, "Need at least one user");

        // Act
        await _service.EnrichUsersWithManagersAsync(users);

        // Assert
        var usersWithManagers = users.Where(u => !string.IsNullOrEmpty(u.ManagerUpn)).ToList();
        
        _logger.LogInformation($"Out of {users.Count} users, {usersWithManagers.Count} have manager information");
        
        foreach (var user in usersWithManagers)
        {
            _logger.LogInformation($"User {user.DisplayName} has manager: {user.ManagerDisplayName} ({user.ManagerUpn})");
        }
    }

    [TestMethod]
    public async Task EnrichedUserInfo_ToAISummary_GeneratesCorrectFormat()
    {
        // Arrange
        var users = await _service.GetAllUsersWithMetadataAsync(maxUsers: 1);
        Assert.IsTrue(users.Count > 0, "Need at least one user");
        
        await _service.EnrichUsersWithManagersAsync(users);
        var user = users.First();

        // Act
        var summary = user.ToAISummary();

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(summary));
        Assert.IsTrue(summary.Contains("UPN:"));
        Assert.IsTrue(summary.Contains("Name:"));

        _logger.LogInformation($"AI Summary: {summary}");
    }

    [TestMethod]
    public async Task GetAllUsersWithMetadataAsync_RespectMaxUsersLimit()
    {
        // Arrange
        var maxUsers = 5;

        // Act
        var users = await _service.GetAllUsersWithMetadataAsync(maxUsers);

        // Assert
        Assert.IsTrue(users.Count <= maxUsers, 
            $"Should not exceed max users limit. Got {users.Count}, expected max {maxUsers}");
    }

    [TestMethod]
    public async Task GetAllUsersWithMetadataAsync_LargeLimit_HandlesCorrectly()
    {
        // Arrange
        var maxUsers = 50; // Reasonable size for testing

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var users = await _service.GetAllUsersWithMetadataAsync(maxUsers);
        stopwatch.Stop();

        // Assert
        Assert.IsNotNull(users);
        _logger.LogInformation($"Retrieved {users.Count} users in {stopwatch.ElapsedMilliseconds}ms");
        
        // Ensure paging worked if tenant has more users than limit
        Assert.IsTrue(users.Count <= maxUsers);
    }
}
