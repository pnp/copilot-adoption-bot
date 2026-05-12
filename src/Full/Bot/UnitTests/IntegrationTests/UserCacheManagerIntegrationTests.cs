using Engine.Services.UserCache;
using Microsoft.Extensions.Logging;

namespace UnitTests.IntegrationTests;

/// <summary>
/// Integration tests for the basic CRUD/cache operations and sync flows on
/// <see cref="UserCacheManager"/>.
/// Note: These tests require Graph API credentials and will make actual API calls.
/// </summary>
[TestClass]
public class UserCacheManagerIntegrationTests : UserCacheManagerIntegrationTestBase
{
    #region Basic Cache Operations

    [TestMethod]
    public async Task ClearCacheAsync_ClearsAllData_Success()
    {
        if (_cacheManager == null)
        {
            Assert.Inconclusive("Cache manager not initialized - check Graph API credentials");
            return;
        }

        // Arrange - Perform initial sync to populate cache
        await _cacheManager.SyncUsersAsync();
        var initialUsers = await _cacheManager.GetAllCachedUsersAsync();

        if (initialUsers.Count == 0)
        {
            Assert.Inconclusive("No users returned from Graph API to test with");
            return;
        }

        // Act
        await _cacheManager.ClearCacheAsync();
        var usersAfterClear = await _cacheManager.GetAllCachedUsersAsync(skipAutoSync: true);

        // Assert
        Assert.IsTrue(initialUsers.Count > 0, "Should have had users before clear");
        Assert.AreEqual(0, usersAfterClear.Count, "Cache should be empty after clear");
        _logger.LogInformation($"Cleared {initialUsers.Count} users from cache");

        _testPassed = true;
    }

    [TestMethod]
    public async Task GetCachedUserAsync_ReturnsUser_AfterSync()
    {
        if (_cacheManager == null)
        {
            Assert.Inconclusive("Cache manager not initialized - check Graph API credentials");
            return;
        }

        // Arrange - Sync to populate cache
        await _cacheManager.SyncUsersAsync();
        var allUsers = await _cacheManager.GetAllCachedUsersAsync();

        if (allUsers.Count == 0)
        {
            Assert.Inconclusive("No users returned from Graph API to test with");
            return;
        }

        var testUser = allUsers.First();

        // Act
        var retrieved = await _cacheManager.GetCachedUserAsync(testUser.UserPrincipalName);

        // Assert
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(testUser.UserPrincipalName, retrieved.UserPrincipalName);
        Assert.AreEqual(testUser.DisplayName, retrieved.DisplayName);
        _logger.LogInformation($"Successfully retrieved user: {retrieved.UserPrincipalName}");

        _testPassed = true;
    }

    [TestMethod]
    public async Task GetCachedUserAsync_ReturnsNull_ForNonExistentUser()
    {
        if (_cacheManager == null)
        {
            Assert.Inconclusive("Cache manager not initialized - check Graph API credentials");
            return;
        }

        // Act
        var result = await _cacheManager.GetCachedUserAsync("nonexistent@doesnotexist.com");

        // Assert
        Assert.IsNull(result);

        _testPassed = true;
    }

    #endregion

    #region Sync Operations

    [TestMethod]
    public async Task SyncUsersAsync_PerformsFullSync_WhenCacheEmpty()
    {
        if (_cacheManager == null)
        {
            Assert.Inconclusive("Cache manager not initialized - check Graph API credentials");
            return;
        }

        // Act
        await _cacheManager.SyncUsersAsync();
        var users = await _cacheManager.GetAllCachedUsersAsync();

        // Assert
        Assert.IsTrue(users.Count > 0, "Should have synced at least one user");
        _logger.LogInformation($"Synced {users.Count} users to cache");

        // Verify user properties are populated
        var firstUser = users.First();
        Assert.IsFalse(string.IsNullOrEmpty(firstUser.Id));
        Assert.IsFalse(string.IsNullOrEmpty(firstUser.UserPrincipalName));

        _testPassed = true;
    }

    [TestMethod]
    public async Task SyncUsersAsync_StoresUserProperties_Correctly()
    {
        if (_cacheManager == null)
        {
            Assert.Inconclusive("Cache manager not initialized - check Graph API credentials");
            return;
        }

        // Arrange & Act
        await _cacheManager.SyncUsersAsync();
        var users = await _cacheManager.GetAllCachedUsersAsync();

        if (users.Count == 0)
        {
            Assert.Inconclusive("No users returned from Graph API to test with");
            return;
        }

        var user = users.First();

        // Assert - Verify core properties are stored
        Assert.IsNotNull(user.Id);
        Assert.IsNotNull(user.UserPrincipalName);

        _logger.LogInformation($"User properties validated for: {user.UserPrincipalName}");
        _logger.LogInformation($"  Display Name: {user.DisplayName}");
        _logger.LogInformation($"  Department: {user.Department}");
        _logger.LogInformation($"  Job Title: {user.JobTitle}");

        _testPassed = true;
    }

    #endregion

    #region Performance Tests

    [TestMethod]
    public async Task GetAllCachedUsersAsync_PerformsWell_WithManyUsers()
    {
        if (_cacheManager == null)
        {
            Assert.Inconclusive("Cache manager not initialized - check Graph API credentials");
            return;
        }

        // Arrange
        await _cacheManager.SyncUsersAsync();

        // Act - Measure retrieval time
        var startTime = DateTime.UtcNow;
        var users = await _cacheManager.GetAllCachedUsersAsync();
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.IsTrue(users.Count > 0, "Should have users in cache");
        Assert.IsTrue(duration.TotalSeconds < 30, $"Query took {duration.TotalSeconds:F2} seconds, expected < 30");

        _logger.LogInformation($"Retrieved {users.Count} users in {duration.TotalMilliseconds:F0}ms");

        _testPassed = true;
    }

    #endregion
}
