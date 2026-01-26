using Common.Engine.Config;
using Common.Engine.Services.UserCache;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTests.Fakes;

namespace UnitTests.IntegrationTests;

/// <summary>
/// Integration tests for Azure Table Storage cache manager.
/// These tests verify table storage operations work correctly with configurable table names.
/// Note: These tests require Graph API credentials and will make actual API calls.
/// </summary>
[TestClass]
public class UserCacheManagerIntegrationTests : AbstractTest
{
    private UserCacheManager? _cacheManager;
    private AzureTableCacheStorage? _storage;
    private GraphServiceClient? _graphClient;
    private UserCacheConfig? _cacheConfig;
    private string _testTablePrefix = string.Empty;
    private bool _testPassed = false;

    [TestInitialize]
    public void Initialize()
    {
        // Use unique table names for each test run to avoid conflicts
        // Include milliseconds and random component to ensure uniqueness even in parallel test execution
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var random = new Random().Next(1000, 9999);
        _testTablePrefix = $"test{timestamp}{random}";
        
        _cacheConfig = new UserCacheConfig
        {
            CacheExpiration = TimeSpan.FromMinutes(5),
            FullSyncInterval = TimeSpan.FromDays(1),
            UserCacheTableName = $"{_testTablePrefix}usercache",
            SyncMetadataTableName = $"{_testTablePrefix}syncmeta"
        };

        try
        {
            // Create Graph client for tests
            var options = new Azure.Identity.TokenCredentialOptions 
            { 
                AuthorityHost = Azure.Identity.AzureAuthorityHosts.AzurePublicCloud 
            };
            var scopes = new[] { "https://graph.microsoft.com/.default" };
            var clientSecretCredential = new Azure.Identity.ClientSecretCredential(
                _config.GraphConfig.TenantId,
                _config.GraphConfig.ClientId,
                _config.GraphConfig.ClientSecret,
                options);

            _graphClient = new GraphServiceClient(clientSecretCredential, scopes);
            
            // Create adapters
            var copilotStatsLoader = new GraphCopilotStatsLoader(
                GetLogger<GraphCopilotStatsLoader>(),
                _cacheConfig,
                _config.GraphConfig);
            var dataLoader = new GraphUserDataLoader(_graphClient, GetLogger<GraphUserDataLoader>(), copilotStatsLoader, _cacheConfig);
            _storage = new AzureTableCacheStorage(GetStorageAuthConfig(), GetLogger<AzureTableCacheStorage>(), _cacheConfig);
            
            _cacheManager = new UserCacheManager(dataLoader, _storage, _cacheConfig, GetLogger<UserCacheManager>());
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to initialize cache manager: {ex.Message}");
            _logger.LogWarning("Tests will be skipped if Graph credentials are not configured");
        }
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_cacheManager != null && _storage != null)
        {
            try
            {
                if (_testPassed)
                {
                    // Test passed - delete the temporary tables
                    await _storage.DeleteTablesAsync();
                    _logger.LogInformation($"Deleted test tables with prefix: {_testTablePrefix}");
                }
                else
                {
                    // Test failed - keep tables for debugging but clear data
                    await _cacheManager.ClearCacheAsync();
                    _logger.LogWarning($"Test failed - kept tables with prefix: {_testTablePrefix} for debugging");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error during cleanup: {ex.Message}");
            }
        }
        
        // Reset for next test
        _testPassed = false;
    }

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

    #region Table Name Configuration

    [TestMethod]
    public async Task CustomTableNames_AreUsedCorrectly()
    {
        if (_graphClient == null)
        {
            Assert.Inconclusive("Graph client not initialized - check Graph API credentials");
            return;
        }

        // Arrange - Create cache manager with custom table names
        var customTimestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var customRandom = new Random().Next(1000, 9999);
        var customPrefix = $"custom{customTimestamp}{customRandom}";
        var customConfig = new UserCacheConfig
        {
            UserCacheTableName = $"{customPrefix}users",
            SyncMetadataTableName = $"{customPrefix}meta"
        };

        var copilotStatsLoader1 = new GraphCopilotStatsLoader(
            GetLogger<GraphCopilotStatsLoader>(),
            customConfig,
            _config.GraphConfig);
        var dataLoader = new GraphUserDataLoader(_graphClient, GetLogger<GraphUserDataLoader>(), copilotStatsLoader1, customConfig);
        var storage = new AzureTableCacheStorage(GetStorageAuthConfig(), GetLogger<AzureTableCacheStorage>(), customConfig);
        var customCacheManager = new UserCacheManager(dataLoader, storage, customConfig, GetLogger<UserCacheManager>());

        try
        {
            // Act
            await customCacheManager.SyncUsersAsync();
            var users = await customCacheManager.GetAllCachedUsersAsync();

            // Assert
            Assert.IsTrue(users.Count > 0, "Custom tables should contain synced users");
            _logger.LogInformation($"Custom table names working: {customConfig.UserCacheTableName}");
            
            _testPassed = true;
        }
        finally
        {
            // Cleanup - always delete custom test tables
            var customStorage = new AzureTableCacheStorage(GetStorageAuthConfig(), GetLogger<AzureTableCacheStorage>(), customConfig);
            await customStorage.DeleteTablesAsync();
        }
    }

