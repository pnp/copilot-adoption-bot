using Engine.Config;
using Engine.Services.UserCache;
using Microsoft.Extensions.Logging;

namespace UnitTests.IntegrationTests;

/// <summary>
/// Integration tests verifying that <see cref="UserCacheManager"/> honours custom
/// table-name configuration and that two cache managers with different table names are
/// fully isolated from each other.
/// </summary>
[TestClass]
public class UserCacheManagerTableConfigurationIntegrationTests : UserCacheManagerIntegrationTestBase
{
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
}