    [TestMethod]
    public async Task DifferentTableNames_IsolateCaches()
    {
        if (_graphClient == null)
        {
            Assert.Inconclusive("Graph client not initialized - check Graph API credentials");
            return;
        }

        // Arrange - Create two cache managers with different table names
        var timestamp1 = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var random1 = new Random().Next(1000, 9999);
        var cache1Prefix = $"iso1{timestamp1}{random1}";
        
        // Ensure unique timestamp for second cache
        await Task.Delay(10);
        var timestamp2 = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var random2 = new Random().Next(1000, 9999);
        var cache2Prefix = $"iso2{timestamp2}{random2}";

        var cache1Config = new UserCacheConfig
        {
            UserCacheTableName = $"{cache1Prefix}cache",
            SyncMetadataTableName = $"{cache1Prefix}meta"
        };

        var cache2Config = new UserCacheConfig
        {
            UserCacheTableName = $"{cache2Prefix}cache",
            SyncMetadataTableName = $"{cache2Prefix}meta"
        };

        var copilotStatsLoader1 = new GraphCopilotStatsLoader(
            GetLogger<GraphCopilotStatsLoader>(),
            cache1Config,
            _config.GraphConfig);
        var dataLoader1 = new GraphUserDataLoader(_graphClient, GetLogger<GraphUserDataLoader>(), copilotStatsLoader1, cache1Config);
        var storage1 = new AzureTableCacheStorage(GetStorageAuthConfig(), GetLogger<AzureTableCacheStorage>(), cache1Config);
        var cache1 = new UserCacheManager(dataLoader1, storage1, cache1Config, GetLogger<UserCacheManager>());

        var copilotStatsLoader2 = new GraphCopilotStatsLoader(
            GetLogger<GraphCopilotStatsLoader>(),
            cache2Config,
            _config.GraphConfig);
        var dataLoader2 = new GraphUserDataLoader(_graphClient, GetLogger<GraphUserDataLoader>(), copilotStatsLoader2, cache2Config);
        var storage2 = new AzureTableCacheStorage(GetStorageAuthConfig(), GetLogger<AzureTableCacheStorage>(), cache2Config);
        var cache2 = new UserCacheManager(dataLoader2, storage2, cache2Config, GetLogger<UserCacheManager>());

        try
        {
            // Act - Sync both caches
            await cache1.SyncUsersAsync();
            await cache2.SyncUsersAsync();

            var cache1Users = await cache1.GetAllCachedUsersAsync();
            var cache2Users = await cache2.GetAllCachedUsersAsync();

            // Clear cache1 but not cache2
            await cache1.ClearCacheAsync();

            var cache1AfterClear = await cache1.GetAllCachedUsersAsync(skipAutoSync: true);
            var cache2AfterClear = await cache2.GetAllCachedUsersAsync();

            // Assert
            Assert.IsTrue(cache1Users.Count > 0, "Cache 1 should have users initially");
            Assert.IsTrue(cache2Users.Count > 0, "Cache 2 should have users initially");
            Assert.AreEqual(0, cache1AfterClear.Count, "Cache 1 should be empty after clear");
            Assert.AreEqual(cache2Users.Count, cache2AfterClear.Count, "Cache 2 should be unaffected");

            _logger.LogInformation($"Cache isolation verified - Cache 1: {cache1AfterClear.Count}, Cache 2: {cache2AfterClear.Count}");
            
            _testPassed = true;
        }
        finally
        {
            // Cleanup both caches - always delete isolation test tables
            var cleanupStorage1 = new AzureTableCacheStorage(GetStorageAuthConfig(), GetLogger<AzureTableCacheStorage>(), cache1Config);
            var cleanupStorage2 = new AzureTableCacheStorage(GetStorageAuthConfig(), GetLogger<AzureTableCacheStorage>(), cache2Config);
            await cleanupStorage1.DeleteTablesAsync();
            await cleanupStorage2.DeleteTablesAsync();
        }
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
